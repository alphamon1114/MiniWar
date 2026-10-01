using System;
using System.Linq;
using MiniWar.Data;
using MiniWar.Dungeons;
using MiniWar.Online;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    // Authoring availability is independent of the online combat/release status.
    public static class DungeonCampaignAuthoring
    {
        static readonly string[] Files = { "GateOutskirtsRooms", "MarketRooms", "PalaceApproachRooms", "UndergroundTempleRooms", "DemonRaidRooms" };
        static string LastKey => "MiniWar.DungeonAuthoring.Last." + Application.dataPath;
        public static string PathFor(int index) => "Assets/Data/RoomDungeons/" + Files[index] + ".asset";
        public static int IndexOf(RoomDungeon dungeon) => Array.FindIndex(Files, f => AssetDatabase.GetAssetPath(dungeon) == "Assets/Data/RoomDungeons/" + f + ".asset");
        public static void Remember(RoomDungeon dungeon)
        {
            string path=AssetDatabase.GetAssetPath(dungeon);
            if(!string.IsNullOrEmpty(path))EditorPrefs.SetString(LastKey,path);
        }
        public static RoomDungeon Last()
        {
            var saved=AssetDatabase.LoadAssetAtPath<RoomDungeon>(EditorPrefs.GetString(LastKey,""));
            return saved!=null?saved:GetOrCreate(0);
        }
        public static void SavePending(RoomDungeon dungeon)
        {
            if(dungeon==null)return;
            foreach(var room in dungeon.rooms)if(room.layout!=null)AssetDatabase.SaveAssetIfDirty(room.layout);
            AssetDatabase.SaveAssetIfDirty(dungeon);
        }

        public static void Picker(RoomDungeon current,Action<RoomDungeon> select,float width=210)
        {
            var paths=Enumerable.Range(0,Files.Length).Select(PathFor).ToList();
            var labels=LanDungeons.All.Select(d=>d.Name).ToList();
            foreach(string path in AssetDatabase.FindAssets("t:RoomDungeon").Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p))
            {
                if(paths.Contains(path)||System.IO.Path.GetFileName(path).StartsWith("__"))continue;
                var extra=AssetDatabase.LoadAssetAtPath<RoomDungeon>(path);if(extra==null)continue;
                paths.Add(path);labels.Add(extra.displayName+" ("+extra.name+")");
            }
            string currentPath=AssetDatabase.GetAssetPath(current);
            int index=paths.IndexOf(currentPath);
            GUILayout.Label("편집할 던전",GUILayout.Width(76));
            int next=EditorGUILayout.Popup(index,labels.ToArray(),GUILayout.Width(width));
            if(next<0||next==index)return;
            SavePending(current);
            var chosen=next<Files.Length?GetOrCreate(next):AssetDatabase.LoadAssetAtPath<RoomDungeon>(paths[next]);
            Remember(chosen);select(chosen);GUIUtility.ExitGUI();
        }

        public static RoomDungeon GetOrCreate(int index)
        {
            if(index<0||index>=Files.Length)throw new ArgumentOutOfRangeException(nameof(index));
            var existing=AssetDatabase.LoadAssetAtPath<RoomDungeon>(PathFor(index));
            if(existing!=null)return existing; // Never regenerate an authored map.
            if(index==0)return RoomDungeonAuthoring.CreateExample();
            var info=LanDungeons.All[index];
            var dungeon=RoomDungeonAuthoring.Create(PathFor(index),info.Name);
            var start=RoomDungeonAuthoring.AddRoom(dungeon,Vector2Int.zero,info.Name+" 입구");
            var boss=RoomDungeonAuthoring.AddRoom(dungeon,Vector2Int.right,info.Boss);boss.isBoss=true;
            var palette=index==1?new[]{"DaggerGuard","CrossbowGuard"}:new[]{"DarkMage","CrossbowGuard","DaggerGuard"};
            var bossEnemy=BossMarker(index);
            foreach(var room in dungeon.rooms)
            {
                var layout=room.layout;
                layout.bounds=new Rect(-6,-6,36,18);layout.entrance=new Vector2(-2,0);layout.exit=new Vector2(26,0);
                layout.jumpHeight=4f/3f;layout.airJumpHeight=LanRules.AirJumpHeight;
                layout.description=info.Description+"\n기본 두 방에서 방 추가·연결과 내부 배치를 편집하세요. 보스는 임시 위치 표시입니다.";
                layout.background=null;
                layout.backgroundColor=index==1?new Color(.16f,.13f,.12f):index==2?new Color(.12f,.12f,.18f):index==3?new Color(.10f,.10f,.15f):new Color(.16f,.09f,.14f);
                layout.floors.Clear();layout.floors.Add(new DungeonFloor{rect=new Rect(-6,-2,36,2)});
                layout.spawns.Clear();
                for(int i=0;i<(room.isBoss?7:8);i++)
                {
                    var enemy=AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/DungeonLayouts/Enemies/"+palette[i%palette.Length]+".asset");
                    layout.spawns.Add(new DungeonSpawn{enemy=enemy,position=new Vector2(2+i*2.5f,0),count=1});
                }
                if(room.isBoss)layout.spawns.Add(new DungeonSpawn{enemy=bossEnemy,position=new Vector2(24,0)});
            }
            RoomDungeonAuthoring.Connect(dungeon,start,boss,PortalDirection.Right);
            RoomDungeonAuthoring.Save(dungeon);return dungeon;
        }

        static EnemyData BossMarker(int index)
        {
            var info=LanDungeons.All[index];
            string path="Assets/Data/DungeonLayouts/Enemies/"+Files[index]+"Boss.asset";
            var existing=AssetDatabase.LoadAssetAtPath<EnemyData>(path);if(existing!=null)return existing;
            var boss=ScriptableObject.CreateInstance<EnemyData>();
            boss.displayName=info.Boss+" (임시)";boss.isBoss=true;
            boss.bodySize=index>=3?new Vector2(3,3.5f):new Vector2(1.4f,2.8f);
            boss.moveSpeed=index==3?0:1;boss.baseHealth=2000;
            boss.chasePlayer=index!=3; // The underground crystal remains a fixed boss.
            AssetDatabase.CreateAsset(boss,path);AssetDatabase.SaveAssetIfDirty(boss);return boss;
        }
    }
}
