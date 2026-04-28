# 7. DraftView 오케스트레이터

## 목적

`DraftView` 를 슬림 오케스트레이터로 재작성한다. `DraftController.DraftStarted` 한 번에 strip unroll → 2초 dwell + 사용자 입력 race → strip roll → 카드 fan 입장 → 폐기 입력 수신 → 3장 폐기 시 toss 완료 후 자동 confirm 까지. **재진입(rapid Redraft) 가드 + toss 완료 대기 후 confirm + PlayMode 테스트 인프라 생성** 까지 본 task 책임.

## 변경 대상

- `Assets/_Project/Scripts/UI/Draft/DraftView.cs` (재작성)
- 신규 (사전): `Assets/_Project/Tests/PlayMode/` 디렉터리 + `Wassup.Tests.PlayMode.asmdef`
- 신규: `Assets/_Project/Tests/PlayMode/DraftFlowSmokeTest.cs`
- (씬) DraftView GameObject 의 자식 구성: WavePatternStripView, DraftCardFanView, MapSettingsPanelView

## Compile-safe 진행 순서 (M5 대응)

본 task 는 4/5/6 모두 의존하므로 다음 순서로 단계별 컴파일 검증:
1. 옛 `Assets/_Project/Scripts/UI/DraftView.cs` 를 `_DraftView_legacy.cs` 로 임시 rename + 클래스 이름도 임시 변경 (또는 `[Obsolete]` 플래그). 컴파일 보존.
2. 새 `Assets/_Project/Scripts/UI/Draft/DraftView.cs` 작성 → 컴파일 PASS 확인.
3. 씬에서 옛 DraftView GameObject 를 새 컴포넌트로 교체 + WavePatternStripView/DraftCardFanView/MapSettingsPanelView 자식 wiring (UnityMCP 자동화).
4. 옛 `_DraftView_legacy.cs` + `.meta` 삭제.
5. PlayMode asmdef + smoke test 추가.

## 구현

