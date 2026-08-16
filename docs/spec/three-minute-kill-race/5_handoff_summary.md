# 5 — Handoff (units 0~4 완료)

## Commit

- `37cbb659` feat: unit 0 — 패배·강제 종료 축 은퇴
- `2b6362e9` feat: unit 1 — 1킬 1점 (killScore 티어 은퇴)
- `16ac6667` feat: units 2~4 — 마음 균열 · 유저 제출 · 결과 정리
- `6fe5ea4a` docs: 검증 결과 기록

**푸시는 승인제** — 이 문서 작성 시점 미푸시.

## Implemented

- **판을 끝내는 것이 2개다**: 3분 만료(`complete`) · 유저 제출(`submitted`). 시스템 판정
  4개(골붕괴 즉사·스트레스 상한·적 마음 붕괴·웨이브 전멸)와 만료 시 승패 비교가 전부 은퇴.
- **`MatchTally.Won` 은퇴** — 승패를 담는 자리가 코드에 없다. 결과 화면 입구도 `Show` 하나.
- **1킬 = 1점, 예외 없음.** `killScore` 축 전체 삭제(ECS 컴포넌트 · 이벤트 필드 · SO 필드 ·
  브리지 누적 · par 가중). 분열체도 1점(결정 A).
- **마음은 게이지를 안 단다.** 게이지가 **둘**이었다(전용 안정도 바 + 구조물 공통 바) —
  둘 다 처리. 빈자리는 `TilemapMapView.SetGoalCrack` 의 4단계 균열이 갖는다.
- **스트레스 배지 분모 제거**(`showLimit: false`). 튜토리얼의 「N이 되면 패배합니다」는
  기존 가드에 걸려 자동으로 빠진다.
- **햄버거 버튼이 60초(P1)를 기점으로 정체 전환**: 「나가기」(0점·로비) → 「제출」(현재 킬·결과 화면).
- **결과 화면 스탯 3줄 → 2줄**(남은 마음 / 도달 웨이브). 히어로는 `47기`.

## Key Files

- `Scripts/Bridge/BattleBridge.cs` — `EndMatch`(호출부 2곳) · `BuildTally` · `CanSubmit`/
  `SubmitMatch` · `PushGoalCrack` · `RefreshLeakHud`
- `Scripts/Core/MatchTally.cs` — `Kills` 한 축. `Total`·`SubmissionScore` 가 그것을 가리킨다
- `Scripts/Core/TilemapMapView.cs` — `SetGoalCrack` / `MarkGoalCollapsed` / `ApplyPropTint`
- `Scripts/UI/MenuPopup.cs` — `RefreshExitButton` · `OnExit` 의 제출 분기
- `Scripts/UI/ResultScreen.cs` — `Show(MatchTally)` 단일 입구
- 정본: `docs/reference/score-formula.md`(전문 재작성)

## Verified

- 컴파일 0 에러
- **EditMode 2435/2435 완주 · 신규 실패 0건.** 실패 5건은 전부 사전 실패
  (`MultiGoalPoolSeparationTests` 4 = map-rework · `WhirlpotAuthoringTests` = 인프라)
- PlayMode `TallyFlowTest` · `GoalStabilityTest` ·
  `StructureLivePlayTest.SiegeMap_DefendersBreakEnemyCore_...` 초록
- **Play 육안 확인 완료(2026-08-16)** — 각 unit 문서 완료 기준 참조

## Notes (되돌리면 안 되는 의도)

- **`EndMatch` 를 부르는 코드를 새로 만들지 말 것.** 만료·제출 둘뿐이고, 세 번째가 생기는
  순간 패배 조건이 부활한 것이다. 이것이 이 spec 의 단일 최상위 계약이다.
- **등급으로 점수를 가르는 필드를 되살리지 말 것.** 강함의 차이는 체력·공격력·등장 빈도로.
  `WaveKillBudgetPinTests` 의 「보스 > 잡몹」 단언을 되살리는 것이 그 신호다.
- **문이 뚫리기 전에는 유출이 0이다.** 도달한 적은 마음을 때리며 살아 있어 아직 잡을 수 있다.
  이 구조에 기회비용 모델(«못 잡은 적 = 못 번 점수»)이 걸려 있으므로 바꾸지 말 것.
- **`GamePhase` enum 값 고정.** `CameraDirectionConfig.asset` 이 정수로 직렬화한다.
  `Tally` 는 판정이 없어져도 남긴다(전투 HUD 게이팅).
- `MenuPopup` 이 브리지를 `FindFirstObjectByType` 으로 찾는 것은 **의도된 예외**다 —
  씬이 다른 세션 WIP 를 물고 있어 저장을 피했다. 씬이 깨끗해지면 SerializeField 로 승격.
- **엔드리스는 스스로 안 끝난다**(`_timerDuration <= 0`). 유저 제출이 유일한 종료다.

## Follow-up

1. **엔드리스 모드의 정체** [M] — 「시간 고정 + 패배 없음」이 되면서 차이가 «시간 무제한»
   하나로 줄었다. 존치 여부부터.
2. **몽마의 계약 코스트 재지정** [M] — 스트레스 한계 표기가 사라져 «허용치 선불» 이 완전히
   공짜다. unit 2 의 스트레스 표기가 «일단은» 인 이유.
3. **적 마음(공성 맵)의 새 역할** [M] — 부숴도 판이 안 끝나면 «사격이 멎는 것» 만 남는다.
4. **제출 개방 인지** [S] — 60초가 지난 걸 유저가 어떻게 아는가(햄버거 배지 등).
5. **조기 제출의 동기** [S] — 무페널티라 3분을 안 쓸 이유가 지금은 없다.
6. **해몽(서사 회수)** [L] — 마음이 겪은 일을 판 후 서사로. 별도 spec.
7. `TallyFlowTest` 가 **배치 실행에서** PrimeTween 로그 누출로 빨개진다(단독은 통과).
   이 spec 범위 밖 — 시퀀스 teardown 소관.
