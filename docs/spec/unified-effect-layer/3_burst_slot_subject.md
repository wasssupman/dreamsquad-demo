# 3 — 버스트 슬롯이 발동 주체를 든다 (코어 · H3)

## 목적
발동 주체(caster)는 이미 사건 주체다(`TriggerDispatcher` 드레인 `owner = _world.Find(e.Subject)`). 귀속·자리·수명이 바인딩을 든 자에 묶인 곳은 **발사 명세 버스트 슬롯**뿐 — 슬롯이 `_binding.Emitters`(바인딩을 든 자) 목록에 들어가 `StepEmitters` 가 그 유닛 자리·그 몫으로 쏜다. 이것을 푼다(계약 4 · U2 · U3). 케이스 1 AA.

## 변경 대상
- `Scripts/BattleCore/Trigger/IntentApplier.cs` `EmitPattern` — 슬롯에 발동 주체 id(= 의도 `Source`).
- 버스트 슬롯 상태(`PatternSlotState`) — 발동 주체 id 한 필드.
- `Scripts/BattleCore/Phases/CombatPhase.cs` `StepEmitters`/`AdvanceSlot` 과 버스트 요청 조립 — 발사 자리 · 스코프 중심 · 스코프 몸(`PatternScope.FilterByReach`) · `Owner` · 진영 · 공격 층을 **발동 주체**에서. 발동 주체가 없으면 슬롯을 닫는다(U2). 바인딩을 든 자가 사라지면 오늘처럼 바인딩째 멈춘다.

## 구현
- 기본값: 자기 사건(발동 주체 = 바인딩을 든 자)은 값이 같다 → 오늘 라이브 버스트(배치 스킬 · 보스 주기 · 불나방떼) 무변. 달라지는 건 남의 사건에 반응하는 바인딩의 버스트뿐(라이브 0 — unit 0 표 1 로 확인).
- 순회는 여전히 슬롯을 든 유닛의 `SimEntityId` 순(결정론 무변). 평타 연발 슬롯(`atk.PatternSlots`) 무변.

## 완료 기준
- 탐침 둘을 U3 에 맞게 이름·단언 수정 후 해제 초록(`…귀속은_U` · 「A 와 AA 는 탄 수·대상 집합·피해 합·귀속이 같다」) · 짝 `현행_…` 뒤집거나 삭제.
- 새 테스트: 버스트 도중 발동 주체 퇴근 → 남은 발 없음 · 호스트 사망 → 남은 발 없음 · AA 탄 킬 → 발동 주체의 OnKill 발화 · 호스트 OnKill 무발화 · **캐논 폭격 탄 수 = 반경 안 적 수**(회귀 고정).
- 헤드리스 · 골든 11 · EditMode Core · PlayMode Core(배치 스킬 씬) · `core-reviewer` APPROVE.
