using MiniWar.Online;

static class RangedPursuitTests
{
    static LanEnemySpawn Enemy(int kind)=>new(){id="ranged",kind=kind,x=0,y=0,facing=1,chasePlayer=true,
        detectionRange=16,viewAngle=180,range=10,interval=1,windup=.4f,projectileSpeed=10};
    static LanDungeonFloor Ground()=>new(){id="ground",x=-20,y=-2,width=50,height=2};
    static LanCombatRoom Room(LanEnemySpawn e,params LanDungeonFloor[] floors)=>new(new LanDungeonRoom{
        x=-20,y=-5,width=50,height=18,jumpHeight=4f/3,airJumpHeight=2,enemies=[e],floors=floors.Length==0?[Ground()]:floors});
    static CombatFighter Player(float x,float y=0)=>new(){Id="player",X=x,Y=y,ProtectedUntil=double.MaxValue};
    static bool Step(LanCombatRoom r,CombatFighter p,int count)
    {bool fired=false;for(int i=0;i<count;i++){r.Tick(.05f,[p],r.Time+.05);fired|=r.Projectiles.Any(b=>b.hostile);}return fired;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int kind in new[]{1,2})
        {
            string type=kind==1?"Crossbow":"Mage";
            var room=Room(Enemy(kind));var p=Player(19);Step(room,p,20);
            check(room.Enemies[0].x==0,type+" does not pursue beyond detection range");
            p.X=-12;Step(room,p,20);check(room.Enemies[0].x==0,type+" respects initial forward field of view");
            p.X=14;Step(room,p,4);
            check(Math.Abs(room.Enemies[0].x-1)<.001f,type+" walks toward out-of-range target at player speed");
            bool fired=Step(room,p,80);var state=room.Enemies[0];
            check(Math.Abs(p.X-state.x-8)<.06f&&fired,type+" stops at preferred range then fires");
            float held=state.x;
            for(int i=0;i<50;i++){p.X=held+8+(i%2==0?.12f:-.12f);Step(room,p,1);}
            check(Math.Abs(state.x-held)<.001f,type+" spacing band prevents oscillation on small target motion");
            p.X=state.x+2;float closeX=state.x;Step(room,p,80);
            check(state.x<closeX-4&&Math.Abs(p.X-state.x-8)<.06f&&state.facing==1,type+" backpedals while facing player and restores distance");
            p.X=state.x+14;float before=state.x;Step(room,p,90);
            check(state.x>before+4&&Math.Abs(p.X-state.x-8)<.06f,type+" follows a target that leaves comfortable range");
            p.Active=false;before=state.x;Step(room,p,40);
            check(state.x==before,type+" immediately stops spacing when target becomes inactive");
            state.hp=0;p.Active=true;p.X=state.x+2;Step(room,p,40);
            check(state.x==before,type+" corpse cannot retreat");

            var wall=new LanDungeonFloor{id="wall",x=-2,y=0,width=1,height=7};
            room=Room(Enemy(kind),Ground(),wall);p=Player(2);fired=Step(room,p,70);
            check(room.Enemies[0].x>=-.701f&&room.Enemies[0].x<0&&fired,type+" backed against a wall stays in bounds and continues firing");
            var cliff=new LanDungeonFloor{id="ledge",x=0,y=-1,width=12,height=1};
            var e=Enemy(kind);e.x=1;room=Room(e,cliff);p=Player(3);fired=Step(room,p,70);
            check(room.Enemies[0].x>-.3f&&room.Enemies[0].y==0&&fired,type+" never retreats off an unsupported ledge");
            var platform=new LanDungeonFloor{id="platform",x=-4,y=2.8f,width=10,height=.2f,oneWay=true};
            e=Enemy(kind);e.y=3;room=Room(e,Ground(),platform);p=Player(2);fired=Step(room,p,70);
            check(room.Enemies[0].y==3&&fired,type+" holds supported upper floor while shooting downward");

            wall.x=4;room=Room(Enemy(kind),Ground(),wall);p=Player(12);Step(room,p,70);
            check(room.Enemies[0].x==0,type+" cannot detect and pursue through a solid wall");
            e=Enemy(kind);e.range=6;e.detectionRange=20;
            var low=new LanDungeonFloor{id="step",x=2,y=0,width=1,height=.8f};
            room=Room(e,Ground(),low);p=Player(16);float peak=0,maxStep=0,old=0;
            for(int i=0;i<100;i++){Step(room,p,1);state=room.Enemies[0];peak=Math.Max(peak,state.y);maxStep=Math.Max(maxStep,Math.Abs(state.x-old));old=state.x;}
            check(state.x>3&&peak>.8f&&peak<=4f/3+.01f&&maxStep<=.251f,type+" uses ordinary obstacle jump without dash or double jump while approaching");
        }
        var cannon=Enemy(3);var fixedRoom=Room(cannon);Step(fixedRoom,Player(2),60);
        check(fixedRoom.Enemies[0].x==0,"Cannon remains fixed even if chase flag is accidentally enabled");
    }
}
