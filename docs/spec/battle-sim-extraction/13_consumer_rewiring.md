# 13 — 소비자 재배선 (82파일 → 세션 계약)

## 목적

스왑 커밋이 "구현체 교체 1곳"이 되려면 **소비자가 이미 세션만 보고 있어야** 한다. 구 sim 위에서
재배선을 끝내 회귀를 여기서 소진한다(sim 회귀와 재배선 회귀를 분리 검증 — 설계 정본 M1-3).

## 변경 대상

실측 소비자 82파일. 성격별 3묶음으로 나눠 **묶음당 독립 커밋**한다:

- **A. 폴링 → 읽기 모델** (`NextWaveDock` · `SpawnAlertPresenter` · `CostDisplay` ·
  `DefenderSelector` · `ScoreHudView` 등): `bridge.X` 직독 → `session.ReadModel.X`.
  ⚠ 청사진 ① §6 실측 — `TryGetSpawnAlertForecast` 는 **캐시 배열 참조**를 넘기므로 읽기 모델은
  복사본/read-only span 을 준다. `TryGetUnitViewAnchor` 를 생존 프로브로 겸용하던 곳은
  **명시 `IsAlive(simId)`** 로 교체.
- **B. push → 이벤트 구독** (`ScoreHudView.OnEnemyKilled` · `SetLeakStatus` · `BossWarningView` ·
  `ResultScreen.ShowVictory/ShowDefeat` · `ScoreTallyView.Play`): Bridge 가 뷰 메서드를 호출하던
  방향을 **뷰가 세션 이벤트를 구독**하는 방향으로 뒤집는다. 승패는 `MatchEnded` 이벤트.
- **C. 입력 → 커맨드** (`DefenderDragPlacementController` · `DefenderRelocationController` ·
  `DirectionAimController` · `DreamcatcherCardDragSlot` · `NextWaveDock` 버튼 ·
  `PlacementPhaseView.FinishPlacement` · `MenuPopup` pause): 직접 호출 → `session.SendCommand`.
  preflight(`CanPlaceDefenderAt` 등)은 **커맨드 검증과 같은 함수를 공유**해 이중 계산을 없앤다.

## 세션 획득 방식 (2026-08-04 사용자 결정)

**정적 로케이터 `MatchSession.Current`.** Bridge 가 `BeginPlacement` 에서 `Arm`, `OnDestroy` 에서
`Release`. 뷰는 `MatchSession.IsActive` 로 가드하고 `MatchSession.Current.ReadModel` 을 읽는다.

- 채택 근거: 뷰가 `BattleBridge` 타입으로 세션을 얻으면(대안 `Bridge.Session` 프로퍼티)
  Bridge 가 해체되는 M1 말에 소비자 82파일을 **한 번 더** 만져야 한다. 로케이터를 거치면 스왑은
  `Arm()` 1곳 교체이고 뷰는 무변이다. 인스펙터 주입은 `IMatchSession` 이 인터페이스라 제공자
  MonoBehaviour 신설 + 씬 82곳 연결이 필요해 기각.
- **`Bridge.Session` 공개 프로퍼티를 두지 않는다** — 두면 뷰가 그것을 쓰고 채택 근거가 무너진다.
- 정적 전역의 위험은 이 프로젝트에서 실측했다(`TestModeContext.RuntimeImportsBlocked` 의 테스트 간
  누출 의심). 그래서 ① `Release(expected)` 는 신분 일치 시에만 해제 ② `Arm` 이 살아 있는 세션을
  덮으면 경고 ③ `ResetForTests()`. 셋 다 EditMode 로 고정(`MatchSessionLocatorTests`).
- 수명은 로케이터가 아니라 **만든 쪽(Bridge)이 소유**한다. `BeginPlacement` 는 `Dispose` → 생성 →
  `Arm` 순이며 **Dispose 선행이 계약**이다(옛 세션이 잡은 pause lease 미반납 = 그 판 영구 정지).

