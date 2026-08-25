using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Marker + meta for blocking hazards. Spawn owns writes; runtime systems read.
    public struct BlockingHazard : IComponentData
    {
        public int hazardSoIndex;
        public float maxHp;

        // bomb-barrel-on-place unit 0 — 「부서지면 터진다」의 런타임 사본. sim 은 SO 를 못
        // 읽으므로 스폰이 실어 둔다(spec 계약 5). explodeDamage 0 = 폭발 없음.
        // explodeDataIndex 는 브리지가 SO→index 로 풀어 넘긴 폭발 탄 인덱스다.
        public float explodeDamage;
        public int explodeTileRange;
        public int explodeTargetCap;
        public int explodeDataIndex;

        // unit 9 — 초당 스스로 닳는 체력. 0 = 안 닳음.
        // 시한(unit 1, 은퇴)과 다른 점: **문이 하나로 유지된다.** 노후화는 별도의 죽음 경로가
        // 아니라 그냥 피해라서, 죽음도 폭발도 「부서짐」 하나로 나간다(계약 4).
        public float decayPerSec;
    }
}
