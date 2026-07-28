# 슬래셔 보스 — 설계 (2026-07-29)

> 브레인스토밍 결과물(얇은 설계). 구현 단위와 계약 상세는 `docs/spec/boss-slasher/` 에 둔다.
> 이 문서는 "무엇을 왜 이렇게 정했나" 와 "리뷰에서 나온 필수 준수 사항" 만 담는다.

## 목표

두 번째 보스를 추가한다. 빠른 공속 근접 **학살자** — 밀집 배치를 응징해 분산을 유도한다.
기존 보스 나이트메어(느리고 단단한 원거리 폭격형)와 축을 달리해서, `bossPool` 로테이션으로 공존한다.

## 검증 질문

> 슬래셔가 방어유닛을 스스로 사냥하며 cleave 3 으로 밀집을 갈아내고, 최대체력 20% 경계마다
> 자기중심 폭발 후 밀집 지점으로 도약하는가? 나이트메어와 `bossPool` 로 공존하면서
> **기존 7덱의 웨이브 편성은 무회귀**인가?

## 확정 사항

### 정체성

- `displayName` = 슬래셔 · `id` = `boss_slasher`
- 외형: 나이트메어와 같은 모듈러 Spine 스켈레톤 + `partSkins` 조합·스케일 변경 (신규 아트 0)

### 스탯

게이머 렌즈의 실효 DPS 추산(방어유닛 20종 dps/코스트 + 코스트 예산 + 보스 조우 시점)을 반영한 값이다.

| 필드 | 값 | 근거 |
|---|---|---|
| `health` | 950 | 700 은 마지막 보스 웨이브에서 3.3초 생존 → 능력이 발동할 시간이 없다 |
| `moveSpeed` | 2.2 | 뱅가드 2.2 / 베이직 2.5 대역. "보스가 잡몹 속도로 달려온다" 가 차별화의 본체 |
| `attackCooldown` | 0.6 | 나이트메어 1.2 의 절반. 공속이 정체성 |
| `attackTargetCount` | 3 | 근접 cleave. **`attackMethod` = Melee + `projectile` = null 이 전제** (projectile 을 채우면 cleave 가 조용히 사라진다) |
| `attackRange` | 2 | 6개 맵 전부 배치칸이 경로 인접(거리 1)이라 1 이면 교전이 성립하지 않는다 |
| `outputs[0]` Damage | 30 | 실효 225 spread dps (나이트메어 83 의 2.7배). 45 는 337 = 4배로 과다 |
| `hitDelaySec` | 0.25 | |
| `killScore` / `awakeningReward` | 2000 / 5 | 나이트메어와 동일 |

### 능력 — 상시 압박 1 + 사건 구동 2

- **상시**: cleave 3 × cd 0.6 (스탯. 별도 mechanic 아님)
- **진동갑주** `HealthThreshold(fraction 0.20)` × `SelfTileAoe(반경 2)` — 경계마다 자기중심 폭발
- **집단 도약** `HealthThreshold(fraction 0.20)` × `SelfBlink` — 방어유닛 밀집도 최대 지점 착지

HP 950 · fraction 0.20 → 760 / 570 / 380 / 190 에서 **각 4회**. 래치 단조라 회복해도 재발동하지 않는다.

**발동 순서는 "폭발 → 도약" 이고 아키텍처가 결정한다.** `SelfTileAoe` 캐리어는
`HealthThresholdSystem` 끝의 ECB playback 으로 **현재 위치**에서 터지고, `SelfBlink` 는
`BlinkRequestEventsSingleton` seam 을 거쳐 Movement 가 나중에 위치를 옮긴다. 따라서 같은 fraction 을
줘도 결정론적으로 폭발이 먼저다. 읽히는 서사는 "제자리를 쓸어버리고 다음 무리로 뛴다".
역순("뛰어들어 터진다")을 원하면 별도의 순서 장치가 필요하므로 이번 범위에서 제외한다.

**타이머 구동 능력을 넣지 않은 이유**: 보스 조우는 t≈50 / 110 / 170초이고 그 시점 플레이어 화력으로
보스 생존은 4~7초다. 9~10초 주기 능력은 3회 조우 중 2회 이상 0발이고, 가장 극적이어야 할 마지막
웨이브에서 사라진다. **슬래셔의 시계는 초가 아니라 사건이다.**

### 어그로 · CC 면역 (`BossTag` 전체 — 나이트메어도 함께 바뀐다)

