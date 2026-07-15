using UnityEngine;

namespace Wassup.Data
{
    // season-gimmick-overwork unit 2 — "야근" 기믹 데이터.
    // 룰 1: 배치 유닛이 fatigueInterval 마다 피로도 +fatigueAmount, 임계 도달 시 번아웃
    //       (임계/번아웃 효과는 fatigueStack SO 의 ThresholdRule 이 보유).
    // 룰 2: redbullSpawnInterval 마다 레드불 스폰, 소비 시 라스트런
    //       (공속 ×lastRunAttackSpeedMul, lastRunDuration 후 최대체력 ×lastRunMaxHealthMul 영구).
    // ECS 소비는 OverworkGimmickConfig 로 복사돼 들어간다 (BattleBridge 주입 seam).
    [CreateAssetMenu(fileName = "Gimmick_Overwork", menuName = "Wassup/Gimmick/Overwork", order = 40)]
    public sealed class OverworkGimmickData : GimmickData
    {
        [Header("룰 1 — 피로도 누적 → 번아웃")]
        [Tooltip("kind=Fatigue StackModifierSO. maxStack/perAppDuration/임계 룰의 원천.")]
        public StackModifierSO fatigueStack;
        [Tooltip("배치 유닛의 피로도 누적 주기 (초)")]
        public float fatigueInterval = 10f;
        [Tooltip("주기당 피로도 누적량")]
        public byte fatigueAmount = 1;

        [Header("룰 2 — 레드불 → 라스트런")]
        [Tooltip("레드불 스폰 주기 (초)")]
        public float redbullSpawnInterval = 5f;
        [Tooltip("레드불 미소비 시 만료 시간 (초) — 보드 누적 상한")]
        public float redbullLifetime = 20f;
        [Tooltip("라스트런 공격속도 배율")]
        public float lastRunAttackSpeedMul = 1.5f;
        [Tooltip("라스트런 지속시간 (초) — 종료 시 최대체력 컷 발동")]
        public float lastRunDuration = 5f;
        [Tooltip("라스트런 종료 시 최대체력 배율 (판 끝까지)")]
        public float lastRunMaxHealthMul = 0.1f;
    }
}
