# 2 — 상시 빨강 청산 (빨강 = 회귀 신호 복구)

## 목적

"의도적 빨강"과 방치된 사전 실패를 청산해 **실패 = 회귀** 라는 신호를 복구한다.
빨강이 평상시 상태면 모든 세션이 기지 실패 목록을 외워서 빼고 판정해야 하고,
그 목록을 모르는 세션은 진짜 회귀를 놓친다.

## 변경 대상

### A. EditMode — MultiGoalPoolSeparationTests 4건 (사용자 결정: 권고안)

`EditModeAssets/MultiGoalPoolSeparationTests.cs` — Coil/Twin/Spiral/Zig 의 근접
차단칸 비율(≥40%) 단언이 map-rework unit 8~12 재저작 대기로 "의도적 빨강".

- **pending 목록** (`PendingMeleeRework`) 도입: 목록에 있는 맵은 choke/width2
  단언만 건너뛴다 — 골 1개·광장·연결성(flood) 단언은 **계속 산다** ([Ignore] 로
  테스트 전체를 끄면 이것까지 죽는다).
- **래칫 테스트** 추가: pending 맵이 실제로는 계약을 통과하면 빨강 — "재저작이
  끝났으니 목록에서 빼서 계약을 재무장하라"는 지시가 메시지에 담긴다. 목록이
  조용히 썩는 것을 막는다.

### B. PlayMode — 기록된 사전 실패 13건 (docs/spec/README.md 2026-07-31)

gift-phase-removal 등이 그 후 커밋되어 **현재 상태 재측정이 선행**된다. 재측정
후 분류 처리:

| 분류 | 대상 (2026-07-31 기준) | 처리 |
|---|---|---|
| stale (원인 확정) | DreamcatcherDeckCarryInTest — 폴백 덱 의도 제거 | 기대값 갱신 (0장) |
| stale 의심 | SquadCarryInSmokeTest · DreamstoneCarryInSmokeTest — Gift 페이즈 기대 | gift-phase-removal 이후 재확인 → 통과면 종결 |
| 환경 의존 | AuthE2ETest — dev 서버 계정 중복키 | `[Explicit]` 로 기본 제외 (명시 선택 시만 실행) |
| 순서 의존 | SceneTransitionSmokeTest · BountyMarkTest — 격리 통과 | 재측정으로 현황 확인, 원인 조사는 규모에 따라 백로그 |
| 격리에서도 실패 | DragCancelZone · DreamcatcherCursedRelic · DreamCocoon · DreamcatcherEffect(2) · PlacementAura(3) | 재측정 → 증거 수집 → 즉석 수정 가능한 stale 만 처리, 제품 버그 의심은 증거와 함께 백로그 |
| Unity 내부 NRE | EntitiesAssetGC (batch 전용) | 처리 없음 — 에디터 실행 판정 원칙 유지 |

### C. 문서 — docs/spec/README.md 사전 실패 절 갱신

재측정 결과로 "PlayMode 사전 실패" 절을 현재 상태로 고쳐 쓴다.

## 구현

1. A 구현 → Assets lane 초록 확인 (155+1건 전부).
2. PlayMode 전체 재측정 (에디터 실행 — 배치 금지 원칙 유지).
3. 재측정 증거로 B 분류 처리 → PlayMode 재실행으로 확인.
4. C 문서 갱신, 한 커밋.

## 완료 기준

- [x] Assets lane 전부 초록 — **161/161 통과** (MultiGoal pending 목록 + 래칫 6케이스 포함)
- [x] PlayMode 재측정 기록 — docs/spec/README.md 사전 실패 절 갱신 (144건, 2회 실행 대조)
- [x] stale 확정분 기대값 갱신으로 초록 — DeckCarryIn(폴백 0장) · CursedRelic·DreamCocoon
      (거부 경고 «already has {kind} state» 동기). AuthE2E 는 [Explicit] 로 기본 제외
- [x] 남는 빨강 9건 전부 원인 분류 + 다음 행동 기록 (README 사전 실패 절):
      PlacementAura 3(스톤 +1.2% 오염 가설) · SceneTransition(순서 의존) · DragCancelZone
      (기하 stale/버그 판별 대기) · DropDismount(신규·증상 불안정 — 우선 조사) ·
      PrimeTween OnComplete 2(gift-phase-removal 후속 라우팅) · BossLullabyLive(flaky 계측)

2026-08-16 구현 + 기계 검증 완료. PlayMode 실패 12 → 9 (해소 4 · 신규 분류 9).
