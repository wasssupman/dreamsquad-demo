# 9 — Squad 카드 호스트 바인딩 (rev 5, 2026-07-10 사용자 결정)

## 목적

Squad 카드의 "사용 즉시·매치 영구·무한 스택" 밸런스를 깬다: **효과는 여전히 스쿼드 전체(축 매칭, 현재+미래 유닛)에 적용되지만, 소유 주체는 부착된 호스트 유닛**이고 호스트가 죽으면 ① 스쿼드 효과가 소멸하고 ② 카드가 큐 맨 뒤로 회수된다(Unit 카드와 동일 순환).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 핸들 발급 적용/철회 API
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — CommitSquad(host) + 사망 철회
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — Squad 도 유닛 타겟 UX(화살표)

## 계약

1. **철회 = 중립화 재적용** (Effects 맥락 무변경): 머지 키 `(source, stat, op, stackId)` 에 `magnitude=new` 갱신 규칙을 이용, 같은 stackId 로 **배율 1.0** 을 재적용해 무력화. 슬롯은 매치 끝까지 inert 로 남음(teardown 이 정리). 신규 채널/시스템 0.
2. **bridge API**: `ApplyDreamcatcherCardHosted(card) → int handle` (효과·워밍업 엔트리에 handle 기록, 적용 없으면 -1) / `RevokeDreamcatcherEffects(handle)` (현재 축 매칭 유닛 전원 중립화 + `_activeDcEffects`/`_activeWarmups` 에서 제거 → 미래 유닛 상속 중단). 구 `ApplyDreamcatcherCard`(무호스트, 매치 영구)는 dormant 컨트롤러 호환용으로 유지.
3. **순환 = Unit 과 동일**: CommitSquad 는 `deck.UseUnit`(아웃풀), 레지스트리 `entryId → (host, handle)`. 호스트 사망 → revoke + `deck.Recover`. Unit 카드 엔트리는 handle=-1(슬롯이 엔티티와 함께 소멸, 철회 불요).
4. **UX**: Squad 카드도 화살표 조준 + 유닛 몸체 드롭(anywhere touchup 폐지). 부착 캡 `maxAttachPerUnit` 은 Unit+Squad **합산 공유**. 호스트의 축 제약 없음(예: Ranger 버프를 Guardian 에 호스팅 가능 — 소유 주체와 효과 대상은 별개 개념).
5. **워밍업(느린각성)**: revoke 시 미래 배치 워밍업 상속 중단. 이미 진행 중인 대기 cooldown 은 자연 소진(철회 불요).
6. **CostRate**: Squad 카드 소비처 없음 확인(2026-07-10) — N/A.

## 완료 기준

- [ ] Squad 카드 드래그 → 화살표 → 유닛 드롭 → 축 매칭 전 유닛 버프 + 카드 아웃풀(손패 축소).
- [ ] 호스트 사망 → 스쿼드 버프 소멸(적용 유닛들 스탯 원복) + 카드 큐 맨 뒤 복귀 + 각성 +4.
- [ ] 호스트 생존 중 신규 배치 유닛은 버프 상속, 호스트 사망 후 배치 유닛은 미상속.
- [ ] 필드 빈 곳 드롭 = 취소(무차감). 기존 Unit/Active 경로 무회귀.

> 확인 2026-07-10 — 커밋 96c6ac3d (사용자 Play 확인 · 리팩토링 f81040a4 포함)
