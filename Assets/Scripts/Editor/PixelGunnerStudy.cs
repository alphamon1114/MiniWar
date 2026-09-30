using System.IO;
using System.Linq;
using MiniWar.Online;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiniWar.EditorTools
{
    /// <summary>Single-pose art study, deliberately separate from the complete gameplay animation set.</summary>
    public sealed class PixelGunnerStudy : EditorWindow
    {
        public const string Folder = "Assets/Art/Studies/PixelGunner/";
        public const string PrefabPath = Folder + "GunnerMalePixelStudy.prefab";
        PreviewRenderUtility preview;
        GameObject actor;
        bool mirrored, layerColors;

        [MenuItem("MiniWar/Characters/Pixel style study")]
        public static void Open() => GetWindow<PixelGunnerStudy>("Pixel Style Study");

        [MenuItem("MiniWar/Characters/Build pixel style study")]
        public static void Build()
        {
            string path = Folder + "GunnerMalePixelV1.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float sx = texture.width / 1254f, sy = texture.height / 1254f;
            float ppu = 1116f * sy / 2.75f;
            importer.spritePixelsPerUnit = ppu;
            string[] names = { "Body", "WholeArms", "Rifle" };
            // Reference coordinates are in the generated square sheet, measured from its top-left.
            Rect[] cuts = { new Rect(0,0,565,1254), new Rect(600,260,530,330), new Rect(560,780,670,170) };
            Vector2[] pivots = { new Vector2(300,1170), new Vector2(705,342), new Vector2(767,884) };
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(x => x.name);
            var sprites = new SpriteRect[3];
            for (int i=0;i<3;i++)
            {
                var cut = cuts[i];
                var rect = new Rect(cut.x*sx,texture.height-(cut.y+cut.height)*sy,cut.width*sx,cut.height*sy);
                var pivot = new Vector2(pivots[i].x*sx,texture.height-pivots[i].y*sy);
                sprites[i] = new SpriteRect { name=names[i],rect=rect,alignment=SpriteAlignment.Custom,
                    spriteID=previous.TryGetValue(names[i],out var old)?old.spriteID:GUID.Generate(),
                    pivot=new Vector2((pivot.x-rect.x)/rect.width,(pivot.y-rect.y)/rect.height) };
            }
            provider.SetSpriteRects(sprites);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sprites.Select(x=>new SpriteNameFileIdPair(x.name,x.spriteID)));
            provider.Apply();importer.SaveAndReimport();
            var art = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(x=>x.name);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("GunnerMalePixelStudy");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                root.AddComponent<SortingGroup>();
                var body = AddSprite(root.transform,"Body",art["Body"],5);
                var arms = AddSprite(root.transform,"WholeArms",art["WholeArms"],8);
                arms.transform.localPosition = new Vector3((220-300)*sx/ppu,(1170-318)*sy/ppu,0);
                var gun = AddSprite(arms.transform,"Rifle",art["Rifle"],7);
                gun.transform.localPosition = new Vector3((812-705)*sx/ppu,(342-525)*sy/ppu,0);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.SaveAssets();
            Debug.Log("Pixel style study built: connected body, whole arm pair, separate rifle. One idle pose only.");
        }

        static SpriteRenderer AddSprite(Transform parent,string name,Sprite sprite,int order)
        {
            var go = new GameObject(name);go.transform.SetParent(parent,false);
            var sr = go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;return sr;
        }

        public static void Capture()
        {
            var p = CreatePreview();
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var go=Instantiate(prefab);p.AddSingleGO(go);
                p.BeginStaticPreview(new Rect(0,0,640,640));p.Render(true);var image=p.EndStaticPreview();
                Directory.CreateDirectory("Logs/QA");File.WriteAllBytes("Logs/QA/GunnerPixelStudy.png",image.EncodeToPNG());DestroyImmediate(image);
            }
            finally { p.Cleanup(); }
        }

        static PreviewRenderUtility CreatePreview()
        {
            var p = new PreviewRenderUtility();p.camera.orthographic=true;p.camera.orthographicSize=1.63f;
            p.camera.transform.position=new Vector3(.35f,1.38f,-10);p.camera.nearClipPlane=.1f;p.camera.farClipPlane=30;
            p.camera.clearFlags=CameraClearFlags.SolidColor;p.camera.backgroundColor=new Color(.09f,.12f,.17f);return p;
        }

        void OnDisable() { if(preview!=null)preview.Cleanup();preview=null; }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("남캐 소총 대기 · 도트 화풍 시험\n몸체 1장 + 양팔 1장 + 별도 총기. 걷기·점프·5방향 원화는 이 화풍으로 아직 제작하지 않았습니다.",MessageType.Info);
            mirrored=GUILayout.Toggle(mirrored,"좌우 반전","Button");layerColors=GUILayout.Toggle(layerColors,"몸체 / 양팔 구분","Button");
            if (preview==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if(prefab==null){if(GUILayout.Button("Build study"))Build();return;}
                preview=CreatePreview();actor=Instantiate(prefab);preview.AddSingleGO(actor);
            }
            actor.transform.localScale=new Vector3(mirrored?-1:1,1,1);
            preview.camera.transform.position=new Vector3(mirrored?-.35f:.35f,1.38f,-10);
            foreach(var sr in actor.GetComponentsInChildren<SpriteRenderer>())
                sr.color=layerColors ? sr.name=="Body"?new Color(.6f,.85f,1):sr.name=="WholeArms"?new Color(1,.55f,.75f):Color.white : Color.white;
            var area=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if(Event.current.type==EventType.Repaint){preview.BeginPreview(area,GUIStyle.none);preview.Render(true);GUI.DrawTexture(area,preview.EndPreview(),ScaleMode.ScaleToFit,false);}
            if(GUILayout.Button("Open study prefab"))UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(PrefabPath);
        }
    }
}