- **어그로 면역**: 보스는 `boss-defender-field` 로 이미 방어유닛을 전멸까지 스스로 사냥한다. 어그로로
  끌려갈 필요가 없다. 근본 원인은 `Aggroed` 가 붙는 순간 `AttackSystem:947` 이 타겟 수를 1로 강제해
  cleave 3 을 소멸시키고, `MovementSystem:97` 의 `Chasing` 조기 return 이 사냥 분기(`:122`)보다 앞이라
  보스가 가디언만 쫓게 되는 것이다. **코스트 2 가디언 1기가 이 보스의 정체성 전체를 껐다.**
- **CC 완전 면역**: Stun / Sleep / Impulse 전부 무효.
- **대가(사용자 확정 2026-07-29)**: 방어 측 CC 카드 — 아이스캐스터 스턴, `lullaby_dart` 수면, 넉백 —
  이 보스전에서 무효가 된다. 자석 아키타입(가디언 어그로 전략)도 보스 상대로는 사장된다. 수용한다.

### bossPool

- **`bossUnit` 을 rename 하지 않는다.** 라이브 덱 9개(`Deck_{Serpent,Coil,Twin,Spiral,Zig,Hook,Endless}`
  + `WaveA` / `WaveB`)가 `bossUnit` guid 를 들고 있다. rename 하면 YAML 키가 orphan 이 되고 생성기는
  `null` 을 graceful no-op 로 처리하므로 **에러도 경고도 없이 전 맵에서 보스만 사라진다.**
- `bossUnit` 유지 + `bossPool` 추가 + `ResolveBossPool()` 폴백. 선례: `AttackDeck.ResolveAttackUnitPool()`
- **`bossPool.Count == 1` 이면 `rng.NextInt` 를 소비하지 않는다** → 기존 7덱의 rng 스트림이
  byte-identical → 웨이브 편성 무회귀.
- `waveGeneratorVersion` 은 올리지 않는다. 순수 로그 라벨이고 런타임이 소비하지 않는다.
- 보스 결정론은 **생성기 안에서 뽑으면 자동 성립**한다(프리뷰·런타임이 같은 `Generate` 를 탄다).
  위험은 생성기 밖(브리지/스폰 시점)에서 뽑는 것뿐 — 하지 않는다.
- 기존 생성기 불변식("`bossUnit` 은 `attackUnitPool` 에 넣지 않는다 — 생성기가 방어적으로 제외")을
  배열 루프로 확장한다.

## 리뷰에서 나온 필수 준수 사항

게이머 렌즈·기술 렌즈 병렬 리뷰가 둘 다 REWORK 를 냈고, 그 실체는 아래다. 구현 시 반드시 지킨다.

1. **`HealthThresholdSystem` 쿼리에 `WithNone<DeadTag>` 를 추가한다.** 현재 없다. `DamageApplicationSystem`
   이 자기 `OnUpdate` 끝에 playback 하므로 죽는 프레임에 `DeadTag` 가 이미 붙어 있고, 오버킬 시
   시체가 경계를 하나 더 넘어 blink 한다. C 가 `SelfBlink` 의 첫 라이브 사용처이므로 여기서 막는다.
2. **밀집도 선택은 순수 함수 + EditMode.** 제약 10 의 (a)비자명 (c)sim-critical 에 해당하고, 같은
   카테고리가 이미 분리돼 있다 — `PatternTargeting.Select` 헤더가 문자 그대로 "순수 수학 + EditMode
   고정(제약 10 — sim-critical 타겟팅)". 인라인이 관례 이탈이다. tie-break 는 **row-major 셀 키 rank**
   (청크 순서 의존 금지).
3. **밀집 최대 셀도 `BlinkMath.TryFindLandingCell` 을 통과시킨다** — walkable ∧ connected 보장.
4. **`SelfBlink` 착지 정책은 필드 추가가 아니라 교체.** 라이브 authoring 사용처가 0이다
   (`Enemy_Boss_Nightmare.asset` 의 3 mechanic 이 전부 `kind: 4` = PeriodicTimer). 제약 8 대로 교체하고,
   `HealthThresholdSystem` 의 관련 주석과 `nightmare-catcher` 의 "위협 리더 근처" 계약을 **같은 커밋**에서
   갱신한다. `ThreatEntry` / threat drain 은 별 책임이니 남긴다.
5. **`SelfTileAoe` 캐리어의 `targetFaction` 을 host 진영에서 도출한다.** 현재 기본값이 `Enemy` 라
   보스가 쓰면 자기 진영을 때린다. `BuildPatternTemplate` 의 `hostIsEnemy` 도출이 선례다.
