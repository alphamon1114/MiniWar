using System;
using System.Linq;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public sealed partial class RoomDungeonBuilderWindow
    {
        enum GraphTool { Move, Place, Connect, Disconnect }
        [SerializeField] GraphTool graphTool;
        [SerializeField] Vector2 mapCenter;
        string linkSource,dragRoom;
        Vector2 dragOrigin,panOrigin;
        Vector2Int dragCell;
        bool panning,roomDragging;
        int graphControl;
        string GraphHelp => graphTool==GraphTool.Move?"방을 드래그해 빈 칸으로 옮깁니다. 이웃이 아니게 되는 통로는 끊기며 Ctrl+Z로 함께 복구됩니다.":
            graphTool==GraphTool.Place?"빈 칸을 클릭하면 방만 배치됩니다. 옆에 방이 있어도 자동으로 연결되지 않습니다.":
            graphTool==GraphTool.Connect?"연결할 방 두 개를 차례로 클릭하세요. 상하좌우로 바로 이웃한 방끼리 연결됩니다.":
            "연결선을 클릭하거나, 연결된 방 두 개를 차례로 클릭하면 통로만 끊습니다. 방은 그대로 남습니다.";

        void FitGraph()
        {
            pan=Vector2.zero;
            if(dungeon==null||dungeon.rooms.Count==0){mapCenter=Vector2.zero;scale=1;return;}
            float minX=dungeon.rooms.Min(r=>r.cell.x),maxX=dungeon.rooms.Max(r=>r.cell.x),minY=dungeon.rooms.Min(r=>r.cell.y),maxY=dungeon.rooms.Max(r=>r.cell.y);
            mapCenter=new Vector2((minX+maxX)/2,(minY+maxY)/2);
            scale=Mathf.Clamp(Mathf.Min(1,Mathf.Min(Mathf.Max(600,position.width-390)/((maxX-minX)*180+220),Mathf.Max(500,position.height-170)/((maxY-minY)*145+170))),.2f,1);
        }
        Vector2 CellPoint(Vector2Int cell)=>new Vector2(canvas.width/2,canvas.height/2)+pan+new Vector2((cell.x-mapCenter.x)*180,-(cell.y-mapCenter.y)*145)*scale;
        Vector2Int CanvasCell(Vector2 p)
        {
            var q=(p-new Vector2(canvas.width/2,canvas.height/2)-pan)/scale;
            return new Vector2Int(Mathf.RoundToInt(q.x/180+mapCenter.x),Mathf.RoundToInt(-q.y/145+mapCenter.y));
        }
        Rect NodeRect(Vector2Int cell)
        {var p=CellPoint(cell);return new Rect(p.x-69*scale,p.y-43*scale,138*scale,86*scale);}
        DungeonRoom HitRoom(Vector2 point)=>dungeon.rooms.LastOrDefault(r=>NodeRect(r.cell).Contains(point));
        bool HitLink(Vector2 point,out DungeonRoom owner,out RoomPortal link)
        {
            owner=null;link=null;float best=11;
            foreach(var room in dungeon.rooms)foreach(var portal in room.portals)
            {
                var target=dungeon.FindRoom(portal.targetRoomId);if(target==null)continue;
                var a=CellPoint(room.cell);var b=CellPoint(target.cell);var delta=b-a;
                float distance=Vector2.Distance(point,a+delta*Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude)));
                if(distance<best){best=distance;owner=room;link=portal;}
            }
            return link!=null;
        }
        static void DrawLine(Vector2 a,Vector2 b,Color color,float width=4)
        {Handles.BeginGUI();Handles.color=color;Handles.DrawAAPolyLine(width,a,b);Handles.EndGUI();}

        void Graph()
        {
            EditorGUI.DrawRect(canvas,new Color(.07f,.095f,.12f));if(dungeon==null||dungeon.rooms.Count==0)return;
            Vector2 mouse=Event.current.mousePosition-canvas.position;
            var hover=CanvasCell(mouse);
            GUI.BeginGroup(canvas);
            var low=CanvasCell(new Vector2(-180,canvas.height+145));var high=CanvasCell(new Vector2(canvas.width+180,-145));
            for(int x=low.x;x<=high.x;x++)for(int y=low.y;y<=high.y;y++)
            {
                var p=CellPoint(new Vector2Int(x,y));
                EditorGUI.DrawRect(new Rect(p.x-90*scale,p.y-72.5f*scale,180*scale,1),new Color(1,1,1,.045f));
                EditorGUI.DrawRect(new Rect(p.x-90*scale,p.y-72.5f*scale,1,145*scale),new Color(1,1,1,.045f));
                EditorGUI.DrawRect(new Rect(p.x-1,p.y-1,2,2),new Color(.35f,.45f,.5f));
            }
            bool overLink=HitRoom(mouse)==null&&HitLink(mouse,out _,out _);
            // Keep the endpoints for a hovered link explicit, independent of room hit testing.
            HitLink(mouse,out _,out var nearest);
            foreach(var room in dungeon.rooms)foreach(var portal in room.portals)
            {
                var next=dungeon.FindRoom(portal.targetRoomId);if(next==null||string.CompareOrdinal(room.id,next.id)>0)continue;
                bool hot=graphTool==GraphTool.Disconnect&&overLink&&nearest!=null&&(portal.id==nearest.id||portal.id==nearest.targetPortalId);
                bool detaching=roomDragging&&(room.id==dragRoom||next.id==dragRoom)&&!RoomDungeonAuthoring.DirectionBetween(dragCell,(room.id==dragRoom?next:room).cell,out _);
                DrawLine(CellPoint(room.cell),CellPoint(next.cell),hot||detaching?new Color(1,.35f,.35f):new Color(.48f,.68f,.71f),hot?7:4);
            }
            var reachable=dungeon.ReachableRooms();
            foreach(var room in dungeon.rooms)
            {
                var rect=NodeRect(room.cell);
                Color color=room.isBoss?new Color(.45f,.19f,.23f):room.id==dungeon.startRoomId?new Color(.16f,.35f,.33f):new Color(.18f,.25f,.32f);
                Color edge=room.id==linkSource?new Color(1,.75f,.3f):room.id==selected?new Color(.35f,.85f,.95f):new Color(.35f,.43f,.47f);
                EditorGUI.DrawRect(new Rect(rect.x-3,rect.y-3,rect.width+6,rect.height+6),edge);EditorGUI.DrawRect(rect,color);
                GUI.Label(new Rect(rect.x,rect.y+5*scale,rect.width,22*scale),room.isBoss?"BOSS":room.id==dungeon.startRoomId?"START":!reachable.Contains(room.id)?"미연결 보관":"ROOM",small);
                GUI.Label(new Rect(rect.x,rect.y+27*scale,rect.width,25*scale),room.displayName,nameStyle);
                GUI.Label(new Rect(rect.x,rect.y+54*scale,rect.width,23*scale),room.layout==null?"배치 없음":"몬스터 "+room.layout.spawns.Sum(s=>s.count)+"마리",small);
            }
            if(linkSource!=null&&dungeon.FindRoom(linkSource)!=null)
                DrawLine(CellPoint(dungeon.FindRoom(linkSource).cell),mouse,graphTool==GraphTool.Disconnect?new Color(1,.35f,.35f):new Color(1,.75f,.3f),2);
            if((graphTool==GraphTool.Place&&canvas.Contains(Event.current.mousePosition+canvas.position))||roomDragging)
            {
                var cell=roomDragging?dragCell:hover;bool occupied=dungeon.rooms.Any(r=>r.cell==cell&&r.id!=dragRoom);
                var rect=NodeRect(cell);EditorGUI.DrawRect(rect,occupied?new Color(1,.2f,.2f,.25f):new Color(.25f,.85f,.75f,.25f));
                GUI.Label(rect,occupied?"이미 방이 있는 칸":roomDragging?"여기로 이동":"+ 방만 배치",nameStyle);
            }
            GUI.Label(new Rect(12,9,canvas.width-24,24),"방을 배치한 뒤, 필요한 통로만 연결하세요. 인접한 방도 통로 없이 둘 수 있습니다.",small);
            GUI.EndGroup();
        }

        void HandleNavigation()
        {
            if(dungeon==null)return;
            var e=Event.current;bool inside=canvas.Contains(e.mousePosition);Vector2 local=e.mousePosition-canvas.position;
            if(e.type==EventType.KeyDown&&!EditorGUIUtility.editingTextField)
            {
                if((e.control||e.command)&&e.keyCode==KeyCode.S){Save();e.Use();}
                else if(e.keyCode==KeyCode.Escape){linkSource=null;EndGraphDrag();e.Use();Repaint();}
                else if(e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha4){graphTool=(GraphTool)(e.keyCode-KeyCode.Alpha1);linkSource=null;EndGraphDrag();e.Use();Repaint();}
                else if(e.keyCode==KeyCode.F){FitGraph();e.Use();Repaint();}
            }
            if(e.type==EventType.MouseMove){Repaint();return;}
            if(e.type==EventType.ScrollWheel&&inside&&!roomDragging)
            {
                var relative=local-new Vector2(canvas.width/2,canvas.height/2)-pan;float old=scale;
                scale=Mathf.Clamp(scale*Mathf.Pow(1.1f,-e.delta.y),.2f,1.5f);pan+=relative*(1-scale/old);e.Use();Repaint();return;
            }
            if(e.type==EventType.MouseDown&&inside)
            {
                GUI.FocusControl(null);GUIUtility.keyboardControl=0;
                if(e.button==2)
                {panning=true;dragOrigin=e.mousePosition;panOrigin=pan;GUIUtility.hotControl=graphControl;e.Use();return;}
                if(e.button!=0)return;
                var room=HitRoom(local);
                if(graphTool==GraphTool.Move)
                {
                    if(room!=null)
                    {
                        selected=room.id;
                        if(e.clickCount==2)DungeonBuilderWindow.ShowRoom(dungeon,room.id);
                        else {dragRoom=room.id;dragCell=room.cell;dragOrigin=e.mousePosition;GUIUtility.hotControl=graphControl;}
                    }
                }
                else if(graphTool==GraphTool.Place)
                {
                    var cell=CanvasCell(local);
                    if(dungeon.rooms.Any(r=>r.cell==cell))status="이미 방이 있는 칸입니다.";else PlaceRoom(cell);
                }
                else if(graphTool==GraphTool.Disconnect&&room==null&&HitLink(local,out var owner,out var portal))
                {DisconnectPair(owner,portal);linkSource=null;}
                else if(room!=null)
                {
                    selected=room.id;
                    var source=dungeon.FindRoom(linkSource);
                    if(source==null){linkSource=room.id;status="상대 방을 클릭하세요. Esc로 취소합니다.";}
                    else if(source==room)linkSource=null;
                    else
                    {
                        if(graphTool==GraphTool.Connect)ConnectPair(source,room);
                        else
                        {var link=source.portals.Find(p=>p.targetRoomId==room.id);if(link!=null)DisconnectPair(source,link);else status="두 방 사이에는 통로가 없습니다.";}
                        linkSource=null;
                    }
                }
                else linkSource=null;
                e.Use();Repaint();return;
            }
            if(e.type==EventType.MouseDrag&&GUIUtility.hotControl==graphControl)
            {
                if(panning)pan=panOrigin+e.mousePosition-dragOrigin;
                else if(dragRoom!=null)
                {
                    if((e.mousePosition-dragOrigin).sqrMagnitude>16)roomDragging=true;
                    var room=dungeon.FindRoom(dragRoom);
                    if(room!=null)
                    {
                        var delta=(e.mousePosition-dragOrigin)/scale;
                        dragCell=room.cell+new Vector2Int(Mathf.RoundToInt(delta.x/180),Mathf.RoundToInt(-delta.y/145));
                    }
                }
                e.Use();Repaint();return;
            }
            if(e.type==EventType.MouseUp&&GUIUtility.hotControl==graphControl)
            {
                if(roomDragging&&inside){var room=dungeon.FindRoom(dragRoom);if(room!=null)MoveSelectedRoom(room,dragCell);}
                EndGraphDrag();e.Use();Repaint();
            }
        }

        void EndGraphDrag()
        {if(GUIUtility.hotControl==graphControl)GUIUtility.hotControl=0;panning=roomDragging=false;dragRoom=null;}
        void PlaceRoom(Vector2Int cell)
        {
            Undo.IncrementCurrentGroup();var room=RoomDungeonAuthoring.AddRoom(dungeon,cell,"방 "+(dungeon.rooms.Count+1));
            selected=room.id;Changed();status="방만 배치했습니다. 3 연결로 원하는 이웃 방과 통로를 만드세요.";
        }
        void ConnectPair(DungeonRoom from,DungeonRoom to)
        {
            if(!RoomDungeonAuthoring.DirectionBetween(from.cell,to.cell,out var direction)){status="상하좌우로 바로 이웃한 방끼리 연결해주세요.";return;}
            if(from.portals.Any(p=>p.targetRoomId==to.id)){status="이미 연결된 방입니다.";return;}
            if(from.portals.Any(p=>p.direction==direction)||to.portals.Any(p=>p.direction==RoomPortal.Opposite(direction))){status="해당 방향의 기존 통로를 먼저 끊어주세요.";return;}
            Undo.IncrementCurrentGroup();RoomDungeonAuthoring.Connect(dungeon,from,to,direction);Changed();status=from.displayName+" ↔ "+to.displayName+" 연결 완료";
        }
        void DisconnectPair(DungeonRoom from,RoomPortal portal)
        {Undo.IncrementCurrentGroup();RoomDungeonAuthoring.Disconnect(dungeon,from,portal);Changed();status="통로를 끊었습니다. 두 방의 내부 배치는 유지됩니다.";}
        void MoveSelectedRoom(DungeonRoom room,Vector2Int cell)
        {
            if(!RoomDungeonAuthoring.MoveRoom(dungeon,room,cell,out int removed)){status="이미 방이 있는 칸으로는 옮길 수 없습니다.";return;}
            Changed();status=removed==0?"방 위치 이동 완료":"방 이동 완료 · 이웃이 아니게 된 통로 "+removed+"개 해제 (Ctrl+Z로 함께 복구)";
        }
    }
}
