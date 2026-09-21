using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 탄 오브젝트 풀. 기술 규약상 탄은 Instantiate 금지다 —
    /// 연사형 8발/초에 산탄 4펠릿이면 초당 수십 개가 생기고 사라진다.
    /// </summary>
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] Projectile prefab;
        [SerializeField, Min(0)] int prewarm = 64;
        [SerializeField, Min(1)] int hardCap = 512;

        readonly Stack<Projectile> _idle = new Stack<Projectile>();
        int _created;

        void Awake()
        {
            for (int i = 0; i < prewarm; i++) _idle.Push(Create());
        }

        Projectile Create()
        {
            var p = Instantiate(prefab, transform);
            p.gameObject.SetActive(false);
            p.Bind(this);
            _created++;
            return p;
        }

        public Projectile Get()
        {
            if (_idle.Count > 0) return _idle.Pop();
            if (_created >= hardCap) return null;   // 폭주 방지
            return Create();
        }

        public void Release(Projectile p)
        {
            if (p == null) return;
            p.gameObject.SetActive(false);
            p.transform.SetParent(transform, false);
            _idle.Push(p);
        }
    }
}
