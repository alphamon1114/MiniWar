using System;
using System.Linq;
using MiniWar.Online;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class CharacterBodyTools
    {
        public static CharacterBodyFrames Build(int body)
        {
            string stem = "Assets/Resources/Online/Gunner" + (body == 0 ? "MaleBodyV3" : "FemaleBodyV2");
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
            float cw = texture.width / 4f, ch = texture.height / 4f;
            var rects = CharacterRigTools.FindPartBounds(pixels, texture.width, texture.height, 4);
            for (int i = 0; i < 16; i++)
            {
                if (rects[i].width < 20 || rects[i].height < 30) throw new InvalidOperationException("Empty body frame " + i);
            }
            // One scale for the entire sheet, never stretch a crouch or lifted foot back to standing height.
            float ppu = rects[0].height / 2.75f;
            importer.spritePixelsPerUnit = ppu;
            float[] socketX = body == 0
                ? new[] { .46f,.51f,.58f,.66f, .49f,.53f,.58f,.65f, .49f,.53f,.58f,.65f, .60f,.59f,.65f,.72f }
                : new[] { .48f,.49f,.49f,.51f, .52f,.53f,.55f,.55f, .51f,.53f,.54f,.54f, .52f,.54f,.56f,.56f };
            float[] socketY = body == 0
                ? new[] { .30f,.30f,.30f,.30f, .31f,.31f,.31f,.31f, .31f,.31f,.31f,.31f, .46f,.25f,.25f,.28f }
                : new[] { .29f,.29f,.29f,.29f, .28f,.28f,.28f,.28f, .28f,.28f,.28f,.28f, .34f,.27f,.27f,.27f };
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(x => x.name);
            var sprites = new SpriteRect[16];
            var shoulders = new Vector2[16];
            for (int i = 0; i < 16; i++)
            {
                float x = (i % 4 + socketX[i]) * cw;
                float y = (4 - i / 4 - socketY[i]) * ch;
                // V3 keeps head-to-shoulder size constant while rows drift in the generated atlas.
                if (body == 0) y = rects[i].yMax - rects[0].height * .20f;
                float baseline = i < 12 ? rects[i].yMin : rects[12].yMin;
                var pivotPixels = new Vector2(x + .17f * ppu, baseline);
                shoulders[i] = (new Vector2(x, y) - pivotPixels) / ppu;
                string name = "Body_" + i.ToString("00");
                sprites[i] = new SpriteRect { name = name,
                    spriteID = previous.TryGetValue(name, out var old) ? old.spriteID : GUID.Generate(),
                    rect = rects[i], alignment = SpriteAlignment.Custom,
                    pivot = new Vector2((pivotPixels.x - rects[i].x) / rects[i].width, (pivotPixels.y - rects[i].y) / rects[i].height) };
            }
            provider.SetSpriteRects(sprites);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sprites.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            string assetPath = stem + ".asset";
            var set = AssetDatabase.LoadAssetAtPath<CharacterBodyFrames>(assetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<CharacterBodyFrames>(); AssetDatabase.CreateAsset(set, assetPath); }
            set.frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            set.frontShoulders = shoulders;
            set.backShoulders = shoulders.Select(x => x + new Vector2(.29f, .025f)).ToArray();
            EditorUtility.SetDirty(set);
            return set;
        }
    }
}
