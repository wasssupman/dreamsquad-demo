# 2 — 물통 연출 + 코스트 변동 처리

## 목적

물통을 살아 있게 만들고, **충전 도중 코스트가 외부에서 늘거나 줄었을 때** 표시가 거짓말하지 않게 한다. 이 unit 의 대부분은 경계 상황 처리다.

## 변경 대상

- `Assets/_Project/Scripts/UI/CostDisplay.cs`

## 구현

### 프레임 판정

`CostRuntime` 은 이벤트를 내보내지 않으므로 `Update` 폴링으로 상태 전이를 감지한다.

```
curInt = CostWellMath.DisplayInt(Current)
fill   = CostWellMath.WellFill(Current, Max)
delta  = curInt - _prevInt
```

| 조건 | 해석 | 연출 |
|---|---|---|
| `_prevInt < 0` | **sentinel — 재동기화** | 없음. 값만 갱신하고 return |
| `delta > 0` && `Current >= Max` | 상한 도달 | **MaxBurst 만** |
| `delta == 1` && `fill < _prevFill - FillEpsilon` | 자연 충전으로 물통이 넘침 | WellTick + ValuePunch |
| `delta > 0` (그 외) | 외부 획득(`AddCost`) | GainFloat(`+N`) + ValuePunch |
| `delta < 0` | 소비 | SpendFlash (숫자 warn 색 0.3s) |
| `delta == 0` | — | fill 추종만 |

위에서 아래로 **첫 매치에서 멈춘다**. `Current` 가 max 에 닿는 프레임은 정수 증가 + 물통 넘침 + 상한 도달이 **동시에 참**이라, max 분기가 맨 위에 있어야 연출 3개가 겹쳐 터지지 않는다.

`maxCost = 10` 이므로 **자연 충전이 완주하는 마지막 사이클(9.x → 10)은 항상 WellTick 이 아니라 MaxBurst 로 간다.** 의도된 동작이다.

### sentinel — 없으면 배치 진입마다 오발

`_prevInt` 를 0 으로 초기화하면 안 된다. `startingCost = maxCost = 10` 이라 매치는 항상 `Current == Max == 10` 에서 시작하고(`PlacementPhaseView.cs:96` 이 배치 진입마다 `ResetToStart()`), 첫 프레임 `delta = +10` + `Current >= Max` → **MaxBurst 오발**. 배치 진입·Restart·Redraft 마다 100% 재현된다.

`_prevInt = -1` sentinel 을 두고 첫 프레임은 값만 흡수한다. 리셋 지점 **3곳**:

1. `AttachToTray` 직후
2. `OnPhaseChanged` (Placement / Battle 진입)
3. **억제 해제 시** (아래)

기존 코드가 이 함정을 이미 알고 방어하고 있었다 — `CostDisplay.cs:65` `_lastShownInt = -1`, `:277` `if (_lastShownInt >= 0 && ...)`. unit 1 이 `EnsureBars` 를 지우면서 유일한 리셋 지점(`:255`)이 사라지므로 반드시 계승한다. 이식 원본도 같다(`AwakeningGaugeView.cs:197`).

### 억제 구간 재동기화

unit 1 에서 억제가 `CanvasGroup.alpha` 로 바뀌어 `Update` 는 계속 돈다. 하지만 트레이 자체가 꺼지는 구간(`DreamcatcherHandView` 플립, Draft/Result 페이즈)에서는 `activeInHierarchy == false` 로 폴링이 멈춘다.

그 동안 코스트는 계속 변한다 — Battle 중 손패를 4초 열면 regen 0.35/s × 4 ≈ +1.4. 다시 보일 때 `delta = +1` 인데 되감김 여부는 운에 달려 **`+1` GainFloat("외부 획득") 오발**이 난다. 실제로는 자연 충전이다.

→ 다시 보이는 순간 `_prevInt = -1` 로 재동기화한다. **억제 구간은 "연출 없이 상태만 따라간다"가 계약이다.**

### epsilon — 없으면 `AddCost(1)` 의 10% 오분류

