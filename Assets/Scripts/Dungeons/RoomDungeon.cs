using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MiniWar.Online;

namespace MiniWar.Dungeons
{
    public enum PortalDirection { Left, Right, Up, Down }

    [Serializable]
    public sealed class RoomPortal
    {
        public string id = Guid.NewGuid().ToString("N");
        public PortalDirection direction;
        public string targetRoomId, targetPortalId;
        public Vector2 position, arrival;
        public bool Contains(Vector2 feet) => Mathf.Abs(feet.x-position.x)<=.7f && Mathf.Abs(feet.y-position.y)<=.45f;
        public static PortalDirection Opposite(PortalDirection d) => d==PortalDirection.Left?PortalDirection.Right:
            d==PortalDirection.Right?PortalDirection.Left:d==PortalDirection.Up?PortalDirection.Down:PortalDirection.Up;
        public static Vector2Int Offset(PortalDirection d) => d==PortalDirection.Left?Vector2Int.left:
            d==PortalDirection.Right?Vector2Int.right:d==PortalDirection.Up?Vector2Int.up:Vector2Int.down;
        public static string Label(PortalDirection d) => d==PortalDirection.Left?"← 왼쪽":d==PortalDirection.Right?"오른쪽 →":d==PortalDirection.Up?"↑ 위":"↓ 아래";
    }

    [Serializable]
    public sealed class DungeonRoom
    {
        public string id = Guid.NewGuid().ToString("N");
        public string displayName = "새 방";
        public Vector2Int cell;
        public bool isBoss;
        public DungeonLayout layout;
        public List<RoomPortal> portals = new List<RoomPortal>();
    }

    [CreateAssetMenu(menuName="MiniWar/Room Dungeon",fileName="RoomDungeon")]
    public sealed class RoomDungeon : ScriptableObject
    {
        public string displayName = "새 던전";
        public string startRoomId;
        public bool allowBacktracking = true;
        public List<DungeonRoom> rooms = new List<DungeonRoom>();
        public DungeonRoom FindRoom(string id) => rooms.Find(r=>r.id==id);

        public List<string> ValidateDungeon()
        {
            var errors=new List<string>();
            if(rooms.Count==0) {errors.Add("방이 없습니다.");return errors;}
            if(FindRoom(startRoomId)==null)errors.Add("시작 방을 지정해주세요.");
            if(rooms.Count(r=>r.isBoss)!=1)errors.Add("보스 방은 하나를 지정해주세요.");
            var ids=new HashSet<string>();var cells=new HashSet<Vector2Int>();
            foreach(var room in rooms)
            {
                if(string.IsNullOrEmpty(room.id)||!ids.Add(room.id))errors.Add("방 ID가 중복되거나 비어 있습니다.");
                if(!cells.Add(room.cell))errors.Add(room.displayName+": 지도에서 방이 겹칩니다.");
                if(room.layout==null){errors.Add(room.displayName+": 내부 배치가 없습니다.");continue;}
                if(room.layout.spawns.Sum(s=>s.count)<8)errors.Add(room.displayName+": 몬스터를 최소 8마리 배치해주세요 (보스 포함).");
                foreach(var error in room.layout.ValidateLayout(false)) errors.Add(room.displayName+": "+error);
                var dirs=new HashSet<PortalDirection>();
                foreach(var portal in room.portals)
                {
                    if(string.IsNullOrEmpty(portal.id)||!ids.Add(portal.id))errors.Add(room.displayName+": 포탈 ID가 중복되었습니다.");
                    if(!dirs.Add(portal.direction))errors.Add(room.displayName+": 같은 방향에 포탈이 두 개 있습니다.");
                    var target=FindRoom(portal.targetRoomId);
                    var back=target?.portals.Find(p=>p.id==portal.targetPortalId);
                    if(target==room||target==null||back==null||back.targetRoomId!=room.id||back.targetPortalId!=portal.id||back.direction!=RoomPortal.Opposite(portal.direction))
                        errors.Add(room.displayName+": 포탈의 왕복 연결이 끊어졌습니다.");
                    if(target!=null && target.cell!=room.cell+RoomPortal.Offset(portal.direction))errors.Add(room.displayName+": 포탈 방향과 방 지도 위치가 다릅니다.");
                    CheckFeet(room,portal.position,"포탈",errors);CheckFeet(room,portal.arrival,"도착 위치",errors);
                    if(room.portals.Any(p=>p.Contains(portal.arrival)))errors.Add(room.displayName+": 도착 위치를 모든 포탈 밖으로 옮겨주세요.");
                    if(room.portals.Any(p=>p!=portal && Mathf.Abs(p.position.x-portal.position.x)<1.4f && Mathf.Abs(p.position.y-portal.position.y)<.9f))
                        errors.Add(room.displayName+": 포탈 진입 영역이 겹칩니다.");
                }
            }
            var reachable=ReachableRooms();
            foreach(var room in rooms)if(room.isBoss&&!reachable.Contains(room.id))errors.Add(room.displayName+": 시작 방에서 보스 방으로 갈 수 없습니다.");
            return errors;
        }

