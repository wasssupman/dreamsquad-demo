using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 6b2 — **판 위에 떨어진 사직서 한 장.**
    //
    // ⚠ **유닛이 줍지 않는다.** 판 위에 쌓이고 **전역 누적 수**가 임계에 닿을 때만 소모된다 —
    // 소비 주체가 유닛인 레드불(`Pickup`)과 뜻이 달라 별개 개체다(옛 전투도 그랬다).
    // 수명도 없다 — 임계 말고는 사라지는 길이 없다.
    public sealed class Resignation
    {
        public SimEntityId Id;

        /// <summary>떨어진 칸. 사망한 방어유닛의 자리(드랍 계기는 unit 7 의 사망 seam).</summary>
        public int2 Cell;

        public float3 Center;

        /// <summary>떨어뜨린 자(트레이스·뷰용). 그 개체는 곧 사라진다 — 되묻지 말 것.</summary>
        public SimEntityId Source = SimEntityId.None;

        /// <summary>떨어뜨린 자의 진영(발화 시점 스냅샷).</summary>
        public Faction Faction;

        public void Reset()
        {
            Id = SimEntityId.None;
            Cell = int2.zero;
            Center = float3.zero;
            Source = SimEntityId.None;
            Faction = Faction.None;
        }
    }

    // 사직서를 떨어뜨리는 **단 하나의 조립 자리.** 생산자는 디버그 커맨드와 unit 7 의 사망 seam 이다.
    // 같은 칸에 여러 장이 겹쳐도 된다(옛 드랍도 중복 검사가 없었다 — 셀 수가 곧 규칙이다).
    public static class ResignationDrop
    {
        public static Resignation At(BattleWorld world, MapRuntime map, int2 cell,
                                     SimEntityId source, Faction faction, int tick)
        {
            if (world == null) return null;
            if (map != null && !map.Snapshot.InBounds(cell)) return null;
            var center = map != null ? map.CenterOf(cell) : new float3(cell.x, 0f, cell.y);
            return world.DropResignation(cell, center, source, faction, tick);
        }
    }
}
