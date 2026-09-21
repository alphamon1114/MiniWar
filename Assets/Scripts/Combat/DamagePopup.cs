using UnityEngine;

namespace MiniWar.Combat
{
    /// <summary>
    /// 빨간 피해 숫자. 원작의 핵심 피드백이라 Day 2부터 넣는다.
    /// 폰트 에셋 없이 Unity 내장 폰트를 쓰므로 별도 임포트가 필요 없다.
    ///
    /// 방어력에 크게 깎인 피해는 <b>회색</b>으로 뜬다. 왜 안 죽는지 플레이어가 알아야
    /// 무기를 바꿔볼 생각을 한다 — 숫자만 작게 뜨면 총이 약한 건지 적이 단단한 건지 구분이 안 된다.
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        static Font _font;

        static readonly Color Normal = new Color(0.85f, 0.10f, 0.10f);
        static readonly Color Absorbed = new Color(0.45f, 0.45f, 0.48f);

        [SerializeField] float riseSpeed = 1.6f;
        [SerializeField] float lifetime = 0.7f;

        TextMesh _text;
        float _age;

        public static DamagePopup Spawn(Vector3 worldPosition, float amount, bool absorbed = false)
        {
            var go = new GameObject("DamagePopup");
            go.transform.position = worldPosition + new Vector3(Random.Range(-0.15f, 0.15f), 0.2f, 0f);

            var popup = go.AddComponent<DamagePopup>();
            popup.Init(Mathf.RoundToInt(amount), absorbed);
            return popup;
        }

        void Init(int amount, bool absorbed)
        {
            if (_font == null)
            {
                // ?? 는 UnityEngine.Object의 가짜 null을 못 걸러내므로 == null 로 비교한다.
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _font = font;
            }

            _text = gameObject.AddComponent<TextMesh>();
            _text.text = absorbed ? $"{amount}" : amount.ToString();
            _text.font = _font;
            _text.fontSize = 48;
            _text.characterSize = absorbed ? 0.06f : 0.08f;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.color = absorbed ? Absorbed : Normal;

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
