using Unity.Mathematics;
using Wassup.Skills;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 6b — **판 위에 깔린 존 장판 하나.**
    //
    // 유닛이 아니다(체력도 공격도 없다) — `FieldCarrier` 와 같이 `BattleWorld.Units` 밖에 산다.
    // 그런데 `FieldCarrier` 와 달리 **정의표 줄을 가리킨다**: 저작이 여럿(불 장판·독 장판·
    // 감속 장판)이고 효과 배열이 그 줄에 있기 때문이다. 캐리어는 저작이 하나뿐이라(포탈·당김·
    // 아군 버프) 값이 개체에 직접 실린다.
    //
    // ⚠ **멤버십은 스냅샷이 아니다.** 매 틱 다시 판정하므로 들어온 적도 걸리고 나간 적은
    // 풀린다 — 그 「풀린다」의 시간이 `HazardEffectDef.RestDuration` 이다(F17).
    public sealed class Hazard
    {
        public SimEntityId Id;

        /// <summary>`MatchDefinition.Hazards` 의 줄 번호. -1 = 정의 없음(= 아무 일도 안 한다).</summary>
        public int DefIndex = -1;

        /// <summary>중심 칸. 판정 원점은 이 칸의 중심이고 **몸이 없다**(제약 13 — 자리에 떨어지는 것).</summary>
        public int2 OriginCell;

        /// <summary>
        /// 멤버십 반경(칸). 스폰 때 정의표 줄의 `HazardDef.RadiusTiles` 에서 한 번 굽는다.
        /// **음수 = 존 효과 없음**(F18) — 개체는 서고 수명도 돌지만 아무에게도 안 건다.
        /// </summary>
        public int RadiusTiles = HazardShapeMath.NoZone;

        /// <summary>중심 칸의 월드 좌표. 스폰 때 한 번 굽는다(장판은 안 움직인다).</summary>
        public float3 Center;

        /// <summary>남은 수명(초). 0 이하가 되는 틱에 사라지고, **그 틱에는 효과를 안 건다.**</summary>
        public float Remaining;

        /// <summary>깐 쪽. 트레이스·귀속용이고 **모디파이어 병합 키가 아니다**(아래 주석).</summary>
        public SimEntityId Source = SimEntityId.None;

        /// <summary>깐 쪽의 진영(발화 시점 스냅샷). 사건이 값으로 나른다.</summary>
        public Faction Faction;

        /// <summary>
        /// 대상 통행 층. **저작값이 아니라 런타임 스냅샷이다**(F15) — 저작은 언제나 0 이고
        /// 까는 자가 덮어쓴다. **0 = 필터 없음**이다. 저작 축으로 오해하면 장판이 아무에게도
        /// 안 먹는다.
        /// </summary>
        public byte TargetLayers;

        public void Reset()
        {
            Id = SimEntityId.None;
            DefIndex = -1;
            OriginCell = int2.zero;
            RadiusTiles = HazardShapeMath.NoZone;
            Center = float3.zero;
            Remaining = 0f;
            Source = SimEntityId.None;
            Faction = Faction.None;
            TargetLayers = 0;
        }
    }
}

namespace Wassup.BattleCore
{
    // 존 장판을 까는 **단 하나의 조립 자리.** 생산자(디버그 커맨드 · unit 7 의 카드·스킬)가
    // 전부 여기를 지난다 — 반경·수명을 정의표 줄에서 굽는 일을 두 곳에서 하면 한쪽이 언젠가
    // 모양→반경 매핑(F18)을 다시 쓴다.
    public static class HazardSpawn
    {
        /// <summary>
        /// `defIndex` 줄의 장판을 `cell` 에 깐다. 정의가 없으면 null(조용히 0 반경 장판을 세우지 않는다).
        /// `targetLayers` 는 **까는 자의 스냅샷**이다(F15) — 저작은 언제나 0 이다.
        /// </summary>
        public static Hazard Spawn(BattleWorld world, Map.MapRuntime map, MatchDefinition def,
                                   int defIndex, Unity.Mathematics.int2 cell, SimEntityId source,
                                   Wassup.Skills.Faction faction, byte targetLayers, int tick)
        {
            if (def == null || defIndex < 0 || defIndex >= def.Hazards.Length) return null;
            ref var hd = ref def.Hazards[defIndex];
            var center = map != null
                ? map.CenterOf(cell)
                : new Unity.Mathematics.float3(cell.x, 0f, cell.y);
            return world.SpawnHazard(defIndex, cell, center, hd.RadiusTiles, hd.Lifetime,
                                     source, faction, targetLayers, tick);
        }
    }
}
