using System.Linq;
using MiniWar.Online;
using UnityEngine;

namespace MiniWar.Dungeons
{
    public static class DungeonCombatData
    {
        public static LanDungeonRoom Room(DungeonLayout layout)
        {
            return new LanDungeonRoom {
                x=layout.bounds.x,y=layout.bounds.y,width=layout.bounds.width,height=layout.bounds.height,
                spawnX=layout.entrance.x,spawnY=layout.entrance.y,jumpHeight=layout.jumpHeight,airJumpHeight=layout.airJumpHeight,
                floors=layout.floors.Select(f=>new LanDungeonFloor{id=f.id,x=f.rect.x,y=f.rect.y,width=f.rect.width,height=f.rect.height,oneWay=f.oneWay}).ToArray(),
                enemies=layout.spawns.SelectMany(s=>Enumerable.Range(0,Mathf.Clamp(s.count,1,30)).Select(i=>{
                    var e=s.enemy;var p=s.PositionAt(i);var size=s.Size;
                    return new LanEnemySpawn{id=s.id+":"+i,name=s.Label,x=p.x,y=p.y,width=size.x,height=size.y,facing=s.faceLeft?-1:1,
                        health=e==null?100:Mathf.Max(1,e.baseHealth),boss=e!=null&&e.isBoss,kind=e==null?0:(int)e.attackKind,
                        chasePlayer=e==null||e.chasePlayer,detectionRange=e==null?8:Mathf.Max(.5f,e.detectionRange),viewAngle=e==null?180:e.viewAngle,
                        damage=e==null?12:Mathf.Max(1,e.attackDamage),range=e==null?1.8f:Mathf.Max(.5f,e.attackRange),
                        interval=e==null?1.5f:Mathf.Max(.2f,e.attackInterval),windup=e==null?.4f:Mathf.Max(.1f,e.attackWindup),
                        projectileSpeed=e==null?10:Mathf.Max(1,e.projectileSpeed)};
                })).ToArray() };
        }
    }
}
