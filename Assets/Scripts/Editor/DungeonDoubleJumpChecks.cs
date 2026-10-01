using System;
using System.IO;
using System.Linq;
using MiniWar.Dungeons;
using MiniWar.Online;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class DungeonDoubleJumpChecks
    {
        static int checks;
        static void Check(bool valid,string label)
        {checks++;if(!valid)throw new InvalidOperationException("Double jump: "+label);}
        static void Settle(DungeonPreviewMotor motor,int frames=100)
        {for(int i=0;i<frames;i++)motor.Step(.02f,0,false,false);}

        [MenuItem("MiniWar/Tests/Double jump checks")]
        public static string Run()
        {
            checks=0;var map=ScriptableObject.CreateInstance<DungeonLayout>();
            try
            {
                map.bounds=new Rect(0,-3,32,24);map.entrance=new Vector2(4,0);
                var ground=new DungeonFloor{rect=new Rect(0,-2,32,2)};map.floors.Add(ground);
                Check(Mathf.Abs(map.airJumpHeight-LanRules.AirJumpHeight)<.001f,"default air jump uses shared two-metre tuning");
                var motor=new DungeonPreviewMotor(map);
                Check(motor.Grounded&&!motor.CanAirJump,"starts on ground");
                motor.Step(.02f,0,true,false);
                Check(motor.CanAirJump&&motor.VerticalSpeed>0,"ground jump retains one air jump");
                for(int i=0;i<15;i++)motor.Step(.02f,0,false,false);
                Check(motor.Position.y>1.2f&&motor.Position.y<1.4f,"first jump height unchanged");
                motor.Step(.02f,0,true,false);
                Check(!motor.CanAirJump&&Mathf.Abs(motor.VerticalSpeed-(LanRules.AirJumpSpeed-.48f))<.001f,"second press replaces vertical speed and consumes air jump");
                float vy=motor.VerticalSpeed;
                motor.Step(.02f,0,true,false);
                Check(Mathf.Abs(motor.VerticalSpeed-(vy-.48f))<.001f,"third jump is ignored");
                float apex=motor.Position.y;
                for(int i=0;i<85;i++){motor.Step(.02f,0,false,false);apex=Mathf.Max(apex,motor.Position.y);}
                Check(apex>3.1f&&apex<3.3f,"combined default reach has headroom above 2.5m platforms");
                Check(motor.Grounded&&Mathf.Abs(motor.Position.y)<.001f,"lands back on ground");
                motor.Step(.02f,0,true,false);Check(motor.CanAirJump,"landing refills air jump");
                motor.Reset(new Vector2(4,6));Settle(motor,20);Check(motor.VerticalSpeed<0,"falling before recovery jump");
                float fallHeight=motor.Position.y;motor.Step(.02f,0,true,false);
                Check(motor.Position.y>fallHeight&&motor.VerticalSpeed>9&&!motor.CanAirJump,"air jump immediately reverses a fall");
                motor.Step(.02f,0,false,true);Check(!motor.CanAirJump,"air dash cannot refill jump");
                motor.Reset();motor.Step(.02f,0,true,false);Check(motor.CanAirJump,"reset restores availability");

                ground.rect=new Rect(0,-2,5,2);motor.Reset();
                for(int i=0;i<18;i++)motor.Step(.02f,1,false,false);
                Check(!motor.Grounded&&motor.CanAirJump,"walking off an edge keeps one recovery jump");
                motor.Step(.02f,0,true,false);Check(!motor.CanAirJump&&motor.VerticalSpeed>9,"ledge recovery consumes that single jump");
                ground.rect=new Rect(0,-2,32,2);

                var platform=new DungeonFloor{rect=new Rect(2,2,10,.5f),oneWay=true};map.floors.Add(platform);
                motor.Reset();motor.Step(.02f,0,true,false);Settle(motor);
                Check(motor.Grounded&&Mathf.Abs(motor.Position.y)<.001f,"single jump cannot reach 2.5m platform");
                motor.Step(.02f,0,true,false);Settle(motor,15);motor.Step(.02f,0,true,false);Settle(motor);
                Check(motor.Grounded&&Mathf.Abs(motor.Position.y-2.5f)<.01f,"double jump passes through then lands on 2.5m platform");
                motor.Step(.02f,0,true,false);Check(motor.CanAirJump,"platform landing refills air jump");
                motor.Reset(new Vector2(4,2.5f));motor.Step(.02f,0,false,false,true);
                Check(!motor.Grounded&&motor.CanAirJump,"drop-through does not spend air jump");
                motor.Step(.02f,0,true,false);Settle(motor);
                Check(motor.Grounded&&Mathf.Abs(motor.Position.y-2.5f)<.01f,"air jump can recover onto a dropped-through platform");
                motor.Reset(new Vector2(4,5));motor.Step(.02f,0,true,false,true);
                Check(motor.VerticalSpeed<0&&motor.CanAirJump,"down+jump in air cannot trigger or consume air jump");

                map.floors.Remove(platform);map.floors.Add(new DungeonFloor{rect=new Rect(0,3,32,1)});
                motor.Reset();motor.Step(.02f,0,true,false);Settle(motor,6);
                Check(motor.Position.y<=.701f&&motor.CanAirJump,"solid ceiling blocks first jump without granting extras");
                motor.Step(.02f,0,true,false);Check(motor.Position.y<=.701f&&!motor.CanAirJump,"air jump cannot tunnel through ceiling or refill at it");
                map.floors.RemoveAt(1);map.floors.Add(new DungeonFloor{rect=new Rect(8,0,1,20)});
                motor.Reset();
                for(int i=0;i<70;i++)motor.Step(.02f,1,i==0||i==16,i==0||i==8);
                Check(motor.Position.x<=7.701f,"two jumps plus two dashes cannot cross solid wall");
                map.floors.RemoveAt(1);
                foreach(float dt in new[]{.01f,.02f,.05f})
                {
                    motor.Reset();float peak=0;
                    for(int i=0;i<(int)(2/dt);i++){motor.Step(dt,0,i==0||i==(int)(.3f/dt),false);peak=Mathf.Max(peak,motor.Position.y);}
                    Check(peak>2.7f&&peak<3.4f&&motor.Grounded,"double jump remains usable at step "+dt);
                }
                map.airJumpHeight=float.NaN;Check(map.ValidateLayout().Any(s=>s.Contains("공중 점프")),"invalid air jump height is rejected");
            }
            finally{UnityEngine.Object.DestroyImmediate(map);}
            string report="PASS: "+checks+" double-jump checks (third press, falls, platforms, drop, dash, ceilings, landing, reset and timing).";
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DungeonDoubleJumpChecks.txt",report);Debug.Log(report);return report;
        }
    }
}
