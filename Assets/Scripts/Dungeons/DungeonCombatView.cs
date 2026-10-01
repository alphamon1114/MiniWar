using System;
using System.Collections.Generic;
using System.Linq;
using MiniWar.Online;
using UnityEngine;

namespace MiniWar.Dungeons
{
    // Damage and movement remain in the simulation; this view places sprites and attack cues.
    public sealed class DungeonCombatView : IDisposable
    {
        sealed class EnemyView {public SpriteRenderer Body,Health,Back,Cue;public Color Color;}
        readonly Dictionary<string,EnemyView> enemies=new Dictionary<string,EnemyView>();
        readonly Dictionary<long,SpriteRenderer> bolts=new Dictionary<long,SpriteRenderer>();
        readonly Transform root;
        readonly Sprite square;
        public DungeonCombatView(Transform root,Sprite square){this.root=root;this.square=square;}
        SpriteRenderer Box(string name,Color color,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);var sr=go.AddComponent<SpriteRenderer>();
            sr.sprite=square;sr.color=color;sr.sortingOrder=order;return sr;
        }
        public void Bind(string id,SpriteRenderer body)
        {
            enemies[id]=new EnemyView{Body=body,Color=body.color,Back=Box("HP background",new Color(.08f,.04f,.04f),8),
                Health=Box("Enemy HP",new Color(.87f,.2f,.19f),9),Cue=Box("Attack warning",new Color(1,.42f,.13f,.4f),4)};
        }
        void Rect(SpriteRenderer sr,Vector2 center,Vector2 size)
        {sr.transform.position=center;sr.transform.localScale=new Vector3(size.x/square.bounds.size.x,size.y/square.bounds.size.y,1);}
        public void Refresh(LanEnemyState[] states,LanCombatProjectile[] projectiles)
        {
            foreach(var s in states)
            {
                if(!enemies.TryGetValue(s.id,out var v))continue;
                bool alive=s.hp>0;v.Body.gameObject.SetActive(alive);v.Back.gameObject.SetActive(alive);v.Health.gameObject.SetActive(alive);
                v.Cue.gameObject.SetActive(alive&&s.windup>0);
                if(!alive)continue;
                var center=new Vector3(s.x,s.y+s.height/2,0);
                v.Body.transform.position=center-Vector3.Scale(v.Body.sprite.bounds.center,v.Body.transform.localScale);
                v.Body.flipX=s.facing<0;v.Body.color=s.flash>0?new Color(1,.4f,.4f):s.windup>0?new Color(1,.75f,.42f):v.Color;
                float width=Mathf.Max(.8f,s.width),fill=Mathf.Clamp01(s.hp/Mathf.Max(1,s.maxHp));
                Rect(v.Back,new Vector2(s.x,s.y+s.height+.17f),new Vector2(width,.11f));
                Rect(v.Health,new Vector2(s.x-width*(1-fill)/2,s.y+s.height+.17f),new Vector2(width*fill,.075f));
                if(s.windup>0)
                {
                    v.Cue.color=s.kind==(int)EnemyAttackKind.Magic?new Color(.77f,.35f,1,.45f):new Color(1,.44f,.13f,.35f);
                    if(s.kind==0){v.Cue.transform.rotation=Quaternion.identity;Rect(v.Cue,new Vector2(s.x+s.facing*s.range/2,s.y+.65f),new Vector2(s.range,1.3f));}
                    else
                    {
                        var from=new Vector2(s.x,s.y+s.height*.65f);var to=new Vector2(s.aimX,s.aimY);var d=to-from;
                        Rect(v.Cue,(from+to)/2,new Vector2(d.magnitude,.035f));v.Cue.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
                    }
                }
            }
            var live=new HashSet<long>();
            foreach(var p in projectiles)
            {
                live.Add(p.id);
                if(!bolts.TryGetValue(p.id,out var sr))
                {sr=Box(p.hostile?"Enemy projectile":"Player bullet",!p.hostile?new Color(1,.88f,.4f):p.kind==2?new Color(.75f,.3f,1):new Color(1,.4f,.17f),7);bolts[p.id]=sr;}
                bool orb=p.hostile&&p.kind>=2;
                Rect(sr,new Vector2(p.x,p.y),orb?Vector2.one*p.radius*2:new Vector2(.32f,p.hostile?.10f:.05f));
                sr.transform.rotation=Quaternion.Euler(0,0,orb?45:Mathf.Atan2(p.vy,p.vx)*Mathf.Rad2Deg);
            }
            foreach(long id in bolts.Keys.Where(id=>!live.Contains(id)).ToArray()){UnityEngine.Object.Destroy(bolts[id].gameObject);bolts.Remove(id);}
        }
        public void Dispose(){enemies.Clear();bolts.Clear();}
    }
}