## 구현

- 순서는 A → B → C. A 는 읽기만이라 가장 안전하고, C 는 게임 상태를 바꾸므로 마지막.

### bundle A 의 실제 분해 (2026-08-04 실측 정정)

A 가 나열한 소비자를 **한 커밋에 전부 옮길 수 없다**. 읽기 모델의 표면이 부족하고, 한 파일은
분류 자체가 틀렸다:

| 조각 | 대상 | 상태 |
|---|---|---|
| **A1** | `MatchSession` 로케이터 + Bridge 공급 + `NextWaveDock`(폴링 5종) | ✅ 완료 |
| **A2** | `SpawnAlertPresenter` forecast — `TryGetSpawnAlertForecast` 가 **캐시 배열 참조**를 넘긴다 → 세션은 `ReadOnlySpan<float>` 로 좁힌다 | ✅ 완료 |
| **A3** | `CostDisplay` · `DefenderSelector` · `DefenderDragSlot` — 코스트·쿨타임 표면 신설 + 재배선 | ✅ 완료 |

- **`ScoreHudView` 는 bundle A 가 아니다** — bridge 폴링이 **0** 이고 점수는 push 로 들어온다.
  **bundle B** 로 이관한다(스펙 초안의 분류 오류).

### bundle C 의 실제 분해 + 순번 계약 (2026-08-05)

| 조각 | 대상 | 상태 |
|---|---|---|
| **C1** | `ForceNextWave`(버튼) · `SetPaused`(MenuPopup — lease 소유 이전) | ✅ 완료 |
| **C2** | `DefenderDragPlacementController` · `DirectionAimController` · `DefenderRelocationController` | 대기 |
| **C3** | `DreamcatcherCardDragSlot` 4변종 — 컨트롤러는 발신자로만, 소유권은 unit 16 | 대기 |
| — | `PlacementPhaseView.FinishPlacement` → **unit 14 로 이관**(아래 근거) | 이관 |

**커맨드 순번 계약 — 순번은 세션이 소유한다.** `IMatchSession.NextClientSeq()` 가 세션이 다음에
기대하는 값을 내주고 `MatchSession.Send(build)` 가 발급·전송을 한 곳에 묶는다. 호출자 쪽에
카운터를 두면 안 된다:

- 어댑터의 갭 분기는 기대값을 **전진시키지 않는다**. 따라서 두 값이 1 어긋나면 **재수렴이 없다** —
  이후 모든 커맨드가 `Session_SeqGap` 이고, 호출부가 receipt 를 보지 않으므로 **콘솔이 깨끗한 채로
  웨이브 버튼·정지·배치가 영구히 죽는다**. 초기 구현이 정적 카운터였고 리뷰가 이것을 잡았다.
- 그래서 갭은 이제 **`Debug.LogError`** 로 시끄럽다. 정상 경로는 이 분기에 오지 않으므로, 로그가
  뜨면 누군가 `MatchSession.Current.SendCommand(...)` 를 직접 부른 것이다 — **하지 말 것**.
- 순번 소유가 세션에 있으므로 매치 경계의 정적 리셋이 사라졌고, `Arm` 이 정적 상태를 만지지 않게
  되어 재진입 위험도 함께 없어졌다(`FinishPlacement` → `StartBattle` → `BeginPlacement` 가 실행
  중인 어댑터를 Dispose 하는 경로가 실재했다).
- 어댑터는 실행 중 자신이 파기되면(`_disposed`) 기록을 남기지 않는다 — 죽은 세션이 비워진
  `_receipts` 에 다시 쓰거나 기대값을 전진시켜 살아 있는 새 세션과 어긋나는 것을 막는다.

