using System;
using System.IO;
using System.Linq;
using MiniWar.Data;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class EnemyIdleTools
    {
        public const string Folder="Assets/Art/Monsters/MaskedV1/";
        public const string GalleryPath="Assets/Data/DungeonLayouts/MaskedEnemyIdleGallery.asset";
        static readonly string[] Files={"PatrolIdleV1","DaggerGuardIdleV2","CrossbowGuardIdleV2","DarkMageIdleV2"};
        static readonly string[] DataFiles={"Patrol","DaggerGuard","CrossbowGuard","DarkMage"};
        static readonly string[] Labels={"순찰대원","단검 경비병","석궁 경비병","흑마법사"};

        [MenuItem("MiniWar/Monsters/Register masked idle sprites")]
        public static void Register()
        {
            DungeonBuilderAssets.CreateGateExample();
            var report=new System.Collections.Generic.List<string>();
            for(int i=0;i<Files.Length;i++)
            {
                if(!File.Exists(Folder+Files[i]+".png"))throw new FileNotFoundException(Files[i]);
                var atlas=PixelCharacterTools.Read(Files[i],1,1,false,Folder);
                var pixels=atlas.texture.GetPixels32();
                int clear=pixels.Count(p=>p.a==0);
                if(clear<pixels.Length/5)throw new InvalidOperationException(Files[i]+": image needs a real transparent background.");
                var bounds=atlas.rects[0];atlas.ppu=bounds.height/2.2f;
                PixelCharacterTools.Slice(atlas,"Idle_",new[]{new Vector2(bounds.center.x,bounds.yMin)});
                var sprite=atlas.sprites.Single();
                string path="Assets/Data/DungeonLayouts/Enemies/"+DataFiles[i]+".asset";
                var enemy=AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if(enemy==null)
                {
                    enemy=ScriptableObject.CreateInstance<EnemyData>();enemy.displayName=Labels[i];enemy.baseHealth=100;enemy.moveSpeed=1.5f;
                    AssetDatabase.CreateAsset(enemy,path);
                }
                enemy.idleSprite=sprite;enemy.bodySize=new Vector2(.8f,2.2f);
                EditorUtility.SetDirty(enemy);AssetDatabase.SaveAssetIfDirty(enemy);
                report.Add(Labels[i]+": "+atlas.texture.width+"x"+atlas.texture.height+", alpha="+(100f*clear/pixels.Length).ToString("0.0")+"%, sprite="+bounds+", height=2.2m");
            }
            CreateGallery();
            foreach(var window in Resources.FindObjectsOfTypeAll<DungeonBuilderWindow>())
                typeof(DungeonBuilderWindow).GetMethod("ReloadPalette",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(window,null);
            Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/EnemyIdleImport.txt",report);Debug.Log(string.Join("\n",report));
        }

        public static DungeonLayout CreateGallery()
        {
            var layout=AssetDatabase.LoadAssetAtPath<DungeonLayout>(GalleryPath);
            if(layout!=null)return layout;
            layout=ScriptableObject.CreateInstance<DungeonLayout>();layout.displayName="가면 병사 · 기본 자세";
            layout.description="움직임 없는 기본 서 있는 모습 4종. 왼쪽부터 순찰대원, 단검 경비병, 석궁 경비병, 흑마법사.";
            layout.bounds=new Rect(0,-2,24,12);layout.entrance=new Vector2(2,0);layout.exit=new Vector2(22,0);
            layout.floors.Add(new DungeonFloor {rect=new Rect(0,-2,24,2)});
            for(int i=0;i<Files.Length;i++)layout.spawns.Add(new DungeonSpawn {
                enemy=AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/DungeonLayouts/Enemies/"+DataFiles[i]+".asset"),
                position=new Vector2(6+i*4,0),faceLeft=false});
            AssetDatabase.CreateAsset(layout,GalleryPath);AssetDatabase.SaveAssetIfDirty(layout);return layout;
        }

        [MenuItem("MiniWar/Monsters/Masked idle preview")]
        public static void Open()=>DungeonBuilderWindow.ShowLayout(CreateGallery());
    }
}
