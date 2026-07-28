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
- `MovementBinding.cs` — `MovementKind` → 타겟 바인딩 클래스(Entity/Cell/Direction) 순수 분류 (README 계약 11 — emitter 의 분기 축. `MovementKind` 는 무의존 enum 이라 로직 계층 적격)

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
    int   fireCount;          // 선택 규칙의 결정론 소스 — Begin 이 baseFireCount 로 시드
    int   shotIndex;          // 현재 버스트 내 순번 (베지어 스윙 소스)
}

static int  EmitterTick.Advance(ref EmitterRuntime rt, float dt, float intervalSec)
static void EmitterTick.Begin(ref EmitterRuntime rt, in PatternSpec spec, int baseFireCount)
```

`Advance` 는 `VolleyMath.TickBurst` 와 같은 계약(잔여 캐리로 드리프트 0, `interval<=0` = 남은 전부 즉시)이라 **그 함수를 그대로 호출**한다. `EmitterTick` 은 `fireCount`/`shotIndex` 전진과 완주 판정(`burstRemaining==0`)만 얹는 얇은 래퍼다 — 중복 구현 금지.

`Begin` = 인스턴스 시작(`burstRemaining = shotCount`, `timer = 0`, `fireCount = baseFireCount`). 첫 발은 시작 프레임에 나간다.

**`baseFireCount` 는 계약이다 (spec-review C2).** 인스턴스는 트리거 발화마다 생성·완주 후 제거되는 transient 라, `fireCount` 를 0 에서 시작하면 RoundRobin 은 영원히 rank 0(같은 대상만 폭격), 셔플은 `hash(0)` 고정(같은 대상만 저격)이 된다. 영속 카운터는 durable 소유자(트리거 슬롯)가 들고, 인스턴스는 시드만 받는다 — 기존 `slot.fireCount` 가 슬롯에 영속하는 이유와 동일. 시드/증가 배선은 unit 3.

**타겟 잠금(`reselectPerShot=false`)은 순수 계층 상태가 아니다 (spec-review H1).** 후보 스냅샷은 매 프레임 재빌드되므로 "첫 발의 후보 index" 를 순수 상태로 잠그면 프레임을 넘는 버스트에서 같은 index 가 다른 유닛을 가리킨다. 잠금 대상의 신원(Entity)은 아키텍처 바인딩이다 — `EmitterInstance.lockedTarget` (unit 2, template 과 같은 결). 순수 계층은 `spec.reselectPerShot` 로 "재추첨하는가" 만 답한다.

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

`BuildOrder` 가 **명령 자료구조를 완성하는 유일한 지점**이다(`shotIndex`/`fireCount` 전진 포함). 아키텍처 계층은 이 order 를 받아 자기 형태(ECS = `ProjectileSpawnRequest`, Mono = 자기 발사 파라미터)로 번역만 한다. 단 잠금 신원 저장은 아키텍처 몫이다(위 H1 항목).

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

### `MovementBinding`

```
enum BindingClass : byte { Entity, Cell, Direction }
static BindingClass MovementBinding.Of(MovementKind kind)
```

순수 switch 하나. **새 `MovementKind` 를 여기 분류하는 것이 emitter 편입의 전부다** — 기존 클래스로 분류되면 emitter 는 무변경으로 그 궤적을 발사한다.

분류 누락은 **컴파일러가 못 잡는다**(C# 은 switch expression 이든 문이든 enum 전수성을 강제하지 않는다 — CS8509 는 경고, 런타임 throw 는 Burst 에서 못 쓴다). 그래서 EditMode 핀으로 대신한다: `MovementBinding.KnownKindCount` 상수와 `Enum.GetValues(typeof(MovementKind)).Length` 를 대조하는 테스트가 있어, 새 kind 를 추가하면 **테스트가 실패해** 분류 갱신을 강제한다. `default` 는 `Direction`(미개통 = emitter 가 loud warn 후 소모)으로 떨어뜨린다.

## 완료 기준

- 컴파일 클린. 신규 `.cs` 추가이므로 `refresh_unity scope=all`(부분 refresh = cascading CS0246).
- **`grep -l "using Unity.Entities" ` 가 이 unit 의 신규 파일 7개에서 0건** (계약 1 기계 검증).
- `MovementBinding.Of`: 현 6 케이스 전 분류 + `KnownKindCount` 대조 핀(신규 kind 추가 시 **테스트 실패** — 컴파일러는 enum 전수성을 강제하지 못한다) EditMode 1.
- EditMode 신규 ≥ 12:
  - `EmitterTick`: 단발 · 버스트 정확 발수 · `interval<=0` 즉시 전부 · 느린 프레임 다중 발사 · 잔여 캐리 드리프트 0 · 완주 후 0
  - `Begin` 시드 연속성: `baseFireCount=k` 로 시작한 인스턴스의 선택이 "영속 카운터 k 인 상태" 와 동일 — **트리거 발화 2회 연속 시나리오에서 RoundRobin 이 다른 대상을 순회**하는 테스트(C2 회귀 핀)
  - `BuildOrder`: `shotIndex`/`fireCount` 전진 · order 필드 정확성
  - `PatternTargeting`: round-robin 순회 · 셔플 결정론(같은 fireCount = 같은 결과) · 셔플이 순회와 갈리는 지점 존재 · 셔플 분포(후보 4개 · 200발 전부 최소 1회) · 청크 순서 무관(후보 배열 셔플해도 동일 셀) · 중복 셀 한계 문서화 · `n==0` → −1
    - 흡수 전 `BarrageEpicenter` 와의 동일성 테스트는 unit 0 시점에 작성해 검증한 뒤 unit 4(원본 삭제)에서 함께 제거했다.
- 기존 EditMode 무회귀(현 905건 기준).
