using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 5 — 라스트런 지연 crash 타이머 (Effects 소유).
    // 레드불 소비 시 부착. remaining 만료 시 최대체력의 lastRunDamageFraction 만큼 데미지 인박스에 넣고 제거.
    // 공격속도 버프는 소비 즉시 StatModifier 로 별도 인큐(자체 만료) — 이 컴포넌트는 crash 만 담당.
    // 유닛 엔티티와 생명주기 동봉 (엔티티 파괴 시 함께 소멸 — 별도 정리 불요).
    public struct LastRun : IComponentData
    {
        public float remaining;
    }
}
