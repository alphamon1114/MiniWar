#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniWar.Online
{
    public enum EnemyAttackKind { Melee, Crossbow, Magic, Cannon }
    [Serializable] public sealed class LanEnemySpawn
    {
        public string id, name;
        public float x,y,width=.8f,height=2.2f,health=100,damage=12,range=1.8f,interval=1.5f,windup=.4f,projectileSpeed=10;
        public int kind;
        public int facing=-1;
        public bool boss;
        public bool chasePlayer=true;
        public float detectionRange=8,viewAngle=180;
    }
    [Serializable] public sealed class LanEnemyState
    {
        public string id;
        public float x,y,width,height,hp,maxHp,windup,flash,range,aimX,aimY;
        public int kind,facing=-1;
        public bool grounded;
    }
    [Serializable] public sealed class LanCombatProjectile
    {
        public long id;
        public float x,y,vx,vy,radius;
        public int kind;
        public bool hostile;
    }
    // Trusted simulation state only; never accepted from a client command.
    public sealed class CombatFighter
    {
        public string Id;
        public float X,Y,Health=100;
        public bool Active=true;
        public double NextAttack,ProtectedUntil;
    }
    public sealed class LanCombatRoom
    {
        sealed class Enemy
        {
            public LanEnemySpawn Def;
            public LanEnemyState State;
            public double NextAttack=.8, Impact;
            public LanEnemyPursuit Pursuit;
        }
        sealed class Bolt
        {
            public LanCombatProjectile State;
            public float Damage,Life;
        }
        readonly LanDungeonFloor[] floors;
        readonly List<Enemy> enemies=new List<Enemy>();
        readonly List<Bolt> bolts=new List<Bolt>();
        long boltId;
        public double Time {get;private set;}
        public int Remaining => enemies.Count(e=>e.State.hp>0);
        public LanEnemyState[] Enemies => enemies.Select(e=>e.State).ToArray();
        public LanCombatProjectile[] Projectiles => bolts.Select(b=>b.State).ToArray();
        public LanCombatRoom(LanDungeonRoom room)
        {
            floors=room.floors??Array.Empty<LanDungeonFloor>();
            foreach(var d in room.enemies??Array.Empty<LanEnemySpawn>())
            {
                var enemy=new Enemy{Def=d,State=new LanEnemyState{id=d.id,x=d.x,y=d.y,width=d.width,height=d.height,
                    hp=d.health,maxHp=d.health,kind=d.kind,range=d.range,facing=d.facing}};
                if(d.kind>=(int)EnemyAttackKind.Melee&&d.kind<=(int)EnemyAttackKind.Magic&&d.chasePlayer)
                    enemy.Pursuit=new LanEnemyPursuit(room,d,enemy.State);
                enemies.Add(enemy);
            }
        }
        public void Pause()
        {bolts.Clear();foreach(var e in enemies){e.Impact=0;e.State.windup=0;e.NextAttack=Time+.8;e.Pursuit?.Forget();}}
        public void ClearForPreview() {foreach(var e in enemies)e.State.hp=0;Pause();}
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static float Clamp(float v,float min,float max)=>Math.Max(min,Math.Min(max,v));
        public static float Damage(int family)
        {switch((WeaponFamily)family){case WeaponFamily.SubmachineGun:return 14;case WeaponFamily.Shotgun:return 15;case WeaponFamily.Rifle:return 22;case WeaponFamily.SniperRifle:return 100;case WeaponFamily.Revolver:return 48;case WeaponFamily.Melee:return 36;default:return 28;}}
        public bool Attack(CombatFighter p,int family,int tier,int enhance,float angle,int facing,float power,double now)
        {
            if(p==null||!p.Active||p.Health<=0||!Finite(angle)||!LanRules.IsWeaponFamily(family)||now<p.NextAttack)return false;
            bool melee=family==(int)WeaponFamily.Melee;
            p.NextAttack=now+(melee?.5:LanShooting.Interval(family));
            float damage=Damage(family)*(1+.25f*(Clamp(tier,1,3)-1)+.05f*Clamp(enhance,0,20))*Clamp(power,1,3.3f);
            if(melee)
            {
                foreach(var e in enemies)
                    if(e.State.hp>0&&Math.Abs(e.State.y-p.Y)<1.2f&&(e.State.x-p.X)*facing>=-.25f
                        &&Math.Abs(e.State.x-p.X)<=1.8f+e.Def.width/2&&!Blocked(p.X,p.Y+1.1f,e.State.x,e.State.y+e.Def.height*.5f))Hurt(e,damage);
            }
            else
            {
                var shot=new LanShot{family=family,angle=angle};
                for(int i=0;i<LanShooting.Pellets(family);i++)
                    Shoot(p.X,p.Y+LanShooting.AimHeight,LanShooting.PelletAngle(shot,i),LanShooting.Speed(family),.045f,damage,false,family,1.2f);
            }
            return true;
        }
        void Shoot(float x,float y,float angle,float speed,float radius,float damage,bool hostile,int kind,float life)
        {
            double a=angle*Math.PI/180;
            bolts.Add(new Bolt{State=new LanCombatProjectile{id=++boltId,x=x,y=y,vx=(float)Math.Cos(a)*speed,vy=(float)Math.Sin(a)*speed,radius=radius,hostile=hostile,kind=kind},Damage=damage,Life=life});
        }
        static void Hurt(Enemy e,float damage)
        {e.State.hp=Math.Max(0,e.State.hp-damage);e.State.flash=.12f;if(e.State.hp<=0){e.Impact=0;e.State.windup=0;}}
        static bool Alive(CombatFighter p)=>p.Active&&p.Health>0&&Finite(p.X)&&Finite(p.Y);
        public void Tick(float dt,IReadOnlyList<CombatFighter> players,double now)
        {
            if(!Finite(dt)||dt<=0||dt>.1f)throw new ArgumentOutOfRangeException(nameof(dt));
            Time+=dt;
            foreach(var e in enemies)
            {
                var s=e.State;var d=e.Def;s.flash=Math.Max(0,s.flash-dt);
                if(s.hp<=0)continue;
                var pursued=e.Pursuit?.Tick(players,dt,Time,e.Impact>0,Blocked);
                if(e.Impact>0)
                {
                    s.windup=(float)Math.Max(0,e.Impact-Time);
                    if(Time<e.Impact)continue;
                    e.Impact=0;e.NextAttack=Time+Math.Max(.2f,d.interval);
                    if(d.kind==(int)EnemyAttackKind.Melee)
                    {
                        foreach(var p in players)
                            if(Alive(p)&&Math.Abs(p.Y-s.y)<.8f&&(p.X-s.x)*s.facing>=-.2f&&Math.Abs(p.X-s.x)<=d.range+.3f
                                &&!Blocked(s.x,s.y+1,p.X,p.Y+1))Hit(p,d.damage,now);
                    }
                    else
                    {
                        float x=s.x+s.facing*d.width*.55f,y=s.y+d.height*.65f;
                        float angle=(float)(Math.Atan2(s.aimY-y,s.aimX-x)*180/Math.PI);
                        Shoot(x,y,angle,Math.Max(1,d.projectileSpeed),d.kind==1?.07f:.16f,d.damage,true,d.kind,4);
                    }
                    continue;
                }
                if(Time<e.NextAttack)continue;
                var target=e.Pursuit==null?players.Where(Alive).Where(p=>Math.Abs(p.X-s.x)<=d.range&&Math.Abs(p.Y-s.y)<(d.kind==0?.8f:7)
                    &&!Blocked(s.x,s.y+d.height*.65f,p.X,p.Y+1.1f)).OrderBy(p=>Math.Abs(p.X-s.x)+Math.Abs(p.Y-s.y)).FirstOrDefault():pursued;
                if(target==null)continue;
                if(e.Pursuit!=null&&(!s.grounded||!e.Pursuit.CanAttack||Math.Abs(target.X-s.x)>d.range||Math.Abs(target.Y-s.y)>=(d.kind==0?.8f:7)))continue;
                s.facing=target.X<s.x?-1:1;s.aimX=target.X;s.aimY=target.Y+1.1f;
                e.Impact=Time+Math.Max(.1f,d.windup);s.windup=Math.Max(.1f,d.windup);
            }
            foreach(var b in bolts.ToArray())
            {
                var s=b.State;float nx=s.x+s.vx*dt,ny=s.y+s.vy*dt;
                float best=WallHit(s.x,s.y,nx,ny,s.radius);Enemy enemy=null;CombatFighter player=null;
                if(s.hostile)
                {
                    foreach(var p in players.Where(Alive))
                    {float t=Segment(s.x,s.y,nx,ny,p.X-.3f-s.radius,p.Y-s.radius,.6f+s.radius*2,2.3f+s.radius*2);if(t<best){best=t;player=p;}}
                }
                else foreach(var e in enemies.Where(e=>e.State.hp>0))
                {var d=e.State;float t=Segment(s.x,s.y,nx,ny,d.x-d.width/2-s.radius,d.y-s.radius,d.width+s.radius*2,d.height+s.radius*2);if(t<best){best=t;enemy=e;}}
                if(player!=null)Hit(player,b.Damage,now);
                if(enemy!=null)Hurt(enemy,b.Damage);
                b.Life-=dt;
                if(best<=1||b.Life<=0)bolts.Remove(b);else{s.x=nx;s.y=ny;}
            }
            if(Remaining==0)bolts.RemoveAll(b=>b.State.hostile);
        }
        static void Hit(CombatFighter p,float damage,double now)
        {
            if(now<p.ProtectedUntil)return;
            p.Health=Math.Max(0,p.Health-damage);p.ProtectedUntil=now+.25;
        }
        bool Blocked(float x,float y,float endX,float endY)=>WallHit(x,y,endX,endY,0)<=1;
        float WallHit(float x,float y,float endX,float endY,float radius)
        {
            float best=float.PositiveInfinity;
            foreach(var f in floors)if(!f.oneWay)best=Math.Min(best,Segment(x,y,endX,endY,f.x-radius,f.y-radius,f.width+radius*2,f.height+radius*2));
            return best;
        }
        public static float Segment(float x,float y,float endX,float endY,float rx,float ry,float rw,float rh)
        {
            float lo=0,hi=1;
            if(!Axis(x,endX-x,rx,rx+rw,ref lo,ref hi)||!Axis(y,endY-y,ry,ry+rh,ref lo,ref hi))return float.PositiveInfinity;
            return lo;
        }
        static bool Axis(float start,float delta,float min,float max,ref float lo,ref float hi)
        {
            if(Math.Abs(delta)<.000001f)return start>=min&&start<=max;
            float a=(min-start)/delta,b=(max-start)/delta;if(a>b){float t=a;a=b;b=t;}
            lo=Math.Max(lo,a);hi=Math.Min(hi,b);return lo<=hi;
        }
    }
}