**`FinishPlacement` 를 unit 14 로 이관한 근거**: `StartBattle` 에는
`if (!_placementAllowed) BeginPlacement()` 라는 **자기치유 재시도**가 있고, 그것이
`BeginPlacement` 조기 반환("Default World not ready … will retry")이 가리키는 경로다. 조기 반환은
`Arm` **앞**이라 그 구간에는 세션이 없으므로, 커맨드로 바꾸면 거절되고 재시도가 사라진다. 그때
상태는 `GamePhase.Battle` · 배치 패널 숨김 · 코스트 리젠 시작 · `_running == false` 로 타이머가
0:00 에 굳고 웨이브 버튼도 숨겨져 **로비로 나가는 외에 복구가 없다**. 매치 개시의 소유는 웨이브·
승패 규칙과 함께 움직여야 한다.

**이벤트 라우터는 발신자를 확인한다.** `Publish(sender, evt)` 가 `Current` 가 아닌 세션의 이벤트를
버린다. 없으면 구현체 4종 중 둘이 깨진다 — Ghost(남의 판 곁 재생)의 `EnemyKilled` 가 같은 창구로
흐르면 `ScoreHudView` 가 누적식이라 **상대 킬이 내 점수를 부풀리고**, Replay 의 seek 도 같은 뷰에
재발행된다. `SessionEvent` 에 세션 신원 필드가 없어 구독자가 걸러낼 수 없으므로 라우터가 막는다.

**`ResetForTests()` 는 `Events` 를 지우지 않는다.** 이 프로젝트는 Play 진입 시 도메인 리로드가 꺼져
있어 뷰의 `OnEnable` 이 다시 실행되지 않는다 — 구독자를 끊으면 그 PlayMode 세션 내내 점수 HUD·
보스 배너가 되살아날 경로 없이 죽는다.

**tick 은 라이브에서 `-1`(모른다)이다.** `_harnessTick` 은 하네스 스테퍼만 증가시켜 라이브 판에서는
내내 0 이다. 0 을 신고하면 "tick-스탬프드 읽기 모델"·"수락 tick"·"tick 내 순서"가 전부 거짓이 되고,
**골든은 하네스로 녹음되므로 byte diff 로 절대 잡히지 않는다**. unit 19(커맨드로그)·unit 20(A/B
parity)이 이 필드 위에 세워지기 전에 부재를 명시했다. 라이브의 `_orderInTick` 리셋 축은 프레임이다.
진짜 tick 을 라이브에 주는 것은 **unit 19(시계 정책)** 의 몫이다 — 여기서 `_battleClock / dt` 로
지어내면 하네스와 라이브가 서로 다른 두 시계를 갖는다.

### bundle C 의 검출기 문제 (2026-08-05 — 진행 전 반드시 읽을 것)

**골든은 bundle C 를 검출하지 못한다.** 골든 하네스(`LegacyTraceGoldenRunner`)는
`bridge.BeginPlacement` · `PlaceFirstValid(bridge, unit)` · `bridge.ForceNextWave` 를 **직접**
호출하고 뷰를 하나도 거치지 않는다. 따라서 C 의 회귀는 sim 이 아니라 **UI 에만** 나타나며
"컴파일 통과 + 화면만 죽음"이 된다. 골든 초록은 이때 "sim 을 안 건드렸다"만 증명한다.

실제 검출기는 PlayMode 테스트다. 진행 전 초록 기준선을 확보했다(집중 실행 `passed=8 failed=3`):
- ✅ `DropDismountTest.RealDragDrop_HandoffContinuity_...` — **실제 드래그·드롭 + 활성화 시계 +
  탭 게이트**를 한 테스트에서 태운다. C2 의 핵심 검출기.
  (선행 파손이었다 — `fe53bd45` 가 `_defenderViewOverride` 값을 `(float3,float,float3)` 튜플로
  바꿨는데 테스트의 리플렉션 헬퍼가 `float3` 캐스팅을 유지해 `InvalidCastException`. Item1 만
  꺼내도록 고쳐 되살렸다. **C2 를 하기 전에 검출기를 먼저 세우는 것이 순서**였다.)
