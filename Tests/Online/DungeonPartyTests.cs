using System.Net;
using MiniWar.Online;
using MiniWar.Server;

static class DungeonPartyTests
{
    public static async Task Run(Action<bool,string> check, string root, string password)
    {
        var w = new LobbyWorld();
        for (int i = 0; i < 5; i++) w.Join(i.ToString(), new LanProfile { nickname = "Dungeon"+i });
        string Cmd(string who, string op, string? party = null, string? dungeon = null, string? revision = null)
            => w.PartyCommand(who, new LanCommand { op=op, partyId=party!, text="던전 모집", dungeonId=dungeon!, dungeonRevision=revision! });
        check(Cmd("0","party_create",dungeon:"missing").Length>0 && Cmd("0","party_create",dungeon:"demon_raid").Length>0, "Unknown and unfinished dungeons cannot be created by forged commands");
        check(Cmd("0","party_create",dungeon:LanDungeons.Gate)=="", "Create party with selected dungeon");
        string id=w.PartyState("0").party.id;
        check(w.PartyState("2").parties.Single().dungeonId==LanDungeons.Gate, "Dungeon ID appears in recruitment listing");
        Cmd("1","party_apply",id);
        var ticket=w.PartyState("0").party.applications.Single().id;
        w.PartyCommand("0",new LanCommand{op="party_accept",partyId=id,applicationId=ticket});
        Cmd("2","party_apply",id);
        check(Cmd("1","party_start",id,revision:LobbyWorld.DungeonRevision).Contains("파티장"), "Member cannot force party departure");
        check(Cmd("0","party_start",id,revision:"stale").Length>0 && !w.PartyState("0").party.inDungeon, "Changed map revision rejects departure atomically");
        check(Cmd("0","party_start",id,revision:LobbyWorld.DungeonRevision)=="", "Leader starts authored dungeon");
        var a=w.SnapshotFor("0"); var b=w.SnapshotFor("1");
        check(a.dungeon.instanceId==b.dungeon.instanceId && a.actors.Length==2 && a.dungeon.roomName=="침투 지점", "Whole party enters same authored starting room");
        check(Math.Abs(a.dungeon.power-1.8f)<.001f && Math.Abs(a.actors[0].x-w.Find("0")!.DungeonRoom!.spawnX)<.01f, "Entry locks two-player bonus and uses authored spawn");
        check(w.SnapshotFor("2").actors.Length==3 && w.Snapshot(1).actors.All(p=>p.nickname!="Dungeon0"), "Town actors and dungeon actors are isolated");
        check(w.PartyState("2").pendingPartyId==null && w.PartyState("2").parties.Single().inDungeon, "Departure clears requests and marks listing in progress");
        check(Cmd("2","party_apply",id).Length>0 && Cmd("0","party_start",id,revision:LobbyWorld.DungeonRevision).Length>0, "No mid-run joining or repeated start");
        Cmd("3","party_create",dungeon:LanDungeons.Gate); string other=w.PartyState("3").party.id;
        Cmd("3","party_start",other,revision:LobbyWorld.DungeonRevision);
        check(w.SnapshotFor("3").actors.Length==1 && w.SnapshotFor("3").instanceId!=a.instanceId && w.SnapshotFor("3").dungeon.power==3.3f, "Other party gets separate solo instance and bonus");
        var p=w.Find("0")!; var actual=p.DungeonRoom!;
        p.Combat.ProtectedUntil=double.MaxValue;
        // Collision cases use a small server-owned room, not client coordinates or executable commands.
        p.DungeonRoom=new LanDungeonRoom { id=actual.id,x=0,y=-3,width=24,height=12,spawnX=2,spawnY=0,jumpHeight=4f/3,airJumpHeight=2,
            floors=[new(){id="ground",x=0,y=-1,width=24,height=1},new(){id="wall",x=5,y=0,width=1,height=4},new(){id="oneway",x=8,y=1,width=4,height=.2f,oneWay=true}] };
        p.X=2;p.Y=0;
        long seq=0;
        for(int i=0;i<30;i++){w.Input("0",new(){sequence=seq++,move=1});w.Tick(.05f);}
        check(p.X<=4.7001f && p.Y==0, "Server blocks horizontal passage through solid floors/walls");
        p.X=9;p.Y=0;w.Input("0",new(){sequence=seq++,jump=true});w.Tick(.05f);
        for(int i=0;i<6;i++)w.Tick(.05f);
        w.Input("0",new(){sequence=seq++,jump=true});w.Tick(.05f);float second=p.VelocityY;
        w.Input("0",new(){sequence=seq++,jump=true});w.Tick(.05f);
        check(second>8 && p.VelocityY<second && !p.AirJumpAvailable, "Dungeon air jump boosts once; third jump rejected");
        for(int i=0;i<60;i++)w.Tick(.05f);
        check(Math.Abs(p.Y-1.2f)<.01 && w.SnapshotFor("0").actors.Single(x=>x.nickname==p.Profile.nickname).grounded, "One-way platform catches descending player with grounded animation");
        w.Input("0",new(){sequence=seq++,drop=true});w.Tick(.05f);
        for(int i=0;i<30;i++)w.Tick(.05f);
        check(p.Y==0 && p.AirJumpAvailable, "Drop-through lands on lower solid ground and resets jump");
        p.DungeonRoom=actual;
        check(Cmd("1","party_return",id).Contains("파티장"), "Only leader can return entire party");
        Cmd("1","party_leave",id);
        check(w.SnapshotFor("1").dungeon==null && w.SnapshotFor("0").actors.Length==1 && w.SnapshotFor("0").dungeon.power==1.8f, "Member exit returns only that player; run bonus remains fixed");
        check(Cmd("0","party_return",id)=="" && w.SnapshotFor("0").dungeon==null && !w.PartyState("0").party.inDungeon, "Return preserves party and dungeon selection");
        Cmd("0","party_start",id,revision:LobbyWorld.DungeonRevision);
        w.Leave("0");
        check(w.PartyState("2").parties.All(x=>x.id!=id), "Last disconnect cleans up in-progress instance and listing");
        await Network(check,Path.Combine(root,"dungeon-party-network"),password);
    }
    static async Task Network(Action<bool,string> check,string data,string password)
    {
        await using var host=new LanHost(data,0,IPAddress.Loopback);host.Start();
        await using var leader=await TestClient.Connect(host.Port,host.Fingerprint);
        await using var member=await TestClient.Connect(host.Port,host.Fingerprint);
        await using var town=await TestClient.Connect(host.Port,host.Fingerprint);
        foreach(var pair in new[]{(leader,"DungeonLeader"),(member,"DungeonMember"),(town,"TownObserver")})
        { await pair.Item1.Send(new(){op="register",nickname=pair.Item2,password=password});await pair.Item1.Wait("welcome"); }
        await leader.Send(new(){op="party_create",text="성문 외곽",dungeonId=LanDungeons.Gate});
        string id=(await leader.Wait(e=>e.op=="parties"&&e.party!=null)).party.id;
        await member.Send(new(){op="party_apply",partyId=id});
        var request=(await leader.Wait(e=>e.op=="parties"&&e.party?.applications.Length==1)).party.applications[0].id;
        await leader.Send(new(){op="party_accept",partyId=id,applicationId=request});
        await member.Wait(e=>e.op=="parties"&&e.party!=null);
        await leader.Send(new(){op="party_start",partyId=id,dungeonRevision=LobbyWorld.DungeonRevision});
        var a=await leader.Wait(e=>e.op=="snapshot"&&e.dungeon!=null);
        var b=await member.Wait(e=>e.op=="snapshot"&&e.dungeon!=null);
        check(a.instanceId==b.instanceId&&b.actors.Length==2,"TLS both clients enter matching dungeon instance");
        var outside=await town.Wait(e=>e.op=="snapshot"&&e.tick>=a.tick);
        check(outside.dungeon==null&&outside.actors.Length==1,"TLS town snapshot excludes dungeon party");
        float before=b.actors.Single(p=>p.nickname=="DungeonMember").x;
        await member.Send(new(){op="input",sequence=1,move=1,jump=true});
        var moved=await leader.Wait(e=>e.op=="snapshot"&&e.actors.Any(p=>p.nickname=="DungeonMember"&&p.x>before+.1f&&p.y>0));
        check(moved.instanceId==a.instanceId,"TLS member movement replicated to leader inside dungeon");
        var spawnPositions=a.dungeon.enemies.Where(e=>e.kind==0).ToDictionary(e=>e.id,e=>e.x);
        for(int i=0;i<13;i++)
        {await member.Send(new(){op="input",sequence=i+2,move=1});await Task.Delay(100);}
        await member.Send(new(){op="input",sequence=15});
        var chasing=await leader.Wait(e=>e.op=="snapshot"&&e.dungeon!=null&&e.dungeon.enemies.Any(enemy=>spawnPositions.ContainsKey(enemy.id)&&Math.Abs(enemy.x-spawnPositions[enemy.id])>.3f));
        var pursuer=chasing.dungeon.enemies.First(enemy=>spawnPositions.ContainsKey(enemy.id)&&Math.Abs(enemy.x-spawnPositions[enemy.id])>.3f);
        var sharedChase=await member.Wait(e=>e.op=="snapshot"&&e.dungeon!=null&&e.dungeon.enemies.Any(enemy=>enemy.id==pursuer.id&&Math.Abs(enemy.x-spawnPositions[enemy.id])>.3f));
        check(sharedChase.instanceId==chasing.instanceId,"TLS both party members receive server-authoritative monster pursuit");
        await leader.Send(new(){op="input",sequence=1,aiming=true,fire=true,aimAngle=0});
        var hit=await leader.Wait(e=>e.op=="snapshot"&&e.dungeon!=null&&e.dungeon.enemies.Any(enemy=>enemy.hp<enemy.maxHp));
        var injured=hit.dungeon.enemies.First(enemy=>enemy.hp<enemy.maxHp);
        var replicated=await member.Wait(e=>e.op=="snapshot"&&e.dungeon!=null&&e.dungeon.enemies.Any(enemy=>enemy.id==injured.id&&enemy.hp<=injured.hp));
        check(replicated.instanceId==hit.instanceId,"TLS server damage replicated consistently to both party members");
        await member.Send(new(){op="chat",text="같이 출발"});
        check((await leader.Wait("chat")).instanceId==a.instanceId,"TLS party chat scoped to dungeon instance");
        await Task.Delay(600);
        await leader.Send(new(){op="party_return",partyId=id});
        a=await leader.Wait(e=>e.op=="snapshot"&&e.dungeon==null);
        b=await member.Wait(e=>e.op=="snapshot"&&e.dungeon==null);
        check(a.actors.Length==3&&b.actors.Length==3,"TLS party returns to shared town together");
    }
}
