using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 빨간 피해 숫자. 원작의 핵심 피드백이라 Day 2부터 넣는다.
    /// 폰트 에셋 없이 Unity 내장 폰트를 쓰므로 별도 임포트가 필요 없다.
    /// (Day 8에 아트 패스 하면서 제대로 된 연출로 교체할 것)
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        static Font _font;

        [SerializeField] float riseSpeed = 1.6f;
        [SerializeField] float lifetime = 0.7f;

        TextMesh _text;
        float _age;

        public static DamagePopup Spawn(Vector3 worldPosition, float amount)
        {
            var go = new GameObject("DamagePopup");
            go.transform.position = worldPosition + new Vector3(Random.Range(-0.15f, 0.15f), 0.2f, 0f);

            var popup = go.AddComponent<DamagePopup>();
            popup.Init(Mathf.RoundToInt(amount));
            return popup;
        }

        void Init(int amount)
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            _text = gameObject.AddComponent<TextMesh>();
            _text.text = amount.ToString();
            _text.font = _font;
            _text.fontSize = 48;
            _text.characterSize = 0.08f;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.color = new Color(0.85f, 0.10f, 0.10f);

            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null && _font != null) renderer.sharedMaterial = _font.material;
            if (renderer != null) renderer.sortingOrder = 100;
        }

        void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            if (_text != null)
            {
                var c = _text.color;
                c.a = Mathf.Clamp01(1f - _age / lifetime);
                _text.color = c;
            }

            if (_age >= lifetime) Destroy(gameObject);
        }
    }
}
