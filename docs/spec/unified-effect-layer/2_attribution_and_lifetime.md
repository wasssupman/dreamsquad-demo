# 2 — 귀속과 수명을 가른다 (코어 · H3)

## 목적
「바인딩을 든 자」가 곧 「탄 소유자·발사 자리·수명」인 묶음을 푼다(계약 4 · U2 · U3). **귀속·발사 자리 = 발동 주체**, **수명 = 발동 주체 ∧ 바인딩을 든 자.** 발사 명세 버스트뿐 아니라 귀속을 채우는 모든 의도에 같은 규칙.

## 변경 대상
- `Scripts/BattleCore/Trigger/IntentApplier.cs` — 의도의 `Source` 를 산출기의 발동 주체로(unit 1 의 `EffectOrigin`). `EmitPattern` 은 인스턴스에 발동 주체 id 를 싣는다.
- 버스트 인스턴스 상태(`PatternSlotState.Instance`) — 발동 주체 id.
- `Scripts/BattleCore/Phases/CombatPhase.cs` `StepEmitters`/`AdvanceSlot` — 발사 자리·스코프 중심·스코프 몸·`Owner`·진영·공격 층을 발동 주체에서. 발동 주체가 없으면 슬롯을 닫는다(U2).
- unit 0 표 2 에서 「귀속 = 바인딩을 든 자」로 채우는 나머지 concrete.

## 구현
- 기본값: 자기 사건 트리거(발동 주체 = 바인딩을 든 자)는 값이 같다 → 오늘의 모든 저작 무변. 달라지는 것은 `Subject.Any` 바인딩뿐(남의 사건에 반응하는 규칙).
- `Subject.Any` 중 오늘 라이브인 것(스쿼드 카드 스탯 · 드림스톤 · 속도·수면 오라 등 — 표 2)은 귀속이 규칙 결과에 영향이 없는지 행마다 확인. 영향이 있으면 정지하고 질문.
- 평타 연발 슬롯(`atk.PatternSlots`) 무변.

## 완료 기준
- 탐침 둘을 U3 에 맞게 이름·단언 수정 후 해제 초록(`…귀속은_U` · 「A 와 AA 는 탄 수·대상 집합·피해 합·귀속이 같다」) · 짝 `현행_…` 뒤집거나 삭제.
- 새 테스트: 버스트 도중 발동 주체 퇴근 → 남은 발 없음 · 호스트 사망 → 남은 발 없음 · AA 탄 킬 → 발동 주체의 OnKill 발화, 호스트의 OnKill 무발화.
- 무변: 헤드리스 · 골든 11 · EditMode Core · PlayMode Core(배치 스킬 씬).
