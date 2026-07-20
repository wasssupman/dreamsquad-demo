# 7 — GiftPhaseView 홀드 seam + 첫 판 연출 억제

## 목적

선물 연출을 데이터 변경 없이 두 지점에서 무기한 홀드할 수 있게 시퀀스를 분할하고, 첫 판(핵심 안내
pending)에는 연출을 통째로 생략한다. 덱 구성(`BuildGiftDeck`)은 어느 경우에도 동일 — 연출만 다르다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs`
- `Assets/_Project/Scenes/BattleScene.unity` — `profileSO` 참조 배선(unit 9 에서 당김 —
  이 unit 의 Play 검증에 필수)

## 구현

**첫 판 억제**: `PlayerProfileSO profileSO`를 SerializeField 로 추가한다. `OnGiftDeckReady`의 기존
skip 분기(`giftConfig == null`, `fastForwardInTestMode`)와 같은 자리에서
`TutorialProgress.ShouldRunCore(profileSO)`이면 연출 없이 `ProceedToPlacement()`. profileSO 미배선
/ 직접 Play(`IsLoadedThisSession=false`)는 기존 연출 그대로(fail-open).

**튜토리얼 모드**: 시퀀스 시작 시 `TutorialProgress.ShouldRunGiftTutorial(profileSO)`를 평가해
`_tutorialMode`에 캐시한다(연출 도중 재평가 없음).

**시퀀스 분할**: `PlayFromRevealFocus`(리빌홀드→스택→리플→부채꼴→흡수)를 두 메서드로 나눈다.

- `PlayStackConverge()` — 스택 수렴(④-a)까지. 일반 모드는 진입 전 `ChainDelay(revealHoldSec)` 유지.
- `PlayShuffleToAbsorb()` — 리플 셔플(④-b)→부채꼴→흡수→`ProceedToPlacement`.

일반 모드는 두 메서드를 `ChainCallback`으로 이어 기존과 동일한 타임라인을 유지한다. 튜토리얼
모드의 흐름:

1. 리빌 포커스 도달(전반부 종료) → `revealHoldSec` 딜레이 없이 **홀드 1**. `TutorialHoldEntered(Reveal)` 발행.
2. 탭 → `TutorialHoldReleased(Reveal)` 발행, `PlayStackConverge()` 재생.
3. 스택 수렴 완료 → **홀드 2**. `TutorialHoldEntered(Shuffle)` 발행.
4. 탭 → `TutorialHoldReleased(Shuffle)` 발행, `PlayShuffleToAbsorb()` 재생(셔플 연출 시작).

**공개 seam**: `enum GiftTutorialHold { Reveal, Shuffle }`(nested),
`event System.Action<GiftTutorialHold> TutorialHoldEntered / TutorialHoldReleased`. 문구 표시는
구독자(unit 8) 소관 — view 는 문구를 모른다. 구독자가 없어도 탭 진행은 view 단독으로 동작한다.

**탭 재해석**: `_tutorialMode`에서 `OnPanelTapped`는 홀드 중일 때만 진행시키고, 그 외 구간에선
무시한다(기존 탭 스킵 비활성 — 2026-07-20 사용자 결정). 홀드 진입 시 `_seqStartTime`을 갱신해
`tapSkipGraceSec`를 오탭 debounce 로 재사용한다.

**정리 경로**: `StopSequence`/`OnPhaseChanged(≠Gift)`/`OnDisable`에서 홀드 상태(`_tutorialMode`,
대기 플래그)를 리셋한다. 홀드 중 페이즈 이탈 시 이벤트 잔여 발행 없이 종료.

## 완료 기준

- [x] compile clean.
- [ ] gift 튜토리얼 조건이 아니면(신규 첫 판 제외) 연출 타임라인·탭 스킵이 기존과 동일하다.
- [x] core pending 프로필: Gift 페이즈 진입 즉시 배치로 — 패널/연출 미노출, 덱은 12장 정상. (사용자 run1 확인)
- [ ] 튜토리얼 모드: 리빌 포커스·셔플 직전 2회 홀드, 각 홀드는 탭으로만 해제된다. (문구는 unit 8 로 검증)
- [ ] 홀드 외 구간 탭이 스킵을 유발하지 않는다.
- [ ] 홀드 중 페이즈 이탈/Disable 에서 시퀀스·FX 가 잔류 없이 정리된다.

구현: 2026-07-20 · 진단 로그로 튜토리얼 모드 진입 확정 후 로그 제거. 홀드 시각 검증은 unit 8 문구로.
