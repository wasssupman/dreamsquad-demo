# 0 — `AttackDeck.bossPool` + 생성기 선택

## 목적

보스를 2종 이상 담을 수 있게 하고, 웨이브마다 결정론적으로 하나를 뽑는다.
**이 작업 단위는 Slasher asset 없이 완결한다** — 빈 pool 폴백으로 기존 7덱 웨이브 무회귀를 먼저 증명하는 것이
목적이다. 보스가 추가되기 전에 안전망을 깔아둔다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `bossPool` 필드 + `ResolveBossPool()`
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 보스 선택 + pool 방어 루프
- `Assets/_Project/Tests/EditMode/WavePatternGeneratorBossTests.cs` — 신규 케이스 + positional 인자 수정
- `Assets/_Project/Tests/EditMode/WaveKillBudgetPinTests.cs` · `WaveSpawnLeadInTests.cs` — 시그니처 변경 대응

## 구현

**`bossUnit` 을 rename 하지 않는다.** 라이브 덱 9개(`Deck_{Serpent,Coil,Twin,Spiral,Zig,Hook,Endless}` +
`WaveA`/`WaveB`)가 `bossUnit` guid 를 직렬화하고 있다. rename 하면 YAML 키가 orphan 이 되고 값이 유실되며,
생성기가 `null` 을 graceful no-op 으로 처리하므로 **에러도 경고도 없이 전 맵에서 보스만 사라진다.**

- `bossUnit`(기존, 단일) 유지 + `bossPool`(신규, `AttackUnitData[]`) 추가.
- `ResolveBossPool()` — `bossPool` 이 비어 있으면 `bossUnit` 단일 원소로 감싸 반환. 둘 다 비면 빈 배열.
  선례: 같은 파일의 `ResolveAttackUnitPool()`.
- `bossPool` 내 `null` 원소는 걸러낸다(authoring 실수 방어).

**보스 선택은 `Generate` 안에서만 한다.** 프리뷰(`WavePatternStripView`)와 런타임이 같은 `Generate` 를 타므로
결정론이 자동 성립한다. 생성기 밖(브리지·스폰 시점)에서 뽑으면 프리뷰와 런타임이 갈라진다.

- **`Count == 1` 이면 `rng.NextInt` 를 소비하지 않는다.** 기존 7덱은 pool 이 단일이므로 rng 스트림이
  byte-identical 해야 웨이브 편성이 무회귀다. `Count >= 2` 일 때만 rng 를 소비한다.
- 기존 생성기 불변식("`bossUnit` 은 `attackUnitPool` 에 넣지 않는다 — 생성기가 방어적으로 제외하고
  있으면 경고")을 **pool 전체 원소에 대한 루프**로 확장한다.
- `waveGeneratorVersion` 은 올리지 않는다. 순수 로그 라벨이고 런타임이 rng 에 투입하지도, stale 플랜을
  거부하지도 않는다.

## 완료 기준

- 컴파일 통과. 시그니처 변경으로 깨지는 테스트 3파일 수정 완료.
- **EditMode 신규**:
  - 같은 seed 로 두 번 `Generate` → 보스 선택이 동일(결정론).
  - `bossPool.Count == 1` 일 때 기존 `bossUnit` 경로와 **전 웨이브 groups 가 완전 동일**(rng 미소비 증명).
  - `bossPool` 이 빈 배열 → `bossUnit` 폴백이 동작.
  - `bossPool` 원소가 `attackUnitPool` 에 섞여 있으면 잡몹 pool 에서 제외 + 경고.
  - `bossPool` 에 `null` 원소가 있어도 크래시 없음.
- **기존 EditMode 전량 통과** — 특히 `NonBossWavesMatchBossOffPlanAtSameSeed`.
- 라이브 덱 9개 asset 을 **건드리지 않은 상태로** 기존 보스가 5·10웨이브에 그대로 등장(Play 1회 확인).

---

**확인 2026-07-29 · 커밋 `bbfc06c1`** — EditMode 신규 7개 통과(단일풀=레거시 동일 · 폴백 · 보스없음 no-op ·
결정론 · 30 seed 로테이션 실증 · null 필터 · pool 누출 방어). 보스 테스트 12/12, 전체 EditMode 1557 중
1555 통과·실패 0·스킵 2(기존 `[Ignore]`). Play 항목은 `SingleEntryBossPoolIsIdenticalToLegacyBossUnit`
이 전 웨이브 group 단위 동일성으로 더 강하게 커버.
