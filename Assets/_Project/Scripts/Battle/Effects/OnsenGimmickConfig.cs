using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-onsen unit 0 — "뜨끈하니 좋네요오오.. 뜨겁네?"(온천) 기믹 config 싱글턴.
    // OnsenGimmickData(SO) 수치의 blittable 사본 — Burst 시스템이 SO 를 직접 만지지 않는다.
    // 존재 = 이 기믹 활성 → HeatAccrualSystem 이 RequireForUpdate 로 self-gate.
    // 생성/파괴: BattleBridge.CreateGimmickConfigIfActive / DestroyEcsInfrastructureEntities.
    public struct OnsenGimmickConfig : IComponentData
    {
        public float heatInterval;
        public byte flipThreshold;
        public float healPercent;
        public float lossPercent;
        public byte heatMaxStack;
    }
}