- ✅ `RelocationSmokeTest` · `RelocationMoveModeTest` · `RelocationPlacementSessionTest` ·
  `OnPlaceApplyStackNearbyTest`
- 🔴 `PlacementAuraTest` ×3 — 선행 파손(×1.2), 이 spec 밖. README 후속 후보 참조.

### bundle B 의 실제 분해 (2026-08-05 실측)

| 조각 | 대상 | 상태 |
|---|---|---|
| **B1** | `ScoreHudView.OnEnemyKilled` · `BossWarningView.Show` — 페이로드가 호출 지점에 이미 있는 스칼라 | ✅ 완료 |
| **B2** | `ScoreHudView.SetLeakStatus` · `ScoreTallyView.Play` · `ResultScreen.ShowVictory/ShowDefeat` | **unit 14 와 함께** |

**B2 를 unit 14 로 미룬 근거**(추측이 아니라 실측):
- `SetLeakStatus(goals, limit, notEndless)` 는 이벤트가 아니라 **상태 푸시**다. 이벤트로 바꾸면
  비멱등해진다. 읽기 모델 폴링이 맞는 모양인데 그 값(`_goalReachedCount`·`EffectiveLeakLimit()`·
  `IsEndless`)이 전부 **Bridge private** 이라, 지금 옮기면 unit 14 가 즉시 걷어낼 `internal` 발판을
  세우게 된다.
- 집계·결과는 `ScoreMath.BattleScore`(4값 구조체)를 넘겨야 하는데 `SessionEvent` 는 스칼라 봉투이고,
  `MatchEnded` 의 **score4 페이로드 발행 지점을 unit 14 가 소유**한다고 unit 14 스펙이 이미 정해뒀다.
- `scoreTallyView.Play(score, scoreHud, callback)` 는 뷰→뷰 결합 + 완료 콜백이라, 이벤트 1건으로
  뒤집는 게 아니라 **프레젠테이션 시퀀스 소유자**를 정하는 일이다(unit 14 의 MatchEnded 위에서).

**B1 의 설계 결정 — 구독 지점은 세션 인스턴스가 아니라 정적 창구다.** 세션은 매치마다 교체되므로
(`BeginPlacement` 가 새 어댑터를 만든다) 뷰가 `Current.SomeEvent += ...` 로 붙으면 다음 판에서 죽은
인스턴스를 잡거나 OnEnable 시점에 `Current` 가 null 이라 아예 못 붙는다. 그래서 `MatchSession.Events`
(정적) + `MatchSession.Publish`(구현체가 호출). 뷰는 `OnEnable`/`OnDisable` 에서 붙이고 뗀다 —
이 코드베이스의 기존 관용구(`GameManager.PhaseChanged`)와 같다.

- `IMatchSession` 에는 이벤트를 **두지 않았다**: 인스턴스 수명이 매치 단위라 구독 지점으로 부적합하다.
  대신 `Publish(sender, evt)` 가 **발신자를 받아 `Current` 만 통과**시켜 라우터가 규약을 강제한다.
  남은 한계: 봉투에 세션 신원 필드가 없어 Ghost/Replay 를 **동시에** 살릴 때는 필드 추가가 필요하다
  (지금은 하나만 Current 이므로 게이트로 충분하다).
- `DrainEvents()` 는 여전히 빈 목록이다 — **의도적**이다. 소비자(기록기)가 없는데 누적하면 무한히
  자란다. fan-out 은 `Publish` 가 이미 했고, 누적·드레인 소유는 기록기가 생기는 unit 19 와 함께 온다.
- `Bridge` 의 `_bossWarning` SerializeField 를 **제거**했다(뷰 참조 0). 뷰 오브젝트는 씬에 그대로
  있고 스스로 구독한다. `scoreHud` 필드는 B2 대상(`SetLeakStatus`·tally)이 남아 잔류한다.

### A3 의 통화·쿨타임 경계 (2026-08-04 사용자 결정: "지금 번역")

