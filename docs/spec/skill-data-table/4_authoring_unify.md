# 4 — 저작 한 형식 (저작 · Unity)

## 목적
카드 · 방어유닛 · 적이 같은 저작 형식으로 효과를 참조한다(계약 2). 오늘 세 저장처(`DreamcatcherCard.mechanics` · `UnitSkillAbility` · `AttackUnitData.nightmareMechanics`)를 하나로 합친다.

## 변경 대상
- 효과 SO(`Scripts/Data/Effects/`) — unit 0 의 Effects 표 한 줄 = SO 하나(에디터에서 참조가 보인다).
- 소유 줄 형식 — 트리거 · 주체 · 게이트 · 수명 + 효과 SO 참조. 세 소유자 SO 가 같은 배열 형식을 든다.
- 카드 `host_kinds`(U5 — 기본 방어유닛) · 굽기가 그 종류마다 `EffectComboRule` 을 돌린다(`hostIsEnemy:false` 가정 제거).
- 빌더 두 개 → 소유자 종류를 모르는 한 경로.
- 이전 에디터 스크립트: 기존 값 → 효과 SO(같은 값 dedupe) + 참조. **dry-run 표를 사용자에게 먼저**(효과 이름 · 묶이는 줄).
- 개사기: 자기 패턴 복사본 대신 캐논 폭격과 같은 효과를 참조할지 — dry-run 표에서 사용자 확인(값이 달라 오늘은 다른 효과).

## 완료 기준
- 이전 전후 굽기 스냅샷 값 동치 · 하드 케이스 3 신설 단언 「방어유닛이 짱쎈 도약 효과를 **같은 id 로** 소유해 발동」(자리 문제는 범위 밖 — 기존 `[Ignore]` 유지).
- EditMode Assets lane · 헤드리스 · core-reviewer.
