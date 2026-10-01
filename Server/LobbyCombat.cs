using MiniWar.Online;

namespace MiniWar.Server;

public sealed partial class LobbyWorld
{
    static LanCombatRoom CombatRoom(Group group,LanDungeonRoom room)
    {
        if(!group.Combat.TryGetValue(room.id,out var combat))group.Combat[room.id]=combat=new LanCombatRoom(room);
        return combat;
    }
    void TickDungeonCombat(float dt)
    {
        foreach(var group in groups.Values)
        {
            if(group.InstanceId.Length==0||group.Completed)continue;
            var room=GateMap.Map.rooms.Single(r=>r.id==group.RoomId);
            var combat=CombatRoom(group,room);
            var fighters=group.Members.Select(id=>players[id].Combat).ToArray();
            foreach(var id in group.Members)
            {
                var p=players[id];var f=p.Combat;f.Id=id;f.X=p.X;f.Y=p.Y;f.Active=!p.Dead&&p.WaitingPortal.Length==0;
                if(!f.Active||!p.Fire)continue;
                var weapon=p.Profile.items.FirstOrDefault(i=>i.id==p.Profile.activeItemId);
                if(weapon==null)continue;
                if(combat.Attack(f,weapon.family,weapon.tier,weapon.enhance,p.AimAngle,p.Facing,group.Power,Time))
                    shots.Add(new LanEvent{op="shot",channel=p.Channel,instanceId=p.InstanceId,tick=TickNumber,
                        shot=new LanShot{id=++shotId,shooter=p.Profile.nickname,family=weapon.family,tier=weapon.tier,facing=p.Facing,x=p.X,y=p.Y+LanShooting.AimHeight,angle=p.AimAngle}});
            }
            combat.Tick(dt,fighters,Time);
            foreach(var id in group.Members)
            {
                var p=players[id];
                if(!p.Dead&&p.Combat.Health<=0)
                {p.Dead=true;p.WaitingPortal="";group.Rally.Remove(id);ResetMotion(p);NotifyParty(id,p.ReviveUsed?"부활 기회를 사용했습니다. 동료를 관전합니다.":"쓰러졌습니다. R로 한 번 부활할 수 있습니다.");}
            }
            if(combat.Remaining==0&&group.ClearedRooms.Add(room.id))
            {
                group.Completed=room.boss;combat.Pause();
                foreach(string id in group.Members)NotifyParty(id,room.boss?"던전 클리어! P 메뉴에서 마을로 돌아갈 수 있습니다.":"방 클리어! 포탈 앞에서 S를 눌러 이동하세요.");
            }
        }
    }
    public string ReviveCommand(string id,LanCommand cmd)
    {
        var p=Find(id);
        if(p==null||p.DungeonRoom==null||!memberships.TryGetValue(id,out var key))return "던전 안에서 사용할 수 있습니다.";
        var group=groups[key];
        if(cmd.roomId!=group.RoomId||cmd.roomSequence!=group.RoomSequence)return "이미 다른 방으로 이동했습니다.";
        if(!p.Dead||p.ReviveUsed)return "던전마다 쓰러졌을 때 한 번만 부활할 수 있습니다.";
        p.ReviveUsed=true;p.Dead=false;p.Combat.Health=100;p.Combat.ProtectedUntil=Time+2;
        p.X=p.DungeonRoom.spawnX;p.Y=p.DungeonRoom.spawnY;p.WaitingPortal="";ResetMotion(p);
        NotifyParty(id,"부활했습니다. 남은 부활 기회가 없습니다.");return "";
    }
}
