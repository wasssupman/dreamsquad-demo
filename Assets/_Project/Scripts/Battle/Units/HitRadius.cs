using Unity.Entities;

namespace Wassup.Battle.Units
{
    // distance-based-range unit 3 — **전투원의 몸.**
    //
    // 오늘 판정은 전부 «중심점 하나 대 중심점 하나» 다. 스프라이트가 일반 유닛의 1.89배인
    // 보스가 몸통을 눈으로 관통당해도 무판정인 이유가 그것이다. unit 4 가 사거리 자를 조이면
    // 그 어긋남이 「겹쳐 있는데 안 때린다」로 즉시 드러나므로 **전환 앞에** 둔다.
    //
    // ⚠ 이름이 `hitRadius` 가 아니라 `bodyRadius` 인 이유: `SweepHitMath.SegmentHits` 의
    // 파라미터명이 이미 `hitRadius` 이고 그 실인자는 `ProjectileData.hitThreshold`(투사체 쪽
    // 피격 반경)다. **뜻이 다르고, 소비처는 그 둘을 더한다** — 같은 이름을 쓰면 어느 쪽 반경인지
    // 읽는 사람이 매번 되짚어야 한다.
    //
    // **맥락 = Units.** 몸 크기는 `Health`·`FactionTag` 과 같은 성격의 「그 유닛이 무엇인가」다.
    // 쓰기는 스폰(Bridge) 1회, 읽기는 전부 Combat(사거리·투사체 충돌)이라 어디 둬도 동작은
    // 같지만, M1 이식 때 **어느 모듈로 가나**가 실질 질문이라 여기로 못박는다 — sim lib 에서는
    // 엔티티 정의 모듈로 가고, `IComponentData` 를 벗기면 `float bodyRadius` 필드 하나로 끝난다.
    //
    // **기본 0 = 오늘과 동일.** 저작은 이 unit 이 하지 않는다(unit 6 소관) — 여기서 값을 주면
    // `long_boss.trace.txt` 가 움직여 계약 13(units 0~3 골든 초록)이 그 자리에서 깨진다.
    public struct HitRadius : IComponentData
    {
        public float value;
    }
}
