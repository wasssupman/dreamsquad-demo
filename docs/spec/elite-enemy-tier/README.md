# elite-enemy-tier — 엘리트 등급 적 (슬라임 · 드래곤)

> 상태: **완료 2026-08-13** — unit 0~7 전부 구현·자동검증·사용자 Play 확인 통과.
> 검증은 **적 1종 단위로 묶었다**(사용자 결정 2026-08-12): 슬라임 = 0·5·6, 드래곤 = 1·3·4·7.
> 인계는 [`8_handoff_summary.md`](8_handoff_summary.md).
>
> **라이브 편입 완료** — `wave-concept-blocks` unit 7(`2712aa01`)이 라이브 덱 7종
> (Serpent·Coil·Twin·Spiral·Zig·Hook·Endless) 전부에 슬라임·드래곤을 넣었다. 이 spec 은
> 「등록은 웨이브 baseline 을 바꾸므로 별도 커밋」이라며 미뤘고 그 커밋이 그쪽에서 왔다 —
> `DragonBreathAuthoringTests` 의 «아직 풀에 없다» 단언도 «풀에 있고 맨 뒤가 아니다» 로
> 뒤집혔다. **둘 다 이제 일반 플레이에 나온다**(TEST MODE 플랜은 단독 검증용으로 존속).
>
> ⚠ **밸런스 실전 미검증** — `66004836`(슬라임 피해·체력 ×2, 드래곤 기본 20·브레스 50,
> 화염 10/틱)은 TEST MODE 에서만 봤다. 그 수치가 이제 라이브 웨이브에 실린다.

## 목표

적을 **일반 / 엘리트 / 보스** 3등급으로 가르고, 엘리트 등급 적 2종을 추가한다.
엘리트는 보스와 마찬가지로 **특수 메커니즘 1개**를 갖되, **보스의 특권은 하나도 갖지 않는다** —
CC 걸리고, 어그로에 유인되고, 등장 경보도 없다. 즉 *«막을 수 있는 강적»* 이 엘리트의 정체성이다.

| 유닛 | 에셋 | 특수 메커니즘 1개 |
|---|---|---|
| **슬라임** | `Spine Skeletons/sack` | **2단계 분열** — 죽으면 체력 절반짜리 2기로 갈라지고, 그것도 죽으면 다시 2기씩(최종 4기). 공격력은 전 단계 계승 |
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
2. **`AttackN` 트리거의 적 쪽 첫 소비자.** `DcTrigger.EnemyTriggerArmed` 화이트리스트는
   `PeriodicTimer`·`HealthThreshold` 둘뿐이고, RESOLVE 의 `AttackN` arm 은 `[Defender only]` 로
   **명시 게이팅**돼 있다(`AttackSystem`, `defenderTagLookup` 술어) → unit 3.
   화이트리스트에 더하는 것은 `AttackN` **하나**다(`OnDeath` 는 열지 않는다 — unit 5 ②).
3. **적이 전투 중에 적을 스폰하는 첫 경로.** `BattleBridge.SpawnUnit` 은 맵 레인 스폰 지점에
   하드와이어돼 있어 «죽은 자리» 스폰이 불가능하다 → unit 5.
4. **부채꼴 광역이 처음 필요해진다.** 지금 광역 판정은 Chebyshev 반경(`TileAoe`)과 유클리드
   반경뿐이고 **방향성 도형이 하나도 없다** → unit 1 이 술어 하나를 더한다. 도형을 데이터로
   추상화하지는 않는다(계약 6).

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

> ⚠ **아래 표는 저작 시점의 초기값이고 이미 튜닝됐다.** 설계 근거(DPS 유도·cd 부등식)를
> 보존하려고 남겨두는 것이지 현재값이 아니다. **현재값의 정본은 SO 다.**
>
> `66004836`(사용자 튜닝 2026-08-13): 슬라임 피해 12→**24** · 쿨 0.9→**1.5** · 체력
> 240/120/60 · 드래곤 기본 6→**20** · 브레스 20→**50** · 화염 임계 4→**10**/틱(Kindler 동반).

