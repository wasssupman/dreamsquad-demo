# boss-slasher — 두 번째 보스 "슬래셔"

> 상태: **작성 완료, 구현 대기** (2026-07-29). 설계 근거·리뷰 이력은 `docs/plans/2026-07-29-boss-slasher-design.md`.

## 목표

빠른 공속 근접 **학살자** 보스를 추가하고, 기존 보스 나이트메어와 `bossPool` 로테이션으로 공존시킨다.
나이트메어(느리고 단단한 원거리 폭격형)와 축을 달리해서 밀집 배치를 응징하고 분산을 유도한다.

## 검증 질문

> 슬래셔가 방어유닛을 스스로 사냥하며 cleave 3 으로 밀집을 갈아내고, 최대체력 20% 경계마다
> 자기중심 폭발 후 밀집 지점으로 도약하는가? 나이트메어와 로테이션으로 공존하면서
> **기존 7덱의 웨이브 편성은 무회귀**인가?

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_boss_pool_field.md` | `AttackDeck.bossPool` 추가(rename 금지) + 생성기 선택 + EditMode |
| 1 | 데이터 | `1_slasher_asset.md` | `Enemy_Boss_Slasher.asset` + AOE `ProjectileData` + `EnemyCatalog` 등록 |
| 2 | 시뮬 | `2_boss_immunity.md` | 보스 어그로 면역 + 직접 행동정지·넉백 면역(`EnemyCcEvent` 출처 필드) |
| 3 | 브리지 | `3_vibration_armor.md` | bake 에 `SelfTileAoe` 추가 + 진영 도출 + `mechanic[0]` |
| 4 | 시뮬 | `4_density_blink.md` | 밀집도 착지 순수함수 + 정책 교체 + `DeadTag` 가드 + `mechanic[1]` |
| 5 | 인계 | `5_handoff_summary.md` | 구현 종료 시 작성 |

**순서 근거**: 0 은 asset 없이 빈 pool 폴백으로 **기존 7덱 무회귀를 먼저 증명**한다. 2(면역)를
1(asset) 직후에 두어야 같은 보스로 "면역 전/후" 를 비교할 수 있다 — 면역이 없으면 가디언이 타겟 수를
1로 강제해서 **cleave 3 을 육안으로 검증할 수 없다**. 컴파일 선행 의존은 없다(`BossTag` 은 이미 존재).

## Feature-wide 계약

1. **`AttackDeck.bossUnit` 을 rename 하지 않는다.** 라이브 덱 9개가 guid 를 들고 있고, rename 하면 YAML
   키가 orphan 이 되어 생성기의 `null` graceful no-op 을 타고 **에러도 경고도 없이 전 맵에서 보스가
   사라진다.** `bossUnit` 유지 + `bossPool` 추가 + `ResolveBossPool()` 폴백(`ResolveAttackUnitPool()` 선례).
2. **`bossPool.Count == 1` 이면 rng 를 소비하지 않는다.** 기존 7덱의 rng 스트림이 byte-identical 해야
   웨이브 편성이 무회귀다. `waveGeneratorVersion` 은 올리지 않는다(순수 로그 라벨).
3. **보스 선택은 생성기 안에서만.** 프리뷰와 런타임이 같은 `Generate` 를 타므로 결정론이 자동 성립한다.
   생성기 밖(브리지·스폰 시점)에서 뽑으면 깨진다.
4. **슬래셔의 시계는 초가 아니라 사건이다.** 보스 생존이 4~7초라 주기(`PeriodicTimer`) 구동 능력은
   조우 3회 중 2회 이상 발동하지 못한다. 이 spec 의 능력은 전부 `HealthThreshold` 사건 구동이다.
5. **발동 순서 = 폭발 → 도약.** 두 슬롯이 같은 `fraction` 이라 같은 프레임에 `fired` 가 되고, 폭발은
   `HealthThresholdSystem` 이 읽는 **blink 전 위치**에서 터지고 blink 는 `BlinkApplySystem`
   (`[UpdateAfter(HealthThresholdSystem)]`)이 나중에 적용한다. **슬롯 순서로 뒤집을 수 없다.**
6. **면역 술어 = `직접 출처 && (CcActionLock.IsLock(kind) || kind == Impulse)`.** 스택 임계가 유발한
   CC(DoT·스턴)와 `DoT`/`Slow` 는 통과한다. 규칙: *직접 걸리는 행동정지·넉백은 무효, 누적해서 임계를
   넘긴 것은 통한다.* 적용 범위는 `BossTag` 전체 — **나이트메어도 함께 바뀐다.**
7. **면역은 부착/부여 시점 차단.** 어그로는 `Aggroed` 의 유일한 writer 1곳(소비 지점 6곳 대비),
   CC 는 부여 2곳. `AggroCapacity` 회계 · `CcClearRequestsSingleton` · FSM 전이는 **무변경**.
8. **신규 ECS 채널 0, 신규 맥락 0, 신규 시스템 0.** 기존 큐·arm·시스템만 확장한다.
9. **`maxHpRef` 는 스폰 시점 스냅샷이다.** 적에게 `MaxHealthMul` 을 거는 기믹/카드가 생기면 경계 4회
   보장이 깨진다(`BountyMark` 가 이미 적에게 `DmgTakenMul` 을 걸고 있어 문턱이 낮다).
10. **하드코딩 금지 준수** — 스탯·반경·경계 비율·데미지는 전부 SO(`AttackUnitData` / `ProjectileData`)에서.

## 파이프라인 커버리지

대조 대상: `docs/reference/object-pipeline-map.md` §적(Enemy) + §투사체(진동갑주 폭발).

| 정거장 | 슬래셔 | 비고 |
|---|---|---|
| 데이터 SO | unit 1 | `Enemy_Boss_Slasher.asset` + **`EnemyCatalog` 등록** + `bossPool` 노출(unit 0) |
| 스폰 진입점 | 기존 `SpawnUnit` 그대로 | 보스 선택은 생성기 안(unit 0). 스폰 경로 무변경 |
| ECS 컴포넌트 | 기존 적 경로 상속 | `AttackUnitTag`·`Health`·`PathFollowState`·`AttackState`·`EnemyAiState` + `BossTag`/`ThreatEntry`/`DcTriggerSlot` (나이트메어와 동일 베이크) |
| 시뮬 시스템 | 기존 + 3곳 수정 | `HealthThresholdSystem`(unit 3·4) · `AggroStateSystem`/`CcApplySystem`(unit 2). **신규 시스템 0** |
| 이벤트 큐 | 기존 + 필드 1개 | `BlinkRequestEventsSingleton`(기존 — **첫 라이브 사용처**) · `EnemyCcEvent` 에 출처 필드 추가(unit 2). **신규 큐 0** |
| View/Pool | `SpineUnitPool` 공유 | 나이트메어와 같은 스켈레톤, `partSkins`/스케일만 다름 |
| 체력 표시 | `UnitOverheadUiLayer` 자동 | 기존 폴링 경로 |
| 씬 wiring | **N/A** | 신규 SerializeField 0. 바뀌는 것은 덱 asset 의 `bossPool` 값뿐 |
| 투사체(폭발) | unit 1 준비 · unit 3 배선 | AOE 연출 `ProjectileData` 1개 신규. 기존 SkyFall × TileAoe 경로 재사용 |

## 후속 후보 (범위 밖)

- **보스 트리거 개방** — `AttackN`(대회전) / `OnKill`(학살 가속) arm 의 defender 전용 게이트 완화.
  bake 가드 + `AttackSystem` pre-scan·counter 루프 게이트 + `OnKill × SelfStatBuff` bake 의
  `buffStat`/`statBuffStackId` 미설정 버그(공속 +25% 가 `DamageMul` +2400% 로 착지)를 함께 처리.
  게이트는 통째로 제거하지 말고 payload 화이트리스트로 좁힌다(투사체 재조준 후보 풀이 적 전용이라
  진영 전제가 깨지면 아군 오사).
- **반복 넉백 지연 플레이** — 넉백은 면역이지만 `Card_GaleShove` 가 다른 적에게는 유효하다.
  보스 앞 잡몹을 밀어 벽을 만드는 플레이의 밸런스는 Play 관찰 항목.
- **`ModifierOrigin` 일관성** — `OnKill` 경로만 `Dreamcatcher` 태깅(다른 두 경로는 `Boss`/`HealthThreshold`).
  현재 오작동 없음(오라 판정 쿼리가 `DefenderUnitTag` 게이트).
- **프리뷰/런타임 seed 불일치** — 드래프트·준비 화면과 런타임의 seed 출처가 달라 **프리뷰가 다른 보스
  이름을 보여줄 수 있다**(기존 불일치, 보스 2종이 되며 눈에 보인다). 일시정지 메뉴 경로만 정확하다.
- **면역으로 죽은 카드 재설계** — `Card_LullabyDart`·`Card_FrostArrow`·`Card_GaleShove` 가 보스전에서
  무효다. 보스전 대체 효과 부여 여부는 카드 밸런스 작업으로 분리.
- **`Defender_IceCaster` CC 경로 확인** — 면역 영향 범위 미확인(unit 2 에서 1회 확인).
