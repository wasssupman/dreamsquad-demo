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