        public HashSet<string> ReachableRooms()
        {
            var reachable=new HashSet<string>();var pending=new Queue<string>();pending.Enqueue(startRoomId??"");
            while(pending.Count>0)
            {
                var room=FindRoom(pending.Dequeue());if(room==null||!reachable.Add(room.id))continue;
                foreach(var p in room.portals)pending.Enqueue(p.targetRoomId??"");
            }
            return reachable;
        }

        public List<string> ConnectionWarnings()
        {
            var reachable=ReachableRooms();
            return rooms.Where(r=>!r.isBoss&&!reachable.Contains(r.id))
                .Select(r=>r.displayName+": 미연결 보관 방입니다. 저장·편집할 수 있으며 현재 경로에서는 방문하지 않습니다.").ToList();
        }

        static void CheckFeet(DungeonRoom room,Vector2 p,string label,List<string> errors)
        {
            var layout=room.layout;
            if(!DungeonLayout.ContainsRect(layout.bounds,new Rect(p.x-.3f,p.y,.6f,2.3f))||!layout.HasSupport(p)||!layout.HasClearance(p,new Vector2(.6f,2.3f)))
                errors.Add(room.displayName+": "+label+"에 캐릭터가 설 수 없습니다.");
        }
    }

    // Positions and spectator/connection state must come from the authoritative simulation when wired online.
    public sealed class DungeonPartyPresence
    {
        public string id;
        public Vector2 feet;
        public bool spectator;
        public bool connected = true;
    }

    public sealed class DungeonRoomRun
    {
        readonly RoomDungeon dungeon;
        readonly HashSet<string> roster,visited=new HashSet<string>();
        readonly Dictionary<string,HashSet<string>> defeated=new Dictionary<string,HashSet<string>>();
        readonly LanPortalRally rally = new LanPortalRally();
        double clock;
        public string CountingPortal => rally.CountingPortal;
        public float Countdown => rally.Remaining(clock);
        public string EnteredPortal(string id) => rally.Entered(id);
        public int EnteredCount(string portalId) => rally.Count(portalId);
        public DungeonRoom Current {get;private set;}
        public bool Completed {get;private set;}
        public bool Cleared => Remaining==0;
        public int Remaining => EnemyIds(Current).Count(id=>!defeated[Current.id].Contains(id));

