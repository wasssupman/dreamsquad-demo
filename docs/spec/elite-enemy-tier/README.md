# elite-enemy-tier — 엘리트 등급 적 (슬라임 · 드래곤)

> 상태: **스펙 작성 완료 2026-08-12 · 구현 대기 (unit 0 부터)**

## 목표

적을 **일반 / 엘리트 / 보스** 3등급으로 가르고, 엘리트 등급 적 2종을 추가한다.
엘리트는 보스와 마찬가지로 **특수 메커니즘 1개**를 갖되, **보스의 특권은 하나도 갖지 않는다** —
CC 걸리고, 어그로에 유인되고, 등장 경보도 없다. 즉 *«막을 수 있는 강적»* 이 엘리트의 정체성이다.

| 유닛 | 에셋 | 특수 메커니즘 1개 |
|---|---|---|
| **슬라임** | `Spine Skeletons/sack` | **분열** — 체력이 다하면 기본 체력의 50% 를 가진 작은 형태 2기로 갈라진다(공격력 계승) |
| **드래곤** | `Spine Skeletons/Dragon` | **화염 브레스** — 3회 기본공격마다 대상 방향 부채꼴에 광역 화염 (비행 타입) |

## 검증 질문

> 엘리트 2종이 **보스가 아닌 상태로** 각자의 특수 메커니즘을 실제로 발동하는가?
> 슬라임을 죽이면 둘로 갈라지고 그 자식은 **다시 갈라지지 않는가**?
> 드래곤이 3번째 공격마다 부채꼴을 태우고, 그 사이에는 화염 스택을 쌓아 화상을 터뜨리는가?
> 기존 보스 3종과 그 외 적 14종의 행동은 **무회귀**인가?

## 이 spec 이 처음 밟는 자리

1. **`BossTag` 가 «메커니즘 유무» 에서 분리된다.** 지금 `BakeNightmareMechanics` 는
   `nightmareMechanics` 가 비어있지 않으면 곧 보스로 보고 `BossTag`+위협테이블+보스경보를 붙인다.
   메커니즘을 가진 «보스가 아닌 적» 이 이 spec 에서 처음 생긴다 → unit 0.
2. **`AttackN`·`OnDeath` 트리거의 적 쪽 첫 소비자.** `DcTrigger.EnemyTriggerArmed` 화이트리스트는
   `PeriodicTimer`·`HealthThreshold` 둘뿐이고, RESOLVE 의 `AttackN` arm 은 `[Defender only]` 로
   **명시 게이팅**돼 있다(`AttackSystem`, `defenderTagLookup` 술어) → unit 3.
3. **적이 전투 중에 적을 스폰하는 첫 경로.** `BattleBridge.SpawnUnit` 은 맵 레인 스폰 지점에
   하드와이어돼 있어 «죽은 자리» 스폰이 불가능하다 → unit 5.
4. **광역 도형이 데이터가 되는 첫 자리.** 지금 광역은 소비처마다 Chebyshev/유클리드 비교가
   인라인돼 있다(`ProjectileHitSystem` TileAoe·splash, `AllyBuffFieldSystem`, `AuraPulse`,
   `BossPeriodicTriggerSystem`, 브리지 3곳). 부채꼴은 그중 어디에도 없다 → unit 1·2·4.

## 티어 축 계약 (unit 0 이 세운다)

- **`EnemyTier { Normal, Elite, Boss }`** 를 `AttackUnitData` 에 저작한다. 폴백 = `Normal`.
- **`BossTag`·`ThreatEntry`·보스경보는 `tier == Boss` 에서만** 나온다. 메커니즘 bake 자체는
  티어와 무관하게 돌아 엘리트도 슬롯을 받는다.
- 기존 보스 3종(`Enemy_Boss_Nightmare` · `_Jjangssen` · `_Mamemo`)에 `tier: Boss` 를 찍는다.
  적 에셋 **17종 중 메커니즘을 가진 것은 실측 이 셋뿐**이고 나머지 14종은 0개다 →
  폴백(`Normal`)만으로 무회귀.
- ⚠ **`killScore`·`stabilityDamage` 값 대역과 티어 enum 을 서로 검증하지 않는다.**
  두 축은 독립이다(티어는 행동, 값은 밸런스). 실측 반례: `Enemy_Tanker` 는 티어가 일반인데
  이미 `killScore 3 / stabilityDamage 2 / awakeningReward 3` = 엘리트 대역으로 저작돼 있다.
  `OnValidate` 경고를 붙이면 정상 콘텐츠에서 발화한다.

## 유닛 사양 (초기값 — 전부 SO 소유, 튜닝 대상)

