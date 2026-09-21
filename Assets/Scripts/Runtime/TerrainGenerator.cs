using System.Collections.Generic;
using UnityEngine;

namespace MiniWar.Runtime
{
    /// <summary>
    /// 앞으로 끝없이 뻗는 능선. 플레이어가 전진하는 만큼 앞을 만들고 지나온 뒤는 버린다.
    ///
    /// 높이는 사인 세 개를 겹친 결정적 함수라 같은 x는 언제나 같은 높이가 나온다 —
    /// 청크를 만들었다 지웠다 해도 지형이 흔들리지 않는다.
    ///
    /// 솟아오른 능선은 탄을 막는다. 그래서 곡사 무기(수류탄)가 능선 너머를 때리는
    /// 유일한 수단이 된다 — 무기 역할이 지형에서 한 번 더 갈린다.
    /// </summary>
    public sealed class TerrainGenerator : MonoBehaviour
    {
        public static TerrainGenerator Instance { get; private set; }

        [Header("능선 모양")]
        [SerializeField] float baseHeight = -2.2f;
        [SerializeField] float bottomY = -8f;

        [Header("청크")]
        [SerializeField] float chunkWidth = 10f;
        [SerializeField] float sampleStep = 0.4f;
        [Tooltip("플레이어 앞으로 확보할 거리.")]
        [SerializeField] float lookAhead = 34f;
        [Tooltip("플레이어 뒤로 남겨둘 거리. 이보다 뒤는 버린다.")]
        [SerializeField] float keepBehind = 16f;

        [Header("참조")]
        [SerializeField] Transform follow;
        [SerializeField] Material surfaceMaterial;

        readonly Dictionary<int, GameObject> _chunks = new Dictionary<int, GameObject>();
        readonly List<int> _expired = new List<int>();

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>어떤 x에서의 지면 높이. 결정적이다.</summary>
        public float HeightAt(float x)
        {
            return baseHeight
                 + Mathf.Sin(x * 0.11f) * 1.25f
                 + Mathf.Sin(x * 0.29f + 2.1f) * 0.55f
                 + Mathf.Sin(x * 0.061f + 5.3f) * 0.95f;
        }

        void Start() => Refresh();

        void Update() => Refresh();

        void Refresh()
        {
            if (follow == null) return;

            float x = follow.position.x;
            int first = Mathf.FloorToInt((x - keepBehind) / chunkWidth);
            int last = Mathf.FloorToInt((x + lookAhead) / chunkWidth);

            for (int i = first; i <= last; i++)
                if (!_chunks.ContainsKey(i)) _chunks[i] = BuildChunk(i);

            _expired.Clear();
            foreach (var kv in _chunks)
                if (kv.Key < first || kv.Key > last) _expired.Add(kv.Key);

            foreach (int key in _expired)
            {
                Destroy(_chunks[key]);
                _chunks.Remove(key);
            }
        }

        GameObject BuildChunk(int index)
        {
            float startX = index * chunkWidth;
            int steps = Mathf.Max(2, Mathf.CeilToInt(chunkWidth / sampleStep));

            var go = new GameObject($"TerrainChunk_{index}") { layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(startX, 0f, 0f);

            var top = new Vector2[steps + 1];
            var vertices = new Vector3[(steps + 1) * 2];
            var triangles = new int[steps * 6];

            for (int i = 0; i <= steps; i++)
            {
                float localX = i * (chunkWidth / steps);
                float h = HeightAt(startX + localX);

                top[i] = new Vector2(localX, h);
                vertices[i * 2] = new Vector3(localX, h, 0f);
                vertices[i * 2 + 1] = new Vector3(localX, bottomY, 0f);
            }

            for (int i = 0; i < steps; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v;     triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = $"Terrain_{index}" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaceMaterial;
            renderer.sortingOrder = -5;

            // 능선 위쪽 선만 콜라이더로 — 탄과 발이 여기에 닿는다.
            go.AddComponent<EdgeCollider2D>().points = top;

            return go;
        }
    }
}
