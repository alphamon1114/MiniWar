using System.Net;
using MiniWar.Online;
using MiniWar.Server;

static class DashTests
{
    static (LobbyWorld World, LobbyPlayer Player) Create()
    {
        var world = new LobbyWorld();
        var p = world.Join("a", new LanProfile {nickname="Dasher", money=100,
            items=new[] {new LanItem {id="pistol",family=0,ammo=12}}, activeItemId="pistol"}).Player!;
        return (world,p);
    }
    static void Advance(LobbyWorld world, double duration)
    {
        double until=world.Time+duration;
        while(until-world.Time>.0000001) world.Tick((float)Math.Min(.05,until-world.Time));
    }
    static void Input(LobbyWorld world, long sequence, bool dash=true, float move=0, bool aim=false, float angle=0)
        => world.Input("a",new LanCommand {sequence=sequence,dash=dash,move=move,aiming=aim,aimAngle=angle});
    static bool Near(double a,double b) => Math.Abs(a-b)<.0001;

    public static async Task Run(Action<bool,string> check,string root,string password)
    {
        float distance=LanRules.DashSpeed*LanRules.DashDuration;
        var (world,p)=Create();
        Input(world,1); world.Tick(.05f);
        var visible=world.Snapshot(1).actors.Single();
        check(Near(p.X-LanRules.TownSpawnX,.75) && visible.dashSequence==1 && visible.dashRemaining>0
            && visible.dashDirection==1 && visible.dashChainWindow>0 && visible.dashCooldown>0,
            "Dash starts immediately with authoritative position, combo window and cooldown snapshot");
        Advance(world,.15);
        check(Near(p.X-LanRules.TownSpawnX,distance) && p.DashRemaining==0,
            "Partial final tick preserves the exact short dash distance");
        Input(world,2,false); Advance(world,.85); Input(world,3);
        check(p.DashSequence==1 && !p.DashQueued,
            "No automatic second dash; input after the one-second window is discarded");
        Advance(world,1.90); Input(world,4);
        check(p.DashSequence==1,"First-dash cooldown cannot be bypassed just before three seconds");
        Advance(world,.05); Input(world,5);
        check(p.DashSequence==2 && p.DashUses==1,"A single dash becomes ready after three seconds without requiring a second use");
        check(p.Profile.money==100 && p.Profile.items[0].ammo==12,"Dashing preserves money and ammunition");

        (world,p)=Create(); Input(world,1); Advance(world,1); Input(world,2);
        check(p.DashSequence==2 && p.DashUses==2 && Near(p.DashReadyAt,4),
            "Second input at the one-second boundary is accepted and restarts the three-second cooldown");
        Input(world,3); Advance(world,.2); Input(world,4);
        check(p.DashSequence==2 && Near(p.X-LanRules.TownSpawnX,2*distance),
            "A third press cannot extend the two-dash chain");
        Advance(world,2.79); Input(world,5);
        check(p.DashSequence==2,"Cooldown is measured from the last dash, not the first");
        Advance(world,.01); Input(world,6);
        check(p.DashSequence==3 && p.DashUses==1,"Cooldown expiry opens a new two-dash chain");
        foreach(double delay in new[] {.999,1.001})
        {
            (world,p)=Create();Input(world,1);Advance(world,delay);Input(world,2);
            check(p.DashSequence==(delay<1?2:1),"Precise chain deadline at "+delay+" seconds");
        }

        (world,p)=Create(); Input(world,1); Input(world,2); Input(world,3);
        check(p.DashQueued && p.DashSequence==1 && p.DashUses==2,
            "Very fast double tap queues exactly one follow-up even before the first simulation tick");
        Advance(world,.4);
        check(p.DashSequence==2 && !p.DashQueued && Near(p.X-LanRules.TownSpawnX,2*distance)
            && Near(p.DashReadyAt,LanRules.DashDuration+LanRules.DashCooldown),
            "Queued follow-up starts at the exact first-dash end and sets its own cooldown");
        check(!world.Input("a",new LanCommand {sequence=1,dash=true}) && p.DashSequence==2,
            "Replayed dash packets cannot create movement");

        (world,p)=Create(); Input(world,1,true,1);Input(world,2,true,-1);Advance(world,.32);
        check(p.DashSequence==2 && p.DashDirection==-1 && Near(p.X,LanRules.TownSpawnX),
            "Each tap captures its direction; opposite second dash does not redirect the first");
        (world,p)=Create();Input(world,1,true,0,true,180);Advance(world,.16);
        check(Near(p.X,LanRules.TownSpawnX-distance) && p.Facing==-1,
            "Stationary dash follows the current aiming/facing direction");
        (world,p)=Create();Input(world,1,true,1,true,180);Advance(world,.1);
        check(p.DashDirection==1 && p.Facing==-1,"Movement direction takes priority over aim for a backward dash");

        var (other,airControl)=Create();(world,p)=Create();p.Y=airControl.Y=1;p.VelocityY=airControl.VelocityY=2;
        Input(world,1);Advance(world,.2);Advance(other,.2);
        check(Near(p.Y,airControl.Y) && Near(p.VelocityY,airControl.VelocityY)
            && Near(p.X-LanRules.TownSpawnX,distance),"Air dash only changes horizontal movement and never resets gravity or jump height");
        (world,p)=Create();p.X=LanRules.TownWidth-.6f;Input(world,1);world.Tick(.05f);
        check(Near(p.X,LanRules.TownWidth-.5f) && p.DashRemaining==0,"Dash stops at the map edge without leaving bounds");
        Input(world,2,true,-1);world.Tick(.05f);
        check(p.X<LanRules.TownWidth-.5f && p.DashSequence==2,"A blocked first dash can still chain away from the wall");

        (world,p)=Create();Input(world,1);Input(world,2);world.Tick(.05f);
        check(world.ChangeChannel("a",2)=="" && p.DashRemaining==0 && !p.DashQueued && p.DashUses==0
            && p.DashReadyAt>world.Time,"Channel transfer cancels motion and queued combo but preserves cooldown");
        Input(world,3);world.Tick(.05f);
        check(p.DashSequence==1 && Near(p.X,LanRules.TownSpawnX),"Channel transfer cannot grant a fresh dash during cooldown");
        check(world.Snapshot(1).actors.Length==0 && world.Snapshot(2).actors.Single().dashChainWindow==0,
            "Dash state follows channel isolation and the canceled chain does not leak");
        (world,p)=Create();
        check(!world.Input("a",new LanCommand{sequence=1,dash=true,move=float.NaN})
            && !world.Input("a",new LanCommand{sequence=2,dash=true,aimAngle=float.PositiveInfinity})
            && p.DashSequence==0,"Non-finite dash intent is rejected before consuming a charge");
        Input(world,3);Advance(world,3.1);
        check(p.DashSequence==1 && p.DashRemaining==0 && Near(p.X-LanRules.TownSpawnX,distance),
            "No input or disconnect timeout can repeat a dash when cooldown expires");

        foreach(float step in new[] {.01f,.025f,.05f,.1f})
        {
            (world,p)=Create();Input(world,1);Input(world,2);
            for(int i=0;i<(int)Math.Round(.4/step);i++)world.Tick(step);
            check(Near(p.X-LanRules.TownSpawnX,2*distance) && p.DashSequence==2,
                "Chained dash distance is independent of simulation step "+step);
        }

        await using var host=new LanHost(Path.Combine(root,"dash-network"),0,IPAddress.Loopback);
        host.Start();
        await using var a=await TestClient.Connect(host.Port,host.Fingerprint);
        await using var b=await TestClient.Connect(host.Port,host.Fingerprint);
        await a.Send(new LanCommand{op="register",nickname="DashA",password=password});await a.Wait("welcome");
        await b.Send(new LanCommand{op="register",nickname="DashB",password=password,body=1});await b.Wait("welcome");
        await b.Wait(e=>e.op=="snapshot" && e.actors.Length==2);
        await a.Send(new LanCommand{op="input",sequence=1,dash=true});
        var remote=await b.Wait(e=>e.op=="snapshot" && e.actors.Any(x=>x.nickname=="DashA" && x.dashSequence==1));
        var own=await a.Wait(e=>e.op=="snapshot" && e.actors.Any(x=>x.nickname=="DashA" && x.dashSequence==1));
        check(remote.actors.Single(x=>x.nickname=="DashA").x>LanRules.TownSpawnX
            && remote.actors.Single(x=>x.nickname=="DashA").dashRemaining>0
            && own.actors.Single(x=>x.nickname=="DashA").dashSequence==1,
            "Both real TLS clients observe the same short dash while it is active");
        await a.Send(new LanCommand{op="input",sequence=2,dash=true,move=-1});
        remote=await b.Wait(e=>e.op=="snapshot" && e.actors.Any(x=>x.nickname=="DashA" && x.dashSequence==2));
        check(remote.actors.Single(x=>x.nickname=="DashA").dashDirection==-1
            && remote.actors.Single(x=>x.nickname=="DashA").dashChainWindow==0,
            "Second dash direction and consumed combo replicate over TLS");
        await a.Send(new LanCommand{op="input",sequence=3,dash=true});
        remote=await b.Wait(e=>e.op=="snapshot" && e.actors.Any(x=>x.nickname=="DashA" && x.dashSequence==2 && x.dashRemaining==0));
        check(remote.actors.Single(x=>x.nickname=="DashA").dashCooldown>2,
            "Network third-tap rejection leaves the last-dash cooldown active");
    }
}