### 슬라임 (`slime`, Elite) / 작은 슬라임 (`slime_small`, Normal)

| 항목 | 부모 | 자식 | 근거 |
|---|---|---|---|
| health | 120 | **60** | 자식 = 부모의 50%(사용자 지정). 부모는 탱커(100) 위 |
| outputs | Damage 12 | **Damage 12** | «공격력 등은 그대로 계승»(사용자 지정) |
| moveSpeed / range / cd | 1.8 / 1 / 0.9 | 동일 | 계승 |
| enemyClass / attackMethod | Bruiser / Melee | 동일 | 라벨 |
| killScore / awakening / stability | 3 / 3 / 2 | 1 / 1 / 1 | 엘리트 대역 → 일반 대역 |
| maxPerWave / minWaveNumber | 1 / 3 | — | 자식은 웨이브 생성 대상이 아니다 |
| mechanics | `OnDeath × SplitOnDeath(2, slime_small)` | **없음** | 자식은 메커니즘이 없어 **재분열이 구조적으로 불가능** |
| Spine | idle=walk · walk=walk · attack=**fall-in** · death=**빈 값** | 동일 | sack 애니는 `fall-in`·`walk` **둘뿐**(실측) |

### 드래곤 (`dragon`, Elite)

| 항목 | 값 | 근거 |
|---|---|---|
| health / moveSpeed | 110 / 2.0 | |
| traversalLayers / flightLift | **Air** / 1.4 | 비행 타입. `Enemy_Skimmer` 와 같은 lift 값 |
| attackRange / **attackCooldown** | 2 / **1.2** | ★cd 는 임의값이 아니다 — 아래 계약 4 |
| outputs | `Damage 6` + `ApplyStack(Fire, +1, perApp 3.0, max 5)` | 킨들러 파이프라인 재사용 |
| projectile | `Projectile_Enemy_Fireball.asset` **재사용** | 킨들러와 같은 탄 |
| mechanics | `AttackN(3) × AreaBreath` — 피해 20 · 사거리 3타일 · 반각 45° | 3.6초마다 발화 |
| killScore / awakening / stability | 3 / 3 / 2 | 엘리트 대역 |
| maxPerWave / minWaveNumber | 1 / 4 | |
| Spine | idle=walk=**flying** · attack=**빈 값** · death=**빈 값** | Dragon 애니는 `flying` **하나뿐**(실측) |

단일 대상 기준 ≈ **13.9 DPS** = 직격 5.0(6÷1.2) + 화상 3.3(1회분 20÷주기 6.0) + 브레스 5.6(20÷3.6).
부채꼴에 3기가 들면 브레스만 16.7 DPS 이므로 밀집 배치가 직격당한다.

## Feature-wide 계약

1. **엘리트는 보스 특권을 하나도 받지 않는다.** CC 면역(`CcActionLock.IsBossImmune` 게이트)·
   어그로 면역(`AggroStateSystem`)·보스경보·위협테이블은 전부 `BossTag` 술어를 타므로,
   unit 0 이 `BossTag` 를 `tier == Boss` 로 좁히면 **코드 변경 0 으로 자동 성립**한다.
2. **자식은 메커니즘을 갖지 않는다 = 재귀 차단이 데이터 구조에 있다.** 세대 카운터·깊이 상한
   같은 런타임 가드를 넣지 않는다. 2단계 분열이 필요해지면 중간 SO 를 하나 더 만든다.
3. **슬라임은 웨이포인트 경로를 쓰지 않는다(`waypointPathIndex = -1`).** 자식은 부모의 순서
   진행도를 물려받지 않으므로, 경로를 쓰면 **자식이 부모가 이미 지난 지점으로 되돌아간다.**
4. **드래곤 `attackCooldown` 을 내리지 말 것.** 화상 지속(4.85s) < 스택 발화 주기(5 × cd)
   부등식이 `enemy-fire-stack-shooter` 계약 2·3 이고, cd 1.2 는 주기 6.0s 로 그 계약의
   킨들러와 **정확히 같은 여유(1.15s)** 를 갖는다. cd 1.0 이면 공백이 0.15s 로 줄어 사실상
   상시 화상이 되고, 방어유닛의 회복 수단이 제한적이라 확정 사망으로 굳는다.
5. **`StackModifier_Fire` 는 킨들러와 공유한다**(사용자 결정 2026-08-12). 화염 임계 규칙은
   전역 1벌이라는 기존 계약 유지. 스택은 `(source, kind)` 라 킨들러와 드래곤이 같은 대상을
   때려도 **각자 슬롯을 쌓고**, 파생 화상은 `(Stack, Fire)` 한 슬롯으로 접힌다.
