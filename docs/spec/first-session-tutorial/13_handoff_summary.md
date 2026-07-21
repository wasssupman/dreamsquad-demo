# 13 — 인계 요약 (units 10~12)

아웃게임 튜토리얼(`docs/spec/outgame-tutorial/`) 연계 개선. units 0~9 인계는 `5_handoff_summary.md`.

## Commit

| 해시 | 내용 |
|---|---|
| `7a704a20` | units 10~12 구현 + 스펙 |
| `aa940f08` | 코드 리뷰 반영 (인트로 소모·해제 왕복·UiLayer) |
| `649991bb` | 클래스 안내 문구를 사용자 작성본으로 되돌림 |

## Implemented

- **첫 판 각성 봉인** — `AwakeningGaugeView.SetSuppressed(bool)` seam. 버튼은 절대 위치라 자리가
  빈 채로 남고 다른 HUD 는 움직이지 않는다. 게이지 충전·덱 회수는 그대로 돌고 표시만 막힌다.
- **클래스 안내 스텝** — `CoreStep.ClassHint`. 첫 배치 직후 6줄, 탭으로 진행. 12초 만료 안전장치.
- **각성 3단계** — 전투 시작 인트로(신규 0단계) → 기존 A → 기존 B.
- **fail-open 계정의 영구 봉인 차단** — `CompleteCoreProgress()` 를 `_coreActive` 와 무관하게 호출.

## Key Files

- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — 상태머신·봉인·3단계
- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceView.cs` — `SetTapToContinue`/`ContinueTapped`
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — `_phase`/`_suppressed`/`ApplyPanelVisibility`

씬 변경 없음 — `BattleScene/FirstSessionTutorial` 의 기존 배선을 그대로 쓴다.

## Verified

- 컴파일 클린, 콘솔 에러 0.
- EditMode 1133 중 1131 통과 · 0 실패 (2 skip 은 기존 의도적 Ignore).
- **PlayMode 튜토리얼 4개 전부 통과** — 특히 `PlacementGate_HoldsCountdownUntilUnlockedStartClick`
  와 `SkipEvent_RestoresPlacementAndCompletesProfileThroughController` 가 `ClassHint` 삽입으로
  깨질 수 있던 경로다.
- 사용자 Play 확인 완료 2026-07-21.
- 코드 리뷰 1회 — CRITICAL·HIGH 없음. 탭 캐처 누수·상태머신 도달성·`_phase` 기본값·
  `CompleteCoreProgress` 파급은 안전 확인됨. MEDIUM 2건·LOW 3건 반영.

> **무관한 사전 실패 3건**: 에디터 PlayMode 40개 중 3개 실패 —
> `SelectedSavedDeck_DrivesDraws`(폴백 덱 0장), `FilledSquad_SkipsDraft_EntersPlacement` ·
> `EquippedSquad_StartSquadMatch_EndToEnd`(Placement 기대인데 Gift). 이 커밋 직전(`596191c5`)에서도
> 동일하게 실패하므로 units 10~12 와 무관하다. backlog 이관.
>
> **배치(`-nographics`)로 재면 14건으로 보인다** — 나머지 11건은 `EntitiesAssetGC` GC 타이밍에
> 터지는 Unity 패키지 내부 NRE 가 임의 테스트에 귀속된 것이다. PlayMode 판정은 에디터로 할 것.

> **테스트 커버리지 gap**: 이 변경의 신규 동작(`ClassHint` 전이·탭 캐처 수명·`SetSuppressed`)은
> 자동 테스트가 **없다.** `TutorialDragGuidanceTests` 는 레이아웃 헬퍼만 보고,
> `FirstSessionTutorialSmokeTest` 는 `UnlockTutorialStart()` 를 스스로 호출해 `ClassHint` 를
> 통과하지 않는다. EditMode 전체 통과는 회귀 확인일 뿐 이 커밋의 신호가 아니다.

## Notes

되돌리면 안 되는 것:

- **`CompleteCoreProgress()` 는 `_coreActive` 와 무관하게 호출한다.** 예전에는 `_coreActive` 뒤에만
  있어서, 안내가 fail-open 된 계정의 `firstBattleTutorialVersion` 이 영원히 0 이었다. unit 10 의
  lock 이 얹히면 **각성이 매 판 영구 봉인**된다. 선물 튜토리얼·로비 챕터 B 가 그 계정에서 영영
  발동하지 못하던 결함도 같은 뿌리다.
- **탭 캐처는 `FullBleedRoot` 아래.** `SafeAreaRoot` 쪽에 두면 Skip 을 덮어 이탈구가 사라진다.
  알파 0.35 dim 은 차단 중임을 보이는 신호이므로 투명으로 되돌리지 말 것.
- **클래스 안내 12초 만료.** 이 스텝이 `BeginStart()` 의 유일한 호출처라 탭이 유실되면 Start 잠금이
  안 풀리고 캐처가 배치까지 막아 첫 판이 Skip 외 탈출 불가가 된다.
- **0단계는 arm 하지 않는다.** `AwakeningConfig.gaugeStart` 는 SO·시트 튜너블이다.
  B 단계는 `_awakeningOfferedThisBattle`(=A 가 실제로 떴다)를 요구한다.
- **0단계는 한 프레임 미루고, 재개 후 패널 활성까지 확인한다.** `gaugeView` 가 같은 `PhaseChanged` 의
  다른 구독자라 순서가 보장되지 않고 `Pulse()` 는 비활성 패널에서 소실된다(링과 달리 복구 안 됨).
- **봉인 해제는 `OnDisable` 에서 Battle 중이 아닐 때만.** `OnDisable` 은 Battle 도중에도 발화하며,
  그때 해제하면 패널이 켜졌다 꺼지는 왕복이 생겨 앰비언트 코루틴이 되살아난다.
- **클래스 안내 문구는 사용자 작성본이다. 리뷰 지적을 이유로 고치지 말 것.**

## Follow-up

`docs/spec/README.md` → Follow-up Backlog → **첫 판 튜토리얼 개선 (first-session-tutorial units 10~12
이관, 2026-07-21)** 참조.
