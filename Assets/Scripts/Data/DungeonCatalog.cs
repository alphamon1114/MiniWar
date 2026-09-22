using System;
using UnityEngine;

namespace MiniWar.Data
{
    /// <summary>
    /// 마을에서 고를 수 있는 던전 목록. 앞 던전을 깨야 다음이 열린다.
    ///
    /// 해금을 "앞 던전 클리어"로 둔 이유는, 강화가 마을에 쌓이기 때문이다 —
    /// 깬 던전을 다시 돌아 돈을 벌고 강화한 뒤 다음으로 가는 되돌아가기가
    /// 이 구조의 유일한 난이도 조절 장치다. 레벨 제한을 걸면 그게 막힌다.
    /// </summary>
    [CreateAssetMenu(menuName = "무기전쟁/Dungeon Catalog", fileName = "SO_DungeonCatalog")]
    public sealed class DungeonCatalog : ScriptableObject
    {
        [Tooltip("위에서부터 순서대로 해금된다.")]
        public StageData[] dungeons = Array.Empty<StageData>();

        public int Count => dungeons != null ? dungeons.Length : 0;

        public StageData At(int index)
            => index >= 0 && index < Count ? dungeons[index] : null;

        public int IndexOf(StageData stage)
        {
            for (int i = 0; i < Count; i++)
                if (dungeons[i] == stage) return i;
            return -1;
        }
    }
}
