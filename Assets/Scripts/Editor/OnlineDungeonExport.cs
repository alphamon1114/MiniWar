using System;
using System.IO;
using System.Linq;
using System.Text;
using MiniWar.Dungeons;
using MiniWar.Online;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    // Keep authoring in its original location. The resource is only a reference + exported server geometry.
    public sealed class OnlineDungeonExport : AssetPostprocessor
    {
        public const string Source = "Assets/Data/RoomDungeons/GateOutskirtsRooms.asset";
        const string Folder = "Assets/Resources/Online/Dungeons";
        [MenuItem("MiniWar/Online/Export authored dungeon")]
        public static void Export()
        {
            var dungeon = AssetDatabase.LoadAssetAtPath<RoomDungeon>(Source);
            if (dungeon == null || dungeon.FindRoom(dungeon.startRoomId)?.layout == null)
                throw new InvalidOperationException("성문 외곽의 시작 방을 지정해주세요.");
            Directory.CreateDirectory(Folder);
            var map = new LanDungeonMap { id = LanDungeons.Gate, startRoomId = dungeon.startRoomId,
                traversalPreview = false, allowBacktracking = dungeon.allowBacktracking,
                rooms = dungeon.rooms.Where(r => r.layout != null).Select(r => {
                    var l = r.layout;
                    return new LanDungeonRoom { id = r.id, name = r.displayName,
                        boss = r.isBoss, enemies = DungeonCombatData.Room(l).enemies,
                        x = l.bounds.x, y = l.bounds.y, width = l.bounds.width, height = l.bounds.height,
                        spawnX = l.entrance.x, spawnY = l.entrance.y, jumpHeight = l.jumpHeight, airJumpHeight = l.airJumpHeight,
                        portals = r.portals.Select(p => {
                            var arrival = SafeArrival(l, p.arrival);
                            return new LanDungeonPortal { id = p.id, direction = (int)p.direction, targetRoomId = p.targetRoomId,
                                targetPortalId = p.targetPortalId, x = p.position.x, y = p.position.y, arrivalX = arrival.x, arrivalY = arrival.y };
                        }).ToArray(),
                        floors = l.floors.Select(f => new LanDungeonFloor { id = f.id,
                            x = f.rect.x, y = f.rect.y, width = f.rect.width, height = f.rect.height, oneWay = f.oneWay }).ToArray() };
                }).ToArray() };
            string json = JsonUtility.ToJson(map, true), path = Folder + "/GateOutskirts.json";
            if (!File.Exists(path) || File.ReadAllText(path) != json)
            { File.WriteAllText(path, json, new UTF8Encoding(false)); AssetDatabase.ImportAsset(path); }
            path = Folder + "/Bindings.asset";
            var binding = AssetDatabase.LoadAssetAtPath<OnlineDungeonAssets>(path);
            if (binding == null) { binding = ScriptableObject.CreateInstance<OnlineDungeonAssets>(); AssetDatabase.CreateAsset(binding, path); }
            if (binding.gateOutskirts != dungeon)
            { binding.gateOutskirts = dungeon; EditorUtility.SetDirty(binding); AssetDatabase.SaveAssetIfDirty(binding); }
        }
        static Vector2 SafeArrival(DungeonLayout layout, Vector2 point)
        {
            // Existing authoring may leave an arrival above empty air. Resolve the exported landing only.
            var size = new Vector2(.6f, 2.3f);
            if (layout.HasSupport(point) && layout.HasClearance(point, size)) return point;
            foreach (var floor in layout.floors.OrderBy(f => Mathf.Abs(f.rect.yMax - point.y)))
            {
                var candidate = new Vector2(point.x, floor.rect.yMax);
                if (layout.HasSupport(candidate) && layout.HasClearance(candidate, size)
                    && DungeonLayout.ContainsRect(layout.bounds, new Rect(candidate.x-.3f,candidate.y,.6f,2.3f))) return candidate;
            }
            return layout.entrance;
        }
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
        {
            if (Array.IndexOf(imported, Source) >= 0)
                EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Export(); };
        }
    }
}
