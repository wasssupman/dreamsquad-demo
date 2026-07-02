using UnityEngine;
using UnityEngine.Tilemaps;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    // effect-tiles unit 4 — 타일 1종이 부여하는 효과 1건. 같은 (stat,op) 중복 entry 는
    // merge-key(stackId 공유) 동일로 마지막만 남으므로 금지 (저작 규칙).
    [System.Serializable]
    public struct EffectTileEntry
    {
        public StatKind stat;
        public CombineOp op;
        [Tooltip("Multiplicative: 배율(1.25=+25%) · Additive: 가산치(RegenPerSec 는 base 0 이라 Additive 필수).")]
        public float magnitude;
    }

    // effect-tiles unit 0 — Place 셀 위 효과 타일 1종의 정의.
    // 배치된 방어 유닛에게 기존 modifier 파이프라인(StatModifierApplyEvents)으로 효과를 부여한다.
    // 비주얼(overlayTile)과 효과 파라미터를 한 에셋에 묶는다 (BlockingHazardSO 패턴).
    // unit 4 — 단일 stat 3필드 → effects[] 다중 stat 배열 (글래스캐논류 복합 타일).
    [CreateAssetMenu(menuName = "Wassup/Effect Tile Data", fileName = "EffectTile")]
    public class EffectTileData : ScriptableObject
    {
        [Tooltip("로그/저장용 안정 식별자.")]
        public string id;
        public string displayName;

        [Header("Visual")]
        [Tooltip("효과 타일맵(런타임 생성, sorting -15)에 칠할 타일.")]
        public TileBase overlayTile;

        [Header("Effects")]
        [Tooltip("배치 유닛에게 부여할 효과 목록. 전부 stackId=2 공유 — stat 이 다르면 슬롯 분리.")]
        public EffectTileEntry[] effects;
    }
}
