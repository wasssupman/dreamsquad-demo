# 1 — 패턴 후보 범위 제한 + 전 후보 1:1 발사

## 목적

발사 명세 패턴에 **두 축**을 연다.

1. **범위**(`scopeTileRange`) — 후보 풀을 host 주변 N타일로 좁힌다. 지금은 맵 전체 고정이라
   "주변 2타일 안 적에게"를 데이터로 표현할 수 없다(캐논이 배치되자마자 맵 반대편을 폭격한다).
   `projectile-emission-pattern` 후속 후보 「**사거리 내 범위(scope) [S] · 필드 1개**」의 첫 소비자.
2. **전 후보 1:1**(`fanOutToAllCandidates`) — 스코프 안 **모든** 후보에게 정확히 1회씩 쏜다.
   v1 은 발수가 저작 고정(`shots` 목록)이고 발마다 후보 **하나**를 뽑는 구조라 "전원에게 1발씩"을
   표현할 수 없다.

이 unit 만으로는 어떤 발사도 바뀌지 않는다(두 축 모두 기본값 = 현행).

## 변경 대상

- `Assets/_Project/Scripts/Data/ProjectilePatternData.cs` — 필드 2개 + `TryToSpec` 복사
- `Assets/_Project/Scripts/Data/PatternSpec.cs` — 같은 필드 2개 (unmanaged 미러)
- 신규 `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/PatternScope.cs` — 순수함수
- `.../Emission/ProjectileEmitterSystem.cs` — 스코프 필터 + fan-out 전개
- 신규 `Assets/_Project/Tests/EditMode/PatternScopeTests.cs`

> `EmitterTick`·`EmitterRuntime`·`PatternTargeting` 은 **무변경**이다.

## 구현

### 필드 2개

```
int  scopeTileRange = 0;          // 0 = 맵 전체(현행). >0 = host 셀 기준 Chebyshev 반경
bool fanOutToAllCandidates = false; // false = 현행(발마다 후보 1개 선택)
```

둘 다 **후보 선택의 성질**이라 패턴 SO 소유다(`selection`·`shots` 와 같은 자리, 계약 3 위반 아님).

⚠ **`TryToSpec`(`ProjectilePatternData.cs:76~`)이 `PatternSpec` 의 유일한 writer 다.** 두 struct 에만
필드를 더하고 여기를 잊으면 **조용한 0 = 맵 전체 폭격**이 된다. 완료 기준에 복사 단언을 둔다.

### fan-out 의미 — 스케줄러를 한 줄도 안 건드린다

**한 shot 이 스코프 안 후보 «전원» 에게 1발씩 나간다.** `shots` 는 그대로 「몇 번의 일제사격」이고,
fan-out 은 그 한 번의 사격이 몇 갈래로 갈라지는지만 바꾼다.

```
비-fanout: shot 1회 → PatternTargeting.Select 로 후보 1개 → 요청 1개
fan-out  : shot 1회 → 스코프 안 후보 전부      → 요청 n 개 (동시)
```

- **`EmitterTick`·`EmitterRuntime` 무변경.** 초안은 `burstRemaining = shots × 후보수` 로 발수를
  동적으로 만들려 했는데, 그러면 순수 스케줄러가 «후보 수» 라는 아키텍처 지식을 받아야 하고
  `Advance` 의 shot index 계산까지 바뀐다. 갈래 수는 **발사 시점의 아키텍처 사실**이므로
  스케줄이 아니라 **한 발의 전개**에서 처리한다.
- `PatternLogic.BuildOrder` 는 **shot 당 1회** 부른다(카운터 전진도 1회). 갈래마다 다른 것은
  타겟뿐이고 damage·telegraph·barrel 은 같은 order 를 공유한다.
- 후보 0 → 기존과 동일하게 **발사를 소모하고 skip**(위상 보존 규약 유지).
- `reselectPerShot` 는 fan-out 에서 **의미가 없다**(잠글 단일 대상이 없다). 잠금 경로를 아예
  타지 않으며, 저작이 false 여도 무해하다.

**결정론**: fan-out 은 스코프 안 모든 후보를(셀 바인딩이면 모든 **칸**을) **정확히 1회** 맞히므로 `PatternTargeting` 의 선택
규칙 자체를 타지 않는다 — 「중복 셀은 스냅샷 index 로 tie-break」 구멍이 **결과에 영향을 주지
않는다**(누가 맞았나가 순서와 무관하다).
⚠ 이 면제는 fan-out 한정이다 — 단일 선택(RoundRobin/Shuffle) 경로의 후속 후보
「defender 패턴 개통 시 안정 키 필요」는 **그대로 살아 있다.**

⚠ **동시 착탄이다(v1).** 발 사이 캐스케이드(융단폭격이 줄지어 떨어지는 느낌)는 요청마다
`flightTime += k * stagger` 한 줄이면 되지만 저작 필드가 하나 더 늘어난다 —
`DrainMeteorBarrageRequests` 의 `landed * meteorStaggerSec` 관용구가 선례다. Play 에서 동시
착탄이 밋밋하면 그때 연다(후속 후보).

