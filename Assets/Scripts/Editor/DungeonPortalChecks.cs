using System;
using System.IO;
using System.Linq;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class DungeonPortalChecks
    {
        [MenuItem("MiniWar/Tests/Portal rally checks")]
        public static string Run()
        {
            int count=0;
            void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
            var dungeon=ScriptableObject.CreateInstance<RoomDungeon>();
            var a=ScriptableObject.CreateInstance<DungeonLayout>();var b=ScriptableObject.CreateInstance<DungeonLayout>();
            try
            {
                a.floors.Add(new DungeonFloor{rect=new Rect(0,-1,24,1)});b.floors.Add(new DungeonFloor{rect=new Rect(0,-1,24,1)});
                a.spawns.Add(new DungeonSpawn{id="a-enemy",count=8});b.spawns.Add(new DungeonSpawn{id="b-enemy",count=8});
                var pa=new RoomPortal{id="east",targetRoomId="b",targetPortalId="west",position=new Vector2(22,0),arrival=new Vector2(20,0),direction=PortalDirection.Right};
                var pb=new RoomPortal{id="west",targetRoomId="a",targetPortalId="east",position=new Vector2(2,0),arrival=new Vector2(4,0),direction=PortalDirection.Left};
                dungeon.rooms.Add(new DungeonRoom{id="a",layout=a,portals={pa}});dungeon.rooms.Add(new DungeonRoom{id="b",layout=b,portals={pb}});dungeon.startRoomId="a";
                var party=Enumerable.Range(1,4).Select(i=>new DungeonPartyPresence{id="P"+i,feet=pa.position}).ToList();
                var run=new DungeonRoomRun(dungeon,party.Select(p=>p.id));
                Check(!run.TogglePortal("P1",pa.id,party),"Uncleared room rejects S entry");
                run.ClearCurrentForPreview();Check(!run.TryTransition(party,out _),"Standing in portal without S cannot travel");
                for(int i=0;i<2;i++)Check(run.TogglePortal(party[i].id,pa.id,party),"S enters portal");
                Check(!run.TryTransition(party,out _,.02f)&&run.CountingPortal==null,"Half is not majority");
                run.TogglePortal("P3",pa.id,party);
                Check(!run.TryTransition(party,out _,.02f)&&Mathf.Abs(run.Countdown-5)<.001f,"Majority starts timer");
                Check(!run.TryTransition(party,out _,4.9f),"No early transition");
                run.TogglePortal("P3",pa.id,party);run.TryTransition(party,out _,.02f);
                Check(run.CountingPortal==null,"Leaving cancels majority");
                run.TogglePortal("P3",pa.id,party);run.TryTransition(party,out _,.02f);
                Check(run.Countdown==5,"Re-entry restarts timer");
                Check(run.TryTransition(party,out _,5)&&run.Current.id=="b","Countdown moves party");
                Check(run.EnteredPortal("P1")==null,"Room entry clears votes");
                foreach(var member in party)member.feet=pb.position;
                run.ClearCurrentForPreview();Check(!run.TryTransition(party,out _,10),"Arrival never bounces without fresh S");
                party[3].spectator=true;party[3].feet=new Vector2(12,0);
                for(int i=0;i<3;i++)run.TogglePortal(party[i].id,pb.id,party);
                Check(run.TryTransition(party,out _)&&run.Current.id=="a","All survivors travel immediately and spectator follows");
                Check(run.Cleared&&run.Remaining==0,"Cleared room stays cleared on backtrack");
                foreach(var member in party)member.spectator=true;
                Check(!run.TryTransition(party,out _,20),"All dead cannot advance");
                var report="PASS: "+count+" isolated Unity portal checks; authored map was not modified.";
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DungeonPortalChecks.txt",report);Debug.Log(report);return report;
            }
            finally {UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(dungeon);}
        }
    }
}
