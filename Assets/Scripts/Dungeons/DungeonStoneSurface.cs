using UnityEngine;

namespace MiniWar.Dungeons
{
    // Separate paved top and masonry side. Tiling keeps bricks the same size on wide or tall blocks.
    public static class DungeonStoneSurface
    {
        const float TileWidth = 3f, TopFraction = .475f;
        static Texture2D atlas;
        static Sprite top, wall;
        public static Texture2D Atlas => atlas != null ? atlas : (atlas = Resources.Load<Texture2D>("Online/Terrain/KingdomStoneAtlas"));
        static Rect TopUV => new Rect(0,1-TopFraction,1,TopFraction);
        static Rect WallUV => new Rect(0,0,1,1-TopFraction);
        public static float CapHeight(float height) => Mathf.Min(.34f,height*.45f);

        public static bool Build(Transform parent, DungeonFloor floor, int order)
        {
            if(floor.sprite!=null||Atlas==null)return false;
            if(top==null)
            {
                float ppu=Atlas.width/TileWidth;
                top=Sprite.Create(Atlas,new Rect(0,Atlas.height*(1-TopFraction),Atlas.width,Atlas.height*TopFraction),new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect);
                wall=Sprite.Create(Atlas,new Rect(0,0,Atlas.width,Atlas.height*(1-TopFraction)),new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect);
                top.name="Paved road top";wall.name="Stone brick side";
                top.hideFlags=wall.hideFlags=HideFlags.HideAndDontSave;
            }
            var r=floor.rect;float cap=CapHeight(r.height);
            Layer(parent,"Masonry "+floor.id,new Rect(r.x,r.y,r.width,r.height-cap),wall,order,false);
            Layer(parent,"Road top "+floor.id,new Rect(r.x,r.yMax-cap,r.width,cap),top,order+1,true);
            return true;
        }
        static void Layer(Transform parent,string name,Rect rect,Sprite sprite,int order,bool compress)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.transform.localPosition=rect.center;
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;
            sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;
            sr.size=new Vector2(rect.width,compress?sprite.bounds.size.y:rect.height);
            if(compress)go.transform.localScale=new Vector3(1,rect.height/sprite.bounds.size.y,1);
        }
        public static bool DrawPreview(Rect screen,DungeonFloor floor)
        {
            if(floor.sprite!=null||Atlas==null)return false;
            float px=screen.width/floor.rect.width,py=screen.height/floor.rect.height;
            float cap=CapHeight(floor.rect.height)*py;
            Tiled(new Rect(screen.x,screen.y+cap,screen.width,screen.height-cap),WallUV,TileWidth*px,TileWidth*Atlas.height/Atlas.width*(1-TopFraction)*py);
            Tiled(new Rect(screen.x,screen.y,screen.width,cap),TopUV,TileWidth*px,cap);
            return true;
        }
        static void Tiled(Rect area,Rect uv,float width,float height)
        {
            if(width<=0||height<=0)return;
            for(float y=area.y;y<area.yMax-.01f;y+=height)
                for(float x=area.x;x<area.xMax-.01f;x+=width)
                {
                    float w=Mathf.Min(width,area.xMax-x),h=Mathf.Min(height,area.yMax-y);
                    GUI.DrawTextureWithTexCoords(new Rect(x,y,w,h),Atlas,new Rect(uv.x,uv.y+uv.height*(1-h/height),uv.width*w/width,uv.height*h/height));
                }
        }
    }
}
