using System;
using System.IO;
using System.Linq;
using MiniWar.Data;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class RoomDungeonAuthoring
    {
        public const string ExamplePath="Assets/Data/RoomDungeons/GateOutskirtsRooms.asset";

        public static RoomDungeon Create(string path,string name)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.Refresh();
            var dungeon=ScriptableObject.CreateInstance<RoomDungeon>();dungeon.displayName=name;
            AssetDatabase.CreateAsset(dungeon,path);return dungeon;
        }

        public static DungeonRoom AddRoom(RoomDungeon dungeon,Vector2Int cell,string name,DungeonLayout source=null)
        {
            if(dungeon.rooms.Any(r=>r.cell==cell))throw new InvalidOperationException("이미 방이 있는 위치입니다.");
            Undo.RegisterCompleteObjectUndo(dungeon,"방 추가");
            var layout=source==null?ScriptableObject.CreateInstance<DungeonLayout>():UnityEngine.Object.Instantiate(source);
            layout.name=name+" 배치";layout.displayName=name;
            if(source==null)
            {
                layout.bounds=new Rect(0,-3,24,12);layout.entrance=new Vector2(4,0);layout.exit=new Vector2(20,0);
                layout.jumpHeight=3.5f;
                layout.floors.Add(new DungeonFloor {rect=new Rect(0,-2,24,2)});
                layout.background=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backdrop/BG_Castle.png");
                layout.spawns.Add(new DungeonSpawn {enemy=DefaultPatrol(),position=new Vector2(7,0),count=8,spacing=1.5f});
            }
            layout.hideFlags=HideFlags.HideInHierarchy;
            // Keep subassets when removing a room: Undo must retain its complete authored geometry.
            AssetDatabase.AddObjectToAsset(layout,dungeon);
            var room=new DungeonRoom {displayName=name,cell=cell,layout=layout};dungeon.rooms.Add(room);
            if(dungeon.rooms.Count==1)dungeon.startRoomId=room.id;
            EditorUtility.SetDirty(layout);EditorUtility.SetDirty(dungeon);return room;
        }

        public static DungeonRoom AddNeighbor(RoomDungeon dungeon,DungeonRoom from,PortalDirection direction)
        {
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            var cell=from.cell+RoomPortal.Offset(direction);
            var next=dungeon.rooms.Find(r=>r.cell==cell)??AddRoom(dungeon,cell,"방 "+(dungeon.rooms.Count+1));
            Connect(dungeon,from,next,direction);Undo.CollapseUndoOperations(group);return next;
        }

        public static void Connect(RoomDungeon dungeon,DungeonRoom from,DungeonRoom to,PortalDirection direction)
        {
            if(to.cell!=from.cell+RoomPortal.Offset(direction))throw new InvalidOperationException("해당 방향의 이웃 방만 연결할 수 있습니다.");
            var reverse=RoomPortal.Opposite(direction);
            var existing=from.portals.Find(p=>p.direction==direction);
            if(existing!=null&&existing.targetRoomId==to.id)return;
            if(existing!=null||to.portals.Any(p=>p.direction==reverse))throw new InvalidOperationException("이미 사용 중인 포탈 방향입니다.");
            Undo.RegisterCompleteObjectUndo(dungeon,"방 연결");
            var a=MakePortal(from,direction);var b=MakePortal(to,reverse);
            a.targetRoomId=to.id;a.targetPortalId=b.id;b.targetRoomId=from.id;b.targetPortalId=a.id;
            from.portals.Add(a);to.portals.Add(b);EditorUtility.SetDirty(dungeon);
        }

        static RoomPortal MakePortal(DungeonRoom room,PortalDirection direction)
        {
            var bounds=room.layout.bounds;
            float x=direction==PortalDirection.Left?bounds.xMin+1.5f:direction==PortalDirection.Right?bounds.xMax-1.5f:
                direction==PortalDirection.Up?bounds.center.x:bounds.center.x-5;
            var position=room.layout.SnapToFloor(new Vector2(x,0),100);
            float offset=direction==PortalDirection.Right?-2.5f:direction==PortalDirection.Left?2.5f:2;
            return new RoomPortal {direction=direction,position=position,arrival=room.layout.SnapToFloor(position+Vector2.right*offset,100)};
        }

        public static void Disconnect(RoomDungeon dungeon,DungeonRoom from,RoomPortal portal)
        {
            Undo.RegisterCompleteObjectUndo(dungeon,"포탈 연결 해제");
            dungeon.FindRoom(portal.targetRoomId)?.portals.RemoveAll(p=>p.id==portal.targetPortalId);
            from.portals.Remove(portal);EditorUtility.SetDirty(dungeon);
        }

        public static bool DirectionBetween(Vector2Int from,Vector2Int to,out PortalDirection direction)
        {
            Vector2Int delta=to-from;
            direction=delta==Vector2Int.left?PortalDirection.Left:delta==Vector2Int.right?PortalDirection.Right:
                delta==Vector2Int.up?PortalDirection.Up:PortalDirection.Down;
            return Mathf.Abs(delta.x)+Mathf.Abs(delta.y)==1;
        }

        public static bool MoveRoom(RoomDungeon dungeon,DungeonRoom room,Vector2Int cell,out int removedLinks)
        {
            removedLinks=0;
            if(room.cell==cell)return true;
            if(dungeon.rooms.Any(r=>r!=room&&r.cell==cell))return false;
            Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(dungeon,"방 위치 이동");
            room.cell=cell;
            foreach(var portal in room.portals.ToArray())
            {
                var target=dungeon.FindRoom(portal.targetRoomId);
                var back=target?.portals.Find(p=>p.id==portal.targetPortalId);
                if(target!=null&&back!=null&&DirectionBetween(cell,target.cell,out var direction))
                {portal.direction=direction;back.direction=RoomPortal.Opposite(direction);}
                else
                {
                    target?.portals.RemoveAll(p=>p.id==portal.targetPortalId);room.portals.Remove(portal);removedLinks++;
                }
            }
            EditorUtility.SetDirty(dungeon);return true;
        }

        public static void RemoveRoom(RoomDungeon dungeon,DungeonRoom room)
        {
            if(dungeon.rooms.Count<=1)return;
            Undo.RegisterCompleteObjectUndo(dungeon,"방 삭제");
            foreach(var other in dungeon.rooms)other.portals.RemoveAll(p=>p.targetRoomId==room.id);
            dungeon.rooms.Remove(room);
            if(dungeon.startRoomId==room.id)dungeon.startRoomId=dungeon.rooms[0].id;
            EditorUtility.SetDirty(dungeon);
        }

        public static RoomDungeon CreateExample()
        {
            var existing=AssetDatabase.LoadAssetAtPath<RoomDungeon>(ExamplePath);if(existing!=null)return existing;
            var dungeon=Create(ExamplePath,"성문 외곽 · 탈환 작전");
            var start=AddRoom(dungeon,new Vector2Int(0,0),"침투 지점");
            var court=AddRoom(dungeon,new Vector2Int(1,0),"무너진 광장");
            var gate=AddRoom(dungeon,new Vector2Int(2,0),"성문 진입로");
            var boss=AddRoom(dungeon,new Vector2Int(2,1),"성문 대포");boss.isBoss=true;
            var supply=AddRoom(dungeon,new Vector2Int(1,-1),"보급 창고");
            var bypass=AddRoom(dungeon,new Vector2Int(2,-1),"성벽 우회로");
            Connect(dungeon,start,court,PortalDirection.Right);
            Connect(dungeon,gate,boss,PortalDirection.Up);Connect(dungeon,court,supply,PortalDirection.Down);
            Connect(dungeon,supply,bypass,PortalDirection.Right);Connect(dungeon,bypass,gate,PortalDirection.Up);
            foreach(var room in dungeon.rooms)
            {
                room.layout.description="방의 몬스터를 처치한 뒤, 같은 방향 포탈에 파티원이 모이면 이동합니다.";
                PopulateExampleRoom(room);
                EditorUtility.SetDirty(room.layout);
            }
            Save(dungeon);return dungeon;
        }

        static EnemyData DefaultPatrol()=>AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/DungeonLayouts/Enemies/Patrol.asset")
            ??DungeonBuilderAssets.CreateGateExample().spawns[0].enemy;

        public static void PopulateExampleRoom(DungeonRoom room)
        {
            var old=DungeonBuilderAssets.CreateGateExample();
            var patrol=DefaultPatrol();var guard=old.spawns[1].enemy;var cannon=old.spawns.Last().enemy;
            var layout=room.layout;layout.spawns.Clear();
            if(room.isBoss)
            {
                layout.spawns.Add(new DungeonSpawn {enemy=cannon,position=new Vector2(19,0)});
                layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(7,0),count=7,spacing=1.25f});
            }
            else if(room.displayName=="성문 진입로"||room.displayName=="보급 창고")
            {
                layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(7,0),count=4,spacing=1.5f});
                layout.spawns.Add(new DungeonSpawn {enemy=guard,position=new Vector2(14,0),count=4,spacing=1.5f});
            }
            else layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(7,0),count=8,spacing=1.5f});
            EditorUtility.SetDirty(layout);
        }

        public static void Save(RoomDungeon dungeon)
        {
            if(dungeon==null)return;
            foreach(var room in dungeon.rooms)if(room.layout!=null)
            {room.layout.name=room.displayName+" 배치";room.layout.displayName=room.displayName;EditorUtility.SetDirty(room.layout);}
            EditorUtility.SetDirty(dungeon);AssetDatabase.SaveAssetIfDirty(dungeon);
        }

        public static RoomDungeon Copy(RoomDungeon original,string path)
        {
            var copy=Create(path,original.displayName+" (복사)");copy.allowBacktracking=original.allowBacktracking;
            // Preserve per-room IDs inside this independent asset so all portal references remain valid.
            foreach(var source in original.rooms)
            {
                var room=AddRoom(copy,source.cell,source.displayName,source.layout);
                room.id=source.id;room.isBoss=source.isBoss;
                room.portals=source.portals.Select(p=>JsonUtility.FromJson<RoomPortal>(JsonUtility.ToJson(p))).ToList();
            }
            copy.startRoomId=original.startRoomId;Save(copy);return copy;
        }
    }
}
