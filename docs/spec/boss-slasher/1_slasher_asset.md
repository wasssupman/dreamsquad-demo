# 1 — Slasher asset + AOE ProjectileData + 카탈로그

## 목적

슬래셔의 몸을 만든다. **`nightmareMechanics` 는 비워 둔다** — 능력 없는 상태의 외형과 cleave 3 타격을
먼저 확정해서 뒤따르는 작업 단위(2~4)의 디버깅 기준선을 만든다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Boss_Slasher.asset` (신규 `AttackUnitData`)
- `Assets/_Project/Data/Projectiles/` 아래 AOE 연출용 `ProjectileData` 1개 (신규 — unit 3 이 소비)
- `Assets/_Project/Data/EnemyCatalog.asset` — 등록
- 덱 asset 은 **아직 건드리지 않는다**(`bossPool` 투입은 unit 3 이후)

## 구현

### 스탯

| 필드 | 값 |
|---|---|
| `id` / `displayName` | `boss_slasher` / 슬래셔 |
| `health` | 950 |
| `moveSpeed` | 2.2 |
| `attackCooldown` | 0.6 |
| `attackTargetCount` | 3 |
| `attackRange` | 2 |
| `outputs[0]` | Damage 30 |
| `hitDelaySec` | 0.25 |
| `killScore` / `awakeningReward` | 2000 / 5 |

**`attackMethod` = Melee, `projectile` = null 이 `attackTargetCount 3` 의 전제다.** `projectile` 을 채우면
`AttackSystem` 이 투사체 분기를 타서 cleave 가 **조용히** 사라진다.

`attackRange` 를 1로 낮추지 않는다 — 6개 맵 전부 배치칸이 경로 인접(Chebyshev 거리 1)이라 1 이면 교전이
성립하지 않고, `boss-defender-field` 의 사거리 소스도 2 여야 정상이다.

### 누락되기 쉬운 필드 (전부 채운다)

`enemyClass`(Bruiser 계열), `targetMode`, `engageMovement`, `targetPriorityClass`, `targetClassMask`,
`walkAnimation`/`idleAnimation`/`attackAnimation`/`deathAnimation`, `minWaveNumber`.
`killScore` 는 0 이면 기존 EditMode 테스트가 실패한다(>0 강제).

### 외형

나이트메어와 **같은 `skeletonDataAsset`** 을 쓰고 `partSkins` 조합·`spineVisualScale`·`slotColors` 로
실루엣을 구분한다. 신규 Spine 아트 0. 나이트메어의 `partSkins` 5줄 구성을 참고 기준으로 삼는다.

### AOE 연출 ProjectileData

unit 3 의 진동갑주 폭발이 `payload.projectile` 참조를 **필수**로 요구한다(없으면 폭발 요청이 통째로
드롭되어 데미지까지 안 나간다). SkyFall × TileAoe 경로를 타는 기존 폭발 계열 `ProjectileData` 를
복제해 만들고, `hitPrefab` 이 실제 히트 VFX(`vfx_Hit_*`)를 가리키는지 확인한다 — GA 계열 일부가
머즐 프리팹을 가리키고 있는 기존 함정이 있다.

## 완료 기준

- `EnemyCatalog.asset` 에서 슬래셔가 조회된다.
- 임시로 `bossPool` 에 넣고 Play → 스폰되고, **"꿈결 위기!!" 배너가 뜬다**(`nightmareMechanics` 가 비어도
  `BossTag` 경로가 동작하는지 확인. 배너 트리거가 `nightmareMechanics` 비어있지 않음 조건이면 이 시점엔
  안 뜨는 것이 정상 — 어느 쪽인지 관찰해 unit 3 완료 기준에 반영).
- 나이트메어와 **육안으로 구분되는 실루엣**(사용자 확인).
- 방어유닛 3기를 인접 배치 → **한 번의 공격에 3기가 동시에 피해**를 받는다.
  단 가디언이 있으면 타겟 수가 1로 강제되므로 **가디언 없는 편성으로 확인**한다(unit 2 이전이므로).
- 콘솔 경고 0. `.meta` 파일이 asset 과 함께 커밋됐다.
