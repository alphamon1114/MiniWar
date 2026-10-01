using System;
using System.Collections.Generic;
using MiniWar.Data;
using UnityEngine;

namespace MiniWar.Dungeons
{
    [Serializable]
    public sealed class DungeonFloor
    {
        public string id = Guid.NewGuid().ToString("N");
        public Rect rect = new Rect(0, -1, 6, 1);
        public Sprite sprite;
        public Color color = new Color(.33f, .41f, .48f);
        public bool oneWay;
    }

    [Serializable]
    public sealed class DungeonSpawn
    {
        public string id = Guid.NewGuid().ToString("N");
        public EnemyData enemy;
        public Sprite sprite;
        public Vector2 position;
        [Min(1)] public int count = 1;
        [Min(.5f)] public float spacing = 1.5f;
        public bool faceLeft = true;
        public Vector2 Size => enemy == null ? new Vector2(.8f, 1.8f) : enemy.bodySize;
        public Sprite VisualSprite => sprite != null ? sprite : enemy == null ? null : enemy.idleSprite;
        public Vector2 VisualSize => VisualSprite == null ? Size
            : new Vector2(Size.y * VisualSprite.rect.width / VisualSprite.rect.height, Size.y);
        public Rect VisualRectAt(int index)
        {var p=PositionAt(index);var size=VisualSize;return new Rect(p.x-size.x/2,p.y,size.x,size.y);}
        public string Label => enemy == null ? "몬스터 미지정" : enemy.displayName;
        public Vector2 PositionAt(int index) => position + Vector2.right * (index * spacing);
    }

    [CreateAssetMenu(menuName = "MiniWar/Dungeon Layout", fileName = "DungeonLayout")]
    public sealed class DungeonLayout : ScriptableObject
    {
        public string displayName = "새 던전";
        [TextArea] public string description;
        public Rect bounds = new Rect(0, -4, 60, 16);
        [Min(.25f)] public float gridSize = .5f;
        [Range(.5f,6f)] public float jumpHeight = 4f / 3f;
        [Range(.5f,6f)] public float airJumpHeight = Online.LanRules.AirJumpHeight;
        public Sprite background;
        public Color backgroundColor = new Color(.09f, .13f, .18f);
        public Vector2 entrance = new Vector2(2, 0);
        public Vector2 exit = new Vector2(56, 0);
        public List<DungeonFloor> floors = new List<DungeonFloor>();
        public List<DungeonSpawn> spawns = new List<DungeonSpawn>();

        public Vector2 Snap(Vector2 point)
        {
            float grid = Mathf.Max(.25f, gridSize);
            return new Vector2(Mathf.Round(point.x / grid) * grid, Mathf.Round(point.y / grid) * grid);
        }

        public Vector2 SnapToFloor(Vector2 feet, float range = 2f)
        {
            float nearest = range + .001f, top = feet.y;
            foreach (var floor in floors)
            {
                if (feet.x < floor.rect.xMin || feet.x > floor.rect.xMax) continue;
                float distance = Mathf.Abs(feet.y - floor.rect.yMax);
                if (distance >= nearest) continue;
                nearest = distance; top = floor.rect.yMax;
            }
            return new Vector2(feet.x, top);
        }

        public bool HasSupport(Vector2 feet, float halfWidth = .3f)
        {
            foreach (var floor in floors)
                if (feet.x + halfWidth > floor.rect.xMin && feet.x - halfWidth < floor.rect.xMax
                    && Mathf.Abs(feet.y - floor.rect.yMax) < .06f) return true;
            return false;
        }

        public bool HasClearance(Vector2 feet, Vector2 size)
        {
            Rect body = new Rect(feet.x - size.x / 2 + .01f, feet.y + .02f, size.x - .02f, size.y - .03f);
            foreach (var floor in floors) if (!floor.oneWay && body.Overlaps(floor.rect)) return false;
            return true;
        }

        public List<string> ValidateLayout(bool checkExit = true)
        {
            var issues = new List<string>();
            if (bounds.width < 6 || bounds.height < 4) issues.Add("맵 범위는 가로 6, 세로 4 이상으로 정해주세요.");
            if (float.IsNaN(jumpHeight) || float.IsInfinity(jumpHeight) || jumpHeight < .5f || jumpHeight > 6f)
                issues.Add("최대 점프 높이는 0.5~6m 사이로 정해주세요.");
            if (float.IsNaN(airJumpHeight) || float.IsInfinity(airJumpHeight) || airJumpHeight < .5f || airJumpHeight > 6f)
                issues.Add("공중 점프 높이는 0.5~6m 사이로 정해주세요.");
            if (floors.Count == 0) issues.Add("바닥이 없습니다. 바닥 도구로 드래그해 만들어주세요.");
            CheckPoint("입구", entrance, new Vector2(.6f, 2.3f), true, issues);
            if(checkExit) CheckPoint("출구", exit, new Vector2(.6f, 2.3f), true, issues);
            var ids = new HashSet<string>();
            foreach (var floor in floors)
            {
                if (string.IsNullOrEmpty(floor.id) || !ids.Add(floor.id)) issues.Add("중복되거나 비어 있는 바닥 ID가 있습니다.");
                if (floor.rect.width <= 0 || floor.rect.height <= 0) issues.Add("크기가 0 이하인 바닥이 있습니다.");
                if (!ContainsRect(bounds, floor.rect)) issues.Add("맵 범위를 벗어난 바닥이 있습니다.");
            }
            foreach (var spawn in spawns)
            {
                if (string.IsNullOrEmpty(spawn.id) || !ids.Add(spawn.id)) issues.Add("중복되거나 비어 있는 몬스터 ID가 있습니다.");
                if (spawn.enemy == null) issues.Add("종류가 지정되지 않은 몬스터가 있습니다.");
                if (spawn.count < 1 || spawn.count > 30 || spawn.spacing < .5f) issues.Add(spawn.Label + ": 수량 1~30, 간격 0.5 이상이 필요합니다.");
                if (spawn.Size.x <= 0 || spawn.Size.y <= 0) issues.Add(spawn.Label + ": 몸체 크기는 0보다 커야 합니다.");
                for (int i = 0; i < Mathf.Clamp(spawn.count, 0, 30); i++)
                    CheckPoint(spawn.Label + " #" + (i + 1), spawn.PositionAt(i), spawn.Size,
                        spawn.enemy == null || spawn.enemy.locomotion == LocomotionKind.Ground, issues);
            }
            return issues;
        }

        void CheckPoint(string label, Vector2 feet, Vector2 size, bool ground, List<string> issues)
        {
            if (!ContainsRect(bounds, new Rect(feet.x - size.x / 2, feet.y, size.x, size.y))) issues.Add(label + ": 맵 범위를 벗어났습니다.");
            if (ground && !HasSupport(feet, size.x / 2)) issues.Add(label + ": 발밑에 바닥이 없습니다.");
            if (!HasClearance(feet, size)) issues.Add(label + ": 몸이 바닥이나 벽에 겹칩니다.");
        }

        public static bool ContainsRect(Rect outer, Rect inner) => inner.xMin >= outer.xMin && inner.xMax <= outer.xMax
            && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;
    }
}
