#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniWar.Online
{
    // The same feet-based body and collision rules serve players and pursuing enemies.
    public static class LanDungeonPhysics
    {
        public const float HalfWidth=.3f, Height=2.3f, Epsilon=.0001f;
        public static bool Supports(LanDungeonFloor f,float x,float y) =>
            x+HalfWidth>f.x && x-HalfWidth<f.x+f.width && Math.Abs(y-f.y-f.height)<.06f;
        public static bool Grounded(LanDungeonRoom room,float x,float y,HashSet<string> ignored=null) =>
            room.floors.Any(f=>(ignored==null||!ignored.Contains(f.id))&&Supports(f,x,y));
        public static bool CanDrop(LanDungeonRoom room,float x,float y) =>
            Grounded(room,x,y)&&!room.floors.Any(f=>!f.oneWay&&Supports(f,x,y));
        public static void Drop(LanDungeonRoom room,float y,HashSet<string> ignored)
        {
            foreach(var f in room.floors)
                if(f.oneWay&&Math.Abs(y-f.y-f.height)<.06f)ignored.Add(f.id);
        }
        public static float JumpSpeed(LanDungeonRoom room) => (float)Math.Sqrt(2*LanRules.Gravity*room.jumpHeight);
        public static float MoveX(LanDungeonRoom room,float x,float y,float dx)
        {
            foreach(var f in room.floors)
            {
                if(f.oneWay||y+Height<=f.y+Epsilon||y>=f.y+f.height-Epsilon)continue;
                if(dx>0&&x+HalfWidth<=f.x+Epsilon)dx=Math.Min(dx,f.x-x-HalfWidth);
                if(dx<0&&x-HalfWidth>=f.x+f.width-Epsilon)dx=Math.Max(dx,f.x+f.width-x+HalfWidth);
            }
            return Math.Max(room.x+HalfWidth,Math.Min(room.x+room.width-HalfWidth,x+dx));
        }
        public static bool Vertical(LanDungeonRoom room,float x,ref float y,ref float speed,HashSet<string> ignored,float dt)
        {
            speed-=LanRules.Gravity*dt;
            float dy=speed*dt;bool landed=false;
            foreach(var f in room.floors)
            {
                if(ignored.Contains(f.id)||x+HalfWidth<=f.x+Epsilon||x-HalfWidth>=f.x+f.width-Epsilon)continue;
                float top=f.y+f.height;
                if(dy<=0&&y>=top-Epsilon&&y+dy<=top){dy=top-y;speed=0;landed=true;}
                if(!f.oneWay&&dy>0&&y+Height<=f.y+Epsilon&&y+Height+dy>=f.y)
                {dy=f.y-y-Height;speed=0;}
            }
            y+=dy;
            float feet=y;
            ignored.RemoveWhere(id=>!room.floors.Any(f=>f.id==id&&feet>=f.y+f.height-.06f));
            return landed;
        }
    }
}
