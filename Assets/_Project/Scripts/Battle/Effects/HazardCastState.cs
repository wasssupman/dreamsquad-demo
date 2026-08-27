using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct HazardCastState : IComponentData
    {
        // skill-layer-migration unit 5a — 라우팅 키. 이 능력은 `DcTriggerSlot` 을 안 쓰고
        // 자기 상태 컴포넌트를 가지므로 키도 여기 산다(`DamagedCounter` 와 같은 자리).
        public int skillId;
        public float range;
        public float cooldownDuration;
        public float cooldownRemaining;
        public int targetMask;
        // waypoint-routing unit 4 rev 4 — target PathFollowState.traversalLayers
        // filter. 0 = legacy unfiltered.
        public byte targetTraversalLayers;
        public int dataIndex;
        public HazardCastKind kind;
        public int footprintWidth;
        public int footprintHeight;
    }
}
