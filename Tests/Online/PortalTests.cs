using MiniWar.Online;
using MiniWar.Server;

static class PortalTests
{
    public static void Run(Action<bool,string> check)
    {
        var gates=new HashSet<string>{"east","west"};
        var voters=Enumerable.Range(0,4).Select(i=>new PortalVoter{Id=i.ToString()}).ToArray();
        var rally=new LanPortalRally();
        rally.Toggle("0","east");rally.Toggle("1","east");
        check(rally.Evaluate(voters,gates,0)==null && rally.CountingPortal==null,"Half of four is not a portal majority");
        rally.Toggle("2","east");
        check(rally.Evaluate(voters,gates,1)==null && rally.Remaining(1)==5,"Three of four starts five-second countdown");
        check(rally.Evaluate(voters,gates,5.99)==null && rally.Evaluate(voters,gates,6)=="east","Majority travels only after full five seconds");
        rally.Reset();rally.Toggle("0","east");rally.Toggle("1","east");rally.Toggle("2","east");
        rally.Evaluate(voters,gates,10);rally.Toggle("2","east");rally.Evaluate(voters,gates,12);
        check(rally.CountingPortal==null,"Leaving portal cancels countdown when majority is lost");
        rally.Toggle("2","east");rally.Evaluate(voters,gates,20);
        check(rally.Remaining(20)==5,"Returning majority starts a fresh countdown");
        rally.Toggle("3","east");check(rally.Evaluate(voters,gates,20.1)=="east","All living members skip remaining countdown");
        rally.Reset();rally.Toggle("0","east");rally.Toggle("1","east");rally.Toggle("2","west");rally.Toggle("3","west");
        check(rally.Evaluate(voters,gates,30)==null && rally.CountingPortal==null,"Split portals cannot combine votes");
        voters[2].Alive=voters[3].Alive=false;
        check(rally.Evaluate(voters,gates,31)=="east" && rally.AliveCount==2 && rally.Count("west")==0,"Dead players and their votes excluded from quorum");
        voters[0].Alive=voters[1].Alive=false;
        check(rally.Evaluate(voters,gates,32)==null && rally.CountingPortal==null,"All-dead party cannot travel");
        foreach(var v in voters)v.Alive=true;
        rally.Toggle("0","east");rally.Toggle("1","east");rally.Toggle("2","east");voters[3].Connected=false;
        check(rally.Evaluate(voters,gates,40)=="east","Disconnected player does not hold remaining party hostage");
        rally.Reset();foreach(var v in voters){v.Connected=true;rally.Toggle(v.Id,"east");}
        check(rally.Evaluate(voters,new HashSet<string>(),50)==null && rally.Count("east")==0,"Locked or removed portals invalidate entry");
        for(int size=1;size<=6;size++)
        {
            rally.Reset();var group=Enumerable.Range(0,size).Select(i=>new PortalVoter{Id=i.ToString()}).ToArray();
            foreach(var member in group)rally.Toggle(member.Id,"east");
            check(rally.Evaluate(group,gates,60)=="east",size+" living players enter immediately at full attendance");
        }
        World(check);
    }

    static void World(Action<bool,string> check)
    {
        var w=new LobbyWorld();
        for(int i=0;i<4;i++)w.Join(i.ToString(),new LanProfile{nickname="Portal"+i});
        w.PartyCommand("0",new(){op="party_create",text="포탈 확인",dungeonId=LanDungeons.Gate});
        string party=w.PartyState("0").party.id;
        for(int i=1;i<4;i++)
        {
            w.PartyCommand(i.ToString(),new(){op="party_apply",partyId=party});
            w.PartyCommand("0",new(){op="party_accept",partyId=party,applicationId=w.PartyState("0").party.applications.Single().id});
        }
        check(w.PartyCommand("0",new(){op="party_start",partyId=party,dungeonRevision=LobbyWorld.DungeonRevision})=="","Portal test enters exported authored room");
        var room=w.Find("0")!.DungeonRoom!;var portal=room.portals.First();
        // Portal quorum is tested independently from combat; seed authoritative cleared state.
        foreach(var enemy in w.SnapshotFor("0").dungeon.enemies)enemy.hp=0;
        w.Tick(.05f);
        LanCommand Command()=>new(){op="portal_enter",roomId=w.SnapshotFor("0").dungeon.roomId,roomSequence=w.SnapshotFor("0").dungeon.roomSequence,portalId=portal.id};
        check(w.PortalCommand("0",Command()).Length>0,"Server rejects S from outside portal");
        for(int i=0;i<4;i++){var p=w.Find(i.ToString())!;p.X=portal.x;p.Y=portal.y;}
        w.Tick(.05f);
        check(w.Find("0")!.DungeonRoom==room,"Walking into portal without S never changes rooms");
        var stale=Command();stale.roomSequence--;
        check(w.PortalCommand("0",stale).Length>0,"Server rejects portal commands from stale room sequence");
        for(int i=0;i<2;i++)check(w.PortalCommand(i.ToString(),Command())=="","Server accepts supported S entry "+i);
        w.Tick(.05f);
        check(w.SnapshotFor("0").dungeon.countingPortalId==null,"Two of four waits without timer on server");
        float x=w.Find("0")!.X,y=w.Find("0")!.Y;
        w.Input("0",new(){sequence=1,move=1,jump=true,dash=true,fire=true,aiming=true});w.Tick(.1f);
        check(w.Find("0")!.X==x && w.Find("0")!.Y==y && w.Shots.Count==0,"Entered player cannot move, jump, dash or fire");
        w.PortalCommand("2",Command());w.Tick(.05f);
        check(w.SnapshotFor("0").dungeon.countdown>4.9f,"Server broadcasts five-second portal countdown");
        w.PortalCommand("2",Command());w.Tick(.05f);
        check(w.SnapshotFor("0").dungeon.countingPortalId==null && w.Find("2")!.WaitingPortal=="","Second S exits and cancels lost majority");
        w.PortalCommand("2",Command());w.Tick(.05f);
        var previous=Command();
        for(int i=0;i<99;i++)w.Tick(.05f);
        check(w.Find("0")!.DungeonRoom==room,"Server does not transition before five seconds");
        w.Tick(.051f);
        var target=w.Find("0")!.DungeonRoom!;
        check(target.id==portal.targetRoomId && Enumerable.Range(0,4).All(i=>w.Find(i.ToString())!.DungeonRoom==target),"Countdown moves whole party including living member outside portal");
        check(w.SnapshotFor("0").dungeon.visitedRooms.Length==2 && w.Find("0")!.WaitingPortal=="","Room transition clears votes and records visited room");
        check(w.PortalCommand("0",previous).Length>0,"Previous-room S cannot trigger a second transition");
        var back=target.portals.Single(p=>p.id==portal.targetPortalId);portal=back;
        foreach(var enemy in w.SnapshotFor("0").dungeon.enemies)enemy.hp=0;
        w.Tick(.05f);
        w.Find("3")!.Dead=true;
        for(int i=0;i<3;i++)
        {var p=w.Find(i.ToString())!;p.X=back.x;p.Y=back.y;check(w.PortalCommand(i.ToString(),Command())=="","Alive player enters return portal "+i);}
        w.Tick(.05f);
        check(w.Find("0")!.DungeonRoom==room && w.Find("3")!.Dead && w.Find("3")!.DungeonRoom==room,"All living members return immediately; dead spectator follows without resurrection");
        check(w.SnapshotFor("0").dungeon.visitedRooms.Length==2,"Backtracking preserves visitation rather than recreating room");
    }
}
