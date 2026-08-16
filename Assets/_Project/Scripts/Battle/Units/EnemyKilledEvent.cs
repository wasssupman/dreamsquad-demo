using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal: an enemy (AttackUnitTag) was killed by
    // damage this frame (HP crossed zero). Enemies that reach the goal go through a
    // separate path (UnitLifecycleSystem) and do NOT emit this. position = dead
    // enemy LocalTransform.Position (reserved for future kill-location flourishes).
    //
    // score-tally-sequence unit 0 — HUD 는 더 이상 count 만 쓰지 않는다. 아래 killScore
    // 를 그대로 표시 점수에 더한다(= 최종 점수의 킬축과 같은 값).
    public struct EnemyKilledEvent
    {
        public float3 position;
        // dreamcatcher-awakening-hand unit 1 — awakening granted by this kill,
        // copied from the enemy's baked AwakeningReward at enqueue time (the
        // entity is destroyed before the bridge drains). Appended last; 0 when
        // the component was absent.
        public int awakeningReward;
        // subconscious-curse-expansion unit 2 (살찌운 제물) — the killed entity.
        // 드레인 시점엔 이미 파괴됐을 수 있으므로 **등록부 키로만** 쓴다(역참조 금지;
        // Entity 값 비교는 파괴 후에도 유효). 표식(bounty mark) 카드 회수 귀속용.
        // Appended last.
        public Entity entity;

        // three-minute-kill-race unit 1 — `killScore` 는 제거했다. **이벤트 1건 = 1점**이라
        // 실을 값이 없다. `awakeningReward` 는 남는다(각성치는 여전히 적별로 다르다) —
        // 나란히 있던 두 축의 대칭이 깨지는 것이 이 unit 의 요점이다.

        // dreamcatcher-content-3 unit 3 (시체폭발) — killer 의 OnKill×SelfTileAoe 첫 매칭
        // 슬롯을 킬 시점에 스탬프(DefenderDeathEvent.hasOnDeathAoe 선례 — 드레인 시점엔
        // 슬롯을 못 읽는다). 실행 = 브리지 드레인의 SkyFall×TileAoe(flightTime 0).
        // killer 는 폭발 데미지 귀속용(owner) — 폭발발 킬도 OnKill 을 재발동시키는 연쇄가
        // 사양이다(flat 데미지 + 반경으로 자연 제동). Appended last; 기본값 = 무폭발.
        public bool hasKillBurst;
        public float burstDamage;
        public int burstTileRange;
        public int burstDataIndex;
        public Entity killer;
    }
}
