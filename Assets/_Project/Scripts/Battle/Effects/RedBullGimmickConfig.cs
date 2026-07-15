using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // gimmick-match-integration — "괜찮아. 먹고 달리자!"(레드불 → 라스트런) 기믹 config 싱글턴.
    // RedBullGimmickData(SO) 수치의 blittable 사본. 존재 = 이 기믹 활성 →
    // PickupSpawn/Consume/LastRunSystem 이 RequireForUpdate 로 self-gate.
    // 생성/파괴: BattleBridge.CreateGimmickConfigIfActive / DestroyEcsInfrastructureEntities.
    public struct RedBullGimmickConfig : IComponentData
    {
        public float redbullSpawnInterval;
        public float redbullLifetime;
        public int redbullMaxActive;
        public float lastRunAttackSpeedMul;
        public float lastRunDuration;
        public float lastRunDamageFraction; // crash 데미지 = 최대체력 × 이 비율
    }
}