### 슬라임 사슬 — `slime`(Elite) → `slime_mid` ×2 → `slime_small` ×4

| 항목 | `slime` | `slime_mid` ×2 | `slime_small` ×4 |
|---|---|---|---|
| health | 120 | **60** | **30** |
| outputs | Damage 12 | 동일(계승) | 동일(계승) |
| moveSpeed / range / cd | 1.8 / 1 / 0.9 | 동일 | 동일 |
| enemyClass / attackMethod | Bruiser / Melee | 동일 | 동일 |
| killScore / awakening | **3 / 3** | **0 / 0** | **0 / 0** |
| stabilityDamage | 2 | 1 | 1 |
| maxPerWave / minWaveNumber | 1 / 3 | — | — |
| mechanics | `OnDeath × SplitOnDeath(2, slime_mid)` | `OnDeath × SplitOnDeath(2, slime_small)` | **없음 = 사슬 종료** |
| Spine | idle=walk · walk=walk · attack=**fall-in** · death=**빈 값** | 동일 | 동일 |

단계마다 체력 절반이지만 마릿수가 배라 **유효 체력이 단계마다 유지된다**(각 120) — 슬롯 하나가
총 360 체력 + 처치 7회다. 상세 근거는 [6_slime_assets.md](6_slime_assets.md).

### 드래곤 (`dragon`, Elite)

| 항목 | 값 | 근거 |
|---|---|---|
| health / moveSpeed | 110 / 2.0 | |
| traversalLayers / flightLift | **Air** / 1.4 | 비행 타입. `Enemy_Skimmer` 와 같은 lift 값 |
| attackRange / **attackCooldown** | 2 / **1.2** | ★cd 는 임의값이 아니다 — 아래 계약 4 |
| **engageMovement / targetMode** | **`Halt` / `FocusUntilDead`** | ★킨들러와 동형. `Nearest` 나 `Advance` 면 **어느 방어유닛도 5스택에 못 가** 화염 스택이 죽는다 |
| outputs | `Damage 6` + `ApplyStack(Fire, +1, perApp 3.0, max 5)` | 킨들러 파이프라인 재사용 |
| projectile | `Projectile_Enemy_Fireball.asset` **재사용** | 킨들러와 같은 탄 |
| mechanics | `AttackN(3) × AreaBreath` — 피해 20 · 사거리 3타일 · 반각 **50°** | 3.6초마다 발화. ★45° 는 셀 대각선 경계라 저작 금지(계약 9-3) — 초판 표가 45° 로 잘못 적혀 있었고 에셋은 처음부터 50° 다 |
| killScore / awakening / stability | 3 / 3 / 2 | 엘리트 대역 |
| maxPerWave / minWaveNumber | 1 / 4 | |
| Spine | idle=walk=**flying** · attack=**빈 값** · death=**빈 값** | Dragon 애니는 `flying` **하나뿐**(실측) |

단일 대상 기준 ≈ **13.9 DPS** = 직격 5.0(6÷1.2) + 화상 3.3(1회분 20÷주기 6.0) + 브레스 5.6(20÷3.6).
부채꼴에 3기가 들면 브레스만 16.7 DPS 이므로 밀집 배치가 직격당한다.

## Feature-wide 계약

1. **엘리트는 보스 특권을 하나도 받지 않는다.** CC 면역(`CcActionLock.IsBossImmune` 게이트)·
   어그로 면역(`AggroStateSystem`)·보스경보·위협테이블은 전부 `BossTag` 술어를 타므로,
   unit 0 이 `BossTag` 를 `tier == Boss` 로 좁히면 **코드 변경 0 으로 자동 성립**한다.
2. **재귀 차단은 «사슬이 유한하다» 는 사실이다** — 세대 카운터를 런타임에 두지 않는다.
   판정은 순수 함수 `Data/SplitChain.Validate`(자기순환·간접순환·과길이)가 소유하고 bake 가
   호출한다. 마지막 단계가 메커니즘을 갖지 않는 것이 종료 조건이다.
   (초판 계약은 «자식은 메커니즘을 갖지 않는다» 였는데 **2단계 분열이 의도가 되면서 거짓이
   됐다** — 중간 단계는 메커니즘을 가져야 한다. 2026-08-12 사용자 결정으로 교체.)
