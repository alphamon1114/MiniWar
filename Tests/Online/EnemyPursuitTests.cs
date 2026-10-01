using MiniWar.Online;

static class EnemyPursuitTests
{
    static LanDungeonFloor Ground()=>new(){id="ground",x=-10,y=-2,width=40,height=2};
    static LanEnemySpawn Enemy(int kind=0,float x=0,float y=0)=>new(){id="pursuer",kind=kind,x=x,y=y,facing=1,chasePlayer=true,detectionRange=8,viewAngle=180};
    static LanCombatRoom Room(LanEnemySpawn e,params LanDungeonFloor[] floors)=>new(new LanDungeonRoom{
        x=-10,y=-5,width=40,height=18,jumpHeight=4f/3,airJumpHeight=2,enemies=[e],floors=floors.Length==0?[Ground()]:floors});
    static CombatFighter Player(float x,float y=0)=>new(){Id="player",X=x,Y=y,ProtectedUntil=double.MaxValue};
    static void Step(LanCombatRoom r,CombatFighter p,int n=1){for(int i=0;i<n;i++)r.Tick(.05f,[p],r.Time+.05);}
    public static void Run(Action<bool,string> check)
    {
        var p=Player(9);var room=Room(Enemy());Step(room,p,30);
        check(room.Enemies[0].x==0,"Pursuer waits outside detection range");
        p.X=-5;Step(room,p,30);check(room.Enemies[0].x==0,"Pursuer does not initially detect behind its field of view");
        p.X=7;Step(room,p,4);check(Math.Abs(room.Enemies[0].x-1)<.0001,"Visible target activates chase at player walk speed, without dash");
        Step(room,p,60);var state=room.Enemies[0];
        check(state.x<6&&state.x>5&&state.grounded,"Pursuer stops before overlapping player, in melee range");
        p.ProtectedUntil=0;Step(room,p,50);check(p.Health<100,"Chase transitions to melee attack at moved position");
        float stopped=state.x;state.hp=0;Step(room,p,30);check(state.x==stopped,"Dead pursuer stops moving and attacking");

        var wall=new LanDungeonFloor{id="wall",x=2,y=0,width=1,height=7};
        room=Room(Enemy(),Ground(),wall);p=Player(6);Step(room,p,40);
        check(room.Enemies[0].x==0,"Solid wall blocks initial aggro even inside detection range");
        room=Room(Enemy());p=Player(6);Step(room,p,5);p.Active=false;float at=room.Enemies[0].x;Step(room,p,40);
        check(room.Enemies[0].x==at,"Dead/disconnected/portal-waiting target is immediately released");
        room=Room(Enemy());p=Player(6);Step(room,p,5);p.X=25;Step(room,p,60);
        at=room.Enemies[0].x;Step(room,p,30);
        check(room.Enemies[0].x==at&&at<8,"Lost target is pursued only to last seen location, then forgotten");
        room=Room(Enemy());p=Player(6);Step(room,p,5);p.X=-3;Step(room,p,8);
        check(room.Enemies[0].facing==-1&&room.Enemies[0].x<1,"Already detected target can be followed after crossing behind");

        var step=new LanDungeonFloor{id="step",x=2,y=0,width=1,height=.8f};
        room=Room(Enemy(),Ground(),step);p=Player(6);float peak=0,maxStep=0,previous=0;
        for(int i=0;i<70;i++){Step(room,p);state=room.Enemies[0];peak=Math.Max(peak,state.y);maxStep=Math.Max(maxStep,Math.Abs(state.x-previous));previous=state.x;}
        check(state.x>3.5&&peak>.8f,"Pursuer uses ordinary jump to cross a low solid obstacle");
        check(peak<=4f/3+.01f&&maxStep<=LanRules.WalkSpeed*.05f+.001f,"Pursuer never uses double-jump height or dash speed");

        var platform=new LanDungeonFloor{id="platform",x=1,y=.8f,width=6,height=.2f,oneWay=true};
        room=Room(Enemy(),Ground(),platform);p=Player(5,1);Step(room,p,70);
        check(Math.Abs(room.Enemies[0].y-1)<.01f&&room.Enemies[0].x>3,"Pursuer jumps through and lands on reachable one-way platform");
        platform.y=1.8f;platform.x=0;room=Room(Enemy(0,3,2),Ground(),platform);p=Player(5,0);Step(room,p,60);
        check(room.Enemies[0].y==0,"Pursuer drops through one-way floor toward lower player");

        platform.oneWay=false;platform.y=2;platform.height=1;platform.width=4;
        room=Room(Enemy(0,2,3),Ground(),platform);p=Player(8);Step(room,p,80);
        check(room.Enemies[0].y==0&&room.Enemies[0].x>4,"Pursuer walks off solid platform edge instead of passing through it");

        var left=new LanDungeonFloor{id="left",x=-10,y=-1,width=11,height=1};
        var right=new LanDungeonFloor{id="right",x=3,y=-1,width=20,height=1};
        room=Room(Enemy(),left,right);p=Player(6);Step(room,p,65);
        check(room.Enemies[0].x>3&&room.Enemies[0].y==0,"Pursuer jumps a gap within single-jump reach");
        right.x=7;room=Room(Enemy(),left,right);p=Player(7.5f);Step(room,p,65);
        check(room.Enemies[0].x<1.31f&&room.Enemies[0].y==0,"Pursuer does not dash/double-jump across unreachable gap");

        platform=new(){id="high",x=0,y=3,width=8,height=.2f,oneWay=true};
        room=Room(Enemy(),Ground(),platform);p=Player(5,3.2f);peak=0;
        for(int i=0;i<100;i++){Step(room,p);peak=Math.Max(peak,room.Enemies[0].y);}
        check(peak<=4f/3+.01f&&room.Enemies[0].y==0,"Unreachable upper platform never grants an air jump");
        var ceiling=new LanDungeonFloor{id="ceiling",x=-1,y=2.5f,width=5,height=.3f};
        room=Room(Enemy(),Ground(),step,ceiling);p=Player(6);Step(room,p,70);
        check(room.Enemies[0].x<=1.701f&&room.Enemies[0].y<.3f,"Low ceiling prevents jumping through solid geometry");

        foreach(int kind in new[]{1,2,3})
        {var fixedEnemy=Enemy(kind);fixedEnemy.chasePlayer=false;room=Room(fixedEnemy);p=Player(6);Step(room,p,50);check(room.Enemies[0].x==0&&room.Enemies[0].y==0,"Explicit fixed type "+kind+" keeps stationary combat");}
        room=Room(Enemy());p=Player(7);Step(room,p,12);state=room.Enemies[0];p.Active=false;
        var shooter=Player(state.x-1);room.Attack(shooter,5,1,0,0,1,1,room.Time);
        check(state.hp<state.maxHp,"Player melee hits current enemy position instead of authored spawn");
        shooter.NextAttack=0;float hp=state.hp;room.Attack(shooter,0,1,0,0,1,1,room.Time);Step(room,p,6);
        check(state.hp<hp,"Player projectile collides with moved enemy position");
    }
}
