#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;

namespace MiniWar.Online
{
    public sealed partial class OnlinePartySmoke
    {
        IEnumerator RunDungeon()
        {
            if (config.leader)
            {
                Set("partyPage", 0); Set("selectedDungeon", LanDungeons.Gate);
                yield return Capture("dungeon-map");
                Set("selectedDungeon", "temple"); yield return Capture("dungeon-upcoming");
                Set("selectedDungeon", LanDungeons.Gate); Set("partyTitle", "성문 외곽 · 함께 탈환해요"); Send("party_create");
                yield return Until(() => State.party != null && State.party.applications.Length == 1);
                if (!ready) { Finish("FAIL dungeon party application"); yield break; }
                yield return Capture("dungeon-application");
                string id = State.party.id;
                Send("party_accept", id, State.party.applications[0].id);
                yield return Until(() => State.party != null && State.party.members.Length == 2);
                if (!ready) { Finish("FAIL dungeon acceptance"); yield break; }
                yield return Capture("dungeon-party");
                Send("party_start", id);
                yield return Until(() => Get<LanDungeonVisit>("dungeonVisit") != null && Get<System.Collections.Generic.Dictionary<string, Transform>>("actors").Count == 2);
                if (!ready) { Finish("FAIL dungeon start: " + Get<string>("notice")); yield break; }
                if (Get<Transform>("townRoot").gameObject.activeSelf || Get<MiniWar.Dungeons.DungeonLayout>("onlineLayout") == null || Get<bool>("partyPanel"))
                { Finish("FAIL dungeon geometry/UI handoff"); yield break; }
                yield return Capture("dungeon-entry-leader");
                yield return FightFirstRoom();
                if (!ready) { Finish("FAIL leader combat clear: " + Get<string>("notice")); yield break; }
                yield return TravelThroughFirstPortal();
                if (!ready) { Finish("FAIL leader portal transfer: " + Get<string>("notice")); yield break; }
                yield return Capture("dungeon-next-room-leader");
                yield return new WaitForSecondsRealtime(3);
                typeof(OnlineLobby).GetMethod("SetPartyPanel", Flags).Invoke(lobby, new object[] { true });
                yield return Capture("dungeon-return-menu");
                Send("party_return", id);
                yield return Until(() => Get<LanDungeonVisit>("dungeonVisit") == null && Get<Transform>("townRoot").gameObject.activeSelf);
                if (!ready) { Finish("FAIL party return"); yield break; }
                yield return new WaitForSecondsRealtime(2);
                Finish("PASS Windows dungeon leader: map, party, synchronized combat, room clear, S portal transfer, party return");
            }
            else
            {
                Set("partyPage", 2);
                yield return Until(() => State.parties.Length == 1);
                if (!ready) { Finish("FAIL dungeon listing"); yield break; }
                if (State.parties[0].dungeonId != LanDungeons.Gate) { Finish("FAIL wrong selected dungeon"); yield break; }
                yield return Capture("dungeon-list");
                string id = State.parties[0].id; Send("party_apply", id);
                yield return Until(() => Get<LanDungeonVisit>("dungeonVisit") != null && Get<System.Collections.Generic.Dictionary<string, Transform>>("actors").Count == 2);
                if (!ready) { Finish("FAIL member dungeon handoff"); yield break; }
                if (Get<LanDungeonVisit>("dungeonVisit").power != 1.8f || Get<Transform>("townRoot").gameObject.activeSelf)
                { Finish("FAIL member instance bonus/scenery"); yield break; }
                yield return Capture("dungeon-entry-member");
                yield return FightFirstRoom();
                if (!ready) { Finish("FAIL member combat clear: " + Get<string>("notice")); yield break; }
                yield return TravelThroughFirstPortal();
                if (!ready) { Finish("FAIL member portal transfer: " + Get<string>("notice")); yield break; }
                yield return Capture("dungeon-next-room-member");
                yield return Until(() => Get<LanDungeonVisit>("dungeonVisit") == null && Get<Transform>("townRoot").gameObject.activeSelf);
                if (!ready) { Finish("FAIL member return"); yield break; }
                Finish("PASS Windows dungeon member: recruitment, synchronized combat, room clear, S portal transfer, party return");
            }
        }
        IEnumerator FightFirstRoom()
        {
            ready=false;Set("sendAt",float.MaxValue);
            var client=Get<LanClient>("client");float deadline=Time.realtimeSinceStartup+35,airJumpAt=float.MaxValue;
            while(Time.realtimeSinceStartup<deadline)
            {
                var visit=Get<LanDungeonVisit>("dungeonVisit");
                if(visit.enemiesRemaining==0&&visit.portalsOpen){ready=true;break;}
                var self=System.Array.Find(Get<LanEvent>("snapshot").actors,p=>p.nickname==config.nickname);
                if(self.dead)
                {
                    if(self.reviveUsed)break;
                    client.Send(new LanCommand{op="dungeon_revive",roomId=visit.roomId,roomSequence=visit.roomSequence});
                    yield return new WaitForSecondsRealtime(.2f);continue;
                }
                LanEnemyState target=null;
                foreach(var enemy in visit.enemies)if(enemy.hp>0&&(target==null||enemy.y<target.y||enemy.y==target.y&&enemy.x<target.x))target=enemy;
                if(target==null)break;
                bool upper=target.y>3;
                float destination=upper?15f:target.x-7;
                float move=Mathf.Abs(destination-self.x)<.2f?0:Mathf.Clamp((destination-self.x)*2,-1,1);
                bool jump=false;
                if(upper&&move==0)
                {
                    if(self.grounded){jump=true;airJumpAt=Time.realtimeSinceStartup+.25f;}
                    else if(Time.realtimeSinceStartup>=airJumpAt){jump=true;airJumpAt=float.MaxValue;}
                }
                // Upper-floor targets can backpedal: aim above the solid platform's front edge.
                float angle=Mathf.Atan2(target.y+target.height*(upper?.9f:.65f)-self.y-LanShooting.AimHeight,target.x-self.x)*Mathf.Rad2Deg;
                long seq=Get<long>("sequence")+1;Set("sequence",seq);
                client.Send(new LanCommand{op="input",sequence=seq,move=move,jump=jump,fire=true,aiming=true,aimAngle=angle});
                yield return new WaitForSecondsRealtime(.05f);
            }
            long stop=Get<long>("sequence")+1;Set("sequence",stop);client.Send(new LanCommand{op="input",sequence=stop});Set("sendAt",0f);
            if(ready)yield return Capture("combat-cleared-"+(config.leader?"leader":"member"));
        }
        IEnumerator TravelThroughFirstPortal()
        {
            ready=false;
            var room=Get<MiniWar.Dungeons.DungeonRoom>("onlineRoom");
            if(room==null || room.layout.bounds.width!=36 || room.layout.bounds.height!=18
                || Get<Transform>("dungeonRoot").Find("Road top "+room.layout.floors[0].id)==null) yield break;
            var portal=room.portals[0];var visit=Get<LanDungeonVisit>("dungeonVisit");long oldSequence=visit.roomSequence;
            var client=Get<LanClient>("client");
            Set("sendAt",float.MaxValue); // QA owns movement intent temporarily; server still owns positions.
            float deadline=Time.realtimeSinceStartup+12;
            bool reached=false;
            while(Time.realtimeSinceStartup<deadline)
            {
                var self=System.Array.Find(Get<LanEvent>("snapshot").actors,p=>p.nickname==config.nickname);
                float delta=portal.position.x-self.x;
                if(Mathf.Abs(delta)<.25f&&self.grounded){reached=true;break;}
                long seq=Get<long>("sequence")+1;Set("sequence",seq);
                client.Send(new LanCommand{op="input",sequence=seq,move=Mathf.Clamp(delta*2,-1,1)});
                yield return new WaitForSecondsRealtime(.05f);
            }
            long stop=Get<long>("sequence")+1;Set("sequence",stop);
            client.Send(new LanCommand{op="input",sequence=stop});Set("sendAt",0f);
            if(!reached)yield break;
            client.Send(new LanCommand{op="portal_enter",roomId=visit.roomId,roomSequence=oldSequence,portalId=portal.id});
            yield return Until(()=>Get<LanDungeonVisit>("dungeonVisit")?.roomSequence>oldSequence);
            if(!ready)yield break;
            ready=Get<LanDungeonVisit>("dungeonVisit").roomId==portal.targetRoomId
                && Get<MiniWar.Dungeons.DungeonRoom>("onlineRoom").id==portal.targetRoomId
                && Get<LanEvent>("snapshot").actors.Length==2;
        }
    }
}
#endif
