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
    }
}
