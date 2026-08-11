namespace Wassup.Battle.Effects
{
    [System.Serializable]
    public struct HazardEffect
    {
        public CcKind kind;
        public float param1;
        public float param2;
        public float restDuration;
        // dot-tick-cadence unit 0 — >0 이면 DoT 를 이 주기(초)마다 param1 청크로 1회 지급.
        // 0 이면 레거시 연속(param1=DPS). append-only(기존 에셋은 0 으로 역직렬화 = 연속).
        public float tickInterval;
        // dot-effect-extraction unit 0 — 이 해저드가 만드는 지속 피해의 원소. append-only
        // (기존 에셋은 0 = None 으로 역직렬화). Hazard_Fire_* 와 Hazard_Poison_* 은 둘 다
        // kind 가 DoT 라 이 필드가 없으면 서로 구분되지 않는다.
        // origin 은 저작하지 않는다 — 해저드가 만들면 언제나 DotOrigin.Zone 이다.
        public DotElement element;

        // waypoint-routing unit 4 rev 4 — runtime-only target layer snapshot.
        // HazardSO authoring leaves this 0; EffectSpawner overwrites the copied
        // value for defender-cast zones. 0 = legacy unfiltered/player-spawned zone.
        [System.NonSerialized] public byte targetTraversalLayers;
    }
}