어댑터가 `GameManager.{CostRuntime,CooldownRuntime}` 을 **번역해 읽기 모델을 지금 채운다**. 그 결과
unit 15 의 일은 "필드를 만드는 것"이 아니라 **소유권을 sim 으로 옮기는 것**이 되고, 그때 뷰는 무변이다
(뷰 재배선을 두 시점으로 쪼개지 않는 편이 82파일 작업의 성격에 맞다).

- **플래그를 분리**했다: `SupportedCost`(A3 에서 true) / `SupportedGauge`(unit 16 까지 false).
  하나로 묶으면 코스트를 채운 순간 게이지 0 이 "지원됨"으로 거짓 신고된다.
- **코스트 값을 둘로 실었다**: `CostCurrent`(raw — 지불 판정 `CanAfford` = `_current >= amount`) +
  `CostCurrentInt`(floor — 표시·부족분). 구 런타임이 두 규칙을 쓰므로 한 필드로 합치면 max 근처에서
  판정이 1 씩 어긋난다.
- **쿨타임은 메서드**다: `TryGetPlacementCooldown(unitDefId, out remaining, out fraction)`.
  구 `PlacementCooldownRuntime` 은 `DefenderUnitData`(ScriptableObject)로 키잉하지만 계약에 엔진
  타입을 넣지 않는다(`SimCell` 이 `int2` 를 대신하는 것과 같은 이유). id→정의 해석은 구현체가
  소유하며 **Deploy 커맨드와 같은 해석기**(`TryResolveUnitDef`)라 뷰와 커맨드가 같은 카탈로그를 본다.
  "하나라도 활성인가"는 스칼라라 `ReadModel.AnyPlacementCooldown` 으로 두어 전 유닛 0 일 때의
  O(1) 스킵을 보존했다.
- **폴백 방향이 두 곳에서 반대**인 것을 보존했다: `TryGetAffordableTutorialSlot` 은 미지원 시
  `int.MinValue`(전부 탈락), affordability `Update` 는 `int.MaxValue`(전부 available — 코드에
  "false-negative 회피"가 계약으로 적혀 있다). 통일하면 둘 중 하나가 뒤집힌다.
- **쓰기는 A3 가 아니다**: `StartCooldown`(`DefenderSelector`) ·
  `ResetToStart`/`ResetAll`/`BeginRegen`(`PlacementPhaseView`) 는 bundle C 로 남는다.
- 각 묶음 후 **PlayMode 스모크**로 그 화면이 살아 있음을 확인한다(A: HUD 수치 갱신, B: 승패·집계
  연출, C: 배치·카드·웨이브 호출).
- **드림캐쳐 손패는 C 에서 가장 무겁다** — 현재 `DreamcatcherHandController` 가 덱·게이지·부착
  등록부를 소유하므로, 이 unit 에서는 컨트롤러를 **커맨드 발신자로만** 바꾸고 소유권 이동은
  unit 16 이 한다(범위 분리).
- 이 unit 이 끝나면 어댑터가 **유일한 drain 소유자**가 된다(unit 12 에서 유보한 것) — Bridge 의
  기존 drain 은 어댑터 호출로 대체하고 중복 소비를 제거한다.

## 완료 기준

- 묶음별 독립 커밋 3개, 각각 compile 0 · EditMode 회귀 0 · 해당 PlayMode 스모크 통과.
- `Assets/_Project/Scripts/{UI,Presentation}` 에서 **게임 상태를 바꾸는** `bridge.*` 호출 0
  (grep 증명. 좌표·픽 서비스 등 뷰 질의는 잔존 허용 — 청사진 ① §6 "계약 밖").
- 골든 7종 byte diff 0 — **재배선은 sim 을 건드리지 않는다**가 이 unit 의 핵심 계약이고 골든이 그
  증인이다. diff 가 나면 재배선이 규칙을 옮겼다는 뜻이므로 되돌린다.

