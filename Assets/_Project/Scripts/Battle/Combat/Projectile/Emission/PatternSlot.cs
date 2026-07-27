using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 3 — host 에 부착된 발사 명세 원본. 트리거 슬롯
    // (DcTriggerSlot)은 여기로의 index 만 들고, 실제 spec/template 은 이 버퍼에 산다.
    // 패턴 mechanic 이 없는 유닛은 이 버퍼가 아예 없다(기존 유닛 비용 0).
    //
    // fireCountBase 가 이 struct 의 존재 이유 중 하나다: EmitterInstance 는 트리거
    // 발화마다 생성·완주 후 제거되는 transient 라 발사 카운터를 영속시킬 수 없다.
    // 0 에서 다시 시작하면 RoundRobin 은 영원히 같은 rank(같은 방어유닛만 폭격),
    // 셔플은 hash(0) 고정(같은 대상만 저격)이 된다 — 카운터는 durable 소유자인
    // 여기 남고 인스턴스는 시드만 받는다(spec-review C2).
    [InternalBufferCapacity(1)]
    public struct PatternSlot : IBufferElementData
    {
        public PatternSpec spec;
        public ProjectileSpawnRequest template;
        public int fireCountBase;
    }
}
