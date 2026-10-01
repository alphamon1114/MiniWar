using System.Net;
using MiniWar.Online;
using MiniWar.Server;

static class JumpTests
{
    public static async Task Run(Action<bool,string> check,string root,string password)
    {
        var world=new LobbyWorld();var p=world.Join("jumper",new LanProfile{nickname="Jumper"}).Player!;
        long sequence=0;
        void Jump()=>world.Input(p.Id,new LanCommand{sequence=++sequence,jump=true});
        void Advance(int frames){for(int i=0;i<frames;i++)world.Tick(.02f);}
        Jump();world.Tick(.02f);
        check(p.Y>0&&p.AirJumpAvailable,"Ground jump retains exactly one server air jump");
        Advance(15);float firstPeak=p.Y;
        check(firstPeak>1.2f&&firstPeak<1.4f,"Server preserves original first jump height");
        Jump();world.Tick(.02f);
        check(!p.AirJumpAvailable&&Math.Abs(p.VelocityY-(LanRules.AirJumpSpeed-.48f))<.001f,"Server approves second jump and resets vertical speed");
        float vy=p.VelocityY;Jump();world.Tick(.02f);
        check(Math.Abs(p.VelocityY-(vy-.48f))<.001f,"Server rejects third jump");
        check(!world.Input(p.Id,new LanCommand{sequence=1,jump=true}),"Replayed jump packet is rejected");
        for(int i=0;i<30;i++)Jump();vy=p.VelocityY;world.Tick(.02f);
        check(Math.Abs(p.VelocityY-(vy-.48f))<.001f&&!p.AirJumpAvailable,"Input flooding cannot refill or stack air jumps");
        Advance(100);check(p.Y==0&&p.AirJumpAvailable,"Landing restores server air jump");
        p.Y=6;p.VelocityY=-12;Jump();world.Tick(.02f);
        check(p.Y>6&&p.VelocityY>9&&!p.AirJumpAvailable,"Air jump rescues falling player with full impulse");
        world.Input(p.Id,new LanCommand{sequence=++sequence,dash=true});world.Tick(.02f);
        check(!p.AirJumpAvailable,"Server dash cannot refill air jump");
        check(world.ChangeChannel(p.Id,2)==""&&p.Y==0&&p.AirJumpAvailable,"Channel respawn clears air jump state on ground");
        Jump();world.Tick(.02f);Jump();world.Tick(.02f);Advance(150);
        check(p.Y==0&&p.VelocityY==0&&p.AirJumpAvailable,"No input or stale input cannot auto-repeat jumps after landing");

        await using var host=new LanHost(Path.Combine(root,"jump-network"),0,IPAddress.Loopback);host.Start();
        await using var a=await TestClient.Connect(host.Port,host.Fingerprint);
        await using var b=await TestClient.Connect(host.Port,host.Fingerprint);
        await a.Send(new LanCommand{op="register",nickname="JumpA",password=password});await a.Wait("welcome");
        await b.Send(new LanCommand{op="register",nickname="JumpB",password=password});await b.Wait("welcome");
        await b.Wait(e=>e.op=="snapshot"&&e.actors.Length==2);
        await a.Send(new LanCommand{op="input",sequence=1,jump=true});
        await b.Wait(e=>e.op=="snapshot"&&e.actors.Any(x=>x.nickname=="JumpA"&&x.y>.7f));
        await a.Send(new LanCommand{op="input",sequence=2,jump=true});
        var remote=await b.Wait(e=>e.op=="snapshot"&&e.actors.Any(x=>x.nickname=="JumpA"&&x.y>1.6f));
        var own=await a.Wait(e=>e.op=="snapshot"&&e.actors.Any(x=>x.nickname=="JumpA"&&x.y>1.6f));
        check(remote.actors.Single(x=>x.nickname=="JumpA").y>1.6f&&own.actors.Single(x=>x.nickname=="JumpA").y>1.6f,
            "Both real TLS clients see the server-approved double jump above single-jump height");
    }
}