> 진행 기록 2026-08-04 — **A1 완료**. 신규 `Core/Session/MatchSession.cs` ·
> `Tests/EditMode/MatchSessionLocatorTests.cs`(3건), 수정 `Bridge/BattleBridge.cs`(무장/해제) ·
> `UI/NextWaveDock.cs`(스냅샷 1회 읽기) · `Core/Session/IMatchSession.cs`(순번 갭 주석 정정 —
> 인터페이스는 "보류 후 거절"이라 적혀 있었으나 실제 기준은 전송 채널의 순서 보장 여부다).
> 검증: 4어셈블리 오류 0 · 로케이터 집중 3/3 · 전체 EditMode **1898 통과/실패 0**(이전 1895 →
> +3 = 신규가 실제로 실행된 증거) · **골든 7종 byte diff 0**(승격 확인 + 백업 대비 cmp 7/7).
> 골든 14 Play 세션에서 `[MatchSession]` 교체 경고 **0**(로그의 경고는 그것을 단정하는 EditMode
> 테스트가 낸 것뿐이다). ⚠ **이 0 은 순서가 옳다는 증거가 아니다** — `BeginPlacement` 가 `Arm`
> 앞에서 `Dispose` 하므로 `Current.IsActive` 는 그 지점에서 항상 false 고, 경고는 **구조적으로**
> 뜰 수 없다(리뷰 minor 5). Dispose 선행 순서는 코드와 테스트로 지켜지는 것이고, 이 로그 수치는
> 그 근거가 아니다.
> **뷰 폴링을 옮겨도 골든이 안 움직인다는 것은 당연하지 않다** — 뷰가 Bridge 프로퍼티를 읽는
> 행위가 sim 상태를 건드렸다면(지연 초기화·캐시 갱신 등) diff 가 났을 것이다.

> 진행 기록 2026-08-04 — **A2 완료**. `IMatchSession.TryGetSpawnAlertForecast(out ReadOnlySpan<float>)`
> 신설, 어댑터가 Bridge 배열을 span 으로 좁혀 서빙, `Presentation/SpawnAlertPresenter.cs` 재배선.
> **실측된 구멍**: 구 `Bridge.TryGetSpawnAlertForecast` 는 `laneFirstSpawnSec = _spawnAlertForecast`
> 로 내부 캐시 배열 **참조**를 넘겨 뷰가 그것으로 sim 상태에 쓸 수 있었다.
> `ReadOnlySpan` 을 고른 이유 = ① 쓰기를 **컴파일러가** 막고 `float[]` 캐스팅 우회도 불가
> (`IReadOnlyList<float>` 는 되돌릴 수 있다) ② 매 프레임 Update 라 복사 할당 0 이 실이익이다.
> 대가는 **유효 범위가 호출 프레임뿐**이라는 계약이며 인터페이스 주석에 명시했다(필드 저장 금지).
> 클럭은 분리했다 — 구 API 는 false 를 돌려주면서도 `battleClockSec` 을 채우는 모양이었는데
> 예보 유무와 묶일 이유가 없어 `ReadModel.BattleClock` 으로 출처를 하나로 모았다.
> `bridge` 참조는 남는다 — `TryGetSpawnPathSim` 은 공간 질의 = 청사진 ① §6 의 "계약 밖" 뷰 서비스.
> 검증: 4어셈블리 오류 0 · 전체 EditMode **1898/실패 0** · **골든 7종 byte diff 0**(신규 PASS
> @343956, cmp 7/7). 골든 구간에서 `[MatchSession]` 경고 0(로그의 경고 3건은 전부 그것을 단정하는
> EditMode 테스트 발 — 줄번호로 확인).
> 뷰 계층의 예보 직독 **0**(어댑터만 번역). `Tests/EditMode/SpawnAlertForecastTests.cs` 5건은
> Bridge API 를 직접 검사하는 **sim 쪽** 테스트라 유지한다 — 어댑터의 번역 대상이 계속 그것이다.
