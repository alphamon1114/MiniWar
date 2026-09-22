using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>적 탄 전용 풀. 보스 탄막은 한 사이클에 10발 넘게 나간다.</summary>
    public sealed class HazardPool : MonoBehaviour
    {
        [SerializeField] HazardProjectile prefab;
        [SerializeField, Min(0)] int prewarm = 24;
        [SerializeField, Min(1)] int hardCap = 160;

        readonly Stack<HazardProjectile> _idle = new Stack<HazardProjectile>();
        int _created;

        void Awake()
        {
            for (int i = 0; i < prewarm; i++) _idle.Push(Create());
        }

        HazardProjectile Create()
        {
            var h = Instantiate(prefab, transform);
            h.gameObject.SetActive(false);
            h.Bind(this);
            _created++;
            return h;
        }

        public HazardProjectile Get()
        {
            if (_idle.Count > 0) return _idle.Pop();
            if (_created >= hardCap) return null;
            return Create();
        }

        public void Release(HazardProjectile h)
        {
            if (h == null) return;
            h.gameObject.SetActive(false);
            h.transform.SetParent(transform, false);
            _idle.Push(h);
        }
    }
}
