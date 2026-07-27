# 0 — 정의 계층 + 로직 계층 (아키텍처 코드 0줄)

## 목적

발사 명세를 데이터로 만들고, 그 명세를 소비해 "이번 프레임에 무엇을 쏘는가"를 결정하는 로직을 **아키텍처 무참조 순수 함수**로 둔다. 이 unit 이 끝나면 ECS 코드 한 줄 없이 발사 결정 전체가 EditMode 로 검증된다(README 계약 1·2).

## 변경 대상

신규 (정의 계층):
- `Assets/_Project/Scripts/Data/ProjectilePatternData.cs` — 발사 명세 SO
- `Assets/_Project/Scripts/Data/PatternSpec.cs` — SO 값의 unmanaged 미러 + `PatternSelectionRule` enum

신규 (로직 계층, `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/`):
- `EmitterRuntime.cs` — 순수 스케줄 상태 struct
- `EmitterTick.cs` — 시간 전진 → 이번 프레임 발사 수
- `ShotOrder.cs` — 한 발의 발사 명령(plain 값)
- `PatternTargeting.cs` — 후보 rank 기반 선택 (round-robin + 결정론 셔플)

신규 테스트:
- `Assets/_Project/Tests/EditMode/EmitterTickTests.cs`
- `Assets/_Project/Tests/EditMode/PatternTargetingTests.cs`

## 구현

### `ProjectilePatternData` (SO, `Wassup/ProjectilePattern` 메뉴)

```
id                  string
barrel              ProjectileData     ← 탄 1발 명세. 효과/궤적/비주얼은 전부 여기 (계약 3)
damage              float              ← 패턴 소유 (카드/스킬 magnitude 컨벤션)
selection           PatternSelectionRule
shotCount           int    = 1         ← 한 번의 발사가 몇 발
shotIntervalSec     float  = 0         ← 그 발들 사이 간격 (0 = 동프레임 전부)
reselectPerShot     bool   = false     ← 발마다 타겟 재추첨(산개) / 첫 타겟 집중
telegraphSec        float  = 0         ← SkyFall 낙하 예고. ProjectileData 에 없는 유일한 값
```

`impactTileRange`·`splashRadius`·`arcHeight`·`pierceCount`·`dropHeight` 는 **넣지 않는다** — barrel 이 이미 갖고 있다(계약 3).

### `PatternSpec` (unmanaged 미러)

SO 와 동일 필드에서 `barrel` 만 `int barrelDataIndex` 로 치환한다. asset 참조 → 정수 핸들이며, 핸들 해석은 아키텍처 몫이다(ECS = 브리지 `GetOrCreateProjectileDataIndex` 레지스트리, Mono 라면 자기 테이블). `PatternSpec` 자체는 `UnityEngine`·`Entities` 무참조 — `Unity.Mathematics` 만.

`PatternSelectionRule { RoundRobin, DeterministicShuffle }` — v1 어휘 2종(소비자: 융단폭격 / 미사일).

### `EmitterRuntime` + `EmitterTick`

```
struct EmitterRuntime {
    int   burstRemaining;
    float timer;
    int   fireCount;          // 인스턴스 누적 발사 수 — 선택 규칙의 결정론 소스
    int   shotIndex;          // 현재 버스트 내 순번 (베지어 스윙 소스)
    int   lockedTargetIndex;  // reselectPerShot == false 일 때 첫 발의 후보 index (−1 = 미확정)
}

static int  EmitterTick.Advance(ref EmitterRuntime rt, float dt, float intervalSec)
static void EmitterTick.Begin(ref EmitterRuntime rt, in PatternSpec spec)
```

`Advance` 는 `VolleyMath.TickBurst` 와 같은 계약(잔여 캐리로 드리프트 0, `interval<=0` = 남은 전부 즉시)이라 **그 함수를 그대로 호출**한다. `EmitterTick` 은 `fireCount`/`shotIndex` 전진과 완주 판정(`burstRemaining==0`)만 얹는 얇은 래퍼다 — 중복 구현 금지.

