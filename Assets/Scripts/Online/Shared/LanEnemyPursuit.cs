#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniWar.Online
{
    // Makes ordinary move/jump/drop decisions. Physics never grants a dash or an air jump.
    sealed class LanEnemyPursuit
    {
        readonly LanDungeonRoom room;
        readonly LanEnemySpawn def;
        readonly LanEnemyState state;
        readonly HashSet<string> dropping=new HashSet<string>();
        readonly HashSet<string> trialDropping=new HashSet<string>();
        string targetId;
        float goalX,goalY,waypointX,velocity;
        double lastSeen,planAt;
        bool following;
        int spacing; // -1 retreat, 0 hold, 1 approach; hysteresis avoids boundary jitter.
        bool Ranged=>def.kind==(int)EnemyAttackKind.Crossbow||def.kind==(int)EnemyAttackKind.Magic;
        public bool CanAttack {get;private set;}=true;
        public LanEnemyPursuit(LanDungeonRoom room,LanEnemySpawn def,LanEnemyState state)
        {this.room=room;this.def=def;this.state=state;}
        public void Forget(){targetId=null;following=false;planAt=0;spacing=0;}
        static bool Alive(CombatFighter p)=>p.Active&&p.Health>0&&!float.IsNaN(p.X)&&!float.IsNaN(p.Y)&&!float.IsInfinity(p.X)&&!float.IsInfinity(p.Y);
        bool Visible(CombatFighter p,bool acquiring,Func<float,float,float,float,bool> blocked)
        {
            float dx=p.X-state.x,dy=p.Y-state.y;
            float distance=(float)Math.Sqrt(dx*dx+dy*dy),range=def.detectionRange*(acquiring?1:1.25f);
            if(distance>range)return false;
            if(acquiring&&distance>.1f&&dx*state.facing/distance<Math.Cos(Math.Max(1,Math.Min(360,def.viewAngle))*Math.PI/360)-.0001)return false;
            return !blocked(state.x,state.y+def.height*.65f,p.X,p.Y+1.1f);
        }
        public CombatFighter Tick(IReadOnlyList<CombatFighter> players,float dt,double now,bool windup,Func<float,float,float,float,bool> blocked)
        {
            var target=players.FirstOrDefault(p=>p.Id==targetId&&Alive(p));
            if(target==null)Forget();
            bool visible=target!=null&&Visible(target,false,blocked);
            if(target!=null&&!visible&&now-lastSeen>1.5){Forget();target=null;}
            if(target==null)
            {
                target=players.Where(Alive).Where(p=>Visible(p,true,blocked))
                    .OrderBy(p=>(p.X-state.x)*(p.X-state.x)+(p.Y-state.y)*(p.Y-state.y)).FirstOrDefault();
                visible=target!=null;
            }
            if(visible){targetId=target.Id;goalX=target.X;goalY=target.Y;lastSeen=now;following=true;}
            bool ground=velocity<=0&&LanDungeonPhysics.Grounded(room,state.x,state.y,dropping);
            bool jump=false,drop=false;
            float stop=visible&&Math.Abs(goalY-state.y)<.8f?Math.Max(.4f,def.range*.8f):.12f;
            float destinationY=goalY;
            if(Ranged&&visible&&Math.Abs(goalY-state.y)<7&&!windup)
            {
                float distance=Math.Abs(goalX-state.x),preferred=def.range*.8f;
                if(spacing<0&&distance>=preferred-.05f||spacing>0&&distance<=preferred+.05f)spacing=0;
                if(spacing==0)spacing=distance<def.range*.6f?-1:distance>def.range*.9f?1:0;
                if(distance>.05f)state.facing=goalX<state.x?-1:1;
                if(spacing<=0)
                {
                    float retreat=0;
                    if(spacing<0&&ground)
                    {
                        float destination=goalX-state.facing*preferred;
                        float next=LanDungeonPhysics.MoveX(room,state.x,state.y,Travel(state.x,destination,.025f,dt));
                        // Backpedal on supported ground; do not step off an edge or hide behind a wall.
                        if(LanDungeonPhysics.Grounded(room,next,state.y,dropping)
                            &&!blocked(next,state.y+def.height*.65f,goalX,goalY+1.1f))retreat=next-state.x;
                    }
                    planAt=0;
                    MoveBody(retreat,false,false,ground,dt);
                    return target;
                }
                // A clear shot across floors does not require descending to the player's floor.
                stop=preferred;destinationY=state.y;
            }
            else if(Ranged&&!windup)spacing=0;
            if(following&&!windup)
            {
                if(Math.Abs(goalX-state.x)>.05f)state.facing=goalX<state.x?-1:1;
                if(ground&&now>=planAt)
                {
                    waypointX=goalX;
                    // To descend from solid ground, walk to its nearest exposed edge.
                    if(destinationY<state.y-.8f&&!LanDungeonPhysics.CanDrop(room,state.x,state.y))
                    {
                        var support=room.floors.Where(f=>!f.oneWay&&LanDungeonPhysics.Supports(f,state.x,state.y)).OrderByDescending(f=>f.width).FirstOrDefault();
                        if(support!=null)
                        {
                            float left=support.x-.5f,right=support.x+support.width+.5f;
                            bool leftValid=left>room.x+.3f,rightValid=right<room.x+room.width-.3f;
                            if(leftValid||rightValid)waypointX=!rightValid||leftValid&&Math.Abs(state.x-left)+Math.Abs(goalX-left)<Math.Abs(state.x-right)+Math.Abs(goalX-right)?left:right;
                        }
                    }
                    float scoreY=destinationY;
                    float idle=Score(state.x,state.y,waypointX,scoreY);
                    float walk=Trial(waypointX,scoreY,stop,false,false);
                    float leap=Trial(waypointX,scoreY,stop,true,false)+.15f;
                    float down=LanDungeonPhysics.CanDrop(room,state.x,state.y)&&destinationY<state.y-.8f?Trial(goalX,destinationY,stop,false,true)+.1f:float.PositiveInfinity;
                    jump=leap<walk-.05f&&leap<idle-.05f&&leap<down;
                    drop=down<walk-.05f&&down<idle-.05f&&down<leap;
                    // An unreachable jump or a bottomless gap is not solved by repeated hopping.
                    if(!jump&&!drop&&walk>=idle-.01f)waypointX=state.x;
                    planAt=now+.15;
                }
                if(!ground)waypointX=goalX;
            }
            else waypointX=state.x;
            float dx=following&&!windup?Travel(state.x,waypointX,stop,dt):0;
            MoveBody(dx,jump,drop,ground,dt);
            return visible?target:null;
        }
        void MoveBody(float dx,bool jump,bool drop,bool ground,float dt)
        {
            float previousX=state.x;
            state.x=LanDungeonPhysics.MoveX(room,state.x,state.y,dx);
            CanAttack=!Ranged||Math.Abs(state.x-previousX)<.001f;
            if(drop){LanDungeonPhysics.Drop(room,state.y,dropping);velocity=-2;}
            else if(jump&&ground)velocity=LanDungeonPhysics.JumpSpeed(room);
            state.grounded=LanDungeonPhysics.Vertical(room,state.x,ref state.y,ref velocity,dropping,dt);
            if(state.y<room.y-3)
            {state.x=def.x;state.y=def.y;velocity=0;dropping.Clear();Forget();}
        }
        static float Score(float x,float y,float gx,float gy)=>Math.Abs(x-gx)+2*Math.Abs(y-gy);
        static float Travel(float x,float goal,float stop,float dt)
        {
            float dx=goal-x;
            return Math.Sign(dx)*Math.Min(LanRules.WalkSpeed*dt,Math.Max(0,Math.Abs(dx)-stop));
        }
        float Trial(float gx,float gy,float stop,bool jump,bool drop)
        {
            float x=state.x,y=state.y,v=0;trialDropping.Clear();
            if(drop){LanDungeonPhysics.Drop(room,y,trialDropping);v=-2;}
            else if(jump)v=LanDungeonPhysics.JumpSpeed(room);
            bool landed=false;
            // Predict only one ordinary jump, with the actual collision solver and speed.
            for(int i=0;i<26;i++)
            {
                x=LanDungeonPhysics.MoveX(room,x,y,Travel(x,gx,stop,.05f));
                landed=LanDungeonPhysics.Vertical(room,x,ref y,ref v,trialDropping,.05f);
            }
            return landed?Score(x,y,gx,gy):float.PositiveInfinity;
        }
    }
}