`fill < _prevFill` 을 epsilon 없이 쓰면 안 된다. `CostRuntime.cs:67` 의 `Mathf.Min(_max, _current + amount)` 는 float32 연산이라, 정수를 더해도 결과가 다른 binade 로 넘어가면 소수부가 1 ULP 하향 드리프트한다. 측정: `AddCost` 케이스의 19% 에서 `fill_after < fill_before`, 그중 `delta == 1` 까지 동시 성립이 10% — 즉 `AddCost(1)` 열 개 중 하나가 "자연 충전"으로 오매치된다. (실례: `4.30 + AddCost(1)` → fill `0.300000012 → 0.299999952`)

→ `fill < _prevFill - CostWellMath.FillEpsilon`. 근거는 unit 0 의 "epsilon 이 두 비교에서 정반대인 이유".

### 복합 변동 — 같은 프레임 소비 + 획득

`PlacementInput.cs:99` 는 `PlaceDefenderAs()` 를 부른 뒤 `TrySpend(cost)` 한다. 그런데 `PlaceDefenderAs` 는 내부에서 이미 `TriggerOnPlaceAndSynergy`(`BattleBridge.cs:3900`) → `AddCost`(`:3162`, `OnPlaceEffectType.GainCost`)를 실행한 상태다. **획득과 소비가 한 프레임에 확정된다.**

순 델타 하나로는 이걸 표현할 수 없다:

- `cost 5 / gain 5` → `delta == 0` → **연출이 하나도 안 난다**
- `cost 5 / gain 3` → SpendFlash 만, 획득 피드백 소실
- `cost 3 / gain 5` → `+2` 인데 실제 획득은 5 (거짓말)

**계약**: 폴링 델타는 **순 변화량만** 표시한다. `delta == 0` 이어도 무연출이 정상이다 — 실제로 보유량이 안 변했기 때문이다.

**획득 피드백은 폴링이 아니라 소스에서 나온다.** `AddCost` 는 이미 실제 획득 정수를 반환하고(`CostRuntime.cs:63-69`) `BattleBridge` 가 `affected` 로 받아 쓰고 있다. 온플레이스 `GainCost` 의 `+N` 은 이 반환값으로 직접 구동한다(폴링 추정 금지). 상한 clamp 로 소실된 경우(`반환값 < 요청값`)는 "가득" 표기를 붙인다.

### 코스트 변동 시나리오 (계약)

| 상황 | 물통 | 숫자 |
|---|---|---|
| 충전 중 유닛 배치로 3 소비 (6.7 → 3.7) | **안 움직임** (소수부 유지) | 6 → 3, warn 플래시 |
| 충전 중 `AddCost(2)` (3.7 → 5.7) | **안 움직임** | 3 → 5, `+2` 플로팅 |
| 상한 근처 `AddCost` clamp (9.4 → 10.0) | 0.4 → 가득 | 9 → 10, MaxBurst |
| max 에서 3 소비 (10.0 → 7.0) | 가득 → 빔 | 10 → 7, warn 플래시 |
| 자연 충전 완료 (6.99 → 7.00x) | 넘침 → 0 | 6 → 7, WellTick |

소비가 물통을 건드리지 않는 것이 이 설계의 핵심이다. 물통은 보유량이 아니라 **다음 1코스트까지의 진행률**이고, 소비는 정수만 가져간다.

> **알려진 학습 리스크 (사용자 결정으로 수용).** 4행이 이 모델의 약점이다. `startingCost = maxCost = 10` 이라 플레이어가 물통을 처음 보는 순간은 언제나 "가득"이고, 거기서 1코스트만 써도 물통이 완전히 빈다 → "물통 = 보유량"으로 학습한 뒤 전투 중에 배신당한다. 리뷰가 보유량 모델을 제안했으나 사용자가 소수부 원안을 유지하기로 했다. **unit 4 의 사용자 Play 판정 항목으로 올린다.**

### 리젠 정지 표현

`RegenActive == false && fill < 1` 이면 물통을 dim + 표면 숨김 + **정지 글리프**(⏸ 또는 "대기")를 띄운다.

배치 페이즈에서 실제로 발생한다: `BeginRegen` 은 Battle 진입에서 불리고 유닛 비용은 전부 정수라, **첫 배치 직후부터 전투 시작까지(30초) 소수부가 0 인 채 정지**한다. 셀 높이의 61%(82px)가 30초간 아무 것도 말하지 않으므로 상태 표시가 없으면 고장으로 읽힌다.

