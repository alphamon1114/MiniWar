using MiniWar.Online;
using MiniWar.Server;

static class CombatTests
{
    static LanEnemySpawn Enemy(int kind=0,float x=1.5f,float y=0) => new() {
        id="guard:0",name="Guard",x=x,y=y,kind=kind,health=100,damage=20,range=kind==0?1.8f:12,
        interval=1,windup=.4f,projectileSpeed=10,width=.8f,height=2.2f,chasePlayer=false };
    static LanCombatRoom Room(LanEnemySpawn enemy,params LanDungeonFloor[] floors)=>new(new LanDungeonRoom{enemies=[enemy],floors=floors});
    static void Step(LanCombatRoom room,CombatFighter p,int count)
    {for(int i=0;i<count;i++)room.Tick(.05f,[p],room.Time+.05);}
    public static void Run(Action<bool,string> check)
    {
        var p=new CombatFighter{Id="p"};var room=Room(Enemy());
        Step(room,p,17);
        check(room.Enemies[0].windup>0&&p.Health==100,"Melee telegraphs before damage; no instant contact damage");
        p.X=-3;Step(room,p,10);
        check(p.Health==100&&room.Enemies[0].x==1.5f,"Escaping melee windup avoids damage; monster remains stationary");
        p.X=0;Step(room,p,40);
        check(p.Health<100,"Guard melee hits a nearby player after windup");
        p=new(){Id="p",Y=2};room=Room(Enemy());Step(room,p,70);
        check(p.Health==100,"Melee cannot hit a player on a different floor");
        p=new(){Id="p",X=4};room=Room(Enemy());Step(room,p,70);
        check(p.Health==100,"Melee does not hit outside its range");
        foreach(int kind in new[]{1,2})
        {
            p=new(){Id="p"};room=Room(Enemy(kind,6));Step(room,p,26);
            check(room.Projectiles.Any(b=>b.hostile&&b.kind==kind)&&p.Health==100,"Ranged type "+kind+" creates traveling projectile");
            Step(room,p,20);check(p.Health==80,"Ranged type "+kind+" damages at projectile impact");
            p=new(){Id="p"};room=Room(Enemy(kind,6));Step(room,p,17);p.Y=4;Step(room,p,30);
            check(p.Health==100,"Ranged type "+kind+" locks aim at windup and can be dodged");
        }
        var wall=new LanDungeonFloor{id="wall",x=3,y=-1,width=1,height=8};
        p=new(){Id="p"};room=Room(Enemy(1,6),wall);Step(room,p,80);
        check(p.Health==100&&room.Projectiles.Length==0,"Solid wall blocks enemy line of sight");
        check(room.Attack(p,4,1,0,0,1,1,5),"Owned weapon simulation accepts first attack");Step(room,p,10);
        check(room.Enemies[0].hp==100,"Solid wall stops player bullets before target");
        wall.oneWay=true;p=new(){Id="p"};room=Room(Enemy(0,6),wall);room.Attack(p,4,1,0,0,1,1,0);Step(room,p,10);
        check(room.Remaining==0,"One-way platform does not absorb bullets; swept sniper hit cannot tunnel");
        p=new(){Id="p"};room=Room(Enemy(0,3));
        check(room.Attack(p,0,1,0,0,1,1,0)&&!room.Attack(p,0,1,0,0,1,1,.01),"Rapid input cannot bypass firearm cadence");Step(room,p,10);
        check(room.Enemies[0].hp==72,"Pistol applies server damage exactly once");
        check(!room.Attack(p,0,1,0,float.NaN,1,1,5)&&!room.Attack(p,99,1,0,0,1,1,5),"Invalid aim and weapon family rejected");
        p.Active=false;check(!room.Attack(p,0,1,0,0,1,1,5),"Spectator or portal-waiting player cannot attack");
        p=new(){Id="p"};room=Room(Enemy());room.Attack(p,5,1,0,0,-1,1,0);
        check(room.Enemies[0].hp==100,"Player melee respects facing direction");
        room.Attack(p,5,1,0,0,1,1,.6);
        check(room.Enemies[0].hp==64&&room.Projectiles.Length==0,"Player melee hits frontal target without creating bullets");
        p=new(){Id="p"};room=Room(Enemy());room.Attack(p,5,1,0,0,1,3.3f,0);Step(room,p,100);
        check(room.Remaining==0&&p.Health==100&&room.Projectiles.Length==0,"Solo power bonus applies; dead enemy never attacks");
        p=new(){Id="p",ProtectedUntil=10};room=Room(Enemy());Step(room,p,70);
        check(p.Health==100,"Entry/revive protection prevents damage");
        World(check);
    }
    static void World(Action<bool,string> check)
    {
        var w=new LobbyWorld();
        for(int i=0;i<2;i++)
        {
            string id=i.ToString();w.Join(id,new LanProfile{nickname="Combat"+id,activeItemId="knife",items=[new(){id="knife",family=5}]});
            w.PartyCommand(id,new(){op="party_create",dungeonId=LanDungeons.Gate,text="Combat"});
            w.PartyCommand(id,new(){op="party_start",partyId=w.PartyState(id).party.id,dungeonRevision=LobbyWorld.DungeonRevision});
        }
        var visit=w.SnapshotFor("0").dungeon;var actor=w.Find("0")!;var room=actor.DungeonRoom!;
        check(!visit.portalsOpen&&visit.enemiesRemaining>0,"Authored dungeon starts with live combat and locked portals");
        var portal=room.portals[0];actor.X=portal.x;actor.Y=portal.y;
        check(w.PortalCommand("0",new(){op="portal_enter",roomId=room.id,roomSequence=visit.roomSequence,portalId=portal.id}).Length>0,"Cannot enter portal before room clear");
        var enemy=visit.enemies.First(e=>e.y==0);actor.X=enemy.x-1;actor.Y=enemy.y;
        w.Input("0",new(){sequence=1,fire=true,aiming=true,aimAngle=0});w.Tick(.05f);
        check(w.SnapshotFor("0").dungeon.enemies.Single(e=>e.id==enemy.id).hp<enemy.maxHp,"Equipped player attack damages authored monster on server");
        check(w.SnapshotFor("1").dungeon.enemies.All(e=>e.hp==e.maxHp),"Different party has independent monster health");
        actor.Combat.Health=0;w.Tick(.05f);
        check(actor.Dead&&w.SnapshotFor("0").actors.Single().hp==0,"Lethal damage becomes authoritative death in snapshot");
        var revive=new LanCommand{roomId=room.id,roomSequence=visit.roomSequence-1};
        check(w.ReviveCommand("0",revive).Length>0,"Stale room revive is rejected");revive.roomSequence++;
        check(w.ReviveCommand("0",revive)==""&&!actor.Dead&&actor.ReviveUsed&&actor.Combat.Health==100,"One revive restores player at room entrance");
        actor.Combat.Health=0;w.Tick(.05f);
        check(w.ReviveCommand("0",revive).Length>0&&actor.Dead,"Second death remains spectator for this dungeon");
        foreach(var e in w.SnapshotFor("0").dungeon.enemies)e.hp=0;w.Tick(.05f);
        check(w.SnapshotFor("0").dungeon.portalsOpen&&w.SnapshotFor("0").dungeon.enemiesRemaining==0,"Room clears only after all enemies die");
        var party=w.PartyState("0").party.id;w.PartyCommand("0",new(){op="party_return",partyId=party});
        w.PartyCommand("0",new(){op="party_start",partyId=party,dungeonRevision=LobbyWorld.DungeonRevision});
        check(!actor.Dead&&!actor.ReviveUsed&&actor.Combat.Health==100&&w.SnapshotFor("0").dungeon.enemies.All(e=>e.hp==e.maxHp),"New dungeon resets health, revive and monsters");
    }
}