2-1. **분열은 보상을 나누지 않는다.** 분열체의 `awakeningReward`·`killScore` = 0, 총량은 본체
   하나 몫이다(각성 3 / 점수 3). 단계를 늘려도 총량 불변. 자식에 1씩 주면 처치 7회짜리 각성
   농장이 되어 보스(5)의 두 배를 뱉는다 — 각성 20 = 드림캐쳐 1장이 기준이다.
   **`stabilityDamage` 는 반대로 자식에게 남긴다**(1씩) — 보상이 아니라 놓쳤을 때의 대가다.
3. **슬라임은 웨이포인트 경로를 쓰지 않는다(`waypointPathIndex = -1`).** 자식은 부모의 순서
   진행도를 물려받지 않으므로, 경로를 쓰면 **자식이 부모가 이미 지난 지점으로 되돌아간다.**
4. **드래곤 `attackCooldown` 을 내리지 말 것.** 화상 지속(4.85s) < 스택 발화 주기(5 × cd)
   부등식이 `enemy-fire-stack-shooter` 계약 2·3 이고, cd 1.2 는 주기 6.0s 로 그 계약의
   킨들러와 **정확히 같은 여유(1.15s)** 를 갖는다. cd 1.0 이면 공백이 0.15s 로 줄어 사실상
   상시 화상이 되고, 방어유닛의 회복 수단이 제한적이라 확정 사망으로 굳는다.
5. **`StackModifier_Fire` 는 킨들러와 공유한다**(사용자 결정 2026-08-12). 화염 임계 규칙은
   전역 1벌이라는 기존 계약 유지. 스택은 `(source, kind)` 라 킨들러와 드래곤이 같은 대상을
   때려도 **각자 슬롯을 쌓고**, 파생 화상은 `(Stack, Fire)` 한 슬롯으로 접힌다.
6. **광역 «도형 어휘» 를 신설하지 않는다.** 초판은 `EffectArea` struct + `EffectAreaShape` enum +
   `EffectAreaMath` 를 만들고 기존 `TileAoe` 페이로드를 그 위로 이관하려 했으나,
   **2026-08-12 리뷰 3건이 독립적으로 과설계로 판정해 접었다** — 두 소비자 모두 도형이 상수라
   태그가 어떤 경계도 건너지 않고, 소비자 2개 중 1개를 이 spec 이 «행동 변화 0 인 순수 인디렉션»
   으로 만들려던 자기충족적 정당화였다. 상세와 근거는 [1_cone_predicate.md](1_cone_predicate.md).
   정당화되는 것은 **콘 판정 순수 함수 1개**이고, `TileAoe.cs`(이미 순수·Burst 공유 프리미티브)
   옆에 `IsInCone` 으로 놓는다. **신규 타입 0 · 신규 파일 0 · 라이브 데미지 경로 수정 0.**
7. **기존 광역 사이트는 하나도 건드리지 않는다.** `TileAoe.IsInTileRange` 의 프로덕션 호출처 5곳
   (`ProjectileHitSystem` TileAoe · `AggroTargeting` · `DefenderDensity` · `BounceRetarget` ·
   `BattleBridge:3574` 미러) · `SingleSplash` 의 «적 풀만» splash(문서화된 의도적 선택) ·
   `HazardShapeSampler`(managed `List<int2>`) · 오라 · 존 — 전부 유지. 도형 통합은 후속 후보.
9. **브레스는 즉발이고 예고가 없다**(사용자 결정 2026-08-12). 공격 애니도 없으므로 플레이어가
   받는 신호는 **화염 VFX 하나뿐**이다. 예고가 필요해지면 `hitDelaySec` 로 붙인다.
