using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-use-flow unit 3 — Combat→Bridge 부착 카드 발동 신호 채널.
    // 부착 카드의 트리거가 "실제로 일한" 프레임을 프레젠테이션에 알린다 — 조건부 카드
    // (궁지폭발·처형타 등)는 이 신호 없이는 조건 충족 여부를 영영 알 수 없다.
    //
    // 생산자: AttackSystem 의 AttackN 계열 발동 3지점(RESOLVE / 폭탄 발사 훅 / 캐스트
    // 드레인 — DcTriggerSlot 의 "owned write 세 지점"과 동일). 발동 = 카운터 소비 성사
    // 프레임이며 payload arm/대상 유무와 무관하게 신호한다(카운트가 소비됐다는 사실이
    // 곧 사건). 소비자: BattleBridge 드레인 → 머리 위 아이콘 행 펄스.
    //
    // 귀속 = host 단위(유닛 행 전체 펄스, 사용자 결정 2026-07-29) — instanceId↔entryId
    // 매핑은 recall registry 후속 spec 이라 카드 정밀 귀속은 그때 확장한다.
    // 큐 lifecycle 은 BattleBridge 소유(생성 Persistent / 싱글턴 파괴 / Dispose 3점 세트).
    public struct DcTriggerFiredEvent
    {
        public Entity host; // 발동한 슬롯이 부착된 디펜더
    }

    public struct DcTriggerFiredEventsSingleton : IComponentData
    {
        public NativeQueue<DcTriggerFiredEvent> queue;
    }
}