        public DungeonRoomRun(RoomDungeon dungeon,IEnumerable<string> memberIds,string initialRoom=null)
        {
            this.dungeon=dungeon;
            var members=memberIds?.ToArray()??throw new ArgumentException("A party roster is required.");
            roster=new HashSet<string>(members);
            if(roster.Count==0||roster.Count!=members.Length||roster.Any(string.IsNullOrEmpty))throw new ArgumentException("Unique party members are required.");
            Current=dungeon.FindRoom(initialRoom??dungeon.startRoomId)??throw new ArgumentException("Invalid starting room.");
            foreach(var room in dungeon.rooms)defeated[room.id]=new HashSet<string>();
            visited.Add(Current.id);UpdateCompletion();
        }
        static IEnumerable<string> EnemyIds(DungeonRoom room)
        {
            foreach(var spawn in room.layout.spawns)
                for(int i=0;i<Mathf.Clamp(spawn.count,0,30);i++)yield return spawn.id+":"+i;
        }
        public bool HasVisited(string roomId)=>visited.Contains(roomId);
        public bool IsCleared(string roomId)
        {
            var room=dungeon.FindRoom(roomId);
            return room!=null&&visited.Contains(roomId)&&EnemyIds(room).All(id=>defeated[roomId].Contains(id));
        }
        public bool Defeat(string spawnId,int index)
        {
            string id=spawnId+":"+index;
            if(!EnemyIds(Current).Contains(id))return false;
            bool added=defeated[Current.id].Add(id);UpdateCompletion();return added;
        }
        public void ClearCurrentForPreview()
        { foreach(string id in EnemyIds(Current))defeated[Current.id].Add(id);UpdateCompletion(); }
        void UpdateCompletion() {if(Current.isBoss&&Cleared)Completed=true;}
        public bool CanUse(RoomPortal portal) => !Completed&&Cleared&&Current.portals.Contains(portal)
            &&(dungeon.allowBacktracking||!visited.Contains(portal.targetRoomId));

        bool ValidParty(IReadOnlyList<DungeonPartyPresence> party)
        {
            if(party==null||party.Count!=roster.Count)return false;
            var seen=new HashSet<string>();
            foreach(var member in party)
                if(member==null||!roster.Contains(member.id)||!seen.Add(member.id)
                    ||float.IsNaN(member.feet.x)||float.IsNaN(member.feet.y)||float.IsInfinity(member.feet.x)||float.IsInfinity(member.feet.y))return false;
            return true;
        }
        public bool TogglePortal(string memberId,string portalId,IReadOnlyList<DungeonPartyPresence> party)
        {
            if(!ValidParty(party))return false;
            var member=party.FirstOrDefault(p=>p.id==memberId);
            var portal=Current.portals.Find(p=>p.id==portalId);
            if(member==null||member.spectator||!member.connected||portal==null||!CanUse(portal)||!portal.Contains(member.feet))return false;
            if(rally.Entered(memberId)!=null&&rally.Entered(memberId)!=portalId)return false;
            rally.Toggle(memberId,portalId);return true;
        }
        public bool TryTransition(IReadOnlyList<DungeonPartyPresence> party,out RoomPortal arrivalPortal,float elapsed=0)
        {
            arrivalPortal=null;
            if(Completed||!ValidParty(party)||float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0)
            {rally.Reset();return false;}
            clock+=elapsed;
            foreach(var member in party)
            {
                var portal=Current.portals.Find(p=>p.id==rally.Entered(member.id));
                if(portal!=null&&!portal.Contains(member.feet))rally.Remove(member.id);
            }
            string chosen=rally.Evaluate(party.Select(p=>new PortalVoter{Id=p.id,Alive=!p.spectator,Connected=p.connected}).ToArray(),
                new HashSet<string>(Current.portals.Where(CanUse).Select(p=>p.id)),clock);
            if(chosen==null)return false;
            var exit=Current.portals.Find(p=>p.id==chosen);
            var next=dungeon.FindRoom(exit.targetRoomId);
            var arrival=next?.portals.Find(p=>p.id==exit.targetPortalId);
            if(arrival==null){rally.Reset();return false;}
            Current=next;visited.Add(next.id);rally.Reset();arrivalPortal=arrival;UpdateCompletion();return true;
        }
    }
}