6. **`EffectArea` 는 인터페이스가 아니라 값 타입이다.** C# `interface` 는 unmanaged 컴포넌트에
   못 들어가고 Burst 에서 깨진다. `struct EffectArea`(도형 enum + 파라미터) + `static
   EffectAreaMath`(순수 판정)로 만든다 — 제약 10 의 «plain 값 입력 → plain 값 출력» 형태.
7. **`EffectArea` 는 첫 커밋부터 소비자 2개를 갖는다**(제약 8). 신설과 같은 spec 에서 기존
   `TileAoe` 를 `EffectArea{TileRadius}` 로 이관하고, 브레스가 `EffectArea{Cone}` 로 두 번째
   소비자가 된다. **v1 도형은 실사용 2종만** — 미사용 도형을 미리 넣지 않는다.
8. **이관 대상은 `ProjectileHitSystem` 의 `TileAoe` 페이로드 **1곳**이다.** `TileAoe.IsInTileRange`
   는 이미 순수·Burst 공유 프리미티브이고 프로덕션 호출처가 5곳인데, 나머지 4곳
   (`AggroTargeting` · `DefenderDensity` · `BounceRetarget` · `BattleBridge:3574` 미러)은 «효과
   영역» 이 아니라 타게팅·통계 질의라 도형이 데이터가 될 이유가 없다. `EffectAreaMath` 의
   `TileRadius` 분기는 그 프리미티브를 **그대로 호출**해 수치 drift 를 원리적으로 막는다.
   `SingleSplash` 의 splash 피해자 풀이 «적 풀만» 인 것도 문서화된 의도적 선택이므로 유지하고,
   `HazardShapeSampler`(managed `List<int2>`)·오라·존도 후속 후보다.
9. **브레스는 즉발이고 예고가 없다**(사용자 결정 2026-08-12). 공격 애니도 없으므로 플레이어가
   받는 신호는 **화염 VFX 하나뿐**이다. 예고가 필요해지면 `hitDelaySec` 로 붙인다.
10. **전 수치는 SO** — 하드코딩 금지(제약 6).

## 작업 단위

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | [0_enemy_tier_axis.md](0_enemy_tier_axis.md) | `EnemyTier` + `BossTag` 유도 분리 + 보스 3종 마이그레이션 |
| 1 | code | [1_effect_area_pure.md](1_effect_area_pure.md) | `EffectArea`/`EffectAreaMath` 순수 함수 + EditMode (**소비자 0**) |
| 2 | code | [2_tileaoe_effectarea_migration.md](2_tileaoe_effectarea_migration.md) | `TileAoe` → `EffectArea{TileRadius}` 이관 (무회귀 증명) |
| 3 | code | [3_enemy_attackn_gate.md](3_enemy_attackn_gate.md) | 적에게 `AttackN` 개방 — RESOLVE arm 진영 파라미터화. **단독 커밋** |
| 4 | code | [4_cone_breath_payload.md](4_cone_breath_payload.md) | `Cone` 도형 + `AreaBreath` 페이로드 |
| 5 | code | [5_enemy_ondeath_split.md](5_enemy_ondeath_split.md) | 적 `OnDeath` 개방 + `SplitOnDeath` + 위치 지정 스폰 경로 |
| 6 | asset | [6_slime_assets.md](6_slime_assets.md) | 슬라임 2종 + sack Spine + 카탈로그/덱 |
| 7 | asset | [7_dragon_assets.md](7_dragon_assets.md) | 드래곤 + Air 층 + 화염 스택 + 브레스 VFX + 카탈로그/덱 |
| 8 | docs | `8_handoff_summary.md` | 인계 요약 (구현 종료 시 작성) |

**순서 근거**

- **0 이 먼저**여야 한다. 이후 전부가 «엘리트 ≠ 보스» 를 전제한다.
- **1 은 소비자 0** 이라 안전하다. **2 가 3·4 보다 앞**인 이유는 `EffectArea` 를 *기존 동작에
  대고* 먼저 증명하기 위해서다 — 새 도형과 새 소비자를 동시에 켜면 실패 원인이 갈라지지 않는다
  (`traversal-layers` unit 5 의 교훈).
- **3 은 단독 커밋.** 방어유닛 카드 전체가 같은 코드를 타므로, 되돌릴 때 콘텐츠와 함께 딸려가면
  안 된다(`enemy-fire-stack-shooter` unit 0 선례).
