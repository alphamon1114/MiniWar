using System;
using System.Linq;
using MiniWar.Online;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>Registers the approved pixel-art body/arm atlases without altering their source pixels.</summary>
    public static class PixelCharacterTools
    {
        public const string Folder = "Assets/Resources/Online/PixelV1/";
        public const string ApprovedMaleBodyPath = "Assets/Art/Studies/GunnerMaleUpscaleV2/MaleWalkV4Body.asset";
        static Vector2 P(float x,float y) => new Vector2(x,y);
        static readonly Vector2[][] WalkShoulders = {
            new[] { P(138,206),P(449,206),P(764,206),P(1077,206),P(139,788),P(453,788),P(766,788),P(1080,788) },
            new[] { P(159,193),P(469,193),P(779,194),P(1090,193),P(141,805),P(454,804),P(767,805),P(1081,805) }
        };
        static readonly Vector2[][] ActionShoulders = {
            new[] { P(181,195),P(582,195),P(1003,280),P(187,781),P(586,781),P(989,790) },
            new[] { P(231,185),P(590,185),P(990,290),P(244,786),P(614,799),P(988,800) }
        };
        static readonly Vector2[][] ArmShoulders = {
            new[] { P(77,219),P(441,220),P(790,231),P(91,512),P(482,460),P(785,536),
                P(69,905),P(440,904),P(781,905),P(99,1201),P(486,1167),P(817,1230) },
            new[] { P(96,219),P(456,223),P(807,239),P(92,520),P(502,456),P(805,551),
                P(82,911),P(466,913),P(799,916),P(99,1200),P(515,1164),P(833,1240) }
        };
        static readonly Vector2[][] ArmGrips = {
            new[] { P(212,212),P(595,203),P(941,260),P(237,643),P(555,674),P(899,634),
                P(174,841),P(632,839),P(1013,896),P(265,1349),P(565,1343),P(922,1172) },
            new[] { P(219,204),P(623,214),P(968,274),P(230,645),P(578,690),P(917,637),
                P(181,850),P(647,859),P(1035,911),P(269,1358),P(587,1370),P(943,1177) }
        };

        internal sealed class Atlas
        {
            public string path;
            public TextureImporter importer;
            public Texture2D texture;
            public Rect[] rects;
            public Sprite[] sprites;
            public float ppu;
        }

        internal static Atlas Read(string name,int columns,int rows,bool connected,string folder=Folder)
        {
            string path=folder+name+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;
            importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=texture.GetPixels32();
            var rects=connected?CharacterRigTools.FindPartBounds(pixels,texture.width,texture.height,columns,rows):new Rect[columns*rows];
            if(!connected) for(int i=0;i<rects.Length;i++)
            {
                int x0=i%columns*texture.width/columns,x1=(i%columns+1)*texture.width/columns;
                int y0=(rows-1-i/columns)*texture.height/rows,y1=(rows-i/columns)*texture.height/rows;
                int left=x1,right=x0,bottom=y1,top=y0;
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(pixels[y*texture.width+x].a>32)
                {left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
                rects[i]=new Rect(left,bottom,right-left+1,top-bottom+1);
            }
            if(rects.Any(x=>x.width<20||x.height<20))throw new InvalidOperationException("Missing frame in "+path);
            return new Atlas{path=path,importer=importer,texture=texture,rects=rects};
        }

        internal static Vector2 Pixel(Atlas atlas,Vector2 point,Vector2 referenceSize)
            => new Vector2(point.x*atlas.texture.width/referenceSize.x,atlas.texture.height-point.y*atlas.texture.height/referenceSize.y);

        internal static void Slice(Atlas atlas,string prefix,Vector2[] pivots)
        {
            atlas.importer.spritePixelsPerUnit=atlas.ppu;
            var factory=new SpriteDataProviderFactories();factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(atlas.importer);provider.InitSpriteEditorDataProvider();
            var old=provider.GetSpriteRects().ToDictionary(x=>x.name);var sprites=new SpriteRect[atlas.rects.Length];
            for(int i=0;i<sprites.Length;i++)
            {
                string name=prefix+i.ToString("00");var rect=atlas.rects[i];
                sprites[i]=new SpriteRect{name=name,rect=rect,alignment=SpriteAlignment.Custom,
                    spriteID=old.TryGetValue(name,out var previous)?previous.spriteID:GUID.Generate(),
                    pivot=new Vector2((pivots[i].x-rect.x)/rect.width,(pivots[i].y-rect.y)/rect.height)};
            }
            provider.SetSpriteRects(sprites);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(sprites.Select(x=>new SpriteNameFileIdPair(x.name,x.spriteID)));
            provider.Apply();atlas.importer.SaveAndReimport();
            atlas.sprites=AssetDatabase.LoadAllAssetsAtPath(atlas.path).OfType<Sprite>().OrderBy(x=>x.name).ToArray();
        }

        static Vector2[] RegisterBody(Atlas atlas,Vector2[] sockets,bool actions)
        {
            var pivots=new Vector2[sockets.Length];var shoulders=new Vector2[sockets.Length];
            for(int i=0;i<sockets.Length;i++)
            {
                var shoulder=Pixel(atlas,sockets[i],P(1254,1254));
                // Grounded frames use the planted boot baseline. Airborne knees may lift independently of root height.
                float baseline=actions&&i>=3?shoulder.y-2.10f*atlas.ppu:atlas.rects[i].yMin;
                pivots[i]=new Vector2(shoulder.x+.18f*atlas.ppu,baseline);
                shoulders[i]=(shoulder-pivots[i])/atlas.ppu;
            }
            Slice(atlas,actions?"Action_":"Walk_",pivots);return shoulders;
        }

        public static CharacterBodyFrames BuildBody(int body)
        {
            if (body == 0)
                return AssetDatabase.LoadAssetAtPath<CharacterBodyFrames>(ApprovedMaleBodyPath)
                    ?? throw new InvalidOperationException("Missing approved male body: " + ApprovedMaleBodyPath);
            string name=body==0?"Male":"Female";
            var actions=Read(name+"Actions",3,2,true);var walk=Read(name+"Walk",4,2,true);
            actions.ppu=actions.rects[0].height/2.75f;walk.ppu=walk.rects[0].height/2.75f;
            var a=RegisterBody(actions,ActionShoulders[body],true);var w=RegisterBody(walk,WalkShoulders[body],false);
            string path=Folder+name+"Body.asset";
            var result=AssetDatabase.LoadAssetAtPath<CharacterBodyFrames>(path);
            if(result==null){result=ScriptableObject.CreateInstance<CharacterBodyFrames>();AssetDatabase.CreateAsset(result,path);}
            result.frames=new Sprite[16];result.frontShoulders=new Vector2[16];
            for(int i=0;i<4;i++){result.frames[i]=actions.sprites[i%2];result.frontShoulders[i]=a[i%2];}
            for(int i=0;i<8;i++){result.frames[i+4]=walk.sprites[i];result.frontShoulders[i+4]=w[i];}
            for(int i=0;i<4;i++){result.frames[i+12]=actions.sprites[i+2];result.frontShoulders[i+12]=a[i+2];}
            result.backShoulders=result.frontShoulders.Select(x=>x+new Vector2(.29f,.025f)).ToArray();
            EditorUtility.SetDirty(result);return result;
        }

        public static CharacterArmFrames BuildArms(int body)
        {
            string name=body==0?"Male":"Female";var atlas=Read(name+"Arms",3,4,false);
            atlas.ppu=(body==0?220f:238f)*atlas.texture.width/1086f;
            var pivots=ArmShoulders[body].Select(x=>Pixel(atlas,x,P(1086,1448))).ToArray();
            Slice(atlas,"Arms_",pivots);
            string path=Folder+name+"Arms.asset";
            var result=AssetDatabase.LoadAssetAtPath<CharacterArmFrames>(path);
            if(result==null){result=ScriptableObject.CreateInstance<CharacterArmFrames>();AssetDatabase.CreateAsset(result,path);}
            result.frames=atlas.sprites;result.grips=new Vector2[12];
            for(int i=0;i<12;i++)result.grips[i]=(Pixel(atlas,ArmGrips[body][i],P(1086,1448))-pivots[i])/atlas.ppu;
            EditorUtility.SetDirty(result);return result;
        }

        public static void BuildEquipment()
        {
            var longGuns=Read("Weapons",6,3,true);longGuns.ppu=100;
            var grips=new[] { P(.22f,.25f),P(.42f,.4f),P(.3f,.34f),P(.4f,.3f),P(.3f,.3f),P(.15f,.5f) };
            var pivots=longGuns.rects.Select((r,i)=>r.min+Vector2.Scale(r.size,grips[i%6])).ToArray();
            Slice(longGuns,"Equipment_",pivots);
            var handguns=Read("Handguns",2,3,true);handguns.ppu=100;
            Slice(handguns,"Handgun_",handguns.rects.Select(r=>r.min+Vector2.Scale(r.size,P(.22f,.25f))).ToArray());
        }
    }
}
