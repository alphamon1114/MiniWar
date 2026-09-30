using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Cosmetic, straight flight after a server-approved shot. Never performs damage or homing.</summary>
    public sealed class PracticeShotView : MonoBehaviour
    {
        LineRenderer[] trails;
        Vector3 origin;
        LanShot shot;
        float age;
        static Material material;
        public static PracticeShotView Spawn(LanShot value, Vector3 muzzle)
        {
            var view = new GameObject("Practice shot " + value.id).AddComponent<PracticeShotView>();
            view.shot = value; view.origin = muzzle;
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            view.trails = new LineRenderer[LanShooting.Pellets(value.family)];
            for (int i = 0; i < view.trails.Length; i++)
            {
                var trail = new GameObject("Tracer").AddComponent<LineRenderer>();
                trail.transform.SetParent(view.transform, false);
                trail.sharedMaterial = material; trail.useWorldSpace = true; trail.positionCount = 2;
                trail.startWidth = .045f; trail.endWidth = .015f;
                trail.startColor = new Color(1,.92f,.50f); trail.endColor = new Color(1,.50f,.12f,.2f);
                trail.sortingOrder = 20; view.trails[i] = trail;
            }
            view.Draw(); return view;
        }
        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (age >= LanShooting.Lifetime) { Destroy(gameObject); return; }
            Draw();
        }
        void Draw()
        {
            for (int i = 0; i < trails.Length; i++)
            {
                LanShooting.Position(shot, i, age, out float x, out float y);
                Vector3 tip = origin + new Vector3(x - shot.x, y - shot.y, 0);
                float radians = LanShooting.PelletAngle(shot, i) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0);
                trails[i].SetPosition(0, tip);
                trails[i].SetPosition(1, tip - direction * Mathf.Min(.55f, .10f + age * LanShooting.Speed(shot.family)));
            }
        }
    }
}
