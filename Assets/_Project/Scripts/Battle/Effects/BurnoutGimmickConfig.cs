using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // gimmick-match-integration — "불금은 없습니다!"(번아웃) 기믹 config 싱글턴.
    // BurnoutGimmickData(SO) 수치의 blittable 사본 — Burst 시스템이 SO 를 직접 만지지 않는다.
    // 존재 = 이 기믹 활성 → FatigueAccrualSystem 이 RequireForUpdate 로 self-gate.
    // 생성/파괴: BattleBridge.CreateGimmickConfigIfActive / DestroyEcsInfrastructureEntities.
    public struct BurnoutGimmickConfig : IComponentData
    {
        public float fatigueInterval;
        public byte fatigueAmount;
        public byte fatigueMaxStack;
        public float fatiguePerAppDuration;
    }
}
