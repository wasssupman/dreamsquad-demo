# 24 — 기믹 리빌 홀드 seam + 안내 한 줄

## 목적

두 번째 판 리빌이 요약까지 뜬 순간 **멈춰 세우고** 한 줄을 얹어, "이번 판 룰이 뭔지"가 아니라
**"특수 룰이 매 판 바뀐다"는 구조**를 인지시킨다. 계정당 1회. 리빌이 처음 보이는 자리가 곧
가르칠 자리다 — 첫 판은 `GimmickPhaseView` 가 리빌을 통째로 건너뛴다.

## 변경 대상

- `Assets/_Project/Scripts/UI/GimmickPhaseView.cs` — 홀드 seam
- `Assets/_Project/Scripts/Data/GimmickRevealConfig.cs` + `Data/Config/GimmickRevealConfig.asset`
  — 폴백 필드
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.GimmickReveal.cs` — **신규**
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — lifecycle 호출 2줄
- `Assets/_Project/Scenes/BattleScene.unity` — `gimmickView` → `GimmickPhaseView` **컴포넌트**
  (`!u!114` fileID 283596198, GameObject 283596197 이 아니다 — 기존 `giftView` 배선과 같은 형식)

## 구현

### GimmickPhaseView (seam)

`BeginReveal` 의 스킵 분기 **뒤에** `_tutorialMode = TutorialProgress.ShouldRunGimmickRevealHint(profileSO)`
를 캐시한다(연출 도중 재평가 없음 — 선물 unit 7 선례).

`Play()` 를 요약 노출까지의 전반과 `summaryHoldSec` + 퇴장의 후반으로 나눈다.

- **일반 모드**: 지금과 동일한 타임라인. `ChainDelay(summaryHoldSec)` → 페이드아웃 → `Finish`.
- **튜토리얼 모드**: `ChainDelay` 대신 `ChainCallback(EnterHold)` 로 시퀀스를 끝낸다.
  `TutorialHoldEntered` 발행 → 탭(또는 폴백 만료) → `TutorialHoldReleased` 발행 →
  퇴장 시퀀스 재생 → `Finish`.

공개 seam 은 `event Action TutorialHoldEntered / TutorialHoldReleased`. **홀드가 하나뿐이라
선물의 `GiftTutorialHold` 같은 enum 을 두지 않는다.** 문구는 구독자 소관이고 view 는 모른다 —
구독자가 없어도 탭 진행은 view 단독으로 동작한다.

**해제는 정확히 한 번.** 탭과 폴백 만료가 경쟁하므로 두 트리거 모두 `_holding` 가드를 거치고,
진입한 쪽이 **즉시 `_holding = false`** 로 내린 뒤 폴백 핸들을 **명시적으로 취소**한다. 없으면
퇴장 트윈이 같은 `_rootGroup.alpha` 에 두 번 걸려 깜빡이고 `TutorialHoldReleased` 가 두 번
발행된다(구독자 쪽은 멱등이라 기능 피해는 없지만 계약 위반이다). `Finish` 가 `_onDone` 을
먼저 비우는 것과 같은 형태다(`GimmickPhaseView.cs:163-172`).

**탭 재해석**: 튜토리얼 모드에서 `OnPanelTapped` 는 **홀드 중일 때만** 진행시키고 그 외 구간은
무시한다(홀드 전 탭이 연출을 통째로 날리면 읽을 것이 사라진다 — 선물과 같은 결정). 홀드 진입 시
`_startedAt` 을 갱신해 `tapSkipGraceSec` 를 오탭 debounce 로 재사용한다.

**만료 폴백**: `GimmickRevealConfig` 에 `tutorialHoldFallbackSec`(기본 20)을 추가하고, 홀드 진입 후
그만큼(unscaled) 지나면 스스로 해제한다. 리빌엔 Skip 버튼이 없고, 홀드가 안 풀리면
`ProceedToPlacement` 가 영영 안 불려 **그 판이 죽는다** — `classHintFallbackSeconds`·
`stressHintFallbackSeconds` 와 같은 목적이다. 값을 `TutorialGuidanceStyle` 에 두지 않는 이유는
의존 방향이다: 뷰는 `Wassup.UI`, 스타일 SO 는 `Wassup.UI.Tutorial` 이라 뷰가 튜토리얼을 알게 된다.
`summaryHoldSec`·`tapSkipGraceSec` 의 이웃이 제자리다.

**정리**: 기존 `Finish` 단일 출구와 `OnDisable` 경로를 그대로 유지하고, 홀드 대기 플래그·폴백
타이머도 거기서 리셋한다. 홀드 중 페이즈 이탈·재진입(`BeginReveal` 이 이전 리빌이 살아있는 채
다시 불림, `GimmickPhaseView.cs:90-91`) 시 `TutorialHoldReleased` 잔여 발행 없이 종료한다.

**`PhaseChanged` 를 구독하지 않는다 — 폴백이 그 역할을 겸한다.** 형제 뷰 `GiftPhaseView` 는
`PhaseChanged` 를 구독해 페이즈 이탈 시 시퀀스를 강제 정리하지만(`GiftPhaseView.cs:99,184`),
`GimmickPhaseView` 에는 그 구독이 없다. `SetPhase` 호출처를 전수 확인한 결과 홀드 중에
Gimmick 을 벗어나는 경로는 **리빌 자신의 콜백뿐**이다 — Tally·Result 는 전투 종료 구동
(`BattleBridge.cs:4716·4734`)이고, Draft·Battle·Gift 는 매치 진입 구동(`GameManager.cs:255·262·293`,
`GiftPhaseView.cs:132`)이며, Placement 는 이 콜백이 부르는 것이다(`PlacementPhaseView.cs:95`).
방어 구독 대신 만료 폴백을 쓰는 이유는 그것이 **모든 미지의 이탈 경로를 함께 덮기** 때문이다.
나중에 "매치 포기" 같은 임의 `SetPhase` 호출처가 생기면 이 판단을 다시 검토할 것.

### FirstSessionTutorialController.GimmickReveal.cs (신규 partial)

`Gift.cs` 와 같은 모양이다 — 이 파일이 자기 `[SerializeField] GimmickPhaseView gimmickView`,
구독/해제, 문구, 완료 저장을 **소유**한다(partial 분할 규칙). 공유 파일에는 `SubscribeGimmickReveal()`
/ `UnsubscribeGimmickReveal()` 호출 2줄만 늘린다. 미배선이면 경고 로그만 남기고 생략(fail-open).

- **Entered** → `guidance.SetMessageAnchor(MessageAnchor.GimmickReveal)` +
  `ShowMessage("매 판마다 특수 룰이 하나 걸립니다. 이번 판은 이것!", showSkip: false)`
- **Released** → `CompleteGimmickRevealProgress()`(저장은 `Complete…` 가 true 일 때만) +
  `guidance.Hide()` + `SetMessageAnchor(Default)` 원복

**`SetElevated` 를 쓰지 않는다.** guidance 기본 order(`guidanceSortingOrder 1500`)가 이미 모든
게임 UI 위다 — 리빌 패널 20 도, 선물 패널 30 도 압도한다. `Gift.cs` 의 `SetElevated(true)` 는
order 체계가 `10 ↔ 40` 이던 시절(unit 8)의 잔재이고 unit 14 의 1499/1500/1501 재번호 이후로는
사실상 no-op 이다. 그것을 "선례"로 복붙하면 원복 경로만 늘어난다.

**guidance 탭 캐처를 켜지 말 것.** 말풍선·plate·포커스 링·포인터는 전부 `raycastTarget = false`
라 리빌의 탭을 가리지 않지만(`TutorialGuidanceView.cs:171·201·218·475·491·521·530·540`),
`SetTapCatcher(true)` 가 켜는 `"TapToContinue"` 는 **전체화면 `raycastTarget = true`**
(`:265`)라 리빌의 `TapCatcher` 를 통째로 덮는다. 켜면 홀드가 **폴백 만료로만** 풀린다. 이
구간의 탭 주인은 리빌 패널이고 `ContinueTapped` 는 쓰지 않는다(클래스 안내·스트레스 정지와
소비자를 다투지 않는다).

**`SetPhase(Gimmick)` 은 반드시 홀드보다 먼저다.** `FirstSessionTutorialController.OnPhaseChanged`
는 `phase != Placement` 이면 `ResetAwakeningSession(hide: true)` → `guidance?.Hide()` 를 부르고
(`…Controller.cs:352-357` · `.Awakening.cs:120`) `Gimmick` 도 여기 걸린다. 지금 안전한 이유는
순서 하나뿐이다 — `BeginReveal` 이 `SetPhase(Gimmick)` 를 `Play()` **앞**에서 부르고
(`GimmickPhaseView.cs:106`) `SetPhase` 는 동기 발화(`GameManager.cs:89-94`)라, 그 `Hide()` 가
말풍선보다 ~2초 먼저 지나간다. **이 순서를 뒤집으면 말풍선이 뜨자마자 지워진다.**

## 완료 기준

- [x] compile clean · EditMode **1786 중 실패 0**.
- [x] 씬 배선 실측(`SerializedObject`): `gimmickView` → `GimmickPhaseView` **컴포넌트**,
      대상 GameObject active, `tutorialHoldFallbackSec = 20`.
- [ ] **두 번째 판**: 선물 홀드 2회 통과 후 리빌이 요약에서 멈추고 말풍선 한 줄이 뜬다. 탭하면
      퇴장 → 배치. 말풍선이 아이콘·룰 라벨·요약·탭힌트 어느 것도 가리지 않는다.
- [ ] **세 번째 판 이후 미노출** — 리빌이 기존 타임라인(자동 퇴장)으로 돌아온다.
- [ ] **첫 판 무변화** — 리빌 자체가 안 뜬다(`ShouldRunCore` 게이트).
- [ ] **기믹 미배정 매치 무변화** — 페이즈를 건너뛴다.
- [ ] 홀드 전 탭이 연출을 스킵하지 않는다. 방치하면 폴백 시간 뒤 자동 진행한다.
- [ ] **탭과 폴백이 겹쳐도 퇴장이 한 번만** — 알파 깜빡임 없고 `TutorialHoldReleased` 1회.
- [ ] 홀드 중 씬 이탈/Disable/재진입 후에도 다음 판이 정상 진행한다(`ProceedToPlacement` 콜백
      유실 없음 · 잔여 `TutorialHoldReleased` 발행 없음).
- [ ] **TestMode 진입 회귀** — `fastForwardInTestMode` 로 선물을 건너뛴 매치(`GiftPhaseView.cs:159`)
      에서도 리빌·홀드가 정상 동작한다(게이트를 선물에 체인하지 않은 근거의 실증).

구현: 2026-08-01. **미확인 항목은 전부 사용자 Play 확인 대기** — 위 체크 안 된 6줄이 그것이다.
`revealHintMessageTopOffset 880` 은 레이아웃 계산에서 나온 값이라 실화면 확정이 필요하다.
