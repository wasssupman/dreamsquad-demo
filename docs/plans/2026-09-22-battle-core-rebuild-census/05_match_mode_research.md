# 매치 모드 설계 조사 — 외부 표준 + 장르 레퍼런스 + 이 게임의 설계안

> 조사 2026-09-23. READ-ONLY 조사 결과. 대상 = `docs/spec/battle-core-rebuild/` 계약 13 의 별첨 `match-mode-design.md` 초안 입력.
> 이 저장소 안에 **같은 축을 한 번 지운 판례**(`docs/spec/endless-mode-removal/`)가 있고, 그 판례가 이 설계의 통과 조건을 이미 정해 두었다.

---

## 조사 요약(출처 표)

| 출처 | 무엇을 말하나 | 이 게임에 주는 교훈 |
|---|---|---|
| [Unreal: Game Mode and Game State](https://dev.epicgames.com/documentation/en-us/unreal-engine/game-mode-and-game-state-in-unreal-engine) | `AGameModeBase` = **규칙**(서버 전용, 복제 안 됨) / `AGameStateBase` = **상태**(전원 공유) / `APlayerState` = 개인 상태. 모드 선택 우선순위 = `DefaultEngine.ini`(`GlobalDefaultGameMode`) → World Settings 의 `GameMode Override` → URL `game=` 옵션 → 맵 접두사 별칭 | 「모드가 규칙을 갖고 상태는 따로 산다」가 표준. 우리 담당자들(`ScoreLedger`·`MatchClock`·`HeartMeter` 등)이 곧 GameState 에 해당하고, 모드는 그 위에 얹히는 **규칙 층**이어야 한다 |
| [AGameStateBase API](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/AGameStateBase?lang=en-US) · [GameState 해설(Cedric Neukirchen)](https://cedric-neukirchen.net/docs/multiplayer-compendium/common-classes/gamestate/) | `AGameMode`(Base 아님)만 매치 상태 기계를 갖는다: `EnteringMap` → `WaitingToStart` → `InProgress` → `WaitingPostMatch` → `LeavingMap` / `Aborted`. GameState 는 「게임이 얼마나 돌았나」·서버 시각·`APlayerState` 배열을 든다 | 우리 `GamePhase` 7값과 같은 성격. **모드가 페이즈를 새로 정의하지 않고 기존 페이즈의 길이·활성만 고른다**는 선이 표준과 일치한다(현행 `placementPhaseEnabled` 가 이미 그 모양) |
| [Lyra Deep Dive ch.2 (unrealist.org)](https://unrealist.org/lyra-part-2/) · [X157: Lyra Experience](https://x157.github.io/UE5/LyraStarterGame/Experience/) | `ULyraExperienceDefinition` = 「GameMode 의 훨씬 발전된 형태」를 **데이터 자산**으로. 속성 4개: `GameFeaturesToEnable` · `DefaultPawnData` · `Actions` · `ActionSets`. 선택은 레벨의 `Default Gameplay Experience`(`LyraWorldSettings`), 로딩·활성은 **GameState 위의** `ULyraExperienceManagerComponent`, 완료 신호는 `OnExperienceLoaded` | 「모드 = SO」의 정본 유비. 단 Lyra 는 **열린 액션 리스트**(`Actions`·`ActionSets`)라 조합이 무한하다 — 사용자가 명시적으로 거부한 방향이므로 **형식만 빌리고 개방성은 빌리지 않는다** |
| [Unity: Architect game code with ScriptableObjects](https://unity.com/how-to/architect-game-code-scriptable-objects) | SO = 「인스턴스와 독립된 공유 데이터」. 싱글톤 대체·하드 의존 제거 용도. ⚠ 경고: *"copy your data into a runtime value to not change the value stored on disk for the ScriptableObject"* — 런타임 상태를 SO 에 두지 말고 `InitialValue`/`RuntimeValue` 를 분리하라 | `MatchModeData`(SO)는 **읽기 전용 저작물**이고, 판이 쓰는 값은 `MatchDefinitionBuilder` 가 구워낸 plain 정의표여야 한다. rebuild 계약 6 이 이미 같은 말을 하고 있다 — 모드도 예외가 아니다 |
| [Unity: Level up your code with design patterns and SOLID](https://unity.com/resources/design-patterns-solid-ebook) | Strategy · State · Command · Observer 등 11 패턴의 공식 권장 형태와 샘플 프로젝트 | `IMatchGoal` 은 Strategy 다. Unity 공식 권장 형태라 팀 밖 근거로 쓸 수 있다 |
| [BTD6 Modes 카테고리](https://www.bloonswiki.com/Category:Modes_in_BTD6) | 모드 **23개 고정 목록**("23 pages … out of 23 total") | 성공한 TD 의 모드는 **닫힌 목록**이다. 무한 조합이 아니다 |
| [BTD6 CHIMPS](https://www.bloonswiki.com/CHIMPS) · [Deflation](https://www.bloonswiki.com/Category:Modes_in_BTD6) · [Apopalypse](https://www.bloonswiki.com/Apopalypse_(BTD6)) | CHIMPS = 목숨 1(증가 불가) · 이어하기/파워/지식/판매 **전부 금지** · 6라운드 시작 100라운드 종료. Deflation = 현금 고정(추가 획득 불가), 31라운드 시작. Apopalypse = 라운드 **무작위 생성** + 라운드 사이 휴식 없음(앞 라운드 전멸을 안 기다림) | 각 모드가 「시작/종료 라운드 + 금지 목록 + 경제 규칙 + 웨이브 케이던스」라는 **같은 슬롯**을 다른 값으로 채운다. 새 컴포넌트를 발명하지 않는다 |
| [Arknights: Annihilation](https://arknights.wiki.gg/wiki/Operation/Annihilation) | 적 400기 · 생명 10. **생명이 0 이 되어도 실패가 아니라 «클리어»로 끝난다**("the Annihilation operation will be cleared instead of ending in a failure"). 보상은 처치 수에 비례. 자동 DP 생성 off, DP 상한 999 | 우리 「지지 않는다 · 끝날 수는 있다」와 **정확히 같은 해법**이 상용 TD 에 있다. `stress_full` 을 패배로 부르지 않는 근거이자, `WaveClear` 모드에서 붕괴를 어떻게 표기할지의 답 |
| [Arknights: Contingency Contract](https://arknights.wiki.gg/wiki/Contingency_Contract) | 규칙 수식자(Risk/Test Criteria)를 **닫힌 목록에서 골라 쌓는다**. 각 계약이 위험도 1~3, 합이 난이도. Key Criteria 는 추가 계약을 해금하고, 선으로 연결된 계약끼리는 **상호 배타** | 「무한 조합」과 「닫힌 집합」 사이의 중간 지대. 여기서도 고를 수 있는 것은 **미리 저작된 유한 목록**이고, 조합 규칙(배타·해금)이 데이터에 명시돼 있다 |
| [Kingdom Rush: Heroic Challenge](https://kingdomrushtd.fandom.com/wiki/Heroic_Challenge) · [Iron Challenge](https://kingdomrushtd.fandom.com/wiki/Iron_Challenge) | 같은 맵, 다른 규칙 3종. Heroic = 업그레이드 경로 상한(레벨마다 다름) / Iron = 목숨 1 + 특정 타워 **잠금**(보통 2개) + 웨이브 적 수 **비공개**(물음표) | 모드가 바꾸는 것은 주로 **「무엇을 쓸 수 있나」와 「몇 번 틀려도 되나」**다. 맵·웨이브 콘텐츠는 재사용한다 — 모드당 저작 비용이 낮은 이유 |
| [Clash Royale: Special Event Challenges](https://clashroyale.fandom.com/wiki/Tournament/Special_Event_Challenges) | 모드가 바꾸는 축: 덱 구성(4장 제한 · 진화 0/1/전체) · 아레나 지형(강·다리 없음, Boost Fields) · 카드 **금지 목록** · 경제(엘릭서 배속) · 덱 포맷(Duel = BO3) | 「덱 수량 제약」이 모드 축으로 장르 표준임을 확인. 드림캐쳐 덱/손패 상한과 스쿼드 슬롯을 모드가 갖는 설계가 정상 범위 안에 있다 |
| [PvZ2: Objectives](https://plantsvszombies.fandom.com/wiki/Objectives) | 목표가 **파라미터 있는 고정 종류들**로 열거된다: 「식물 N개 이상 잃지 말 것」·「N초 안에 좀비 M기 처치」·「N분 버티기」·「햇빛 N 이상 생산」·「꽃을 밟히지 말 것」 | 목표를 enum + 파라미터로 닫는 형태의 실물 선례. 종류는 소수, 값은 레벨마다 다르다 |
| **내부 판례** [`docs/spec/endless-mode-removal/README.md`](../../../Users/sy/dev/wassup/docs/spec/endless-mode-removal/README.md) | `AttackDeck.battleMode` 를 2026-08-16 에 **제거**했다. 조사 결과 그 모드가 실제로 하던 일은 「토너먼트 리포트 스킵」 하나 + dev 진입 스위치였고, `Deck_Endless.asset` 의 `timerDurationSec` 은 **180 이었다**(타이머를 끈 적이 없다). 후속 후보에 못 박혀 있다: *「다시 모드 축이 필요해지면 enum 을 되살리기 전에 «그 모드가 실제로 무엇을 다르게 하는가» 를 한 줄로 적을 것」* | **이 설계의 통과 조건이다.** 모든 모드는 한 줄 차이 문장을 가져야 하고, 그 문장이 「플래그 하나」로 요약되면 그건 모드가 아니다 |

> 참고: `AttackDeck.cs` 상단에 그 판례가 주석으로 살아 있다 — *「모드 축이 다시 필요해지면 «그 모드가 무엇을 다르게 하는가» 를 한 줄로 답할 수 있을 때 만든다」*. 이번 요구는 그 조건을 만족한다(목표 자체가 다르다). 하지만 **모드마다** 그 한 줄을 요구하는 규율은 유지한다.

---

## 설계 원칙

조사에서 반복적으로 나온 것을 이 게임의 어휘로 옮기면 여섯 가지다.

### 1. 모드는 규칙, 담당자는 상태

Unreal 의 GameMode/GameState 분리가 그대로 적용된다. `WaveScheduler` · `CostLedger` · `HeartMeter` · `ScoreLedger` · `MatchClock` · `PlacementService` · `HandDeck` · `GimmickHost` 는 **모드를 모른다.** 이들은 「웨이브를 낸다」·「코스트를 잰다」·「마음이 부서지면 끝낸다」만 안다.

모드는 이들을 **파라미터로 구성**하고, 그 위에서 **종료·점수·표기**만 판정한다. 담당자 안에 `if (mode == ...)` 가 들어가는 순간 모드는 실패한 것이다 — 그건 계약 12(「매니저를 두지 않는다」)가 막으려던 것이 담당자 안으로 스며든 형태다.

### 2. 닫힌 목표 종류 + 종류별 파라미터

BTD6 23개, Kingdom Rush 3개, PvZ2 목표 종류 열거가 전부 같은 모양이다. 목표는 **enum 으로 닫고**, 차이는 파라미터로 표현한다.

열린 컴포지션(Lyra 의 `Actions` 리스트)은 저작 자유도를 주지만 「이 조합이 성립하는가」를 아무도 검증하지 않는다. Arknights CC 조차 — 겉보기엔 가장 조합적인 시스템인데 — 상호 배타·해금 규칙을 **데이터에 명시**해서 조합을 닫는다.

### 3. 모드는 값을 덮어쓰지 않고 «어느 저작 자산을 쓸지»를 고른다

이게 조합 폭발을 막는 핵심이다.

웨이브 수량 램프(`unitGrowthPerWave`) · 당김 상한(`maxPullsPerClear`) · 웨이브 간 상한 간격(`maxWaveIntervalSec`) · 리드인(`waveSpawnLeadInSec`) · 보스 케이던스는 이미 `AttackDeck` 이 소유한다. 모드가 그 위에 오버라이드를 얹으면 「이 값의 소유자가 둘」이 되고, 그건 CLAUDE.md 제약 12 가 경고하는 바로 그 형태다(*「그 값을 이미 소유한 곳이 노출하고 있지 않은지 먼저 확인한다」*).

**다르게 하고 싶으면 다른 덱을 만든다.** 모드가 **소유**하는 것은 지금 어디에도 단일 소유자가 없는 것들뿐이다 — 목표 · 종료 정책 · 점수 정책 · 시계 정책.

> 시계는 실제로 소유자가 **둘**이다: `AttackDeck.timerDurationSec`(seed/legacy 경로) 와 `WavePlanAsset.timerDurationSec`(저작 경로, 0 = 무한). `BattleBridge.StartBattle` 이 둘 중 하나를 고른다. 모드로 올리면 이 이중 소유가 해소된다 — 이 설계가 새 축을 얹는 게 아니라 **기존 중복을 걷어내는** 첫 번째 이득이다.
>
> 당김 상한은 **적 덱이 계속 소유한다.** 설계 지향 2축이 *「당김 상한도 적 덱 소유라 전원 같은 값을 받는다 — 로드아웃으로 옮기면 「내 덱이 남보다 많이 당긴다」가 된다」* 라고 이유를 적어 뒀다. 모드도 매치 단위라 공정성은 깨지지 않지만, **옮길 이유가 없으면 옮기지 않는다**가 원칙 3 이다.

### 4. 저작물은 읽기 전용, 판이 쓰는 값은 구워낸 사본

Unity 공식 경고 그대로다. `MatchModeData` 는 SO 로 저작하되 `MatchDefinitionBuilder` 가 `ModeDef`(plain struct)로 구워 `MatchDefinition` 에 싣는다. 전투 코어는 SO 를 본 적이 없다.

rebuild 계약 4(「전투 코어는 엔진을 모른다」)·6(「값의 정본은 판 밖」)이 이미 요구하는 바이고, 모드도 예외가 아니다. 부수 효과: 헤드리스 EditMode 하네스가 SO 로딩 없이 모드를 바꿔 가며 판을 돌릴 수 있다.

### 5. 재현 = 모드 id + 시드

Lyra 의 user-facing experience(맵 id + 경험 id)가 같은 모양이다. 토너먼트는 서버가 시드를 주고, 시드가 맵·웨이브·비주얼·픽업·기믹·메테오 6계열을 파생한다(`Core/MatchSeed.cs`).

여기에 **modeId 를 명시적으로 더한다.** 시드에서 모드를 파생하지 **않는다** — 모드는 플레이어(또는 토너먼트 서버)가 고르는 것이고, 시드는 그 모드 안에서 콘텐츠를 정한다. 두 축을 섞으면 「모드를 골랐는데 시드가 바꿔 버린다」가 된다.

### 6. 공정성 축은 모드가 건드리지 않는다

설계 지향 2축(「개인 유불리를 계속 깎아낸다」)은 맵·웨이브 편성·공용 액티브 2장·당김 상한이 **전원 동일**하라고 요구한다. 모드는 매치 단위 값이므로 같은 토너먼트 안에서는 전원 동일하다 — 축을 위반하지 않는다.

단 **모드가 다르면 점수를 섞을 수 없다**(§위험 3).

---

## 이 게임의 매치 모드 설계안

### `MatchModeData` (SO) 스키마

기존 자산을 **참조만** 하고 수치를 복제하지 않는다.

| 그룹 | 필드 | 형 | 비고 |
|---|---|---|---|
| **정체성** | `modeId` | string | 안정 키. 토너먼트 제출·리플레이·리더보드가 이 값을 저장한다. **리네임 금지**(저장 데이터가 문자열로 든다) |
| | `displayName` | string | 문안은 formatter 가 이기는 판례 유지 — SO 값은 폴백 |
| **목표** | `goalKind` | enum(3) | `KillScoreTimed` · `WaveClear` · `TimeAttack` |
| | `goalParams` | struct | 종류별 의미(아래 표). 종류마다 쓰는 필드가 다르고, 안 쓰는 필드는 무시 |
| **시계** | `clockKind` | enum | `FixedLimit`(만료 = 종료) · `CountUp`(상한 없음, 목표가 끝낸다) |
| | `durationSec` | float | `FixedLimit` 일 때만. 현행 180 |
| | `submitUnlockSec` | float | 현행 `BattleBridge.SubmitUnlockSec = 60f` **코드 상수** → 여기로 내려온다. census 가 이미 「튜닝 확정되면 저작으로 내릴 것」이라 적어 둠 |
| | `allowSubmit` | bool | 제출 통로 개방 여부 |
| **웨이브 원천** | `waveSourceKind` | enum | `GeneratedFromDeck` · `AuthoredPlan` |
| | `deck` | `AttackDeck` | null = 맵 풀 엔트리의 짝 사용(현행 동작 유지) |
| | `plan` | `WavePlanAsset` | 저작 플랜 모드용. null = 덱 생성 웨이브 |
| **맵** | `mapPool` | `MapStagePool` | null = 기본 풀. 모드 전용 맵 세트를 쓸 때만 지정. 선택은 기존 `seed % Count` 결정론 그대로 |
| **기믹** | `gimmickEnabled` | bool | 현행 `BattleConfig.gimmickEnabled`(false) 가 여기로 |
| | `gimmickPool` | `GimmickData[]` | 비우면 `BattleConfig.gimmickPool`. 배정은 `DeriveGimmickSeed` 그대로 |
| **배치** | `costConfig` | `CostConfig` | 시작 10 · 상한 15 · 재생 1/s · 배치 페이즈 30초 한 묶음 |
| | `placementPhaseEnabled` / `autoStartCountdownSec` | bool / float | 현행 `BattleConfig` 2필드가 여기로 |
| | `squadSlots` | int | 편성 가능 방어유닛 수(현행 7). Kingdom Rush 의 「타워 잠금」과 같은 슬롯 |
| | `retireEnabled` | bool | 퇴근 동사 on/off |
| **드림캐쳐** | `deckRuleConfig` | `DeckRuleConfig` | 덱 크기 10 · Squad 상한 2 |
| | `awakeningConfig` | `AwakeningConfig` | 게이지 100/시작 20 · 비용 · 손패 5 · 부착 상한 3 · 슬로모 0.3 |
| | `publicActiveCount` | int | 이번 판 공용 액티브 장수(현행 2, 큐 12 = 덱 10 + 2) |
| **토너먼트** | `submitsReport` | bool | 구 `BattleMode.Endless` 가 **실제로 하던 유일한 일**. 그 판례가 「모드가 아니라 플래그로 충분」이라 했으므로 여기서 플래그로 인정한다 |
| | `leaderboardId` | string | 기본 = `modeId` |

`BattleConfig` 는 4필드가 전부 모드로 이사하므로 **소멸하거나 기본 모드 참조 한 칸만 남는다.** 그게 이 설계의 두 번째 이득이다.

### 닫힌 목표 종류 — 3개, 「무엇이 다른가」 한 줄

| `goalKind` | 한 줄 차이 | 파라미터 | 담당자에게 요구하는 것 |
|---|---|---|---|
| `KillScoreTimed` **(현행 라이브)** | 「정해진 시간 안에 몇 마리 잡았나」 | — | `MatchClock` 고정 만료 → `EndMatch("complete")` · `ScoreLedger` 킬 수 **생값** · `HeartMeter` 붕괴 = 즉시 종료(남은 시간 몰수) · `WaveScheduler` 무한 생성 |
| `WaveClear` | 「정해진 웨이브 N 을 끝까지 막았나」 | `targetWaves` | `WaveScheduler` 가 **「마지막 웨이브 dispatch 완료 + 전멸」** 신호를 낼 것(현행 전멸 판정 `NoQueuedAttackersRemain` 재사용) · `MatchClock` 상한 없음 또는 넉넉 |
| `TimeAttack` | 「정해진 웨이브를 **얼마나 빨리** 끝냈나」 | `targetWaves` | 위와 같은 종료 신호 + `MatchClock` count-up · `ScoreLedger` 가 **경과 시간**을 점수로(작을수록 좋음) |

**`Endless` 를 넣지 않는 이유**: 내부 판례가 답을 이미 준다. 그 모드가 다르게 하던 일이 「토너먼트 리포트 스킵」 하나였고, 타이머는 한 번도 꺼진 적이 없었다. 지금 그 축이 필요해도 `KillScoreTimed` + `clockKind = CountUp` + `submitsReport = false` 로 전부 표현된다 — **enum 값을 쓸 자격이 없다.**

**세 종류가 담당자 3개만 건드린다**는 점이 중요하다. `PlacementService` · `CostLedger` · `HandDeck` · `GimmickHost` 는 모드에 따라 **동작이 바뀌지 않는다** — 파라미터만 다르게 받는다. 목표가 실제로 읽고 쓰는 것은 `MatchClock` · `ScoreLedger` · `WaveScheduler` 셋뿐이다.

`WaveClear` / `TimeAttack` 이 `WaveScheduler` 에 새로 요구하는 것은 **신호 하나**다: 「저작/생성된 마지막 웨이브가 dispatch 됐고 필드가 비었다」. 현행에도 전멸 판정(`NoQueuedAttackersRemain`)과 당김 회복 로직이 그 정보를 이미 갖고 있으므로, 새 상태가 아니라 **읽기 모델 한 줄**이다.

### 모드 로직이 사는 자리 — `IMatchGoal`

`class-diagram.md` 가 이미 그려 둔 형태를 확정한다.

```
interface IMatchGoal
    void OnBegin(MatchGoalContext ctx)
    void OnTick(MatchGoalContext ctx)                  // 종료 판정. EndMatch 는 여기서만
    MatchOutcome BuildOutcome(MatchGoalContext ctx)    // 점수 · 표기 · 정렬 방향
    GoalReadModel Read { get; }                        // HUD 가 읽는 것
```

`MatchGoalContext` 는 **담당자들의 읽기 모델 묶음 + `MatchClock.EndMatch` 하나의 쓰기 권한**이다. 그 외 쓰기 권한이 없다.

이게 「매니저가 아니다」의 이행 조건이고 계약 12 를 만족하는 방식이다 — 목표는 개체 상태를 하나도 들지 않고, 판정 두 개(끝났나 / 점수가 얼마인가)만 소유한다. `ISkill` 이 33 concrete 로 이미 쓰고 있는 형태와 같다(제약 8 의 「구현체 2개 이상」 조건을 목표 3종이 만족한다).

**종료의 «사유»는 담당자가, 종료의 «의미»는 목표가 정한다.** `HeartMeter` 는 모드와 무관하게 붕괴 시 `EndMatch("stress_full")` 을 부른다(현행 그대로, 종료 통로 3개 유지). 그 종료가 「2분에 터졌지만 같은 잣대로 줄 세워지는 판」인지(`KillScoreTimed`) 「웨이브 7/12 에서 멈춘 판」인지(`WaveClear`)는 `BuildOutcome` 이 정한다.

Arknights Annihilation 이 생명 0 을 실패가 아니라 **부분 보상 클리어**로 처리하는 것과 같은 해법이다 — 패배 라벨을 붙이지 않고도 결과에 차이를 낼 수 있다.

### 모드가 흐르는 경로

```
로비: 플레이어가 모드 선택 (토너먼트면 서버가 modeId 지정)
   ↓
MatchDefinitionBuilder.Build(mode, squad, deck, stage, seed)
   ↓  SO → plain. ModeDef 가 MatchDefinition 에 실린다
BattleMatch.Begin(def)
   ↓  담당자들을 ModeDef 파라미터로 구성 + goalKind 로 concrete 1개 생성
IMatchGoal ← 담당자 읽기 모델 (쓰기는 MatchClock.EndMatch 하나)
   ↓
HUD / 결과화면 ← GoalReadModel (모드별 표기를 목표가 소유)
   ↓
토너먼트 제출: { modeId, seed, score, sortDirection }
```

**모드 선택 우선순위**는 Unreal 의 4단계 선택 규칙을 축소해 3단계로 둔다:

1. 테스트 모드 강제(dev 진입)
2. 로비 선택 / 토너먼트 서버 지정
3. 프로젝트 기본 모드

현행 `GameManager.Start` 의 진입 3종(테스트모드 > 스쿼드 > draft 폴백)과 같은 모양이라 배선이 새로 생기지 않는다.

### 현행 라이브를 한 모드로 — 정확히 표현된다

```
MatchMode_KillScore3Min.asset
  modeId                = "kill_score_3min"
  goalKind              = KillScoreTimed
  clockKind             = FixedLimit
  durationSec           = 180
  submitUnlockSec       = 60
  allowSubmit           = true
  waveSourceKind        = GeneratedFromDeck
  deck                  = null            (맵 풀 엔트리의 짝)
  mapPool               = null            (기본 풀, seed % Count)
  gimmickEnabled        = false
  gimmickPool           = []
  costConfig            = DefaultCostConfig
  placementPhaseEnabled = false
  autoStartCountdownSec = 3
  squadSlots            = 7
  retireEnabled         = true
  deckRuleConfig        = DeckRuleConfig_Default
  awakeningConfig       = AwakeningConfig
  publicActiveCount     = 2
  submitsReport         = true
  leaderboardId         = "kill_score_3min"
```

`KillScoreTimedGoal` 이 하는 일 전부:

- `MatchClock` 만료 시 `EndMatch("complete")`
- `BuildOutcome` = 「점수 = `ScoreLedger.Kills` 생값, 내림차순, 가공 없음」

종료 통로 3개(`complete` · `stress_full` · `submitted`)는 **그대로 3개**다. 목표가 통로를 늘리지 않고 하나만 소유하며, 나머지 둘은 `HeartMeter` 와 제출 커맨드가 소유한다. three-minute-kill-race 의 계약(「`EndMatch` 호출부가 2곳을 넘으면 패배 부활」 → heart-stress-axis 가 3으로 재고정)이 그대로 유지된다.

---

## 옵션 비교와 권고

| 옵션 | 형태 | 장점 | 대가 | 판정 |
|---|---|---|---|---|
| **A. `IMatchGoal` concrete/종류** | 목표 종류마다 클래스 1개, 담당자는 제네릭 | 「모드를 모르는 담당자」가 구조로 강제된다. `ISkill` 33 concrete 선례와 같은 모양이라 팀이 이미 아는 형태. 새 모드 = 새 클래스 1개 + SO 1개. 「이 모드가 무엇인가」를 한 파일에서 읽는다 | 종류가 1개면 제약 8(구현체 2개 이상) 위반 | **권고** — 단 v1 에 최소 2종을 같이 낸다 |
| B. 담당자별 strategy | `WaveScheduler` · `ScoreLedger` · `MatchClock` 이 각각 정책 객체를 받는다 | 담당자 경계가 더 선명 | 한 모드의 규칙이 3~4곳에 흩어져 「이 모드가 무엇인가」를 한 파일에서 못 읽는다. 정책 간 조합이 다시 무한(원칙 2 위반) | 기각 |
| C. `ModeDef` 플래그 뭉치 (concrete 없음) | 담당자가 `if (def.endOnWaveClear)` 를 본다 | 클래스 0개, 가장 가볍다 | **담당자가 모드를 알게 된다** — 원칙 1 위반. 플래그 5개면 조합 32개가 암묵적으로 생기고 아무도 검증하지 않는다 | 기각(목표 2종 이상일 때) |
| D. Lyra 식 열린 액션 리스트 | `MatchModeData.Actions[]` 에 규칙 조각을 쌓는다 | 저작 자유도 최대. Game Feature 식 확장 | 사용자가 명시적으로 거부한 「무한 컴포넌트 조합」 그 자체. 조합 유효성을 아무도 검증 못 한다. Arknights CC 조차 배타 규칙으로 닫는다 | 기각 |

### 권고: A, 단 조건부

`IMatchGoal` 은 목표 종류가 **실제로 2개 이상 존재할 때만** 만든다. v1 에 `KillScoreTimed` 하나만 낼 거면 인터페이스는 제약 8 이 금지하는 「나중을 위한 추상 레이어」다. 그 경우 규칙을 `MatchClock` · `ScoreLedger` 안에 두고, 두 번째 모드를 낼 때 추출한다 — *「구체 구현부터 시작해서 반복이 생기면 그때 추출한다」* 가 CLAUDE.md 금지 행동 항목이 요구하는 순서다.

따라서 **`3_match_owners.md` 에 `WaveClear` 를 같이 넣을지가 선행 결정**이다.

- **넣는다** → `IMatchGoal` 을 바로 쓴다. 두 concrete 가 같은 담당자 읽기 모델을 쓰므로 인터페이스 표면이 첫 커밋에 검증된다.
- **안 넣는다** → unit 3 은 현행 규칙만 구현하되, **「종료 판정과 점수 산출이 각각 한 메서드로 격리되어 있을 것」** 을 완료 기준에 넣어 추출 비용을 0 으로 만든다.

**`MatchModeData` SO 와 `ModeDef` 물질화는 어느 쪽이든 지금 만든다.** 그건 추상화가 아니라 이미 흩어진 값(시계 2곳 · `BattleConfig` 4필드 · 코드 상수 `SubmitUnlockSec`)의 소유자를 하나로 모으는 일이고, 그 자체로 계약 6 과 제약 6(하드코딩 금지)을 전진시킨다.

---

## 위험·열린 질문

1. **v1 목표 종류가 1개면 `IMatchGoal` 은 과잉 추상화다.**
   「모드 SO 는 지금, 목표 인터페이스는 2종째부터」가 이 설계의 실행 순서. 두 번째 모드를 언제 낼지가 결정되지 않으면 unit 3 의 범위가 확정되지 않는다. **선행 결정 필요.**

2. **「지지 않는다」 축 × `WaveClear` 는 규칙의 성질 문제다.**
   웨이브 12 중 7 에서 마음이 부서진 판을 뭐라고 부를 것인가. Arknights 는 「부분 보상 클리어」로 푼다. 이건 플레이어가 겪는 규칙이라 **워크플로 0(신규 기능의 «성질»은 먼저 묻는다)에 해당** — 에이전트가 정하면 안 된다. 같이 물을 것: 「제출」 어휘를 다른 모드에서도 유지하는가(현행 UI 어휘 규칙은 「포기」류 금지 · 「제출」 고정).

3. **모드 간 점수는 비교 불가다.**
   `TimeAttack` 은 작을수록 좋아 **정렬 방향조차 반대**다. 리더보드를 `modeId` 로 분리하고 `MatchOutcome` 에 `sortDirection` 을 실어야 한다. 토너먼트가 한 회차에 여러 모드를 섞으면 랭킹이 의미를 잃는다 — **「한 토너먼트 = 한 모드」가 계약**이어야 하고, 이건 서버 쪽 계약이기도 하다.

4. **골든 코퍼스가 모드 변경을 못 잡을 수 있다.**
   기존 `configHash` 는 웨이브·덱에 반응하고 **참조 SO 필드엔 무반응**이라는 것이 이미 알려진 함정이다. `modeId` 와 모드가 고른 자산 id 들을 해시에 **명시적으로** 섞지 않으면, 모드를 바꿔도 코퍼스가 초록으로 남아 기준선이 조용히 잘못 구워진다.

5. **`AttackDeck.timerDurationSec` 이관이 조용히 실패할 수 있다.**
   라이브 덱 asset 들이 그 값을 직렬화 중이고, 이 저장소에는 **`bossUnit` 을 리네임하면 전 맵에서 보스가 에러도 경고도 없이 사라진다**는 판례가 같은 파일 주석에 적혀 있다. 필드를 즉시 지우지 말고 「모드가 이기고 덱 값은 폴백」으로 한 릴리스 병행한 뒤 제거하는 편이 안전하다.

6. **모드 × 기믹 × 맵 풀 조합의 저작 유효성을 아무도 검증하지 않는다.**
   예: `WaveClear(targetWaves = 12)` 인데 선택된 덱의 `maxWaveCount` 가 10 이면 판이 영원히 안 끝난다. `StagePoolBuildabilityTests` 선례대로 **모드 유효성 EditMode 테스트**가 같이 나와야 한다(목표 파라미터 ↔ 선택된 덱/플랜의 정합 · 저작 플랜 모드에서 `clockKind` 와 `plan.timerDurationSec` 의 충돌).

7. **모드를 고르는 곳이 하나인가.**
   현행 진입 3종(테스트모드 · 스쿼드 · draft 폴백)이 각자 모드를 고르면 축이 또 갈린다. `MatchDefinitionBuilder` 가 **모드를 읽는 유일한 지점**이어야 하고, 진입 경로는 `modeId` 만 넘긴다. 참고로 draft 폴백은 census 가 「로비 `LoadoutGate` 우회 경로」라고 **코드 스스로 로그를 남긴다**고 적었고 존치 결정 이력이 없다 — 이 기회에 정리 대상 후보다.

---

## 부록 — 이 설계가 걷어내는 중복

| 지금 | 이후 |
|---|---|
| 타이머 소유자 2곳 (`AttackDeck.timerDurationSec` · `WavePlanAsset.timerDurationSec`) | `MatchModeData.durationSec` 하나 (플랜 값은 폴백 후 은퇴) |
| `BattleConfig` 4필드 (기믹 2 + 배치 페이즈 2) | 모드로 이사, `BattleConfig` 소멸 또는 기본 모드 참조 1칸 |
| `BattleBridge.SubmitUnlockSec` 코드 상수 60 | `MatchModeData.submitUnlockSec` 저작 |
| 「토너먼트 리포트 스킵」이 모드 enum 으로 표현됐던 이력 | `submitsReport` 플래그 (판례가 요구한 형태) |
| 진입 3종이 각자 config 를 조립 | `MatchDefinitionBuilder` 단일 지점 |