### `PatternScope` (순수 계층 — 아키텍처 무참조)

```
// 후보 셀 중 host 반경 안의 것의 **원본 index** 를 outIndices 에 채우고 개수를 반환.
static int Filter(in NativeArray<int2> candidateCells, int2 hostCell, int tileRange,
                  NativeArray<int> outIndices)
```

- `tileRange <= 0` → 전량 통과(현행 동작). **이 arm 이 무회귀의 근거다.**
- **셀 중복 제거를 여기서 하지 않는다.** 이 함수는 **반경 필터**이고 「한 칸에 몇 발이냐」는
  궤적의 성질이라 소비자가 정한다 — 셀을 겨누는 궤적은 emitter 가 **칸당 1발**로 접고,
  엔티티를 겨누는 궤적은 안 접는다.
  ⚠ 처음엔 「1:1 이라 접으면 한 명이 공짜로 산다」로 아예 안 접었는데, **그 전제가 셀
  바인딩에서 거짓**이었다(리뷰 지적 → 실측): `TileAoe` 는 `impactTileRange 0` 이어도 그 칸
  전원을 때리므로 접어도 아무도 안 살고, 안 접으면 오히려 **각자 N배**를 맞는다(2기 → 각 160).
- 결과는 **원본 index 오름차순**이다.

### emitter 결선

```
scope > 0 이면:
    count = PatternScope.Filter(pool.cells, hostCell, scope, scratchIndices)
    scoped 배열로 Select 를 호출하고, 반환 index 를 scratchIndices 로 **원본 index 로 되돌린다**
scope == 0 이면 기존 경로 그대로 (할당 0)
```

⚠ **index 공간을 섞지 말 것 (hard contract).** `reselectPerShot=false` 잠금 경로는
`IndexOf(poolEntities, target)` 로 **원본 풀 index** 를 얻어 `poolCells[cellIdx]` 를 읽는다
(`ProjectileEmitterSystem` 내). scoped index 를 그대로 흘리면 엉뚱한 칸을 때리거나 OOB 다.
**필터의 출력은 항상 원본 index 로 환원해 반환한다.**

⚠ **잠금은 scope 를 우회한다** — 잠근 대상이 스코프 밖으로 걸어나가도 남은 발이 따라간다.
fan-out 은 `reselectPerShot=true` 라 미해당이지만, `scope × 잠금` 조합의 알려진 한계로 기록한다.

⚠ **`Allocator.Temp` 를 발-루프 안에서 잡지 말 것.** 풀은 프레임-로컬이고 `hostCell` 은 버스트
내내 고정이라 결과가 매 발 동일하다. `OnUpdate` 스코프에서 1회 할당 + **인스턴스당 1회 계산**으로
호이스트한다(발 루프 안에 두면 host × instance × shot 만큼 쌓인다).

⚠ **`Select` 에 넘기는 `gridSize` 는 원본 그대로.** rank 가 `gridSize` 로 row-major 키를 만들므로
스코프 배열 길이를 넘기면 순위가 조용히 달라진다.

- `hostCell` = `GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, ff.origin)`. 시스템이 이미
  `hostPos` 를 갖고 있다.
- **Direction 바인딩은 후보 풀을 안 쓰므로 무관** = 머신거너/샷거너 다연발 무접촉(실측 확인됨).

### 덤 — 잘못된 주석 정정

`MovementBinding.cs` 주석이 "미분류 kind 는 emitter 가 loud warn 후 발사 소모"라 하지만 emitter 에
warn 경로가 **없다**(실제로는 방향탄을 쏜다). 이 폴더를 건드리는 김에 정정한다.

## 완료 기준

- [x] compile 0 error (신규 `.cs` — `refresh_unity scope=all`)
- [x] **`grep "using Unity.Entities"` 가 `PatternScope.cs` 에서 0건** (계약 1 기계 검증)
- [x] EditMode `PatternScopeTests`
  - `tileRange 0` → 전량 통과 + 원본 순서 보존 (**무회귀 핀**)
  - 반경 안/밖 분리가 Chebyshev 로 맞음
  - **같은 셀 후보 3개 → 결과 3개**(이 함수는 반경 필터일 뿐 — 칸 접기는 emitter 몫)
  - 반환값이 **원본 index** 다 (scoped index 가 새지 않는다)
  - 반경 안 후보 0 → 반환 0
- [x] EditMode: `TryToSpec` 이 신규 필드 2개를 복사한다 (**조용한 0 = 맵 전체 폭격** 방지 핀)
- [x] EditMode `EmitterTickTests`·`PatternTargetingTests` **무변경 전량 통과**(이 unit 이 그
      두 파일을 안 건드린다는 증거)
- [x] PlayMode 는 이 unit 에서 돌리지 않는다 — 사슬 끝(unit 2)이 자연스러운 관측점
      («적마다 미사일 1발»)으로 scope·fan-out·트리거를 한 번에 검증한다
      (2026-08-16 사용자 결정: unit 단위 PlayMode → 사슬 끝 1회)
