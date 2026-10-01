using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class RoomDungeonChecks
    {
        static int count;
        static void Check(bool value,string message){count++;if(!value)throw new InvalidOperationException("Room dungeon check: "+message);}
        static List<DungeonPartyPresence> Party(int size)=>Enumerable.Range(1,size).Select(i=>new DungeonPartyPresence {id="P"+i}).ToList();
        static void Place(List<DungeonPartyPresence> party,Vector2 p){foreach(var member in party)member.feet=p;}

        [MenuItem("MiniWar/Tests/Room dungeon checks")]
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
            count=0;var source=RoomDungeonAuthoring.CreateExample();
            Check(source.ValidateDungeon().Count==0,"example validation: "+string.Join(" / ",source.ValidateDungeon()));
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Data/RoomDungeons/__RoomChecks.asset");
            var data=RoomDungeonAuthoring.Copy(source,path);
            try
            {
                Check(data.rooms.Count==6&&data.rooms.Sum(r=>r.portals.Count)==10,"six rooms with only five explicit corridors");
                Check(data.rooms.All(r=>r.layout.spawns.Sum(s=>s.count)>=8),"at least eight enemies per room");
                var boss=data.rooms.Single(r=>r.isBoss);
                Check(boss.layout.spawns.Where(s=>s.enemy.isBoss).Sum(s=>s.count)==1&&boss.layout.spawns.Where(s=>!s.enemy.isBoss).Sum(s=>s.count)==7,"boss and seven guards");
                Check(data.rooms.All(r=>r.layout!=source.FindRoom(r.id).layout),"copied room geometry is independent");
                TestRun(data);TestEditing(data);TestPortals(data);
                RoomDungeonAuthoring.Save(data);
                foreach(var w in Resources.FindObjectsOfTypeAll<DungeonBuilderWindow>())w.Close();
                string firstId=data.rooms[0].id,portalId=data.rooms[0].portals[0].id;
                Undo.ClearUndo(data);Resources.UnloadAsset(data);
                data=AssetDatabase.LoadAssetAtPath<RoomDungeon>(path);
                Check(data.ValidateDungeon().Count==0,"reload keeps all valid connections and geometry");
                Check(data.rooms[0].id==firstId&&data.rooms[0].portals[0].id==portalId,"stable room and portal IDs after reload");
                Check(data.rooms.All(r=>AssetDatabase.GetAssetPath(r.layout)==path),"room geometry saved in the dungeon asset");
                string report="PASS: "+count+" room dungeon checks (party gate, clear persistence, branches, undo, portal gestures and asset reload).";
                File.WriteAllText("Logs/RoomDungeonChecks.txt",report);Debug.Log(report);return report;
            }
            finally
            {
                foreach(var w in Resources.FindObjectsOfTypeAll<DungeonBuilderWindow>())w.Close();
                if(data!=null)Undo.ClearUndo(data);AssetDatabase.DeleteAsset(path);RoomDungeonBuilderWindow.ShowDungeon(source);
            }
        }

        static void TestRun(RoomDungeon data)
        {
            bool rejected=false;try{new DungeonRoomRun(data,new[]{"P1","P1"});}catch(ArgumentException){rejected=true;}
            Check(rejected,"duplicate IDs cannot shrink the required party roster");
            var party=Party(4);var run=new DungeonRoomRun(data,party.Select(p=>p.id));
            var first=run.Current;var exit=first.portals[0];Place(party,exit.position);
            Check(!run.TryTransition(party,out _),"uncleared room stays locked even with all four at portal");
            int enemies=run.Remaining;var spawn=first.layout.spawns[0];
            Check(run.Defeat(spawn.id,0)&&run.Remaining==enemies-1,"authoritative kill reduces remaining count");
            Check(!run.Defeat(spawn.id,0)&&run.Remaining==enemies-1,"duplicate kill ignored");
            Check(!run.Defeat("unknown",0),"unknown enemy kill ignored");run.ClearCurrentForPreview();
            party[3].feet=first.layout.entrance;Check(!run.TryTransition(party,out _),"three out of four must wait");
            party[3].feet=exit.position;party[3].connected=false;Check(!run.TryTransition(party,out _),"disconnected member cannot silently disappear from roster");party[3].connected=true;
            Check(!run.TryTransition(party.Take(3).ToList(),out _),"omitted member blocks transition");
            string fourth=party[3].id;party[3].id=party[0].id;Check(!run.TryTransition(party,out _),"duplicate party member blocks transition");party[3].id=fourth;
            party[3].feet=new Vector2(float.NaN,0);Check(!run.TryTransition(party,out _),"invalid positions cannot pass the gate");party[3].feet=exit.position;
            foreach(var member in party)Check(run.TogglePortal(member.id,exit.id,party),"explicit S entry "+member.id);
            Check(run.TryTransition(party,out var arrival)&&run.Current.id==exit.targetRoomId,"all four enter the same next room");
            Check(!run.TryTransition(party,out _),"arrival latch prevents immediate bounce");
            Place(party,arrival.arrival);run.TryTransition(party,out _);run.ClearCurrentForPreview();
            Check(run.Current.portals.All(p=>p.direction!=PortalDirection.Right),"adjacent gate room has no direct portal");
            var left=run.Current.portals.First(p=>p.direction==PortalDirection.Left);var right=run.Current.portals.First(p=>p.direction==PortalDirection.Down);
            party[0].feet=party[1].feet=left.position;party[2].feet=party[3].feet=right.position;
            Check(!run.TryTransition(party,out _),"split between two portals does not move the party");
            Place(party,left.position);foreach(var member in party)run.TogglePortal(member.id,left.id,party);
            Check(run.TryTransition(party,out arrival)&&run.Current.id==first.id,"backtrack to previous room");
            Check(run.Cleared&&run.Remaining==0,"cleared enemies do not respawn on return");
            Place(party,arrival.arrival);run.TryTransition(party,out _);Place(party,exit.position);
            party[3].spectator=true;party[3].feet=first.layout.entrance;
            foreach(var member in party.Where(p=>!p.spectator))run.TogglePortal(member.id,exit.id,party);
            Check(run.TryTransition(party,out arrival),"spectator follows surviving members without walking to portal");
            foreach(var member in party)member.spectator=true;run.ClearCurrentForPreview();
            Check(!run.TryTransition(party,out _),"all-dead party cannot advance");

            for(int size=1;size<=4;size++)
            {
                var group=Party(size);var single=new DungeonRoomRun(data,group.Select(p=>p.id));single.ClearCurrentForPreview();Place(group,single.Current.portals[0].position);
                foreach(var member in group)single.TogglePortal(member.id,single.Current.portals[0].id,group);
                Check(single.TryTransition(group,out _),size+"-member party uses its actual roster size");
            }
            party=Party(4);run=new DungeonRoomRun(data,party.Select(p=>p.id));
            Walk(run,party,PortalDirection.Right);Walk(run,party,PortalDirection.Down);Walk(run,party,PortalDirection.Right);Walk(run,party,PortalDirection.Up);Walk(run,party,PortalDirection.Up);
            Check(run.Current.isBoss&&!run.Completed,"branch / bypass route reaches a live boss room");run.ClearCurrentForPreview();Check(run.Completed,"boss room clear finishes dungeon");
            Check(!run.TryTransition(party,out _),"finished dungeon cannot transition again");
        }

        static void Walk(DungeonRoomRun run,List<DungeonPartyPresence> party,PortalDirection direction)
        {
            Place(party,run.Current.layout.entrance);run.TryTransition(party,out _);run.ClearCurrentForPreview();
            var portal=run.Current.portals.First(p=>p.direction==direction);Place(party,portal.position);
            foreach(var member in party.Where(p=>!p.spectator))run.TogglePortal(member.id,portal.id,party);
            Check(run.TryTransition(party,out var arrival),"walk through "+direction);
            Place(party,arrival.arrival);run.TryTransition(party,out _);
        }

        static void TestEditing(RoomDungeon data)
        {
            int rooms=data.rooms.Count;string startId=data.startRoomId;
            var added=RoomDungeonAuthoring.AddNeighbor(data,data.FindRoom(startId),PortalDirection.Up);string newId=added.id;
            Check(data.rooms.Count==rooms+1&&data.ValidateDungeon().Count==0,"add neighboring room with paired portals");
            Undo.PerformUndo();Check(data.rooms.Count==rooms,"undo room and its connection together");
            Undo.PerformRedo();Check(data.FindRoom(newId)!=null&&data.ValidateDungeon().Count==0,"redo restores geometry and paired links");
            Undo.IncrementCurrentGroup();RoomDungeonAuthoring.Disconnect(data,data.FindRoom(startId),data.FindRoom(startId).portals.First(p=>p.direction==PortalDirection.Up));
            Check(data.ValidateDungeon().Count==0&&data.ConnectionWarnings().Count==1,"disconnected non-boss room is allowed and reported as storage");
            Undo.PerformUndo();Check(data.ValidateDungeon().Count==0,"undo link removal restores both ends");
            Undo.IncrementCurrentGroup();RoomDungeonAuthoring.RemoveRoom(data,data.FindRoom(newId));
            Check(data.rooms.Count==rooms&&data.ValidateDungeon().Count==0,"room deletion removes dangling links");
            Undo.PerformUndo();Check(data.FindRoom(newId)!=null&&data.ValidateDungeon().Count==0,"undo room deletion retains its subasset");
            Undo.IncrementCurrentGroup();RoomDungeonAuthoring.RemoveRoom(data,data.FindRoom(newId));
            var portal=data.rooms[0].portals[0];Vector2 old=portal.arrival;portal.arrival=portal.position;
            Check(data.ValidateDungeon().Any(e=>e.Contains("도착 위치를")),"arrival inside a portal is rejected");portal.arrival=old;
            string target=portal.targetRoomId;portal.targetRoomId="missing";
            Check(data.ValidateDungeon().Any(e=>e.Contains("왕복 연결")),"broken destination is rejected");portal.targetRoomId=target;
        }

        static void TestPortals(RoomDungeon data)
        {
            var room=data.FindRoom(data.startRoomId);var portal=room.portals[0];string id=portal.id;
            var w=DungeonBuilderWindow.ShowRoom(data,room.id);w.position=new Rect(40,60,1280,760);w.SendEvent(new Event{type=EventType.Layout});
            Vector2 original=portal.position;
            Mouse(w,EventType.MouseDown,original+Vector2.up);Mouse(w,EventType.MouseDrag,original+Vector2.up+Vector2.left);Mouse(w,EventType.MouseUp,original+Vector2.up+Vector2.left);
            Check(Mathf.Abs(data.FindRoom(room.id).portals.First(p=>p.id==id).position.x-(original.x-1))<.01f,"drag portal in room canvas");
            Undo.PerformUndo();Check(data.FindRoom(room.id).portals.First(p=>p.id==id).position==original,"portal drag Undo updates owning dungeon");
            Undo.PerformRedo();Check(data.FindRoom(room.id).portals.First(p=>p.id==id).position.x==original.x-1,"portal drag Redo");
            Check(data.ValidateDungeon().Count==0,"moved portal remains valid");
        }

        static void Mouse(DungeonBuilderWindow w,EventType type,Vector2 world)
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var local=(Vector2)typeof(DungeonBuilderWindow).GetMethod("WorldToCanvas",flags).Invoke(w,new object[]{world});
            var canvas=(Rect)typeof(DungeonBuilderWindow).GetField("canvas",flags).GetValue(w);
            w.SendEvent(new Event {type=type,button=0,mousePosition=local+canvas.position+w.rootVisualElement.worldBound.position});
        }
    }
}