1. PlayMode 테스트 인프라 사전 생성 (H3):
   - 디렉터리: `Assets/_Project/Tests/PlayMode/`.
   - asmdef: `Wassup.Tests.PlayMode.asmdef`. 내용:
     - `references`: `Wassup.Runtime`, `PrimeTween` (asmdef 이름은 task 0 에서 확정), `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `nunit.framework.dll`.
     - `optionalUnityReferences`: `["TestAssemblies"]`.
     - `includePlatforms`: `[]` (모든 플랫폼).
     - `defineConstraints`: `["UNITY_INCLUDE_TESTS"]`.
2. `DraftView` 직렬화 필드:
   - `DraftController controller`
   - `WavePatternStripView strip`
   - `DraftCardFanView fan`
   - `MapSettingsPanelView mapSettings`
3. sub-state enum: `Idle, Unrolling, Dwelling, Rolling, Drafting, Confirming`. 초기 `Idle`.
4. 라이프사이클:
   - `OnEnable`: `controller.DraftStarted += OnDraftStarted; controller.DraftConfirmed += OnDraftConfirmed; strip.OnDwellInterrupt += OnDwellInterrupt;`
   - `OnDisable`: 모두 해제. 진행 중 시퀀스 cleanup (`KillAllTweens()`).
5. 재진입 가드 (H1) — `OnDraftStarted` 진입부:
   ```
   if (State != State.Idle && State != State.Confirming)
   {
       Tween.StopAll(this);       // 오케스트레이터 발생 트윈
       Tween.StopAll(strip);      // strip 발생 트윈
       Tween.StopAll(fan);        // fan 발생 트윈 (입장/재배치/toss 포함)
       CancelDwellTimer();
       fan.CancelInProgress();    // fan 측 카드 GameObject 별 정리 (필요 시 카드 단위 StopAll)
       strip.SnapHidden();         // strip 강제 Hidden, scaleX=0
       State = State.Idle;
   }
   ```
   PrimeTween 의 `Tween.StopAll(object onTarget)` 가 해당 target 으로 시작된 모든 트윈을 즉시 정지 (task 0 smoke 확정 API).
6. 정상 시퀀스:
   ```
   State = Unrolling;
   strip.RebuildFromDeck();
   fan.Build(controller.Session.Pool);
   strip.SetToggleEnabled(false);
   await strip.Unroll();          // 0.45s
   State = Dwelling;
   await DwellOrInput(2.0f);      // 2초 또는 OnDwellInterrupt
   State = Rolling;
   await strip.Roll();            // 0.35s
   strip.SetToggleEnabled(true);
   State = Drafting;
   await fan.PlayEnterSequence(); // ~0.85s
   // 이후 fan 카드의 Discarded 이벤트로 진입
   ```
7. `DwellOrInput(2.0f)` 패턴 (코루틴 권장):
   - 시작 시 `bool _dwellResolved = false;`
   - `var delayTween = Tween.Delay(this, 2.0f, () => _dwellResolved = true);` — `Tween.Delay(target, duration, onComplete)` 형태.
   - `void OnDwellInterrupt() { if (!_dwellResolved) { _dwellResolved = true; delayTween.Stop(); } }`
   - 코루틴 측: `while (!_dwellResolved) yield return null;`
   - 또는 async/await 버전: `TaskCompletionSource<bool>` 한 개 + 두 곳에서 `TrySetResult` 호출.
8. 폐기/자동 confirm (H2):
   - fan 의 카드 `Discarded += OnCardDiscarded` 구독은 `fan.Build` 직후.
   - `OnCardDiscarded(card)`:
     ```
     if (!controller.Session.ToggleDiscard(card.Unit)) {
         // cap 초과 → 카드 정위치 복귀는 task 6 의 fan view 측 처리
         return;
     }
     bool isLast = controller.Session.IsFull;
     var tossSeq = fan.PlayDiscardCard(card);
     fan.LayoutRemaining();
     if (isLast) {
         State = Confirming;
         tossSeq.OnComplete(() => controller.TryConfirm());
     }
     ```
     **toss 시퀀스 완료 후에만 `TryConfirm` 호출** → `OnDraftConfirmed` 의 SetActive(false) 가 트윈 도중에 발생하지 않음.
9. `OnDraftConfirmed` 핸들:
   - `Tween.StopAll(this); Tween.StopAll(strip); Tween.StopAll(fan);` 후 strip/fan/mapSettings GameObject SetActive(false). State = Idle. — toss 완료 후이므로 안전.
10. PlayMode smoke `DraftFlowSmokeTest`:
    - 테스트 씬 setup: 코드로 빈 GameObject 들에 DraftController + DraftView + 자식 컴포넌트 + Mock catalog 10 + Mock AttackDeck.
    - 시나리오:
      - `controller.BeginDraft(seed: 12345)` 호출.
      - `yield return new WaitForSeconds(2.0f + 0.45f + 0.35f + 0.85f + 0.1f)` — Drafting 도달 대기.
      - 헬퍼 `fan.SimulateClick(int index)` 또는 `fan.GetCard(index).Discarded?.Invoke(...)` 로 3장 폐기 트리거.
      - 마지막 toss 완료 대기: `yield return new WaitForSeconds(0.5f)`.
      - 검증:
        - `controller.DraftConfirmed` 1회 발화 (이벤트 카운터)
        - `BattleBridge.SetDefenderPool` 마지막 호출의 길이 = 7
        - `controller.Session.PickedArray().Length == 7`
    - smoke 테스트는 시각 검증이 아닌 흐름 검증만 수행. PrimeTween 시간 기반은 `WaitForSeconds` 로 충분.

## 완료 기준

- 게임 시작 → 상단 strip unroll → 2초 dwell (또는 strip 클릭 즉시 진행) → roll → fan 등장 → 카드 3장 클릭/스와이프 → 마지막 toss 끝나고 Placement 진입.
- Redraft 시 시퀀스 도중 BeginDraft 가 다시 호출돼도 깨지지 않음 (재진입 가드 동작 확인).
- 토글 버튼으로 strip 재펼침/접기 (Drafting state).
- PlayMode `DraftFlowSmokeTest` PASS.
- 옛 `DraftView.cs` (옛 위치) + .meta 삭제.
- Console 컴파일 에러 / NullRef 0.