9-1. **광역 효과의 피해자 진영은 저절로 걸러지지 않는다.** `AttackSystem` 의 후보 배열
   (`targetCandidatesQuery`)은 **전 진영 통합 풀**이고 진영 판정은 공격자 루프 안의
   `AttackState.targetMask` 가 한다. 콘 순회는 ① 진영 마스크 ② 통행층(`Air`/`Path`) 교집합
   ③ 자기 제외를 **명시 적용**해야 한다 — 빠뜨리면 드래곤이 동료와 적 마음을 태운다.
   (초판 스펙이 «풀이 이미 걸러져 있다» 고 잘못 적었고 리뷰에서 잡혔다.)
9-2. **Burst ISystem 은 `VfxSpawner` 를 부를 수 없다.** 브레스 연출은 기존
   `UnitAttackVisualEvent` 에 필드를 append 해 브리지 드레인에서 스폰한다 — 이 이벤트가 이미
   `attacker` + `targetWorld` 를 실어 콘 방향을 만들 수 있다. **신규 채널 0 은 유지된다.**
9-3. **콘 반각을 셀 대각선 경계(45°·135°)에 저작하지 않는다.** 부동소수 비교가 동전 던지기가
   되고, 이 프로젝트는 «비동기 토너먼트 양측 동일 시뮬» 결정론을 요건으로 둔 채
   Android·iOS·에디터를 동시에 타깃한다. 초기값 **50°**. 판정은 `normalize` 없는 제곱 비교,
   코사인²은 **bake 에서 1회** 변환한다.
10. **전 수치는 SO** — 하드코딩 금지(제약 6).

## 작업 단위

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | [0_enemy_tier_axis.md](0_enemy_tier_axis.md) | `EnemyTier` + `BossTag` 유도 분리 + 보스 3종 마이그레이션 |
| 1 | code | [1_cone_predicate.md](1_cone_predicate.md) | `TileAoe.IsInCone` 순수 함수 + EditMode (**소비자 0**) |
| ~~2~~ | — | — | **삭제** (리뷰 H1 — `TileAoe`→`EffectArea` 이관 철회). 번호는 커밋·링크 참조 안정성 때문에 **재사용하지 않는다** |
| 3 | code | [3_enemy_attackn_gate.md](3_enemy_attackn_gate.md) | 적에게 `AttackN` 개방 — RESOLVE arm 진영 파라미터화. **단독 커밋** |
| 4 | code | [4_cone_breath_payload.md](4_cone_breath_payload.md) | `AreaBreath` 페이로드 (콘 적용 + bake 의무 + 연출) |
| 5 | code | [5_enemy_ondeath_split.md](5_enemy_ondeath_split.md) | 분열 `SplitOnDeath`(브리지 드레인 SO 직독) + 위치 지정 스폰 경로 + 드레인 순서 |
| 6 | asset | [6_slime_assets.md](6_slime_assets.md) | 슬라임 2종 + sack Spine + 카탈로그/덱 |
| 7 | asset | [7_dragon_assets.md](7_dragon_assets.md) | 드래곤 + Air 층 + 화염 스택 + 브레스 VFX + 카탈로그/덱 |
| 8 | docs | `8_handoff_summary.md` | 인계 요약 (구현 종료 시 작성) |

**순서 근거**

- **0 이 먼저**여야 한다. 이후 전부가 «엘리트 ≠ 보스» 를 전제한다.
- **1 은 소비자 0** 이라 안전하다. 술어와 그 테스트를 먼저 착지시켜야 4 에서 실패가 났을 때
  «수학이 틀렸나 / 배선이 틀렸나» 가 갈린다(`traversal-layers` unit 5 의 교훈).
- **3 은 단독 커밋.** 방어유닛 카드 전체가 같은 코드를 타므로, 되돌릴 때 콘텐츠와 함께 딸려가면
  안 된다(`enemy-fire-stack-shooter` unit 0 선례).
