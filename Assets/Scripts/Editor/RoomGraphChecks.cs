using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class RoomGraphChecks
    {
        static int checks;
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Check(bool valid,string message){checks++;if(!valid)throw new InvalidOperationException("Room graph check: "+message);}
        [MenuItem("MiniWar/Tests/Room placement and links")]
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
            checks=0;var source=RoomDungeonAuthoring.CreateExample();
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Data/RoomDungeons/__GraphChecks.asset");
            var data=RoomDungeonAuthoring.Copy(source,path);var window=RoomDungeonBuilderWindow.ShowDungeon(data);
            window.position=new Rect(40,60,1280,800);window.SendEvent(new Event{type=EventType.Layout});
            try
            {
                string start=data.startRoomId;int count=data.rooms.Count;
                Vector2 anchor=Point(window,Vector2Int.zero);
                Mode(window,1);Click(window,new Vector2Int(0,1));
                var extra=data.rooms.Single(r=>r.cell==new Vector2Int(0,1));string id=extra.id;
                Check(data.rooms.Count==count+1&&extra.portals.Count==0,"placing next to a room does not connect it");
                Check(extra.layout.spawns.Sum(s=>s.count)==8,"new room starts with eight enemies");
                Check(data.ValidateDungeon().Count==0&&data.ConnectionWarnings().Count==1,"disconnected ordinary room is valid storage");
                Check(Vector2.Distance(anchor,Point(window,Vector2Int.zero))<.01f,"placing a room does not shift the map under the mouse");
                Undo.PerformUndo();Check(data.rooms.Count==count,"undo placement");
                Undo.PerformRedo();Check(data.FindRoom(id)!=null&&data.FindRoom(id).portals.Count==0,"redo keeps room disconnected");
                Mode(window,2);Click(window,Vector2Int.zero);Click(window,new Vector2Int(0,1));
                Check(data.FindRoom(id).portals.Count==1&&data.FindRoom(start).portals.Any(p=>p.targetRoomId==id),"two clicks create paired portals");
                string link=data.FindRoom(id).portals[0].id;
                Undo.PerformUndo();Check(data.FindRoom(id).portals.Count==0,"undo connection keeps both rooms");
                Undo.PerformRedo();Check(data.FindRoom(id).portals[0].id==link,"redo retains stable portal ID");
                Mode(window,3);ClickLocal(window,(Point(window,Vector2Int.zero)+Point(window,new Vector2Int(0,1)))/2);
                Check(data.FindRoom(id).portals.Count==0&&!data.FindRoom(start).portals.Any(p=>p.targetRoomId==id),"clicking a line removes both portal ends");
                Check(data.rooms.Count==count+1&&data.FindRoom(id).layout.spawns.Sum(s=>s.count)==8,"disconnect preserves room and enemies");
                Undo.PerformUndo();Check(data.FindRoom(id).portals[0].id==link,"undo cut restores exact portal");
                Click(window,Vector2Int.zero);Click(window,new Vector2Int(0,1));
                Check(data.FindRoom(id).portals.Count==0,"two-room disconnect gesture");
                Mode(window,2);Click(window,Vector2Int.zero);Click(window,new Vector2Int(0,1));
                int links=data.rooms.Sum(r=>r.portals.Count);Click(window,Vector2Int.zero);Click(window,new Vector2Int(0,1));
                Check(data.rooms.Sum(r=>r.portals.Count)==links,"connecting an existing link does not duplicate portals");
                var layout=data.FindRoom(id).layout;string[] enemies=layout.spawns.Select(s=>s.id).ToArray();
                Mode(window,0);Drag(window,new Vector2Int(0,1),new Vector2Int(-1,1));
                Check(data.FindRoom(id).cell==new Vector2Int(-1,1)&&data.FindRoom(id).portals.Count==0,"drag to a non-neighbor cell detaches old corridor");
                Check(data.FindRoom(id).layout==layout&&enemies.SequenceEqual(layout.spawns.Select(s=>s.id)),"drag preserves authored contents");
                Undo.PerformUndo();Check(data.FindRoom(id).cell==new Vector2Int(0,1)&&data.FindRoom(id).portals.Count==1,"one undo restores position and connection");
                Undo.PerformRedo();Check(data.FindRoom(id).cell==new Vector2Int(-1,1)&&data.FindRoom(id).portals.Count==0,"redo move and detach");
                Drag(window,new Vector2Int(-1,1),new Vector2Int(2,1));
                Check(data.FindRoom(id).cell==new Vector2Int(-1,1),"occupied-cell drop is rejected without changing either room");
                Drag(window,new Vector2Int(-1,1),new Vector2Int(-1,0));
                Mode(window,2);Click(window,Vector2Int.zero);Click(window,new Vector2Int(-1,0));
                string portalId=data.FindRoom(id).portals[0].id;Vector2 feet=data.FindRoom(id).portals[0].position;
                Mode(window,0);Drag(window,new Vector2Int(-1,0),new Vector2Int(0,-1));
                var retained=data.FindRoom(id).portals.Single();
                Check(retained.id==portalId&&retained.direction==PortalDirection.Up&&retained.position==feet,"still-adjacent move retains portal placement and updates its direction");
                Check(data.FindRoom(start).portals.Single(p=>p.targetRoomId==id).direction==PortalDirection.Down,"reciprocal direction updates together");
                Mode(window,2);links=data.rooms.Sum(r=>r.portals.Count);Click(window,new Vector2Int(0,-1));Click(window,new Vector2Int(2,1));
                Check(data.rooms.Sum(r=>r.portals.Count)==links,"non-neighbor connection is rejected");
                Mode(window,0);Mouse(window,EventType.MouseDown,Point(window,new Vector2Int(0,-1)));
                Mouse(window,EventType.MouseDrag,Point(window,new Vector2Int(0,-2)));
                window.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
                Check(data.FindRoom(id).cell==new Vector2Int(0,-1)&&GUIUtility.hotControl==0,"Escape cancels drag and releases capture");
                var boss=data.rooms.Single(r=>r.isBoss);Undo.IncrementCurrentGroup();RoomDungeonAuthoring.Disconnect(data,boss,boss.portals[0]);
                Check(data.ValidateDungeon().Any(s=>s.Contains("보스 방으로 갈 수")),"only an unreachable boss path blocks play");
                Undo.PerformUndo();Check(data.ValidateDungeon().Count==0,"boss connection restored");
                var group=data.FindRoom(id).layout.spawns[0];int original=group.count;group.count=7;
                Check(data.ValidateDungeon().Any(s=>s.Contains("최소 8마리")),"less than eight enemies is reported");group.count=original;
                RoomDungeonAuthoring.Save(data);window.Close();Undo.ClearUndo(data);Resources.UnloadAsset(data);
                data=AssetDatabase.LoadAssetAtPath<RoomDungeon>(path);
                Check(data.FindRoom(id).cell==new Vector2Int(0,-1)&&data.ValidateDungeon().Count==0,"save and reload retain graph changes");
                Check(data.FindRoom(id).portals[0].id==portalId,"portal IDs survive save and reload");
                var report="PASS: "+checks+" graph placement/link checks (real UI clicks, drag/drop, undo, population and reload).";
                File.WriteAllText("Logs/RoomGraphChecks.txt",report);Debug.Log(report);return report;
            }
            finally
            {
                if(window!=null)window.Close();if(data!=null)Undo.ClearUndo(data);
                AssetDatabase.DeleteAsset(path);RoomDungeonBuilderWindow.ShowDungeon(source);
            }
        }
        static void Mode(RoomDungeonBuilderWindow w,int value)
        {var f=typeof(RoomDungeonBuilderWindow).GetField("graphTool",Private);f.SetValue(w,Enum.ToObject(f.FieldType,value));}
        static Vector2 Point(RoomDungeonBuilderWindow w,Vector2Int cell)=>(Vector2)typeof(RoomDungeonBuilderWindow).GetMethod("CellPoint",Private).Invoke(w,new object[]{cell});
        static void Click(RoomDungeonBuilderWindow w,Vector2Int cell)=>ClickLocal(w,Point(w,cell));
        static void ClickLocal(RoomDungeonBuilderWindow w,Vector2 p){Mouse(w,EventType.MouseDown,p);Mouse(w,EventType.MouseUp,p);}
        static void Drag(RoomDungeonBuilderWindow w,Vector2Int from,Vector2Int to)
        {Mouse(w,EventType.MouseDown,Point(w,from));Mouse(w,EventType.MouseDrag,Point(w,to));Mouse(w,EventType.MouseUp,Point(w,to));}
        static void Mouse(RoomDungeonBuilderWindow w,EventType type,Vector2 local)
        {
            var canvas=(Rect)typeof(RoomDungeonBuilderWindow).GetField("canvas",Private).GetValue(w);
            w.SendEvent(new Event{type=type,button=0,mousePosition=local+canvas.position+w.rootVisualElement.worldBound.position});
        }
    }
}