6. **보스 bake 경로가 `SelfTileAoe` 의 AOE 연출 인덱스를 채우는지 확인한다.** defender 경로는
   ProjectileData 가 없으면 loud skip 한다 — 보스 경로에 같은 가드가 있는지 미확인.
7. **bake 에 degenerate 값 loud 거절을 추가한다.** 현재 `fraction` / `periodSeconds` / `period` 를
   검증하지 않고 순수함수들이 조용히 false 를 반환한다. 오타 하나에 능력이 로그 없이 사라진다.
   bake 에 loud 거절 선례가 이미 4개 있다.
8. **asset 필드 누락 주의**: `enemyClass`, `targetMode`, `engageMovement`, `hitDelaySec`, `walkAnimation`,
   `minWaveNumber`, `killScore`(>0 을 강제하는 테스트가 있다), 그리고 `EnemyCatalog` 등록.
9. **`maxHpRef` 스냅샷 계약을 문서화한다.** 현재는 안전하다(`MaxHealthScaleSystem` 의 입력이
   `DefenderUnitTag` 전용, 웨이브 램프는 수량만 조정). 단 **적에게 `MaxHealthMul` 을 거는 기믹/카드가
   생기면 경계 4회 보장이 깨진다.** `BountyMark` 가 이미 적에게 `DmgTakenMul` 을 걸고 있어 문턱이 낮다.
10. **`DcTriggerSlot` 버퍼 베이크 루프에 `AddComponent` / `AddBuffer` 를 추가하지 않는다.**
    현 코드가 `AddBuffer` 3회를 전부 루프 밖에 두고 루프 안에서는 재획득하는 전제로 쓰여 있다.

## 범위 밖 (Follow-up Backlog 이관 후보)

- **보스 트리거 개방** — `AttackN`(대회전 = 3타마다 강공) / `OnKill`(학살 가속) arm 의 defender 전용
  게이트 완화. 세 가지를 함께 처리해야 한다: bake 가드가 `PeriodicTimer`/`HealthThreshold` 외 trigger 를
  skip 하는 것, `AttackSystem` 의 pre-scan·counter 루프가 둘 다 `defenderTagLookup` 안인 것, 그리고
  `OnKill × SelfStatBuff` 의 보스 bake 가 `buffStat`/`statBuffStackId` 를 안 채우고 magnitude 를
  무변환으로 넣어 **공속 +25% 가 `DamageMul` +2400% 로 착지**하는 버그. 게이트는 통째로 제거하지 말고
  payload 화이트리스트로 좁힌다(투사체 재조준 후보 풀이 적 전용이라 진영 전제가 깨지면 아군 오사).
- **주기 구동 폭격** — 나이트메어의 10초 융단폭격과 중복이고, 보스 생존 4~7초 안에 발동하지 못한다.
- **`ModifierOrigin` 일관성** — `OnKill` 경로만 `Dreamcatcher` 로 태깅한다(다른 두 경로는 `Boss` /
  `HealthThreshold`). 현재 오작동은 없다 — 오라 판정 쿼리가 `DefenderUnitTag` 게이트이고 origin 은
  merge 키가 아니다. defender 카드 전용 시절의 잔재.
- **프리뷰 / 런타임 seed 불일치** — 드래프트·준비 화면은 스트립 자기 deck + seed 폴백, 런타임은
  `ActiveDeck` + `DeriveWaveSeed(matchSeed)`. 기존 불일치지만 보스가 2종이 되면 **프리뷰가 다른 보스
  이름을 보여준다.** 일시정지 메뉴 경로만 정확하다.
- **`.claude/skills/ecs-reviewer/references/` 부재** — ecs-reviewer 정의의 체크리스트 로드 단계가 조용히
  no-op 이다. 정의를 고치거나 파일을 만들어야 한다.
- **CLAUDE.md 의 Unity 버전 표기** — `6000.4.3f1` 로 적혀 있으나 실제는 `6000.4.7f1`.
- **적별 피격 반경** — `attackRange 2` + `attackTargetCount 3` 의 실제 cleave 체감(방어유닛 3기가 동시에
  사거리에 들어오는 빈도)은 플레이로만 알 수 있다. `docs/spec/README.md` 의 기존 후속 후보와 얽힌다.

## 구현

작업 단위는 `docs/spec/boss-slasher/` 에 `README.md` + `0_*.md` ~ `N_*.md` 로 작성한다.
