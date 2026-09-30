using System;
using System.Linq;
using MiniWar.Online;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class CharacterArmTools
    {
        // Authored points in the 1086x1448 reference atlas, measured from its top-left.
        // One scale per atlas: a bent pose is never stretched to match a straight pose.
        static readonly Vector2[][] Shoulders = {
            new[] { V(72,204),V(441,204),V(800,204),V(91,475),V(480,440),V(798,508),
                V(66,878),V(441,873),V(794,876),V(100,1158),V(481,1138),V(825,1184) },
            new[] { V(76,205),V(451,206),V(818,213),V(89,489),V(494,454),V(819,519),
                V(73,905),V(451,907),V(819,910),V(100,1194),V(496,1171),V(847,1218) }
        };
        static readonly Vector2[][] Grips = {
            new[] { V(199,177),V(586,200),V(948,237),V(216,604),V(558,636),V(891,610),
                V(172,807),V(611,819),V(1018,871),V(262,1310),V(559,1307),V(915,1129) },
            new[] { V(210,194),V(611,207),V(968,251),V(224,624),V(580,660),V(917,625),
                V(179,826),V(624,847),V(1043,902),V(272,1347),V(576,1339),V(939,1167) }
        };
        static Vector2 V(float x, float y) => new Vector2(x, y);

        public static CharacterArmFrames Build(int body)
        {
            string stem = "Assets/Resources/Online/Gunner" + (body == 0 ? "Male" : "Female") + "ArmsV1";
            string path = stem + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.isReadable = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pixels = texture.GetPixels32();
            float scale = texture.width / 1086f;
            float ppu = (body == 0 ? 215f : 235f) * scale;
            importer.spritePixelsPerUnit = ppu;
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(x => x.name);
            var sprites = new SpriteRect[12]; var grips = new Vector2[12];
            for (int i = 0; i < 12; i++)
            {
                // Include ALL opaque islands in this cell (the supporting glove can be separate).
                int x0 = i % 3 * texture.width / 3, x1 = (i % 3 + 1) * texture.width / 3;
                int y0 = (3 - i / 3) * texture.height / 4, y1 = (4 - i / 3) * texture.height / 4;
                int left = x1, right = x0, bottom = y1, top = y0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { left = Mathf.Min(left,x); right = Mathf.Max(right,x); bottom = Mathf.Min(bottom,y); top = Mathf.Max(top,y); }
                if (right - left < 20 || top - bottom < 20) throw new InvalidOperationException("Empty arm pose " + i);
                var rect = new Rect(left, bottom, right-left+1, top-bottom+1);
                var shoulder = new Vector2(Shoulders[body][i].x * scale, texture.height - Shoulders[body][i].y * scale);
                var grip = new Vector2(Grips[body][i].x * scale, texture.height - Grips[body][i].y * scale);
                grips[i] = (grip - shoulder) / ppu;
                string name = "Arms_" + i.ToString("00");
                sprites[i] = new SpriteRect { name = name, rect = rect, alignment = SpriteAlignment.Custom,
                    spriteID = previous.TryGetValue(name, out var old) ? old.spriteID : GUID.Generate(),
                    pivot = new Vector2((shoulder.x - rect.x) / rect.width, (shoulder.y - rect.y) / rect.height) };
            }
            provider.SetSpriteRects(sprites);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sprites.Select(x => new SpriteNameFileIdPair(x.name,x.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            string assetPath = stem + ".asset";
            var set = AssetDatabase.LoadAssetAtPath<CharacterArmFrames>(assetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<CharacterArmFrames>(); AssetDatabase.CreateAsset(set,assetPath); }
            set.frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            ApplyDownPoses(body, set.frames, grips);
            set.grips = grips; EditorUtility.SetDirty(set);
            return set;
        }

        static void ApplyDownPoses(int body, Sprite[] frames, Vector2[] grips)
        {
            const string path = "Assets/Resources/Online/GunnerArmsDownV1.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float scale = texture.width / 1254f;
            // Both genders share the same pixel scale and reference atlas coordinates.
            importer.spritePixelsPerUnit = 460f * scale;
            Vector2[] shoulders = { V(222,150),V(793,149),V(226,730),V(794,732) };
            Vector2[] hands = { V(469,535),V(990,533),V(483,1113),V(1022,1114) };
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(x => x.name);
            var sprites = new SpriteRect[4];
            for (int i=0;i<4;i++)
            {
                var rect = new Rect(i%2*texture.width/2f, (1-i/2)*texture.height/2f, texture.width/2f, texture.height/2f);
                var shoulder = new Vector2(shoulders[i].x*scale,texture.height-shoulders[i].y*scale);
                string name = "Down_"+i.ToString("00");
                sprites[i] = new SpriteRect { name=name,rect=rect,alignment=SpriteAlignment.Custom,
                    spriteID=previous.TryGetValue(name,out var old)?old.spriteID:GUID.Generate(),
                    pivot=new Vector2((shoulder.x-rect.x)/rect.width,(shoulder.y-rect.y)/rect.height) };
            }
            provider.SetSpriteRects(sprites);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sprites.Select(x=>new SpriteNameFileIdPair(x.name,x.spriteID)));
            provider.Apply();importer.SaveAndReimport();
            var down = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x=>x.name).ToArray();
            for (int col=0;col<2;col++)
            {
                int index=body*2+col, target=col==0?4:10;
                frames[target]=down[index];
                grips[target]=new Vector2(hands[index].x-shoulders[index].x,shoulders[index].y-hands[index].y)/460f;
            }
        }
    }
}
