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
- `Assets/_Project/Scenes/BattleScene.unity` — `gimmickView → GimmickPhaseView`(fileID 283596197)

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
타이머도 거기서 리셋한다. 홀드 중 페이즈 이탈 시 `TutorialHoldReleased` 잔여 발행 없이 종료한다.

### FirstSessionTutorialController.GimmickReveal.cs (신규 partial)

`Gift.cs` 와 같은 모양이다 — 이 파일이 자기 `[SerializeField] GimmickPhaseView gimmickView`,
구독/해제, 문구, 완료 저장을 **소유**한다(partial 분할 규칙). 공유 파일에는 `SubscribeGimmickReveal()`
/ `UnsubscribeGimmickReveal()` 호출 2줄만 늘린다. 미배선이면 경고 로그만 남기고 생략(fail-open).

- **Entered** → `guidance.SetMessageAnchor(MessageAnchor.GimmickReveal)` +
  `ShowMessage("매 판마다 특수 룰이 하나 걸립니다. 이번 판은 이것!", showSkip: false)`
- **Released** → `CompleteGimmickRevealProgress()`(저장은 `Complete…` 가 true 일 때만) +
  `guidance.Hide()` + `SetMessageAnchor(Default)` 원복

**`SetElevated` 를 쓰지 않는다.** guidance canvas 는 `guidanceSortingOrder 1500`, 리빌 패널은
`sortingOrder 20` 이라 이미 위다. 선물(30)에서 필요했던 것을 복붙하면 원복 경로만 늘어난다.

## 완료 기준

- [ ] compile clean · EditMode 실패 0(기준선 대비 증가 없음).
- [ ] 씬 배선 실측: `gimmickView → GimmickPhaseView`.
- [ ] **두 번째 판**: 선물 홀드 2회 통과 후 리빌이 요약에서 멈추고 말풍선 한 줄이 뜬다. 탭하면
      퇴장 → 배치. 말풍선이 아이콘·룰 라벨·요약·탭힌트 어느 것도 가리지 않는다.
- [ ] **세 번째 판 이후 미노출** — 리빌이 기존 타임라인(자동 퇴장)으로 돌아온다.
- [ ] **첫 판 무변화** — 리빌 자체가 안 뜬다(`ShouldRunCore` 게이트).
- [ ] **기믹 미배정 매치 무변화** — 페이즈를 건너뛴다.
- [ ] 홀드 전 탭이 연출을 스킵하지 않는다. 방치하면 폴백 시간 뒤 자동 진행한다.
- [ ] 홀드 중 씬 이탈/Disable 후에도 다음 판이 정상 진행한다(`ProceedToPlacement` 콜백 유실 없음).