dim(밝기) 단독으로 구분하지 않는다 — 이 spec 계열의 "색·밝기 단독 판별 금지" 계약(`battle-hud-action-tray` unit 1 의 X glyph 근거)을 물통에도 적용한다. **정지 / max / 거의-가득 세 상태는 각각 글리프 · 액체 높이+MaxIdle · 액체 높이로 구분된다.**

### 연출 (각성 게이지에서 이식하되 어휘를 가른다)

우하단 각성 게이지도 "차오르는 액체"다. 형태(원형 vs 직사각)만 다르고 **모션 어휘까지 같으면** 두 리소스가 한 덩어리로 읽힌다 — 더구나 각성은 `Gauge / GaugeMax` = **보유량**이고 카드를 쓰면 내려간다(`DreamcatcherHandController.cs:436`). 코스트 물통은 "쓰면 안 내려간다"라 **동작 규칙이 정반대**다. 둘은 Battle 페이즈에 동시에 화면에 있다.

| 신규 | 원본 | 이식 여부 |
|---|---|---|
| WellTick | `PunchValue` | **짧은 틱 스냅으로 축소** — 각성의 큰 바운스와 구분 |
| MaxBurst | `MaxReadyRoutine` | 이식 |
| ValuePunch | `PunchValue` | 이식 |
| SpendFlash | `FlashLostSegment` | 대상이 세그먼트 → 숫자 |
| GainFloat | `ShowGain` | 온플레이스 `GainCost` 전용 (위 복합 변동 절) |
| ~~MaxIdle 펄스~~ | `MaxIdleRoutine` | **이식하지 않음** — 각성 전용으로 예약 |

액체 색(`wellLiquidColor → wellLiquidFullColor`)은 각성의 보라→시안과 **색상·채도 축에서 분리**하고, 두 색의 명도차를 확보한다(채움도를 색 단독으로 읽지 않게).

액체 표면(`WellSurface`)은 `fill` 을 따라 y 이동하고 `0.01 < fill < 0.99` 에서만 활성 — 각성의 `LiquidSurface` 와 같은 규칙(`AwakeningGaugeView.cs:191-195`).

### 하지 않는 것

- **fill smoothing 을 넣지 않는다.** 소비 시 물통이 툭 떨어지는 것은 "썼다"는 피드백으로 읽힌다. 튀어 보이면 그때 튜닝한다.
- `PulseInsufficient` 는 기존 동작 유지(부족은 소비 실패라 `delta` 가 움직이지 않아 위 표와 겹치지 않는다).

## 완료 기준

- [ ] Play — 전투 진입 후 물통이 약 2.9초(regen 0.35/s)에 걸쳐 차오르고, 가득 차면 숫자 +1 · 물통 0 리셋
- [ ] **배치 페이즈를 3회 이상 진입해도 MaxBurst 가 오발하지 않는다** (sentinel 회귀 가드)
- [ ] **손패를 열었다 닫아도 `+1` GainFloat 이 오발하지 않는다** (재동기화 회귀 가드)
- [ ] Play — 충전 중 유닛을 배치하면 **물통은 그대로**, 숫자만 줄고 warn 플래시
- [ ] Play — `10/10` 도달 시 MaxBurst 1회, 물통은 가득 유지(빈 통으로 보이지 않음)
- [ ] Play — 배치 페이즈에서 코스트를 쓰면 물통이 dim + 정지 글리프, 전투 진입과 함께 해제 + 충전 시작
- [ ] max 도달 프레임에 WellTick 과 MaxBurst 가 겹쳐 재생되지 않는다
- [ ] `GainCost` 온플레이스 유닛을 `cost == gain` 으로 배치했을 때의 무연출이 의도대로인지 확인(계약)
- [ ] 슬로모(드래그 중)에서 물통 충전이 함께 느려지고, 팝/플래시는 실시간 속도를 유지한다
- [ ] EditMode — `FillEpsilon` 경계 케이스 테스트 (1 ULP 드리프트 샘플로 오분류가 안 나는지)
