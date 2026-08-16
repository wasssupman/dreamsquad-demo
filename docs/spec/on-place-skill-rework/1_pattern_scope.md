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
- `.../Emission/EmitterTick.cs` — `Begin` 에 후보 수 전달
- `.../Emission/ProjectileEmitterSystem.cs` — 스코프 필터 + fan-out 발수
- 신규 `Assets/_Project/Tests/EditMode/PatternScopeTests.cs` + `EmitterTickTests` 케이스

## 구현

### 필드 2개

```
int  scopeTileRange = 0;          // 0 = 맵 전체(현행). >0 = host 셀 기준 Chebyshev 반경
bool fanOutToAllCandidates = false; // false = 현행(발마다 후보 1개 선택)
```

둘 다 **후보 선택의 성질**이라 패턴 SO 소유다(`selection`·`shots` 와 같은 자리, 계약 3 위반 아님).

⚠ **`TryToSpec`(`ProjectilePatternData.cs:76~`)이 `PatternSpec` 의 유일한 writer 다.** 두 struct 에만
필드를 더하고 여기를 잊으면 **조용한 0 = 맵 전체 폭격**이 된다. 완료 기준에 복사 단언을 둔다.

### fan-out 의미 — `shots` 계약을 깨지 않는다

`shots` 는 **「한 표적에게 몇 발」** 로 읽는다. fan-out 은 그 스케줄을 후보마다 반복한다:

```
burstRemaining = fanOut ? (후보 수 × shots.Length) : shots.Length
selection      = RoundRobin  →  k = fireCount % n  →  후보 0..n-1 을 정확히 1회씩 순회
baseFireCount  = 0           →  rank 0 부터 시작 (시드가 0 이 아니면 순회가 어긋난다)
```

- `EmitterTick.Begin(ref rt, spec, baseFireCount, candidateCount)` 로 후보 수를 받는다.
  **후보 수는 발화 시점에만 알 수 있다** — 그래서 순수 계층이 세지 않고 아키텍처가 세어 넘긴다
  (`ShotOrder` 가 Entity 를 모르는 것과 같은 분업).
- `reselectPerShot` 는 fan-out 에서 **참이어야 한다**(발마다 다른 후보). false 와 조합되면
  loud warn 후 fan-out 을 끈다 — 잠금과 순회는 양립 불가다.
- 후보 0 → `burstRemaining = 0`. **발사가 아예 시작되지 않는다**(발사 소모도 없다).
  ⚠ 현행 "후보 0 이면 발사를 소모하고 skip" 과 다른 동작이며, fan-out 한정이다.

**결정론**: fan-out 은 모든 후보를 **정확히 1회** 맞히므로 rank tie-break 순서가 바뀌어도
결과(누가 몇 대 맞았나)가 동일하다. `PatternTargeting.cs` 의 「중복 셀은 스냅샷 index 로
tie-break」 구멍이 **fan-out 경로에서는 결과에 영향을 주지 않는다.**
⚠ 이 면제는 fan-out 한정이다 — 단일 선택(RoundRobin/Shuffle) 경로의 후속 후보
「defender 패턴 개통 시 안정 키 필요」는 **그대로 살아 있다.**

### `PatternScope` (순수 계층 — 아키텍처 무참조)

```
// 후보 셀 중 host 반경 안의 것의 **원본 index** 를 outIndices 에 채우고 개수를 반환.
static int Filter(in NativeArray<int2> candidateCells, int2 hostCell, int tileRange,
                  NativeArray<int> outIndices)
```

- `tileRange <= 0` → 전량 통과(현행 동작). **이 arm 이 무회귀의 근거다.**
- **셀 중복 제거를 하지 않는다.** 같은 칸에 적 둘이면 후보도 둘이다 — 캐논은 **적 1기당 1발**
  (1:1 타격)이 사양이므로 dedupe 하면 한 명이 공짜로 산다.
  (rev2 초안은 dedupe 를 넣었다가 이 사양에서 뒤집혔다. 되돌리지 말 것.)
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

- [ ] compile 0 error (신규 `.cs` — `refresh_unity scope=all`)
- [ ] **`grep "using Unity.Entities"` 가 `PatternScope.cs` 에서 0건** (계약 1 기계 검증)
- [ ] EditMode `PatternScopeTests`
  - `tileRange 0` → 전량 통과 + 원본 순서 보존 (**무회귀 핀**)
  - 반경 안/밖 분리가 Chebyshev 로 맞음
  - **같은 셀 후보 3개 → 결과 3개**(dedupe 하지 않는다 — 1:1 사양 핀)
  - 반환값이 **원본 index** 다 (scoped index 가 새지 않는다)
  - 반경 안 후보 0 → 반환 0
- [ ] EditMode `EmitterTickTests` 추가
  - fan-out: 후보 5 × shots 1 → 정확히 5발, RoundRobin 이 rank 0..4 를 **각 1회**
  - fan-out + `reselectPerShot=false` → loud warn + fan-out off
  - fan-out + 후보 0 → 0발, 발사 소모 없음
  - **비-fan-out 경로 전량 무회귀**
- [ ] EditMode: `TryToSpec` 이 신규 필드 2개를 복사한다 (조용한 0 방지 핀)
- [ ] EditMode: `reselectPerShot=false × scope>0` → 잠금 대상 셀이 원본 index 로 해석된다
- [ ] PlayMode: 보스 융단폭격·나이트메어 미사일·머신거너 다연발이 **이전과 동일**(두 필드 기본값 경로)
