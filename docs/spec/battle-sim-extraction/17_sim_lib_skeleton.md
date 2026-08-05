# 17 — sim lib 골격(asmdef 격리) + conform 유틸 이주

## 목적

**의존 방향을 컴파일러가 강제**하게 만든다. unit 11 이 grep 으로 증명한 것(`Battle/` 에 Bridge·
UnityEditor 참조 0)을 asmdef 로 승격해, 이후 이식(unit 18)이 실수로 Unity 를 끌어들이면 **빌드가
깨지도록** 한다. 상설 가드 ①(설계 정본 §4).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Sim/Wassup.Sim.asmdef` — **UnityEngine 참조 없음**.
  `noEngineReferences: true`, `allowUnsafeCode: false`, autoReferenced 는 켠다(Bridge 가 참조)
- 신규 폴더 `Scripts/Sim/{Match,Units,Movement,Combat,Effects,Math}/`
- **conform 유틸 이주**(salvage 판정 conform 전량 — 이식 unit 18 보다 **먼저**, 의존이 없어 안전):
  타겟팅 랭킹 4종 · `ModifierMath` · `CcEffectMerge` · `DotEffectMerge` · `KillAttribution` ·
  `AggroPolicy` · `AggroTargeting` · `TileAoe` · `GridMath` · `LaneMath` · `ShotOrder` ·
  `PatternShotRandomizer` · `VolleyMath` · `HeatMath` · `ScoreMath` · `SweepHitMath` ·
  `MovementCellTrim` · `PlacementPhasePolicy` 등
  (`SpatialPlacementCheck` 은 unit 15-B 가 이미 `Sim/Match/MatchPlacementRules.Spatial` 로 옮겼다 —
  `BattleBridge` 쪽 같은 이름은 포워더다. 여기서 다시 옮기지 말 것)
- unit 14~16 이 만든 `Sim/Match/` 모듈을 이 asmdef 안으로 편입

### ⚠ asmdef 를 긋는 순간 동시에 걸리는 엔진 타입 4종 (2026-08-05 리뷰 H1)

`Sim/Match/` 는 **엔진 무참조가 아니다.** asmdef 로 `UnityEngine` 을 컴파일 에러로 만들면 아래가
한꺼번에 걸리므로, 이 unit 의 계획에 그 이식 비용을 포함해야 한다:

| 타입 | 쓰는 곳 | 이식 방향 |
|---|---|---|
| `UnityEngine.Vector2Int` | `MatchPlacementRules` 의 `HashSet<Vector2Int> occupied` (공개 시그니처) | `int2` — `BattleBridge._occupiedTiles` 까지 함께 바꿔야 한다 |
| `GeneratedMap` | `MatchPlacementRules.Spatial/Check` 를 **값으로** 통과 (내부에 `NativeArray` 4개) | plain tile view. by-value 복사가 dispose 된 원본에서도 `IsCreated == true` 인 모양도 함께 해소 |
| `SpawnEntry` | `MatchWaveSchedule.PendingSpawnEntry`(공개) · `SeedLegacySpawns` | plain struct. `AttackUnitData`(SO) 참조를 id/인덱스로 |
| `GeneratedWavePlan` | `MatchWaveSchedule.Initialize`/`Plan`(공개) | plain struct |

**드리프트 게이트**: `SimEngineIndependenceTests` 가 허용 목록으로 이것을 지킨다. 목록에 파일을
추가하는 것은 이 표를 늘리는 것이므로 둘을 함께 갱신한다.

### ⚠ `Core/Session` 을 어휘와 로케이터로 갈라야 한다 (2026-08-05 리뷰 M3)

`Wassup.Core.Session` 은 **엔진 무참조 순수 계약 계층**이다(5개 파일이 `System`·
`System.Collections.Generic` 만 쓴다) — 그래서 `MatchOutcomeRules` 가 `MatchOutcome` 을 재사용하는
방향은 옳다. 문제는 같은 네임스페이스에 **정적 서비스 로케이터**가 함께 산다는 것이다:

| 성격 | 타입 | unit 17 에서 갈 곳 |
|---|---|---|
| 어휘(순수 계약) | `MatchOutcome` · `CommandReject` · `MatchCommand`/`CommandReceipt` · `MatchReadModel` · `SessionEvent` · `SimCell` | sim 이 참조 가능한 어셈블리 |
| 로케이터(소비자측 전역) | `MatchSession.Current`/`Arm`/`Release`/`Send`/`Events` | **프레젠테이션/Bridge 쪽 잔류** |

가르지 않고 `Core.Session` 을 통째로 sim 참조 가능 어셈블리에 넣으면 **sim 이 `MatchSession.Current`
에 손이 닿는다** — 제약 1 후계(sim 은 소비자를 모른다)의 정확한 역전이고, 그때는 컴파일러가
막아주지 않는다. 분리는 unit 17 의 **선행 조건**으로 잡는다.

## 구현

- **최대 함정: Unity 수학 타입**. 이주 대상 다수가 `float3`/`int2`(`Unity.Mathematics`)를 쓴다.
  `Unity.Mathematics` 는 UnityEngine 을 참조하지 않으므로 `noEngineReferences` 와 **양립 가능**하나,
  "특정 런타임 가정 금지"(정본 결정 #4) 관점에서 **패키지 의존이 M2 물리 제거 범위에 남는다**.
  → **이 unit 에서 결정하고 문서에 못박는다**: (a) `Unity.Mathematics` 유지(이식 비용 최소·
  Burst 상수 계승 용이) vs (b) 자체 벡터 타입(패키지 완전 탈출). **권고는 (a)** — Mathematics 는
  순수 수학 어셈블리이고 RNG 상수 계승(정본 §3)이 이것에 걸려 있다. (b) 는 M2 이후 선택지로 남긴다.
- `Unity.Collections`(NativeArray 등)는 **이주 대상 아님** — 신 sim 은 관리 컬렉션(List/배열)을 쓴다.
  conform 유틸의 `NativeArray` 파라미터는 이주 시 `Span`/배열로 시그니처 변경(순수 로직 불변).
- 테스트: conform 유틸 테스트(미참조 188파일 중 해당분)를 **그대로 통과**시켜야 한다 — 이 unit 의
  실질 안전망이다(salvage §5).

## 완료 기준

- `Wassup.Sim.asmdef` 가 UnityEngine·Entities·Bridge 를 **참조하지 않고** 컴파일된다
  (참조 추가 시 컴파일 에러가 나는 것을 1회 실측해 증명 — 게이트가 실제로 작동하는지 확인).
- 이주한 conform 유틸의 EditMode 테스트 **전량 통과**(기대값 변경 0).
- `Unity.Mathematics` 유지/탈출 결정이 이 문서에 기재됨 + `m1_blueprint_data_mapping.md` 에 반영.
- 골든 7종 byte diff 0(유틸 이주는 로직 무변).

---

## ⚠ 정찰 결과 (2026-08-05) — 현행 범위로는 완료 기준 달성 불가

### 차단 근거 — `SpawnEntry` 는 에셋에 직렬화돼 있다

`Data/AttackDeck.cs:68` `public List<SpawnEntry> spawns` + `:96` `[Serializable]` + `:100`
`public AttackUnitData unitType`(SO 참조). ⇒ `SpawnEntry` 를 plain struct/id 로 접으면 **`AttackDeck`
.asset 의 직렬화 모양이 바뀐다**. 이것은 unit 18 의 `MatchConfig` 물질화가 id↔SO 표를 만들 때 함께
처리할 일이고 `configHash` 입력에도 닿는다. **따라서 `MatchWaveSchedule` 은 unit 17 에서 asmdef 안에
들어갈 수 없다**(`SpawnEntry`·`GeneratedWave*`·`WavePatternGenerator` 에 전면 의존).

### 해법 — `Sim/Lib/` 2층 구조 (경계선을 폴더 한 겹 깊이 옮긴다)

```
Scripts/
├── Wassup.Runtime.asmdef              (references 에 "Wassup.Sim" 추가)
│   └── Core/Session/MatchSession.cs   ← 로케이터 잔류 (UnityEngine.Debug ×2)
└── Sim/                               ← "sim 후보" 스테이징. Wassup.Runtime 소속.
    ├── Match/MatchWaveSchedule.cs     ← 미졸업 (SpawnEntry 차단)
    └── Lib/                           ← ★ Wassup.Sim.asmdef (noEngineReferences: true)
        ├── Contracts/  (Core/Session 어휘 4파일)
        ├── Math/       (ScoreMath)
        └── Match/      (MatchOutcomeRules · MatchOutcomeNames · PlacementRejectReason …)
