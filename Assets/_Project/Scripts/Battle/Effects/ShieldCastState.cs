using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    // shield-guardian-defender unit 1 — 공격과 독립된 실드 캐스트 상태
    // (HazardCastState 선례). BattleBridge 가 스폰 시 1회 베이크, ShieldCastSystem 이
    // 쿨다운을 소유. range 는 SO attackRange 복사값(계약 5 — 별도 SO 필드 없음).
    public struct ShieldCastState : IComponentData
    {
        public float range;
        public float cooldownDuration;   // A
        public float cooldownRemaining;
        public float amount;             // B
        public int targetCount;          // C
        public ShieldTargetFilter filter;
    }
}
