using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class DungeonPlatformChecks
    {
        static int checks;
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Check(bool valid,string message)
        {checks++;if(!valid)throw new InvalidOperationException("Platform check: "+message);}
        static void Near(float value,float expected,string message)=>Check(Mathf.Abs(value-expected)<.03f,message+" ("+value+")");
        static void Settle(DungeonPreviewMotor motor,int frames=80)
        {for(int i=0;i<frames;i++)motor.Step(.02f,0,false,false);}

        [MenuItem("MiniWar/Tests/Multilevel dungeon checks")]
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            checks=0;
            var example=RoomDungeonPlatforms.CreateExample();
            Check(example.ValidateDungeon().Count==0,string.Join("; ",example.ValidateDungeon()));
            var original=RoomDungeonAuthoring.CreateExample();
            Check(example.rooms.Select(r=>r.id).SequenceEqual(original.rooms.Select(r=>r.id)),"room identities unchanged");
            Check(example.rooms.SelectMany(r=>r.portals).Select(p=>p.targetPortalId).SequenceEqual(original.rooms.SelectMany(r=>r.portals).Select(p=>p.targetPortalId)),"approved connections preserved");
            Check(example.rooms.All(r=>r.layout.spawns.Sum(s=>s.count)==8),"eight enemies in every room");
            Check(example.rooms.All(r=>r.layout.spawns.Select(s=>s.position.y).Distinct().Count()==3),"enemies occupy three levels");
            Check(example.rooms.All(r=>r.layout!=original.FindRoom(r.id).layout),"original room layouts preserved");
            var boss=example.rooms.Single(r=>r.isBoss);
            Check(boss.layout.spawns.Where(s=>s.enemy.isBoss).Sum(s=>s.count)==1,"one boss and seven guards");
            TestMovement();TestEditor(example);
            string report="PASS: "+checks+" multilevel checks (jump-through, three-floor routes, drop-through, solids, undo and persistence).";
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DungeonPlatformChecks.txt",report);Debug.Log(report);return report;
        }

        static void TestMovement()
        {
            var map=ScriptableObject.CreateInstance<DungeonLayout>();
            try
            {
                map.bounds=new Rect(0,-2,24,20);map.entrance=new Vector2(4,0);map.jumpHeight=3.5f;
                var ground=new DungeonFloor {rect=new Rect(0,-2,24,2)};
                var low=new DungeonFloor {rect=new Rect(2,2.5f,8,.5f),oneWay=true};
                var high=new DungeonFloor {rect=new Rect(7,5.5f,10,.5f),oneWay=true};
                map.floors.AddRange(new[]{ground,low,high});
                var motor=new DungeonPreviewMotor(map);
                Check(!motor.CanDrop,"solid ground cannot drop");
                motor.Step(.02f,0,true,false);Settle(motor);
                Near(motor.Position.y,3,"jump through first platform then land on it");
                Check(motor.Grounded&&motor.CanDrop,"platform is stable and droppable");
                float apex=3;
                for(int i=0;i<65;i++){motor.Step(.02f,i<45?1:0,i==0,false);apex=Mathf.Max(apex,motor.Position.y);}
                Near(motor.Position.y,6,"diagonal jump reaches top floor");
                Check(apex>6.2f&&apex<6.5f,"jump has headroom above a three-metre step");
                motor.Step(.02f,0,false,false,true);Settle(motor);
                Near(motor.Position.y,3,"drop stops at middle floor");
                motor.Step(.02f,0,false,false,true);Settle(motor);
                Near(motor.Position.y,0,"second drop reaches ground");
                motor.Step(.02f,0,true,false,true);Settle(motor);
                Near(motor.Position.y,0,"down+jump cannot pass solid ground");

                motor.Reset(new Vector2(8,3));motor.Step(.02f,0,false,false,true);motor.Reset(new Vector2(8,3));Settle(motor);
                Near(motor.Position.y,3,"reset cancels platform exclusion");
                motor.Reset(new Vector2(1.6f,2.8f));motor.Step(.1f,1,false,false);
                Check(motor.Position.x>2,"platform sides do not block movement");
                Check(map.HasClearance(new Vector2(4,1),new Vector2(.6f,2.3f)),"one-way overlap is valid clearance");
                map.jumpHeight=1;motor.Reset();motor.Step(.02f,0,true,false);float shortApex=0;
                for(int i=0;i<80;i++){motor.Step(.02f,0,false,false);shortApex=Mathf.Max(shortApex,motor.Position.y);}
                Check(shortApex>.8f&&shortApex<1.1f,"head passes through without snapping feet to unreachable platform");
                Near(motor.Position.y,0,"insufficient jump returns to ground");map.jumpHeight=3.5f;

                motor.Reset(new Vector2(8,12));motor.Step(.75f,0,false,false);
                Near(motor.Position.y,6,"large falling step catches highest crossed platform");
                map.floors.Reverse();motor.Reset(new Vector2(8,12));motor.Step(.75f,0,false,false);
                Near(motor.Position.y,6,"landing independent of floor list order");
                var close=new DungeonFloor {rect=new Rect(2,2.55f,8,.25f),oneWay=true};map.floors.Add(close);
                motor.Reset(new Vector2(8,3));motor.Step(.02f,0,false,false,true);Settle(motor);
                Near(motor.Position.y,2.8f,"dropping ignores only current support, even with close floors");map.floors.Remove(close);
                var solid=new DungeonFloor {rect=new Rect(2,2.5f,8,.5f)};map.floors.Add(solid);
                motor.Reset(new Vector2(8,3));Check(!motor.CanDrop,"solid overlap prevents dropping through it");map.floors.Remove(solid);
                map.floors.Clear();map.floors.Add(ground);map.floors.Add(new DungeonFloor {rect=new Rect(0,3,24,1)});
                motor.Reset();motor.Step(.02f,0,true,false);float ceilingApex=0;
                for(int i=0;i<80;i++){motor.Step(.02f,0,false,false);ceilingApex=Mathf.Max(ceilingApex,motor.Position.y);}
                Check(ceilingApex<=.701f,"solid ceiling still blocks higher jump");
                map.floors.RemoveAt(1);map.floors.Add(new DungeonFloor {rect=new Rect(6,0,1,10)});
                motor.Reset();for(int i=0;i<50;i++)motor.Step(.02f,1,false,i==0||i==10);
                Near(motor.Position.x,5.7f,"two dashes cannot cross a solid wall");
                map.jumpHeight=float.NaN;Check(map.ValidateLayout().Any(s=>s.Contains("점프 높이")),"invalid jump height rejected");
            }
            finally{UnityEngine.Object.DestroyImmediate(map);}
        }

        static void TestEditor(RoomDungeon example)
        {
            var data=UnityEngine.Object.Instantiate(example.rooms[0].layout);
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Data/DungeonLayouts/__PlatformCheck.asset");
            DungeonBuilderWindow window=null;
            try
            {
                AssetDatabase.CreateAsset(data,path);window=DungeonBuilderWindow.ShowLayout(data);window.position=new Rect(60,80,1280,760);
                var type=typeof(DungeonBuilderWindow);type.GetField("zoom",Private).SetValue(window,32f);
                type.GetField("center",Private).SetValue(window,new Vector2(12,6));
                var field=type.GetField("tool",Private);field.SetValue(window,Enum.ToObject(field.FieldType,1));
                type.GetField("brushOneWay",Private).SetValue(window,true);window.SendEvent(new Event{type=EventType.Layout});
                int count=data.floors.Count;Mouse(window,EventType.MouseDown,new Vector2(3,9));Mouse(window,EventType.MouseDrag,new Vector2(9,9.5f));Mouse(window,EventType.MouseUp,new Vector2(9,9.5f));
                Check(data.floors.Count==count+1&&data.floors.Last().oneWay,"floor brush creates one-way platform");
                Undo.FlushUndoRecordObjects();Undo.PerformUndo();Check(data.floors.Count==count,"undo painted platform");
                Undo.PerformRedo();Check(data.floors.Last().oneWay,"redo preserves collision type");
                type.GetMethod("DuplicateSelection",Private).Invoke(window,null);Undo.FlushUndoRecordObjects();
                Check(data.floors.Count==count+2&&data.floors.Last().oneWay,"duplicate preserves platform type");
                EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);DungeonBuilderWindow.ShowRoom(example,example.startRoomId);
                Undo.ClearUndo(data);Resources.UnloadAsset(data);data=AssetDatabase.LoadAssetAtPath<DungeonLayout>(path);
                Check(data.floors.Count==count+2&&data.floors.Last().oneWay,"reload preserves platform type");Near(data.jumpHeight,3.5f,"reload preserves jump tuning");
            }
            finally
            {
                if(data!=null)Undo.ClearUndo(data);if(window!=null)window.Close();AssetDatabase.DeleteAsset(path);
            }
        }

        static void Mouse(DungeonBuilderWindow window,EventType type,Vector2 world)
        {
            var local=(Vector2)typeof(DungeonBuilderWindow).GetMethod("WorldToCanvas",Private).Invoke(window,new object[]{world});
            var canvas=(Rect)typeof(DungeonBuilderWindow).GetField("canvas",Private).GetValue(window);
            window.SendEvent(new Event{type=type,button=0,mousePosition=local+canvas.position+window.rootVisualElement.worldBound.position});
        }
    }
}
