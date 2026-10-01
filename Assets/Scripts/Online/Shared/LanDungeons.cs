#nullable disable
using System;

namespace MiniWar.Online
{
    public sealed class LanDungeonInfo
    {
        public readonly string Id, Name, Region, Description, Boss;
        public readonly int Capacity, Minimum;
        public readonly bool Available;
        public LanDungeonInfo(string id, string name, string region, string description, string boss, bool available = false, int capacity = 4, int minimum = 1)
        { Id = id; Name = name; Region = region; Description = description; Boss = boss; Available = available; Capacity = capacity; Minimum = minimum; }
    }

    public static class LanDungeons
    {
        public const string Gate = "gate_outskirts";
        public static readonly LanDungeonInfo[] All = {
            new LanDungeonInfo(Gate, "성문 외곽", "왕국 탈환 · 첫 번째 전선", "무너진 성벽을 넘어 침공군이 점령한 성문을 되찾으세요.", "성문 대포", true),
            new LanDungeonInfo("market", "상점가 부근", "왕국 탈환 · 두 번째 전선", "잿더미가 된 시장 골목. 단검병과 석궁병의 경계를 돌파하세요.", "경비대장"),
            new LanDungeonInfo("palace", "왕궁 진입로", "왕국 탈환 · 세 번째 전선", "흑마법에 물든 왕궁으로 진격해 적국의 왕과 맞서세요.", "아라곤"),
            new LanDungeonInfo("temple", "지하 신전", "왕국 탈환 · 네 번째 전선", "왕궁 아래 숨겨진 신전. 마족을 부르는 검은 수정을 파괴하세요.", "검은 수정"),
            new LanDungeonInfo("demon_raid", "마족 레이드", "왕국 탈환 · 최후의 결전", "차원을 넘어온 마족. 동료들과 힘을 모아 왕국의 마지막 위협을 막으세요.", "차원을 넘어온 마족", false, 6, 4)
        };
        public static LanDungeonInfo Find(string id) => Array.Find(All, d => d.Id == id);
    }

    // Exported from the authored Unity asset. The server uses this geometry, never client positions.
    [Serializable] public sealed class LanDungeonMap
    {
        public string id, startRoomId;
        public bool traversalPreview;
        public bool allowBacktracking = true;
        public LanDungeonRoom[] rooms = Array.Empty<LanDungeonRoom>();
    }
    [Serializable] public sealed class LanDungeonRoom
    {
        public string id, name;
        public bool boss;
        public LanEnemySpawn[] enemies = Array.Empty<LanEnemySpawn>();
        public float x, y, width, height, spawnX, spawnY, jumpHeight, airJumpHeight;
        public LanDungeonFloor[] floors = Array.Empty<LanDungeonFloor>();
        public LanDungeonPortal[] portals = Array.Empty<LanDungeonPortal>();
    }
    [Serializable] public sealed class LanDungeonPortal
    {
        public string id, targetRoomId, targetPortalId;
        public int direction;
        public float x, y, arrivalX, arrivalY;
        public bool Contains(float feetX, float feetY) => Math.Abs(feetX - x) <= .7f && Math.Abs(feetY - y) <= .45f;
    }
    [Serializable] public sealed class LanDungeonFloor
    {
        public string id;
        public float x, y, width, height;
        public bool oneWay;
    }
    [Serializable] public sealed class LanDungeonVisit
    {
        public string instanceId, dungeonId, roomId, roomName, revision;
        public float power;
        public long roomSequence;
        public bool traversalPreview, portalsOpen;
        public string countingPortalId;
        public float countdown;
        public int aliveCount;
        public int enemiesRemaining;
        public bool completed;
        public LanEnemyState[] enemies = Array.Empty<LanEnemyState>();
        public LanCombatProjectile[] projectiles = Array.Empty<LanCombatProjectile>();
        public LanPortalWaiting[] portals = Array.Empty<LanPortalWaiting>();
        public string[] visitedRooms = Array.Empty<string>();
    }
    [Serializable] public sealed class LanPortalWaiting
    {
        public string id, targetName;
        public int direction, entered;
        public bool open;
    }
}
