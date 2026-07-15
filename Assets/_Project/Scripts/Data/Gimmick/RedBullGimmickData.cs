using UnityEngine;

namespace Wassup.Data
{
    // gimmick-match-integration — "괜찮아. 먹고 달리자!" 기믹 (레드불 → 라스트런).
    // 룰: redbullSpawnInterval 마다 레드불 스폰, 소비 시 라스트런
    //     (공속 ×lastRunAttackSpeedMul, lastRunDuration 후 최대체력의 lastRunDamageFraction 만큼 피해).
    // ECS 소비는 RedBullGimmickConfig 로 복사돼 들어간다 (BattleBridge 주입 seam).
    [CreateAssetMenu(fileName = "Gimmick_RedBull", menuName = "Wassup/Gimmick/RedBull", order = 41)]
    public sealed class RedBullGimmickData : GimmickData
    {
        [Header("룰 — 레드불 → 라스트런")]
        [Tooltip("레드불 스폰 주기 (초)")]
        public float redbullSpawnInterval = 5f;
        [Tooltip("레드불 미소비 시 만료 시간 (초)")]
        public float redbullLifetime = 20f;
        [Tooltip("보드 위 동시 존재 레드불 최대 개수")]
        public int maxActivePickups = 6;
        [Tooltip("라스트런 공격속도 배율")]
        public float lastRunAttackSpeedMul = 1.5f;
        [Tooltip("라스트런 지속시간 (초) — 종료 시 crash 데미지 발동")]
        public float lastRunDuration = 5f;
        [Tooltip("라스트런 종료 시 입는 데미지 = 최대체력 × 이 비율 (0.5 = 50% 피해)")]
        public float lastRunDamageFraction = 0.5f;
    }
}
