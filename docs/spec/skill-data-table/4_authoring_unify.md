# 4 — 저작 한 형식 (저작 · Unity)

## 목적
카드 · 방어유닛 · 적이 같은 저작 형식으로 효과를 참조한다(계약 2). 오늘 네 저장처(`DreamcatcherCard.mechanics` · `UnitSkillAbility` · `AttackUnitData.nightmareMechanics` · 코드가 굽는 `ShieldCastAbility` — `BindingDefinitionBuilder.BakeShieldCast`)를 하나로 합친다. `SplitOnDeath` 는 소유 줄이 아니라 적 고유 값(`Enemies.split_*` — `tables.md` §8)으로 옮긴다.

## 변경 대상
- 효과 SO(`Scripts/Data/Effects/`) — unit 0 의 Effects 표 한 줄 = SO 하나(에디터에서 참조가 보인다).
- 소유 줄 형식 — 트리거 · 주체 · 게이트 · 수명 + 효과 SO 참조. 세 소유자 SO 가 같은 배열 형식을 든다.
- 카드 `host_kinds`(U5 — 기본 방어유닛) · 굽기가 그 종류마다 `EffectComboRule` 을 돌린다(`hostIsEnemy:false` 가정 제거).
- 빌더 두 개 → 소유자 종류를 모르는 한 경로.
- **출처 이름·거울 enum 은퇴**(2026-09-28): 저작 형식 이름에서 `Dc`(드림캐쳐) 접두어를 뗀다 — `DcMechanic` → 소유 줄(가칭 `SkillRule`) + 효과 SO. 트리거·효과 종류는 **코어 enum(`TriggerKind` · `TriggerPayload`)을 저작이 직접 쓴다**(`Wassup.Runtime` 이 이미 `Wassup.BattleCore` 를 참조한다) — 거울 enum(`DcTriggerKind` · `DcPayloadKind` · `DcGateKind` …) · 번역 함수(`ToCore*`) · 번호 핀 테스트 제거. 번호 순서가 달라 저장 정수가 바뀌므로 **이전 스크립트가 번호 변환**을 맡고 굽기 스냅샷 diff 0 으로 확인.
- **옛 임포터 차단 먼저**: `DcSheetApplier.OverlayMechanics`(로비 진입마다 `so.mechanics[slot].payload` 에 씀) · `Core/Dreamcatcher/DcSheetRuntimeRefresher` 의 mechanics 경로를 이 unit 첫 커밋에서 끈다(unit 5 전까지 시트가 새 형식을 모른다).
- 이전 에디터 스크립트: 기존 값 → 효과 SO(같은 값 dedupe) + 참조. **dry-run 표를 사용자에게 먼저**(효과 이름 · 묶이는 줄).
- 개사기: 자기 패턴 복사본 대신 캐논 폭격과 같은 효과를 참조할지 — dry-run 표에서 사용자 확인(값이 달라 오늘은 다른 효과).

## 완료 기준
- 이전 전후 굽기 스냅샷 값 동치 · 하드 케이스 3 신설 단언 「방어유닛이 짱쎈 도약 효과를 **같은 id 로** 소유해 발동」(자리 문제는 범위 밖 — 기존 `[Ignore]` 유지).
- EditMode Assets lane · 헤드리스(`Data/` 형 변경 → `BattleCoreUnity.Check` 거짓 빨강 가능 · 증거는 `Retire.Check`) · PlayMode Core · 골든(해시 계약 8 — 재베이크 필요 시 격리 커밋) · 리뷰 = 묶음(4–5).