```

이렇게 하면 unit 17 이 **"게이트를 세우고 깨끗한 것부터 졸업시킨다"** 가 되어 **코드 변경 0 ·
byte-diff-0 unit** 으로 끝날 수 있고, 엔진 타입 4종은 데이터 계층이 함께 움직이는 unit 18 로 넘어간다.
`Sim/Match/` 를 통째로 asmdef 에 넣으면 `MatchWaveSchedule` 하나가 수십 개 CS0246/CS0012 를 낸다.

### `Wassup.Contracts`(3번째 어셈블리)는 만들지 않는다

리뷰 M3 가 막으려던 것(sim 이 `MatchSession.Current` 에 손이 닿는 것)은 **어셈블리 순환 금지만으로
이미 막힌다** — 어휘를 `Wassup.Sim` 에 올리고 로케이터를 `Wassup.Runtime` 에 남기면, sim 이 로케이터를
보려면 순환 참조가 필요한데 Unity 가 거부한다. 3번째 어셈블리의 고유 소비자는 M3 서버뿐이고 그 스택은
미정이므로, 지금 만들면 **제약 8("나중을 위한 추상 레이어 금지")과 정면 충돌**한다.
뷰가 sim 내부를 못 보게 하는 것은 `internal` 이 더 싸다(Bridge 직접 구동이 사라지는 unit 18~20 시점).
**재론 조건**: M2 헤드리스 러너가 어휘만 필요로 하거나, M3 wire contract 를 별도 배포해야 할 때.

### 단계 — 매 단계 컴파일 초록

| 단계 | 내용 | 초록 근거 |
|---|---|---|
| **17-0**(폐기) | 빈 asmdef + `int2` 한 줄 스파이크. 이어 `float3` 로 CS0012 재현 여부 측정 | 커밋 없음. 결과만 결정 기록 |
| **17-A** | `Sim/Lib/` + `Wassup.Sim.asmdef` + **`PlacementRejectReason.cs` 1개만** 이동, 3어셈블리 references 추가 | 이동 파일이 완전 순수(using 0) — 배선만 검증 |
| **17-B** | 게이트 실측: sim 에 `using UnityEngine;` 임시 추가 → 컴파일 에러 확인 → revert | 커밋 없음. **완료 기준의 "1회 실측" 충족** |
| **17-C** | 세션 어휘 4파일 → `Sim/Lib/Contracts/`. **네임스페이스 불변** | 소비자 21파일 diff 0 |
| **17-D** | `ScoreMath.cs` → `Sim/Lib/Math/`. **네임스페이스 `Wassup.Core` 불변** | 소비자 7파일 무변 |
| **17-E** | `MatchOutcomeRules`·`MatchOutcomeNames`·`MapTileType` 이동 | `MatchOutcomeRules` 의 `using Wassup.Core;` 가 이제 `ScoreMath` 만 본다 — `GameManager` 는 컴파일러가 차단 |
| **17-F**(선택) | `Vector2Int`→`int2` + `GeneratedMap`→`SimTileGrid` + `MatchPlacementRules`/`RelocationCheck` 졸업 | 유일하게 코드가 바뀌는 단계 — 골든 재확인 |
| **17-G** | 게이트를 **스테이징 층 전용**으로 조정(졸업 파일은 컴파일러가 정본) · 허용 목록 축소 · 문서 갱신 | 테스트만 |

**네임스페이스를 폴더에 맞춰 바꾸지 말 것** — `Wassup.Core.Session`·`Wassup.Core` 유지가 소비자
21+7 파일 diff 0 의 근거다. C# 은 폴더와 네임스페이스가 무관하다.

### 골든 영향 — 파일 이동은 바이트에 닿을 경로가 없다

① `LegacyTraceV0.cs:25` 의 `JsonUtility` 는 **필드명 기반**이라 어셈블리명이 트레이스에 안 들어간다 ·
② configHash blob writer(`MatchConfigSnapshot.cs:218-250`)는 **필드명 + 스칼라만** 기록하고 `:227`
`GetType()` 은 분기용이다 · ③ 이동 타입 중 Unity 직렬화 대상 0. **17-F 만 증명이 필요**한데,
`_occupiedTiles` 는 `Add`/`Remove`/`Contains`/`Clear` 만 쓰고 **열거 지점이 0**(9곳 전수)이라
`HashSet<Vector2Int>`→`HashSet<int2>` 는 순회 순서를 노출하지 않는다.

### `Unity.Mathematics` — (a) 유지, 단 **조건부**

실측: `math_unity_conversion.cs` 는 `float2/3/4`·`quaternion`·`float4x4` 만 UnityEngine 변환을 갖고
**`int2`·`int3`·`int4` 에는 없다.** unit 17 이 쓰는 수학 타입은 `int2` 뿐이라 `noEngineReferences:true`
와 양립한다. ⚠ **unit 18 에서 재검증**: `float3` 를 들이면 `implicit operator Vector3` 가 오버로드
해석에 들어와 **CS0012** 가 날 수 있다. (a) 를 M2 까지의 최종 결정으로 못박지 않는다.
`Unity.Collections` 는 `noEngineReferences:false` 라 **`NativeArray` 는 sim 에 들어올 수 없다**.

### 문서 드리프트 정정

- **`MatchSeed` 는 순수하지 않다** — `Core/MatchSeed.cs:25` 가 `UnityEngine.Random.Range` 를
  정규화 참조로 부른다. 순수한 것은 `Derive*`·`Mix` 뿐이고, 이주 시 그 진입점을 떼는 분할이 선행한다.
  (`ScoreMath` 는 `using` 지시문이 **0개**로 완전 순수 — 이쪽만 선례다.)
- 위 §"conform 유틸 이주" 의 "의존이 없어 안전" 은 **사실과 다르다**. 실측: `Unity.Entities` 3종 ·
  `Unity.Collections` 2종 · `Wassup.Data` 2종 · Mathematics-only 6종 · **무의존 1종(`AggroPolicy`)**.
  unit 17 에서 확정 이주 가능한 것은 `AggroPolicy` 하나다.
- **`VolleyMath` 는 존재하지 않는다** — `EmitterTick.cs:24` 주석에만 남은 은퇴 타입.
- **`PlacementPhasePolicy` 는 `UI/PlacementPhaseView.cs:371` 안의 internal 클래스**다(UI 파일에 규칙 상주).
- `MatchPlacementRules.cs:5` 의 `using Wassup.Data.MapGrid;` 는 **죽은 using**(`GeneratedMap`·
  `MapTileType` 둘 다 `Wassup.Data`).
- 참조 방향은 **이미 단방향**이다: `Wassup.Sim` 을 쓰는 프로덕션 5파일 · 테스트 13파일, **sim → 소비자
  참조 0건**. 단 `BattleBridge.Relocation.cs:22` `RelocationCheck` 는 MonoBehaviour static 이 sim enum 을
  반환하므로 `MatchPlacementRules` 졸업 시 함께 정리한다.