- **6·7 은 마지막.** 아트가 잘못된 동작을 예쁘게 포장하지 않게 한다(`waypoint-routing` 순서 근거).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` **적(Enemy)** 아키타입 대조. 신규 플레이 오브젝트 3종
(엘리트 적 2 + 분열 자식 1). 투사체는 기존 에셋 재사용이라 신규 아님.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Enemy_Slime` · `Enemy_Slime_Small` · `Enemy_Dragon` 신규 + `AttackUnitData` 에 `tier` 1필드. **`EnemyCatalog` 3종 등록**. 덱 `attackUnitPool` 노출은 **부모 2종만** — 자식은 분열로만 등장(`Enemy_Skimmer` 의 «풀에 안 넣는다» 선례) |
| 스폰 진입점 | `BattleBridge.SpawnUnit` **본문을 위치 지정 경로로 갈라낸다**(unit 5). 레인 스폰은 그 위 얇은 래퍼가 된다 — `CreatePatrolEntity` 처럼 병렬 복제하지 않는다(적의 표준 세트 전부가 필요) |
| ECS 컴포넌트 | 표준 세트 그대로. `DcTriggerSlot`(엘리트도 받는다) + `PathFollowState`(Air 층은 값만 다름). **신규 컴포넌트 0** |
| 시뮬 시스템 | `AttackSystem`(적 AttackN arm + 콘 적용) · `DamageApplicationSystem`(분열 스탬프) · `ProjectileHitSystem`(TileAoe 도형 이관). **신규 시스템 0** — 콘은 AttackSystem 이 이미 들고 있는 후보 배열 위에서 판정한다 |
| 이벤트 큐 | **신규 채널 0.** 분열은 기존 `EnemyKilledEventsSingleton` 에 필드 3개 append(`hasKillBurst` 선례) |
| View/Pool | 기존 `SpineUnitPool`. ★**death 애니 빈 값 = `Destroy(gameObject)` 즉시**(`SpineUnitView`) — 슬라임의 «죽으면 그냥 분리» 가 코드 변경 없이 성립한다. 드래곤은 `flying` 루프가 공격에 끊기지 않는다(`PlayAttack` 이 빈 애니에 early-return) |
| 체력 표시 | 변경 없음 — `UnitOverheadUiLayer`. ⚠ 드래곤은 lift 를 따라 올라가는지 확인(`waypoint-routing` 미확인 항목) |
| 씬 wiring | **N/A — 신규 SerializeField 0.** `stackModifierAuthoring` 도 `StackModifier_Fire` 를 공유하므로 추가 배선이 없다 |
| VFX | 브레스 = `VFXPACK_FIRE_WALLCOEUR` 복제본. ⚠ 벤더 원본을 직접 참조하지 않는다 — `Assets/_Project/VFX/` 아래 복제본만(`projectile-ga-reskin` 공통 원칙) |

⚠ **`attackUnitPool` 에 2종을 더하면 그 덱의 웨이브가 1번부터 전부 재추첨된다.**
`WavePatternGenerator` 가 `rng.NextInt(0, pool.Count)` 로 뽑으므로 `waveSeed` 가 고정이어도
구성이 바뀐다. 삽입은 **풀 중간에** 하고(맨 뒤면 `ResolveWaveEligibleIndex` 의 전방 순환이
초반 웨이브를 `pool[0]` 로 쏠리게 한다), 새 baseline 을 커밋 diff 에 드러낸다.

## 후속 후보 (현 spec 범위 밖)

1. **`EffectArea` 나머지 이관** [M] — `SingleSplash` splash(진영 대칭화 동반) · `HazardShapeSampler`
   (managed → Burst) · `AllyBuffFieldSystem` · `AuraPulse` · 어그로 반경 · 브리지 3곳.
2. **브레스 예고(telegraph)** [S] — 계약 9. 공격 애니가 없어 신호가 VFX 하나뿐인 것이 실플레이에서
   읽히지 않으면 `hitDelaySec` + 바닥 링.
3. **슬라임 2단계 분열** [S] — 중간 SO 1개 추가로 4마리까지. 계약 2 참조.
4. **엘리트 전용 등장 연출·HUD** [M] — 보스경보를 재사용하지 않기로 했으므로(계약 1) 엘리트를
   시각적으로 구분하는 수단이 지금은 스켈레톤 크기뿐이다.
5. **엘리트 전용 아트** [M] — 둘 다 벤더 Spine 예제 as-is.
6. **드래곤 `engageMovement` 재판정** [S] — unit 7 은 `Advance` 로 시작한다(애니가 `flying`
   하나뿐이라 정지 시 `UpdateWalkTimeScale` 이 날갯짓을 슬로모로 만든다). 육안으로 어색하지
   않으면 원안인 `Halt`(사거리 2 진입 후 정지)로 되돌린다.
