using UnityEngine;

namespace Wassup.Data
{
    // gimmick-match-integration — "불금은 없습니다!" 기믹 (번아웃).
    // 룰: 배치 유닛이 fatigueInterval 마다 피로도 +fatigueAmount, 임계 도달 시 번아웃
    //     (임계/번아웃 효과는 fatigueStack SO 의 ThresholdRule 이 보유).
    // 판마다 `MatchDefinitionBuilder` 가 모드의 기믹 풀에서 정의표로 옮긴다(옛 ECS 주입 seam 은 이력).
    [CreateAssetMenu(fileName = "Gimmick_Burnout", menuName = "Wassup/Gimmick/Burnout", order = 40)]
    public sealed class BurnoutGimmickData : GimmickData
    {
        [Header("룰 — 피로도 누적 → 번아웃")]
        [Tooltip("kind=Fatigue StackModifierSO. maxStack/perAppDuration/임계 룰의 원천.")]
        public StackModifierSO fatigueStack;
        [Tooltip("배치 유닛의 피로도 누적 주기 (초)")]
        public float fatigueInterval = 10f;
        [Tooltip("주기당 피로도 누적량")]
        public byte fatigueAmount = 1;
    }
}
