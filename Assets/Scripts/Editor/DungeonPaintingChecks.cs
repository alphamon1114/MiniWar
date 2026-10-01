using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MiniWar.Data;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class DungeonPaintingChecks
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static int checks;
        static void Check(bool condition,string label)
        {checks++;if(!condition)throw new InvalidOperationException("Painting check: "+label);}
        static void Set(DungeonBuilderWindow w,string field,object value)=>typeof(DungeonBuilderWindow).GetField(field,Private).SetValue(w,value);
        static void Tool(DungeonBuilderWindow w,int value)
        {var f=typeof(DungeonBuilderWindow).GetField("tool",Private);f.SetValue(w,Enum.ToObject(f.FieldType,value));}
        static void Mouse(DungeonBuilderWindow w,EventType type,Vector2 point,int button=0)
        {
            var local=(Vector2)typeof(DungeonBuilderWindow).GetMethod("WorldToCanvas",Private).Invoke(w,new object[]{point});
            var canvas=(Rect)typeof(DungeonBuilderWindow).GetField("canvas",Private).GetValue(w);
            w.SendEvent(new Event{type=type,button=button,mousePosition=local+canvas.position+w.rootVisualElement.worldBound.position});
        }
        static void Stroke(DungeonBuilderWindow w,Vector2 from,Vector2 to,int button=0)
        {Mouse(w,EventType.MouseDown,from,button);Mouse(w,EventType.MouseDrag,to,button);Mouse(w,EventType.MouseUp,to,button);}
        static void UndoStroke(){Undo.FlushUndoRecordObjects();Undo.PerformUndo();}

        [MenuItem("MiniWar/Tests/Map painting checks")]
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            checks=0;
            foreach(var old in Resources.FindObjectsOfTypeAll<DungeonBuilderWindow>())old.Close();
            var example=RoomDungeonPlatforms.CreateExample();
            string sourceJson=EditorJsonUtility.ToJson(example.rooms[0].layout);
            var working=DungeonBuilderWindow.GetWorkingMap();
            Check(working==DungeonBuilderWindow.GetWorkingMap(),"reopening retains existing work");
            Check(working.rooms.All(r=>r.layout!=example.FindRoom(r.id)?.layout),"working copy has independent room layouts");
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Data/DungeonLayouts/__PaintingCheck.asset");
            var map=ScriptableObject.CreateInstance<DungeonLayout>();DungeonBuilderWindow w=null;
            try
            {
                map.bounds=new Rect(0,-2,48,16);map.gridSize=.5f;
                map.entrance=new Vector2(4,0);map.exit=new Vector2(45,0);
                map.floors.Add(new DungeonFloor{rect=new Rect(0,-2,48,2)});
                AssetDatabase.CreateAsset(map,path);
                w=DungeonBuilderWindow.ShowLayout(map);w.position=new Rect(60,80,1280,800);
                Set(w,"zoom",19f);Set(w,"center",new Vector2(24,6));Set(w,"brushLine",true);
                Set(w,"brushOneWay",true);Set(w,"brushThickness",.5f);
                w.SendEvent(new Event{type=EventType.Layout});Tool(w,1);
                Stroke(w,new Vector2(10,9),new Vector2(18,10));
                Check(map.floors.Count==2,"horizontal drag paints one platform");
                Check(map.floors.Last().rect==new Rect(10,8.5f,8,.5f),"mouse-down fixes top height despite vertical jitter");
                Check(map.floors.Last().oneWay,"platform is jump-through");
                UndoStroke();Check(map.floors.Count==1,"undo horizontal platform");
                Undo.PerformRedo();Check(map.floors.Count==2,"redo horizontal platform");
                Stroke(w,new Vector2(44,11),new Vector2(49,14));
                Check(Mathf.Abs(map.floors.Last().rect.xMax-48)<.01f&&Mathf.Abs(map.floors.Last().rect.yMax-11)<.01f,"captured drag clips to map bounds: "+map.floors.Last().rect+" count="+map.floors.Count);
                UndoStroke();
                Mouse(w,EventType.MouseDown,new Vector2(30,10));Mouse(w,EventType.MouseDrag,new Vector2(23,11));
                w.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
                Mouse(w,EventType.MouseUp,new Vector2(23,11));Check(map.floors.Count==2,"Escape cancels floor preview");
                Set(w,"brushLine",false);Set(w,"brushOneWay",false);
                Stroke(w,new Vector2(30,11),new Vector2(26,9));
                Check(map.floors.Last().rect==new Rect(26,9,4,2)&&!map.floors.Last().oneWay,"reverse rectangle draws solid wall");
                UndoStroke();
                Tool(w,2);Set(w,"brushEnemy",AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/DungeonLayouts/Enemies/DaggerGuard.asset"));
                Set(w,"brushMonsterSprite",null);Set(w,"monsterStroke",true);Set(w,"monsterSpacing",2f);
                Stroke(w,new Vector2(8,.5f),new Vector2(16,.5f));
                Check(map.spawns.Count==5,"fast drag interpolates five placements at two metre intervals");
                Check(map.spawns.All(s=>s.position.y==0&&s.VisualSprite!=null),"monsters snap to ground with idle art");
                Check(map.spawns.Select(s=>s.position.x).SequenceEqual(new[]{8f,10f,12f,14f,16f}),"even monster spacing");
                Check(map.spawns.Select(s=>s.id).Distinct().Count()==5,"unique spawn identities");
                UndoStroke();Check(map.spawns.Count==0,"one undo removes entire monster stroke");
                Undo.PerformRedo();Check(map.spawns.Count==5,"redo restores entire stroke");
                Mouse(w,EventType.MouseDown,new Vector2(24,.5f));Mouse(w,EventType.MouseDrag,new Vector2(30,.5f));
                w.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
                Mouse(w,EventType.MouseUp,new Vector2(30,.5f));Check(map.spawns.Count==5,"Escape rolls back monster stroke");
                Stroke(w,new Vector2(20,-1),new Vector2(20,1));
                Check(map.spawns.Count==6,"snapping does not stack duplicate monsters during vertical stroke");UndoStroke();
                Stroke(w,new Vector2(8,1),new Vector2(16,1),1);
                Check(map.spawns.Count==0,"right drag erases all crossed monsters");
                Check(map.floors.Count==2,"erasing monsters above ground keeps ground");
                UndoStroke();Check(map.spawns.Count==5,"one undo restores whole erase stroke");
                Tool(w,5);Stroke(w,new Vector2(14,8.75f),new Vector2(14,8.75f));
                Check(map.floors.Count==1,"eraser deletes platform");UndoStroke();
                Mouse(w,EventType.MouseDown,new Vector2(8,1),1);Mouse(w,EventType.MouseDrag,new Vector2(16,1),1);
                w.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});
                Mouse(w,EventType.MouseUp,new Vector2(16,1),1);Check(map.spawns.Count==5,"Escape restores erased objects");
                Vector2 entrance=map.entrance;Stroke(w,new Vector2(4,1),new Vector2(4,1),1);
                Check(map.entrance==entrance&&map.floors.Count==2,"eraser preserves entrance");
                Tool(w,2);Stroke(w,new Vector2(-1,0),new Vector2(-1,0));
                Check(map.spawns.Count==5,"out of bounds monster is rejected");
                Set(w,"monsterStroke",false);Stroke(w,new Vector2(24,.5f),new Vector2(30,.5f));
                Check(map.spawns.Count==6,"click-only brush does not paint on drag");
                Set(w,"selected",map.spawns.Last().id);Tool(w,0);
                Stroke(w,new Vector2(24,1),new Vector2(26,1));
                Check(map.spawns.Last().position==new Vector2(26,0),"selection drag moves painted monster");UndoStroke();
                Check(map.spawns.Last().position==new Vector2(24,0),"undo move restores feet");
                EditorUtility.SetDirty(map);AssetDatabase.SaveAssetIfDirty(map);
                w.Close();w=null;Undo.ClearUndo(map);Resources.UnloadAsset(map);
                map=AssetDatabase.LoadAssetAtPath<DungeonLayout>(path);
                Check(map.spawns.Count==6&&map.floors.Count==2&&map.floors.Last().oneWay,"saved painting survives reload");
                Check(EditorJsonUtility.ToJson(example.rooms[0].layout)==sourceJson,"source example preserved");
            }
            finally
            {
                if(w!=null)w.Close();if(map!=null)Undo.ClearUndo(map);AssetDatabase.DeleteAsset(path);
            }
            TestIndividualMonsters();
            string report="PASS: "+checks+" map painting checks (real mouse strokes, individual monsters, snapping, cancel, undo/redo, erasing, bounds and persistence).";
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DungeonPaintingChecks.txt",report);Debug.Log(report);return report;
        }

        static void TestIndividualMonsters()
        {
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Data/DungeonLayouts/__IndividualMonsterCheck.asset");
            var map=ScriptableObject.CreateInstance<DungeonLayout>();DungeonBuilderWindow w=null;
            try
            {
                map.bounds=new Rect(0,-2,48,16);map.entrance=new Vector2(4,0);map.exit=new Vector2(45,0);
                map.floors.Add(new DungeonFloor{rect=new Rect(0,-2,48,2)});
                var enemy=AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/DungeonLayouts/Enemies/Patrol.asset");
                var group=new DungeonSpawn{enemy=enemy,sprite=enemy.idleSprite,position=new Vector2(8,0),count=3,spacing=8,faceLeft=false};
                string originalId=group.id;map.spawns.Add(group);
                AssetDatabase.CreateAsset(map,path);w=DungeonBuilderWindow.ShowLayout(map);
                w.position=new Rect(60,80,1280,800);Set(w,"zoom",19f);Set(w,"center",new Vector2(24,6));
                w.SendEvent(new Event{type=EventType.Layout});Tool(w,0);
                Stroke(w,new Vector2(8,1),new Vector2(10,1));
                Check(map.spawns.Count==3&&map.spawns.All(s=>s.count==1),"legacy group becomes individual placements");
                Check(map.spawns.Select(s=>s.position.x).SequenceEqual(new[]{10f,16f,24f}),"moving first member keeps the others fixed");
                Check(map.spawns[0].id==originalId&&map.spawns.Select(s=>s.id).Distinct().Count()==3,"split preserves original identity and assigns unique new IDs");
                Check(map.spawns.All(s=>s.enemy==enemy&&s.sprite==enemy.idleSprite&&!s.faceLeft),"split preserves enemy, sprite override and facing");
                UndoStroke();Check(map.spawns.Count==1&&map.spawns[0].count==3&&map.spawns[0].position.x==8,"single undo restores group and movement");
                Stroke(w,new Vector2(24,1),new Vector2(26,1));
                Check(map.spawns.Select(s=>s.position.x).SequenceEqual(new[]{8f,16f,26f}),"moving last member uses its own feet position");
                string movedId=map.spawns[2].id;
                UndoStroke();Undo.PerformRedo();
                Check(map.spawns[2].position.x==26&&map.spawns[2].id==movedId,"redo preserves individual identity and location");
                UndoStroke();
                Stroke(w,new Vector2(16,1),new Vector2(18,1));
                Check(map.spawns.Select(s=>s.position.x).SequenceEqual(new[]{8f,18f,24f}),"moving middle member does not shift either neighbor");
                UndoStroke();
                Mouse(w,EventType.MouseDown,new Vector2(24,1));Mouse(w,EventType.MouseDrag,new Vector2(28,1));
                w.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape});Mouse(w,EventType.MouseUp,new Vector2(28,1));
                Check(map.spawns.Count==1&&map.spawns[0].count==3&&map.spawns[0].position.x==8,"Escape restores legacy group without moving neighbors");
                Stroke(w,new Vector2(16,1),new Vector2(16,1),1);
                Check(map.spawns.Count==2&&map.spawns.All(s=>s.count==1)&&map.spawns.Select(s=>s.position.x).SequenceEqual(new[]{8f,24f}),"right-click deletes only the middle monster");
                UndoStroke();Check(map.spawns.Count==1&&map.spawns[0].count==3,"undo individual erase restores group");
                Stroke(w,new Vector2(24,1),new Vector2(24,1));
                w.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.D,control=true});Undo.FlushUndoRecordObjects();
                Check(map.spawns.Count==4&&map.spawns.All(s=>s.count==1)&&map.spawns.Last().position.x>24&&map.spawns.Last().position.x<=29,"duplicate adds one nearby monster, not a legacy group");
                UndoStroke();Check(map.spawns.Count==3,"undo duplicate removes one monster");
                Stroke(w,new Vector2(16,1),new Vector2(16,1));
                w.SendEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Delete});Undo.FlushUndoRecordObjects();
                Check(map.spawns.Select(s=>s.position.x).SequenceEqual(new[]{8f,24f}),"Delete removes only selected monster");
                UndoStroke();Check(map.spawns.Count==3,"undo Delete restores selected monster");
                Check(map.spawns.Sum(s=>s.count)==3,"editing retains original monster total");
                EditorUtility.SetDirty(map);AssetDatabase.SaveAssetIfDirty(map);w.Close();w=null;
                Undo.ClearUndo(map);Resources.UnloadAsset(map);map=AssetDatabase.LoadAssetAtPath<DungeonLayout>(path);
                Check(map.spawns.Count==3&&map.spawns.All(s=>s.count==1)&&map.spawns.Select(s=>s.position.x).SequenceEqual(new[]{8f,16f,24f}),"individual positions persist after asset reload");
            }
            finally
            {if(w!=null)w.Close();if(map!=null)Undo.ClearUndo(map);AssetDatabase.DeleteAsset(path);}
        }
    }
}
