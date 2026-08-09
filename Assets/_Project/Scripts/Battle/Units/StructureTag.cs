using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // battle-structures unit 4 — 거점 엔티티 표식. 모든 거점(방어 마음 = 현행 골 타워 ·
    // 본능 · 적 마음)이 단다. unit 0 이 «실소비처가 생길 때 만든다» 고 미룬 그 태그다 —
    // 첫 소비처 = teardown(타입 기반 파괴)과 브리지 등록부 스폰.
    //
    // cell/faction 을 여기 싣는 이유: 붕괴 감지(ⓐ)가 «사라진 엔티티» 를 다루므로, 죽고
    // 나서는 어느 셀의 무엇이었는지 엔티티에게 물을 수 없다. 브리지 등록부가 스폰 시점
    // 값을 보관하고, 이 컴포넌트는 sim 쪽 소비자(unit 5 본능 공격 등)가 같은 정보를
    // 엔티티에서 직접 읽을 수 있게 한다.
    //
    // GoalTowerTag 는 존치 — 패배 판정과 기존 쿼리(투사체 defender 풀 등)가 그 부재/존재를
    // 읽는다. 방어 마음 = GoalTowerTag + StructureTag 둘 다, 본능·적 마음 = StructureTag 만.
    public struct StructureTag : IComponentData
    {
        public int2 cell;
        public Faction faction;
    }
}
