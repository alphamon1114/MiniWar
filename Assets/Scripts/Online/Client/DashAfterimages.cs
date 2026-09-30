using UnityEngine;
using UnityEngine.Rendering;

namespace MiniWar.Online
{
    /// <summary>Four pooled world-space copies of the complete rig. They never follow the moving actor.</summary>
    public sealed class DashAfterimages : System.IDisposable
    {
        const float Lifetime = .14f;
        sealed class Ghost
        {
            public GameObject root;
            public readonly SpriteRenderer[] layers = new SpriteRenderer[3];
            public float remaining;
        }
        readonly Ghost[] ghosts = new Ghost[4];
        readonly System.Action<GameObject> register;
        int next;

        public DashAfterimages(System.Action<GameObject> registerInPreview = null) {register = registerInPreview;}

        public void Emit(CharacterRig rig)
        {
            var ghost = ghosts[next];
            if (ghost == null)
            {
                ghost = new Ghost {root = new GameObject("Dash afterimage")};
                if (register != null) register(ghost.root);
                ghost.root.AddComponent<SortingGroup>().sortingOrder = 2;
                for (int i=0;i<3;i++)
                {
                    var child = new GameObject("Layer " + i);
                    child.transform.SetParent(ghost.root.transform, false);
                    ghost.layers[i] = child.AddComponent<SpriteRenderer>();
                }
                ghosts[next] = ghost;
            }
            next = (next+1) % ghosts.Length;
            ghost.remaining = Lifetime;
            ghost.root.SetActive(true);
            for (int i=0;i<3;i++)
            {
                var source = i == 0 ? rig.connectedBody : i == 1 ? rig.connectedArms : rig.weapon;
                var copy = ghost.layers[i];
                copy.sprite=source.sprite;copy.sharedMaterial=source.sharedMaterial;copy.sortingOrder=source.sortingOrder;
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                copy.transform.localScale=source.transform.lossyScale;
                copy.color=new Color(.65f,.86f,1f,.24f);
            }
        }

        public void Advance(float dt)
        {
            foreach (var ghost in ghosts)
            {
                if (ghost == null || ghost.remaining <= 0) continue;
                ghost.remaining = Mathf.Max(0, ghost.remaining-dt);
                if (ghost.remaining <= 0) {ghost.root.SetActive(false);continue;}
                foreach(var layer in ghost.layers) layer.color = new Color(.65f,.86f,1f,.24f*ghost.remaining/Lifetime);
            }
        }

        public void Dispose()
        {
            foreach(var ghost in ghosts) if(ghost != null && ghost.root != null)
            {
                if(Application.isPlaying) Object.Destroy(ghost.root);
                else Object.DestroyImmediate(ghost.root);
            }
        }
    }
}
