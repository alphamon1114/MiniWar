using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniWar.Dungeons
{
    public sealed partial class DungeonSandbox
    {
        public RoomDungeon dungeon;
        public string initialRoom;
        [Range(1,6)] public int partySize=4;
        public DungeonRoomRun RoomRun {get;private set;}
        public IReadOnlyList<DungeonPartyPresence> Party=>party;
        readonly List<DungeonPartyPresence> party=new List<DungeonPartyPresence>();
        readonly Dictionary<string,List<SpriteRenderer>> portalVisuals=new Dictionary<string,List<SpriteRenderer>>();
        readonly List<Transform> companionVisuals=new List<Transform>();

        void InitializeRoomRun()
        {
            if(dungeon==null)return;
            for(int i=0;i<Mathf.Clamp(partySize,1,6);i++)party.Add(new DungeonPartyPresence {id="P"+(i+1)});
            RoomRun=new DungeonRoomRun(dungeon,party.Select(p=>p.id),string.IsNullOrEmpty(initialRoom)?null:initialRoom);
            layout=RoomRun.Current.layout;
        }

        void BuildRoomPortals(Vector2 arrival)
        {
            portalVisuals.Clear();companionVisuals.Clear();
            foreach(var portal in RoomRun.Current.portals)
                portalVisuals[portal.id]=Portal(portal.position,new Color(.77f,.32f,.34f),RoomPortal.Label(portal.direction));
            for(int i=0;i<party.Count;i++)
            {
                party[i].feet=arrival;
                if(i==0)continue;
                var marker=Box("Simulated party member "+party[i].id,new Rect(arrival.x-.18f,arrival.y,.36f,1.2f),new Color(.35f,.78f,.94f,.4f),2);
                companionVisuals.Add(marker.transform);
            }
            RefreshPortalColors();
        }

        void RefreshPortalColors()
        {
            foreach(var portal in RoomRun.Current.portals)
            {
                Color color=RoomRun.CanUse(portal)?new Color(.33f,.88f,.72f):new Color(.78f,.32f,.35f);
                if(RoomRun.CountingPortal==portal.id)color=new Color(1,.78f,.3f);
                if(portalVisuals.TryGetValue(portal.id,out var renderers))foreach(var renderer in renderers)renderer.color=color;
            }
        }

        public void ClearRoomForPreview()
        {
            if(RoomRun==null)return;
            RoomRun.ClearCurrentForPreview();
            currentCombat?.ClearForPreview();
            foreach(var go in enemyVisuals)if(go!=null)go.SetActive(false);
            RefreshPortalColors();
        }

        public void GatherCompanionsForPreview()
        {
            if(RoomRun==null||Motor==null)return;
            var portal=RoomRun.Current.portals.Find(p=>p.Contains(Motor.Position));
            if(portal==null)return;
            for(int i=1;i<party.Count;i++)
            {
                party[i].feet=portal.position;
                if(RoomRun.EnteredPortal(party[i].id)!=portal.id)RoomRun.TogglePortal(party[i].id,portal.id,party);
            }
        }

        void UpdatePartyMarkers()
        {
            for(int i=1;i<party.Count&&i-1<companionVisuals.Count;i++)
            {
                companionVisuals[i-1].gameObject.SetActive(!party[i].spectator&&RoomRun.EnteredPortal(party[i].id)==null);
                companionVisuals[i-1].position=party[i].feet+Vector2.up*.6f+Vector2.right*((i-2)*.23f);
            }
        }

        void UpdateRoomFlow()
        {
            party[0].feet=Motor.Position;
            if(RoomRun.TryTransition(party,out var arrival,Time.fixedDeltaTime))
            {
                move=0;jump=dash=drop=false;BuildWorld(arrival.arrival);
            }
            RefreshPortalColors();
        }
        bool WaitingInPortal => RoomRun!=null&&RoomRun.EnteredPortal("P1")!=null;
        void ToggleLocalPortal()
        {
            if(RoomRun==null||Motor==null||!Motor.Grounded||Motor.Dashing)return;
            party[0].feet=Motor.Position;
            var portal=RoomRun.Current.portals.Find(p=>p.Contains(Motor.Position));
            if(portal!=null&&RoomRun.TogglePortal("P1",portal.id,party)) {move=0;jump=dash=drop=false;}
        }

        void DrawRoomHUD()
        {
            GUI.Box(new Rect(14,14,550,113),GUIContent.none);
            GUI.Label(new Rect(30,20,520,29),RoomRun.Current.displayName+" · 전투·방 연결 테스트",heading);
            GUI.Label(new Rect(25,50,525,24),"A/D 이동  Space ×2 점프  ↓+Space 하강  Shift 대쉬",label);
            GUI.Label(new Rect(25,75,525,24),"S 포탈 진입/나가기 · F6 클리어 · G 모의 동료 진입",label);
            GUI.Label(new Rect(25,99,525,23),"모의 "+party.Count+"인 · 과반수 진입 5초 / 생존자 전원 즉시 이동",label);
            DrawLivingEnemyNames();
            foreach(var portal in RoomRun.Current.portals)
            {
                string target=dungeon.FindRoom(portal.targetRoomId)?.displayName??"?";
                DrawLabel(portal.position+Vector2.up*3.15f,RoomPortal.Label(portal.direction)+" · "+target+" [S] "+RoomRun.EnteredCount(portal.id)+"/"+party.Count(p=>!p.spectator&&p.connected));
            }
            if(party.Count>1)DrawLabel(party[1].feet+Vector2.up*3.25f,"모의 동료 "+(party.Count-1)+"명");
            string state=RoomRun.Completed?"던전 클리어!":!RoomRun.Cleared?"몬스터 "+RoomRun.Remaining+"마리 남음 · 포탈 잠김":"방 클리어 · 이동할 포탈에 모여주세요";
            var current=RoomRun.Current.portals.Find(p=>p.Contains(Motor.Position));
            if(current!=null&&RoomRun.CanUse(current))state=WaitingInPortal?"포탈 안에서 대기 중 · S 나가기":"S 포탈 진입 · G 모의 동료 진입";
            if(RoomRun.CountingPortal!=null)state=Mathf.CeilToInt(RoomRun.Countdown)+"초 후 이동 · 생존자 전원이 들어오면 즉시 출발";
            GUI.Box(new Rect(14,Screen.height-53,620,38),GUIContent.none);GUI.Label(new Rect(20,Screen.height-51,605,34),state,label);
            DrawMinimap();
            if(RoomRun.Completed)
            {
                GUI.Box(new Rect(Screen.width/2-220,Screen.height/2-50,440,100),GUIContent.none);
                GUI.Label(new Rect(Screen.width/2-210,Screen.height/2-35,420,35),"DUNGEON CLEAR",heading);
                GUI.Label(new Rect(Screen.width/2-210,Screen.height/2+5,420,30),"보스 방 클리어 · 보상 지급 없는 전투 테스트",label);
            }
        }

        void DrawMinimap()
        {
            var rect=new Rect(Screen.width-280,14,266,190);Fill(rect,new Color(.04f,.07f,.1f,.94f));
            GUI.Label(new Rect(rect.x+5,rect.y+4,rect.width-10,24),dungeon.displayName,label);
            float minX=dungeon.rooms.Min(r=>r.cell.x),maxX=dungeon.rooms.Max(r=>r.cell.x),minY=dungeon.rooms.Min(r=>r.cell.y),maxY=dungeon.rooms.Max(r=>r.cell.y);
            float size=Mathf.Min(55,Mathf.Min(210/(maxX-minX+1),130/(maxY-minY+1)));
            Vector2 center=new Vector2(rect.center.x,rect.y+112);
            Vector2 Point(DungeonRoom r)=>center+new Vector2(r.cell.x-(minX+maxX)/2,-r.cell.y+(minY+maxY)/2)*size;
            foreach(var room in dungeon.rooms)foreach(var p in room.portals)
            {
                var target=dungeon.FindRoom(p.targetRoomId);if(target==null||string.CompareOrdinal(room.id,target.id)>0)continue;
                Vector2 a=Point(room),b=Point(target);
                Fill(new Rect(Mathf.Min(a.x,b.x)-2,Mathf.Min(a.y,b.y)-2,Mathf.Abs(a.x-b.x)+4,Mathf.Abs(a.y-b.y)+4),new Color(.4f,.49f,.5f));
            }
            foreach(var room in dungeon.rooms)
            {
                var p=Point(room);Color color=room==RoomRun.Current?new Color(.95f,.75f,.28f):RoomRun.IsCleared(room.id)?new Color(.25f,.68f,.53f):room.isBoss?new Color(.72f,.28f,.32f):new Color(.3f,.38f,.46f);
                float side=size*.55f;Fill(new Rect(p.x-side/2,p.y-side/2,side,side),color);
                if(room.isBoss)GUI.Label(new Rect(p.x-14,p.y-13,28,26),"B",label);
                else if(room.id==dungeon.startRoomId)GUI.Label(new Rect(p.x-14,p.y-13,28,26),"S",label);
            }
        }

        static void Fill(Rect rect,Color color)
        {var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    }
}
