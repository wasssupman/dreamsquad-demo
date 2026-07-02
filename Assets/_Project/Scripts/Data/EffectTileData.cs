using UnityEngine;
using UnityEngine.Tilemaps;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    // effect-tiles unit 0 — Place 셀 위 효과 타일 1종의 정의.
    // 배치된 방어 유닛에게 기존 modifier 파이프라인(StatModifierApplyEvents)으로 효과를 부여한다.
    // 비주얼(overlayTile)과 효과 파라미터를 한 에셋에 묶는다 (BlockingHazardSO 패턴).
    [CreateAssetMenu(menuName = "Wassup/Effect Tile Data", fileName = "EffectTile")]
    public class EffectTileData : ScriptableObject
    {
        [Tooltip("로그/저장용 안정 식별자.")]
        public string id;
        public string displayName;

        [Header("Visual")]
        [Tooltip("효과 타일맵(런타임 생성, sorting -15)에 칠할 타일.")]
        public TileBase overlayTile;

        [Header("Effect")]
        public StatKind stat = StatKind.DamageMul;
        public CombineOp op = CombineOp.Multiplicative;
        [Tooltip("배율. 예: 1.25 = +25%. 디버프도 1보다 큰 값으로 표현될 수 있다(DmgTakenMul 1.25 = 받는 피해 +25%).")]
        public float magnitude = 1f;
    }
}
