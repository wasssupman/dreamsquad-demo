using UnityEngine;

namespace Wassup.Data
{
    // gimmick-match-integration — "워라벨 지켜주시죠?" 기믹 (번아웃).
    // 룰: 배치 유닛이 fatigueInterval 마다 피로도 +fatigueAmount, 임계 도달 시 번아웃
    //     (임계/번아웃 효과는 fatigueStack SO 의 ThresholdRule 이 보유).
    // ECS 소비는 BurnoutGimmickConfig 로 복사돼 들어간다 (BattleBridge 주입 seam).
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