- **6·7 은 마지막.** 아트가 잘못된 동작을 예쁘게 포장하지 않게 한다(`waypoint-routing` 순서 근거).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` **적(Enemy)** 아키타입 대조. 신규 플레이 오브젝트 4종
(엘리트 적 2 + 분열체 2). 투사체는 기존 에셋 재사용이라 신규 아님.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Enemy_Slime` · `Enemy_Slime_Mid` · `Enemy_Slime_Small` · `Enemy_Dragon` 신규 + `AttackUnitData` 에 `tier` 1필드. **`EnemyCatalog` 4종 등록**. 덱 `attackUnitPool` 노출은 **본체 2종만**(슬라임·드래곤) — 분열체는 분열로만 등장(`Enemy_Skimmer` 의 «풀에 안 넣는다» 선례) |
| 스폰 진입점 | `BattleBridge.SpawnUnit` **본문을 위치 지정 경로로 갈라낸다**(unit 5). 레인 스폰은 그 위 얇은 래퍼가 된다 — `CreatePatrolEntity` 처럼 병렬 복제하지 않는다(적의 표준 세트 전부가 필요) |
| ECS 컴포넌트 | 표준 세트 그대로. `DcTriggerSlot`(엘리트도 받는다) + `PathFollowState`(Air 층은 값만 다름). **신규 컴포넌트 0** |
| 시뮬 시스템 | **`AttackSystem` 단 하나**(적 AttackN arm + 콘 적용). 신규 시스템 0 · `ProjectileHitSystem` 무변경(계약 7) · `DamageApplicationSystem` 무변경(unit 5 ②). 콘은 AttackSystem 이 이미 들고 있는 후보 배열 위에서 판정하고, 순회 본문은 private static 으로 뺀다 |
| 이벤트 큐 | **신규 채널 0.** 분열은 이벤트 필드조차 늘리지 않는다 — 킬 드레인이 `_enemyTypeByEntity` 로 죽은 적의 SO 를 이미 손에 들고 있다(unit 5 ②). 브레스 연출만 `UnitAttackVisualEvent` 필드 append(`hasKillBurst` 선례). ⚠ **`DrainEnemyKilledEvents` 를 `QueueDueWaves` 앞으로 옮긴다** — unit 5 ④ |
| View/Pool | 기존 `SpineUnitPool`. ★**death 애니 빈 값 = `Destroy(gameObject)` 즉시**(`SpineUnitView`) — 슬라임의 «죽으면 그냥 분리» 가 코드 변경 없이 성립한다. 드래곤은 `flying` 루프가 공격에 끊기지 않는다(`PlayAttack` 이 빈 애니에 early-return) |
| 체력 표시 | 변경 없음 — `UnitOverheadUiLayer`. ⚠ 드래곤은 lift 를 따라 올라가는지 확인(`waypoint-routing` 미확인 항목) |
| 씬 wiring | **N/A — 신규 SerializeField 0.** `stackModifierAuthoring` 도 `StackModifier_Fire` 를 공유하므로 추가 배선이 없다 |
| VFX | 브레스 = `VFXPACK_FIRE_WALLCOEUR` 복제본. ⚠ 벤더 원본을 직접 참조하지 않는다 — `Assets/_Project/VFX/` 아래 복제본만(`projectile-ga-reskin` 공통 원칙) |

⚠ **`attackUnitPool` 에 2종을 더하면 그 덱의 웨이브가 1번부터 전부 재추첨된다.**
`WavePatternGenerator` 가 `rng.NextInt(0, pool.Count)` 로 뽑으므로 `waveSeed` 가 고정이어도
구성이 바뀐다. 삽입은 **풀 중간에** 하고(맨 뒤면 `ResolveWaveEligibleIndex` 의 전방 순환이
초반 웨이브를 `pool[0]` 로 쏠리게 한다), 새 baseline 을 커밋 diff 에 드러낸다.

## 후속 후보 (현 spec 범위 밖)

spec 종료(2026-08-13)와 함께 **중앙 백로그로 이관했다** — 두 곳에 두면 갈라진다.

→ [`docs/spec/README.md`](../README.md) 의 **Follow-up Backlog → 엘리트 등급 적** 그룹 (9항목).
가장 급한 것은 **밸런스 실전 미검증** 이다: `66004836` 의 수치가 TEST MODE 검증만 거친 채
`2712aa01` 로 라이브 웨이브에 실렸다.