`Begin` = 인스턴스 시작(`burstRemaining = shotCount`, `timer = 0`, `lockedTargetIndex = -1`). 첫 발은 시작 프레임에 나간다.

### `ShotOrder` + `BuildOrder` — 로직이 만들고 아키텍처가 소비하는 자료구조

```
struct ShotOrder {
    int   shotIndex;            // 버스트 내 순번 (베지어 제어점 스윙 소스)
    int   targetCandidateIndex; // 후보 배열의 index — Entity 를 모른다 (계약 2)
    float damage;
    int   barrelDataIndex;
    float telegraphSec;
}

static ShotOrder PatternLogic.BuildOrder(in PatternSpec spec, ref EmitterRuntime rt,
                                        int selectedCandidateIndex)
```

`BuildOrder` 가 **명령 자료구조를 완성하는 유일한 지점**이다. `reselectPerShot == false` 면 `rt.lockedTargetIndex` 를 확정/재사용하고(첫 발만 새로 뽑음), `true` 면 전달된 index 를 그대로 쓴다. 아키텍처 계층은 이 order 를 받아 자기 형태(ECS = `ProjectileSpawnRequest`, Mono = 자기 발사 파라미터)로 번역만 한다 — 선택·잠금 판단을 아키텍처에서 되풀이하지 않는다.

### `PatternTargeting`

`BarrageEpicenter.Select` 를 rule 축으로 일반화해 흡수한다(unit 4 에서 원본 삭제).

```
static int Select(in NativeArray<int2> candidateCells, PatternSelectionRule rule,
                  int fireCount, int2 gridSize)
```

- 후보를 **row-major 셀 키 rank** 로 정렬한 순위에서 뽑는다(계약 6). 청크 순서 무관 = 결정론.
- `RoundRobin`: `k = fireCount % n` — 기존 `BarrageEpicenter` 와 **비트 동일** 결과여야 한다(unit 4 무회귀 근거).
- `DeterministicShuffle`: `k = Hash(fireCount) % n`. Hash = `h = (uint)fireCount * 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;` (Burst 안전, 곱셈+시프트만). 연속 중복은 허용 — 그게 랜덤의 성질이고, 회피하려면 이전 선택 상태가 필요해 순수성을 깬다.
- `n == 0` → `-1`(호출자가 발사 소모 후 skip. 진앙 없는 융단폭격 선례).

`NativeArray` 사용은 Burst 호환을 위한 기존 선택과 일관하다(`ThreatTable`·`BarrageEpicenter`). Mono 이식 시 컨테이너 타입만 교체된다.

## 완료 기준

- 컴파일 클린. 신규 `.cs` 추가이므로 `refresh_unity scope=all`(부분 refresh = cascading CS0246).
- **`grep -l "using Unity.Entities" ` 가 이 unit 의 신규 파일 6개에서 0건** (계약 1 기계 검증).
- EditMode 신규 ≥ 12:
  - `EmitterTick`: 단발 · 버스트 정확 발수 · `interval<=0` 즉시 전부 · 느린 프레임 다중 발사 · 잔여 캐리 드리프트 0 · 완주 후 0
  - `BuildOrder`: `reselectPerShot=false` 면 버스트 전 발이 같은 `targetCandidateIndex` · `true` 면 발마다 전달값 반영
  - `PatternTargeting`: round-robin 순회 · **`BarrageEpicenter` 와 동일 결과**(같은 입력 비교) · 셔플 결정론(같은 fireCount = 같은 결과) · 셔플 분포(후보 4개 · 100 발 전부 최소 1회) · 청크 순서 무관(후보 배열 셔플해도 동일 선택) · `n==0` → −1
- 기존 EditMode 무회귀(현 905건 기준).
