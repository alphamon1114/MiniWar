using System;
using System.IO;
using System.Linq;
using MiniWar.Online;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>Registers the approved Mac/Elias drawings. Source pixels and limb proportions are preserved.</summary>
    public static class RedrawCharacterTools
    {
        public const string Folder = "Assets/Resources/Online/RedrawV1/";
        public static string BodyPath(int body) => Folder + (body == 0 ? "Male" : "Female") + "Body.asset";
        static Vector2 P(float x, float y) => new Vector2(x, y);
        static readonly Vector2[][] ActionShoulders = {
            new[] {P(366,139),P(716,139),P(1121,193),P(366,605),P(750,625),P(1134,617)},
            new[] {P(344,133),P(722,135),P(1130,201),P(392,605),P(764,634),P(1144,621)}
        };
        static readonly Vector2[][] WalkShoulders = {
            new[] {P(193,144),P(554,144),P(886,143),P(1238,144),P(190,650),P(550,651),P(890,652),P(1238,650)},
            new[] {P(241,143),P(589,145),P(933,143),P(1287,141),P(225,650),P(575,651),P(925,650),P(1288,648)}
        };
        static readonly Vector2[][] ArmShoulders = {
            new[] {P(100,215),P(447,215),P(793,212),P(101,444),P(489,445),P(809,500),
                P(99,870),P(463,873),P(790,870),P(110,1139),P(490,1140),P(859,1213)},
            new[] {P(90,200),P(418,192),P(727,192),P(106,468),P(434,450),P(750,504),
                P(104,905),P(403,905),P(718,891),P(110,1194),P(445,1190),P(764,1255)}
        };
        static readonly Vector2[][] KeyShoulders = {
            new[] {P(274,194),P(819,194),P(1320,194),P(1825,194)},
            new[] {P(294,190),P(836,192),P(1342,192),P(1854,192)}
        };
        static readonly Vector2[][] ArmGrips = {
            new[] {P(211,123),P(608,165),P(1024,241),P(305,634),P(593,675),P(959,622),
                P(185,817),P(605,805),P(1030,884),P(296,1320),P(620,1360),P(947,1129)},
            new[] {P(201,137),P(578,146),P(949,224),P(292,649),P(538,677),P(883,645),
                P(203,828),P(568,856),P(949,929),P(305,1390),P(546,1427),P(867,1232)}
        };

        static Vector2[] RegisterBody(PixelCharacterTools.Atlas atlas, Vector2[] sockets, bool actions, bool keys=false)
        {
            var pivots = new Vector2[sockets.Length];
            var shoulders = new Vector2[sockets.Length];
            for (int i = 0; i < sockets.Length; i++)
            {
                var shoulder = PixelCharacterTools.Pixel(atlas, sockets[i], keys ? P(2172,724) : P(1536,1024));
                // Grounded frames share the lowest planted sole. Airborne legs tuck independently of the actor root.
                float baseline = actions && i >= 3 ? shoulder.y - 1.94f * atlas.ppu : atlas.rects[i].yMin;
                pivots[i] = new Vector2(shoulder.x + .18f * atlas.ppu, baseline);
                shoulders[i] = (shoulder - pivots[i]) / atlas.ppu;
            }
            PixelCharacterTools.Slice(atlas, actions ? "Action_" : keys ? "Key_" : "Walk_", pivots);
            return shoulders;
        }

        public static CharacterBodyFrames BuildBody(int body)
        {
            string name = body == 0 ? "Male" : "Female";
            var actions = PixelCharacterTools.Read(name+"Actions",3,2,true,Folder);
            var walk = PixelCharacterTools.Read(name+"Walk",4,2,true,Folder);
            var keys = PixelCharacterTools.Read(name+"WalkKeys",4,1,true,Folder);
            actions.ppu = actions.rects[0].height / 2.6f;
            walk.ppu = walk.rects[0].height / 2.6f;
            keys.ppu = keys.rects[0].height / 2.6f;
            var a = RegisterBody(actions, ActionShoulders[body], true);
            var w = RegisterBody(walk, WalkShoulders[body], false);
            var k = RegisterBody(keys, KeyShoulders[body], false, true);
            var result = AssetDatabase.LoadAssetAtPath<CharacterBodyFrames>(BodyPath(body));
            if (result == null) { result = ScriptableObject.CreateInstance<CharacterBodyFrames>(); AssetDatabase.CreateAsset(result, BodyPath(body)); }
            result.frames = new Sprite[16]; result.frontShoulders = new Vector2[16];
            for (int i=0;i<4;i++) {result.frames[i]=actions.sprites[i%2];result.frontShoulders[i]=a[i%2];}
            for (int i=0;i<8;i++) {result.frames[i+4]=walk.sprites[i];result.frontShoulders[i+4]=w[i];}
            // Contact poses dwell for one extra beat. Explicit passing keys preserve near/far leg identity.
            int[] slots = {2,3,4,5,6}; int[] keyIndices = {3,0,0,1,2};
            for(int i=0;i<slots.Length;i++) {result.frames[4+slots[i]]=keys.sprites[keyIndices[i]];result.frontShoulders[4+slots[i]]=k[keyIndices[i]];}
            for (int i=0;i<4;i++) {result.frames[i+12]=actions.sprites[i+2];result.frontShoulders[i+12]=a[i+2];}
            result.backShoulders = result.frontShoulders.Select(x=>x+new Vector2(.29f,.025f)).ToArray();
            result.walkCycleRadiansPerSecond = Mathf.PI * 2;
            EditorUtility.SetDirty(result);
            return result;
        }

        public static CharacterArmFrames BuildArms(int body)
        {
            string name = body == 0 ? "Male" : "Female";
            // Connected bounds retain the fingers that extend into ideal grid gutters.
            var atlas = PixelCharacterTools.Read(name+"Arms",3,4,true,Folder);
            var reference = body == 0 ? P(1097,1434) : P(1024,1536);
            atlas.ppu = 360f * atlas.texture.width / reference.x;
            var pivots = ArmShoulders[body].Select(x=>PixelCharacterTools.Pixel(atlas,x,reference)).ToArray();
            PixelCharacterTools.Slice(atlas,"Arms_",pivots);
            string path = Folder+name+"Arms.asset";
            var result = AssetDatabase.LoadAssetAtPath<CharacterArmFrames>(path);
            if (result == null) {result=ScriptableObject.CreateInstance<CharacterArmFrames>();AssetDatabase.CreateAsset(result,path);}
            result.frames=atlas.sprites; result.grips=new Vector2[12];
            for (int i=0;i<12;i++) result.grips[i]=(PixelCharacterTools.Pixel(atlas,ArmGrips[body][i],reference)-pivots[i])/atlas.ppu;
            EditorUtility.SetDirty(result);
            return result;
        }

        [MenuItem("MiniWar/Characters/Render character selection portraits")]
        public static void CapturePortraits()
        {
            var visuals = new OnlineVisuals();
            for (int body=0;body<2;body++)
            {
                var preview = new PreviewRenderUtility();
                try
                {
                    preview.camera.orthographic=true; preview.camera.orthographicSize=1.48f;
                    preview.camera.transform.position=new Vector3(.20f,1.35f,-10);
                    preview.camera.nearClipPlane=.1f; preview.camera.farClipPlane=30;
                    preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=Color.clear;
                    var go=UnityEngine.Object.Instantiate(visuals.RigPrefab(body)); preview.AddSingleGO(go);
                    var rig=go.GetComponent<CharacterRig>();
                    rig.SetWeapon(visuals.Weapon(0,1),OnlineVisuals.WeaponWidths[0]);
                    rig.Pose(0,0,false,0,1,false,0,0);
                    // EndStaticPreview returns RGB24 and loses transparency, leaving a black UI rectangle.
                    preview.BeginPreview(new Rect(0,0,256,384),GUIStyle.none); preview.Render(true);
                    var rendered=preview.EndPreview() as RenderTexture;
                    var previous=RenderTexture.active;
                    var texture=new Texture2D(256,384,TextureFormat.RGBA32,false);
                    try
                    {
                        RenderTexture.active=rendered;
                        texture.ReadPixels(new Rect(0,0,256,384),0,0);
                        // The preview target contains linear values; PNG consumers expect sRGB.
                        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                        {
                            var colors=texture.GetPixels();
                            for(int i=0;i<colors.Length;i++) colors[i]=colors[i].gamma;
                            texture.SetPixels(colors);
                        }
                        texture.Apply();
                    }
                    finally {RenderTexture.active=previous;}
                    string path=Folder+(body==0?"Male":"Female")+"Portrait.png";
                    File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
                    importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
                    importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                finally {preview.Cleanup();}
            }
        }
    }
}
