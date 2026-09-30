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
    public static class DungeonBuilderChecks
    {
        static int checks;
        static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Require(bool condition,string message)
        { checks++; if(!condition)throw new InvalidOperationException("Dungeon check failed: "+message); }
        static void Near(float actual,float expected,string message) => Require(Mathf.Abs(actual-expected)<.025f,message+" ("+actual+" != "+expected+")");

        [MenuItem("MiniWar/Tests/Dungeon builder checks")]
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
            checks=0;
            var example=DungeonBuilderAssets.CreateGateExample();
            Require(example.ValidateLayout().Count==0,"example is valid: "+string.Join("; ",example.ValidateLayout()));
            var data=UnityEngine.Object.Instantiate(example);
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Data/DungeonLayouts/__BuilderCheck.asset");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string scenePath=scene.path;bool wasDirty=scene.isDirty;
            try
            {
                AssetDatabase.CreateAsset(data,path);
                var window=DungeonBuilderWindow.ShowLayout(data);
                window.position=new Rect(60,80,1280,760);
                Set(window,"center",new Vector2(32,4));Set(window,"zoom",14f);
                window.SendEvent(new Event {type=EventType.Layout});
                int floors=data.floors.Count,spawns=data.spawns.Count;
                Tool(window,1);
                Mouse(window,EventType.MouseDown,new Vector2(44,2));
                Mouse(window,EventType.MouseDrag,new Vector2(48,3));
                Mouse(window,EventType.MouseUp,new Vector2(48,3));
                Require(data.floors.Count==floors+1,"drag creates one floor");
                var floor=data.floors.Last(); Near(floor.rect.width,4,"floor width");Near(floor.rect.height,1,"floor height");Near(floor.rect.yMax,3,"draw floor top");
                Undo.FlushUndoRecordObjects();Undo.PerformUndo();Require(data.floors.Count==floors,"undo floor draw");
                Undo.PerformRedo();Require(data.floors.Count==floors+1,"redo floor draw");floor=data.floors.Last();
                Tool(window,0);
                Mouse(window,EventType.MouseDown,new Vector2(46,2.5f));
                Mouse(window,EventType.MouseDrag,new Vector2(47,2.5f));
                Mouse(window,EventType.MouseUp,new Vector2(47,2.5f));
                Near(data.floors.Last().rect.x,45,"drag moves selected floor");Near(data.floors.Last().rect.yMax,3,"move preserves floor height");
                Undo.PerformUndo();Near(data.floors.Last().rect.x,44,"undo move");
                Undo.PerformRedo();Near(data.floors.Last().rect.x,45,"redo move");
                // Grab the highlighted lower-right corner and resize.
                Mouse(window,EventType.MouseDown,new Vector2(49,2));
                Mouse(window,EventType.MouseDrag,new Vector2(50,1.5f));
                Mouse(window,EventType.MouseUp,new Vector2(50,1.5f));
                Near(data.floors.Last().rect.width,5,"resize width");Near(data.floors.Last().rect.height,1.5f,"resize height");Near(data.floors.Last().rect.yMax,3,"resize preserves top anchor");
                Tool(window,2);Set(window,"brushEnemy",example.spawns[0].enemy);
                Mouse(window,EventType.MouseDown,new Vector2(46,4));Mouse(window,EventType.MouseUp,new Vector2(46,4));
                Require(data.spawns.Count==spawns+1,"click places monster");Near(data.spawns.Last().position.y,3,"monster feet snap to floor");
                Call(window,"DuplicateSelection");Undo.FlushUndoRecordObjects();
                Require(data.spawns.Count==spawns+2,"duplicate monster");
                Require(data.spawns.Last().enemy==example.spawns[0].enemy,"duplicate retains enemy reference");
                Require(data.spawns.Last().id!=data.spawns[data.spawns.Count-2].id,"duplicate gets unique ID");
                Call(window,"DeleteSelection");Undo.FlushUndoRecordObjects();Require(data.spawns.Count==spawns+1,"delete monster");
                Undo.PerformUndo();Require(data.spawns.Count==spawns+2,"undo delete");
                Require(GUIUtility.hotControl==0,"canvas releases mouse capture");
                EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
                string serialized=File.ReadAllText(path);
                Require(serialized.Contains(data.spawns.Last().id),"saved stable spawn ID");
                Call(window,"SetLayout",example);Undo.ClearUndo(data);Resources.UnloadAsset(data);
                data=AssetDatabase.LoadAssetAtPath<DungeonLayout>(path);
                Require(data.floors.Count==floors+1 && data.spawns.Count==spawns+2,"save / unload / reload retains placements");
                Require(data.spawns.Last().enemy==example.spawns[0].enemy,"reload retains EnemyData reference");
                Near(data.floors.Last().rect.width,5,"reload retains resized geometry");
                TestValidation(data);
                TestMovement();
                DungeonBuilderAssets.PrepareSandbox(example);
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==scenePath,"sandbox preserves active scene");
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty==wasDirty,"sandbox preserves scene dirty state");
                Require(!EditorBuildSettings.scenes.Any(s=>s.path==DungeonBuilderAssets.SandboxPath),"sandbox stays out of client build scene list");
                string report="PASS: "+checks+" dungeon checks (canvas gestures, undo/redo, persistence, validation, movement and scene isolation).";
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DungeonBuilderChecks.txt",report);Debug.Log(report);return report;
            }
            finally
            {
                DungeonBuilderWindow.ShowLayout(example);
                if(data!=null)Undo.ClearUndo(data);
                AssetDatabase.DeleteAsset(path);
            }
        }

        static void TestValidation(DungeonLayout data)
        {
            var copy=UnityEngine.Object.Instantiate(data);
            try
            {
                copy.entrance=new Vector2(3,5);Require(copy.ValidateLayout().Any(x=>x.StartsWith("입구")&&x.Contains("발밑")),"unsupported entrance detected");
                copy.entrance=new Vector2(3,-1);Require(copy.ValidateLayout().Any(x=>x.StartsWith("입구")&&x.Contains("겹칩니다")),"buried entrance detected");
                copy.spawns[0].enemy=null;Require(copy.ValidateLayout().Any(x=>x.Contains("종류가 지정")),"missing enemy detected");
                copy.spawns[0].count=31;Require(copy.ValidateLayout().Any(x=>x.Contains("수량")),"invalid group count detected");
                copy.floors[0].rect=new Rect(0,-2,-3,2);Require(copy.ValidateLayout().Any(x=>x.Contains("크기가")),"invalid floor dimensions detected");
            }
            finally {UnityEngine.Object.DestroyImmediate(copy);}
        }

        static void TestMovement()
        {
            var map=ScriptableObject.CreateInstance<DungeonLayout>();
            try
            {
                map.floors.Add(new DungeonFloor {rect=new Rect(0,-2,60,2)});
                var motor=new DungeonPreviewMotor(map);
                for(int i=0;i<50;i++)motor.Step(.02f,1,false,false);
                Near(motor.Position.x,7,"walk five metres per second");Near(motor.Position.y,0,"ground remains stable");
                float apex=0;for(int i=0;i<50;i++){motor.Step(.02f,0,i==0,false);apex=Mathf.Max(apex,motor.Position.y);}
                Require(apex>1.2f&&apex<1.4f,"jump apex matches game rules");Require(motor.Grounded,"lands after jump");
                motor.Reset();for(int i=0;i<50;i++)motor.Step(.02f,0,false,i==0||i==10||i==20);
                Near(motor.Position.x,6.8f,"two dashes; third rejected");
                motor.Reset();for(int i=0;i<60;i++)motor.Step(.02f,0,false,i==0||i==55);
                Near(motor.Position.x,4.4f,"second input after one second rejected");
                motor.Reset();for(int i=0;i<170;i++)motor.Step(.02f,0,false,i==0||i==155);
                Near(motor.Position.x,6.8f,"dash available after three-second cooldown");
                motor.Reset();for(int i=0;i<30;i++)motor.Step(.02f,0,false,i==0||i==2);
                Near(motor.Position.x,6.8f,"fast second press queues a full dash");
                map.floors.Add(new DungeonFloor {rect=new Rect(4,0,1,5)});motor.Reset();
                for(int i=0;i<50;i++)motor.Step(.02f,1,false,i==0||i==10);
                Near(motor.Position.x,3.7f,"dash cannot tunnel through wall");
                map.floors.RemoveAt(1);map.floors.Add(new DungeonFloor {rect=new Rect(3,0,3,1)});motor.Reset();
                for(int i=0;i<25;i++)motor.Step(.02f,1,i==0,false);
                Require(motor.Position.x>3.4f&&motor.Position.y>.9f,"jump reaches a one-metre platform");
                for(int i=0;i<35;i++)motor.Step(.02f,0,false,false);
                Near(motor.Position.y,1,"stands on raised platform");
                map.floors.RemoveAt(1);map.floors.Add(new DungeonFloor {rect=new Rect(0,3,8,1)});motor.Reset();
                apex=0;for(int i=0;i<40;i++){motor.Step(.02f,0,i==0,false);apex=Mathf.Max(apex,motor.Position.y);}
                Require(apex<=.701f,"ceiling collision stops ascent");
                map.floors.Clear();map.floors.Add(new DungeonFloor {rect=new Rect(0,-2,4,2)});motor.Reset();
                bool respawned=false;
                for(int i=0;i<100;i++){motor.Step(.02f,1,false,false);if(i>30&&motor.Position.x<3){respawned=true;break;}}
                Require(respawned,"falling out of map returns to entrance");
            }
            finally {UnityEngine.Object.DestroyImmediate(map);}
        }

        static void Tool(DungeonBuilderWindow window,int value)
        {var field=typeof(DungeonBuilderWindow).GetField("tool",Private);field.SetValue(window,Enum.ToObject(field.FieldType,value));}
        static void Set(DungeonBuilderWindow window,string field,object value)=>typeof(DungeonBuilderWindow).GetField(field,Private).SetValue(window,value);
        static void Call(DungeonBuilderWindow window,string method,params object[] args)=>typeof(DungeonBuilderWindow).GetMethod(method,Private).Invoke(window,args);
        static void Mouse(DungeonBuilderWindow window,EventType type,Vector2 world)
        {
            // Same geometry as the visible viewport, using its live coordinate conversion.
            var local=(Vector2)typeof(DungeonBuilderWindow).GetMethod("WorldToCanvas",Private).Invoke(window,new object[]{world});
            var canvas=(Rect)typeof(DungeonBuilderWindow).GetField("canvas",Private).GetValue(window);
            // SendEvent addresses the host panel (including its tab strip), while OnGUI uses content coordinates.
            window.SendEvent(new Event {type=type,button=0,mousePosition=local+canvas.position+window.rootVisualElement.worldBound.position});
        }
    }
}
