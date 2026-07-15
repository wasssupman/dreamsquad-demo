using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 2 — 야근 기믹 config 싱글턴.
    // OverworkGimmickData(SO) 수치의 blittable 사본 — Burst 시스템이 SO 를 직접 만지지 않는다.
    // 존재 = 기믹 활성 (룰 시스템들은 RequireForUpdate 로 self-gate).
    // 생성: BattleBridge.EnsureQueriesAndQueues (활성 시즌의 gimmick 이 Overwork 일 때만).
    // 파괴: BattleBridge.DestroyEcsInfrastructureEntities (대칭 — 누락 시 재입장마다 중복).
    public struct OverworkGimmickConfig : IComponentData
    {
        public float fatigueInterval;
        public byte fatigueAmount;
        public byte fatigueMaxStack;
        public float fatiguePerAppDuration;
        public float redbullSpawnInterval;
        public float redbullLifetime;
        public float lastRunAttackSpeedMul;
        public float lastRunDuration;
        public float lastRunMaxHealthMul;
    }
}
