# 0 — 페이즈 모델 & 라우팅

## 목적

`GamePhase.Gift` 를 새 페이즈로 추가하고, 배치 진입 신호를 가로채 **Gift → (연출) → Placement** 순서로 흐르게 하는 토대를 만든다. 이벤트/타이밍 값을 담는 `GiftConfig` SO 와 `GiftKind` enum 도 여기서 정의한다. (이 단계는 라우팅 배관만; 실제 덱 조합은 unit 1, 연출은 unit 3~4.)

## 변경 대상

- `Assets/_Project/Scripts/Core/GameManager.cs` — `GamePhase` enum(line 10)에 `Gift` 추가. 진입 라우팅 신호.
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — `BeginPlacementPhase()`(line 83)는 그대로 두되, 진입 신호를 GiftPhase 가 먼저 소비하도록 배선(구독 이관 또는 gate).
- `Assets/_Project/Scripts/Core/DraftController.cs` — `DraftConfirmed` 이벤트 소비처가 Gift 로 향하도록.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `OnRestartRequested`(~line 296)의 `_placementPhaseView?.BeginPlacementPhase()` 직접 호출을 Gift 경유로 라우팅.
- (신규) `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` — `[CreateAssetMenu]` SO. `GiftKind` enum 도 여기 또는 인접 파일.
- (신규) `Assets/_Project/Data/Dreamcatcher/GiftConfig_Default.asset`.

## 구현

1. `enum GamePhase { None, Draft, Placement, Battle, Result }` → `Gift` 를 `Placement` **앞에** 삽입: `{ None, Draft, Gift, Placement, Battle, Result }`. enum 을 `switch`/비교하는 모든 지점(각 phase view 의 `OnPhaseChanged`, `DefenderSelector`, `AwakeningGaugeView` 등)이 `Gift` 를 "자기 페이즈 아님 → 숨김" 으로 처리하는지 확인. 대부분 `phase == Placement` 명시 비교라 자동으로 숨겨짐.
2. **라우팅 seam**: GiftPhase 컨트롤러(unit 3 의 `GiftPhaseView` 가 겸하거나 전용 `GiftPhaseController`)가 다음을 구독:
   - `gameManager.PlacementRequested`
   - `draftController.DraftConfirmed`
   그리고 이 신호를 받으면 곧장 `PlacementPhaseView.BeginPlacementPhase()` 로 가는 대신 **`gameManager.SetPhase(Gift)` + `BeginGift()`** 를 호출. `PlacementPhaseView` 는 이 신호들의 직접 구독을 끊고, GiftPhase 가 연출 종료 시 `BeginPlacementPhase()` 를 **명시 호출**하도록 한다.
   - 실행 순서 주의: `GameManager` 는 `[DefaultExecutionOrder(-100)]`. GiftPhase 오브젝트가 `PlacementPhaseView` 보다 먼저 신호를 잡아야 하므로 구독 이관(PlacementPhaseView 가 더 이상 `PlacementRequested`/`DraftConfirmed` 를 직접 구독하지 않음)이 가장 깔끔하다.
3. **재시작**: `BattleBridge.OnRestartRequested` 의 `_placementPhaseView?.BeginPlacementPhase()` 를 `_giftPhaseView?.BeginGift()`(폴백: 없으면 기존 직접 호출)로 교체. 재시작마다 이벤트 재추첨(unit 1 의 시드 처리와 연동 — 재시작 시 새 시드/재롤 여부는 unit 1 계약을 따른다).
4. `GiftConfig` SO 필드(placeholder):
   - `float lucidWeight = 1f`, `float rimWeight = 1f` (이벤트 가중치)
   - `float introTextSec`, `float baseCardsInSec`, `float giftAppendDelaySec`, `float giftAppendSec`, `float shuffleSec`, `float holdSec`, `float flyOutSec` (연출 구간 타이밍 — unit 4 가 소비)
   - `bool fastForwardInTestMode = true`
5. `GiftKind { Lucid, Rim }` enum.

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] `GamePhase.Gift` 삽입 후 기존 phase view 들이 Gift 동안 정상 숨김(각성 버튼/DefenderSelector 등 오류 없음).
- [ ] Draft/Squad/Test/Restart 네 경로 모두 진입 신호가 GiftPhase 로 라우팅되고, 임시 stub `BeginGift()`(즉시 `BeginPlacementPhase()` 호출)로도 배치까지 정상 도달(연출은 아직 없음).
- [ ] `GiftConfig_Default.asset` 생성·기본값 세팅.
