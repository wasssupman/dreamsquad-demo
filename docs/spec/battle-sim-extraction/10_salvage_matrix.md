# 10 — salvage 판정표 (conform / adapt / rewrite / discard)

## 목적

"설계는 백지, 실행은 스트랭글러"(ADR D6)의 실행 절반. 모듈 단위 ≈60건을 4등급으로 판정해 이식 작업량·순서·위험을 수치화한다. 11+ 구현 unit 분해가 이 표를 직접 인용한다.

## 변경 대상

- 신규 `docs/spec/battle-sim-extraction/m1_salvage_matrix.md`

## 구현

판정 대상 3군:

1. **시스템 44** (맥락별) — 각 행: 시스템 · 등급 · 근거 1줄 · 청사진 ②③ 참조 셀. 순수 계산 유틸(Targeting 랭킹·ModifierMath·TileAoe 등 제약-10 함수들)은 conform 유력, Burst/ECS 관용구 시스템 몸체는 adapt/rewrite 판정.
2. **채널 27** — 출력 18(unit 4 manifest)은 semantic/presentation 판정과 함께, 내부 9는 phase 함수 호출로 접히는지(discard 후보) 판정.
3. **Bridge 서브시스템** — 파셜·책임 클러스터 단위(웨이브 스케줄·승패/타이머·배치 규칙·코스트·점수·드림캐쳐 파셜(64KB)·해저드/픽업 스폰·debug 메뉴·뷰 sync/드레인 군). `_em.` 305 지점의 클러스터 귀속 집계 포함.

**선행 머지 3건**(설계 정본 M1-4 — 적출 전에 Bridge 를 가볍게): 비주얼 statics 분리 · `GetStackThresholds` 의존 역전(유일한 sim→Bridge 프로덕션 결합, `StackModifierTickSystem.cs:90`) · DebugMenu 퇴거. 각각의 대상 파일과 등급을 이 표에서 확정한다.

테스트도 판정한다: Entities 참조 테스트 74 중 World-조립 ≈38 은 "어서션만 salvage, 골격 재작성"(설계 정본 M1-5) — 파일 단위 목록화.

## 완료 기준

- ≈60건 전수 판정 + 등급별 집계표(각 등급 건수·예상 규모).
- 선행 머지 3건의 대상 파일 확정.
- 11+ unit 분해 초안이 이 표의 등급 집계에서 도출 가능함을 마지막 섹션에서 시연(다음 unit 후보 나열).
- 코드 변경 0.
