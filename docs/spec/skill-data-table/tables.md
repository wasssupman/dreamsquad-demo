# skill-data-table — 새 표 설계 (unit 0)

> 실측 2026-09-28 · 브랜치 `unified-effect-layer` · 방법 = `Assets/_Project/Data/**` YAML 파싱(스크립트 GUID 로 SO 종류 판별) + 빌더·코어 정독. **옛 시트 구조(`DcSheetImportDto` 6탭)는 폐기** — 호환 칸 없음. 스탯 시트(`UnitStatImportDto`)는 이 spec 이 모양을 바꾸지 않는다.
> 표기: `★` = 비율 적용 칸(§9) · 필수 = `필수` / 선택 = `—` · 기본값 = 비어 있을 때 굽는 값. 파일:줄은 이 커밋 기준.

## 0. 표 목록

| 표 | 한 줄 = | 키 | 줄 수(라이브) |
|---|---|---|---|
| `Effects` | 효과 하나(종류 + 수치 + 수치 방식) | `effect_id` | ≈ 66(아래 §2 · dedupe 는 unit 4 dry-run 이 확정) |
| `Skills` | 소유자 한 명의 규칙 하나(언제 → 효과 id) | (`owner_kind`, `owner_id`, `slot`) | 69 |
| `Projectiles` | 탄 한 종 — 궤적·피격 모양(피해 없음) | `id` | 83 |
| `Patterns` · `PatternShots` | 발사 명세 모양(발 수·각·간격 — 피해 없음) · 그 발 하나 | `id` · (`pattern_id`, `idx`) | 13 · 발 합계 |
| `Hazards` · `HazardEffects` | 존 장판 모양·지속 · 그 하위 효과 하나(피해 없음) | `id` · (`hazard_id`, `idx`) | 7 · 7 |
| `Blockers` | 길막 설치물 모양·체력·폭발 모양(피해 없음) | `id` | 3 |
| `Cards` · `CardStatEffects` · `CardAttackMods` | 카드 고유 값 · 스쿼드 스탯 효과 하나 · 공격 수식자 하나 | `id` · (`card_id`, `slot`) ×2 | 54 · 15 · 3 |
| `Units` · `Enemies` | 소유자 고유 값(스탯 = 현행 스탯 시트 열 그대로 · 스킬 칸 없음) | `id` | 27 · 24 |

## 1. 라이브 전수(재계수)

| 저작 | 수 | 비고 |
|---|---|---|
| `DreamcatcherCard` | **54** = Squad 13 · Unit 35 · Active 6 | census(33 Unit) 이후 `star_strike` · `gaesagi` 추가 |
| 카드 `mechanics` | 34(32장 — `calamity_heart` 3) | Unit 35 중 attackMod 전용 3장은 메커닉 0 |
| 카드 `attackMods` · 스쿼드 `effects` | 3 · 15(13장 — `guardian_fortress` · `cracked_grail` 2개씩) | |
| `UnitSkillAbility` · `ShieldCastAbility` | 17 · 1 = **규칙 레일 능력 18** | 방어유닛당 `GetAbility<UnitSkillAbility>` 하나(`BindingDefinitionBuilder.cs:43`) |
| 악몽(`nightmareMechanics` 비지 않은 적) | **6**(메커닉 13 · 그중 `SplitOnDeath` 2) | |
| `SkillData` · `ProjectileData` · `ProjectilePatternData` | 6 · 83 · 13 | |
| `HazardSO` · `BlockingHazardSO` | 7 · 3 | 효과 경로 라이브 = `Hazard_Ember` · `Blocker_BombBarrel` 뿐(나머지 = 씬 디버그 스폰 · `HazardCastAbility`(코어 소비 0)) |
| `DreamstoneData` · 평타 경로 능력 | 64 · 8(`BombThrow` 1 · `DirectionalVolley` 2 · `SummonPatrol` 1 · `HazardCast` 4) | 범위 밖(§11) |

## 2. `Effects`

| 열 | 형 | 허용 값 | 기본 | 필수 |
|---|---|---|---|---|
| `effect_id` | string | `^[a-z][a-z0-9_]*$` · 표 안 유일 | — | 필수 |
| `kind` | enum | 효과 종류 이름(코어 `TriggerPayload`)(§3 에서 표에 들 수 있는 것만) + 액티브 6(§3 끝) | — | 필수 |
| `deprecated` | bool | | false | — |
| `magnitude_mode` | enum | `Flat` · `OwnerStatRatio`(U7) | `Flat` | — |
| `basis_stat` | enum | `Attack`(U11 — 평타 한 발 출력 합 × 공격자 쪽 배율) · `MaxHealth` | — | ratio 면 필수 |
| `ratio` | float | > 0 | — | ratio 면 필수 |
| `damage` ★ | float | ≥ 0 | 0 | 종류별(§3) |
| `shield` ★ | float | > 0 | 0 | 종류별 |
| `percent` | float | % (+30 = +30% · 음수 허용) | 0 | 종류별 |
| `mul` | float | 배율(2 = ×2) | 0 | 종류별 |
| `count` | int | ≥ 0 | 0 | 종류별 |
| `radius_tiles` | int | ≥ 0 — 광역 원 반경(+칸 반폭·대상 몸은 코어가 더한다 · 제약 13) | 0 | 종류별 |
| `range_tiles` | int | ≥ 0 — 사거리·재조준·조준 거리 | 0 | 종류별 |
| `duration_sec` | float | ≥ 0 | 0 | 종류별 |
| `flight_sec` | float | ≥ 0 — 발사 → 착탄(자리형 · 예고 시간) | 0 | 종류별 |
| `tick_sec` | float | ≥ 0 — 0 이면 `damage` = DPS | 0 | `AreaDot` |
| `stack_cap` | int | ≥ 0 | 0 | 종류별 |
| `speed` | float | ≥ 0 — 넉백·당김 속도 | 0 | 종류별 |
| `cone_half_deg` | float | (0, 90) | 0 | `AreaBreath` |
| `density_radius_tiles` · `landing_ring_tiles` | int | ≥ 0 | 0 | 도약 2종 |
| `cc_kind` · `stack_kind` · `buff_stat` | enum | `DcCcKind` · `DcStackKind` · `CardBuffKind` 이름 | 첫 값 | 종류별 |
| `shield_filter` · `includes_self` | enum · bool | `ShieldTargetFilter` 이름 | `Self` · false | `GrantShield` |
| `projectile_id` · `pattern_id` · `hazard_id` | string | 각 표의 키 | — | 종류별 |
| `telegraph` | bool | U1 착탄 예고 | false | — |

- **피해는 여기에만 있다(U10).** 패턴 `damage` · 장판 DoT 수치 · 길막 폭발 피해 · 슬램 피해 → 이 표 `damage`. 한 효과 = 피해 칸 하나(§3 전 종류가 피해 원천 ≤ 1 — 확인됨).
- 뷰 전용(SO 에 남고 시트에 없다): `auraPrefab` · `auraScale` · `stackModifier`(문안 전용 참조 — `DcMechanic.cs:376-382`).
- 검증: 종류가 안 쓰는 칸이 비지 않으면 경고 · 참조 id 는 그 표에 있어야 · `deprecated` 줄을 `Skills` 가 참조하면 거절 · 비율형 규칙은 §9.
- 해시(README 계약 8): `effect_id` · 줄 순서 · `deprecated` 는 해시 밖 · 새 칸(`magnitude_mode` 등)은 기본값이면 안 쓴다.

## 3. 종류 × 칸 (옛 `DcPayloadSpec` 칸 → 새 칸)

「옛 → 새」의 `m` = `magnitude` · `t` = `tileRange` · `d` = `duration`. 라이브 = 저작 줄 수(카드/유닛/적).

| kind | 옛 → 새 | 필수 참조 | 라이브 |
|---|---|---|---|
| ProjectileToTarget | m→`damage`★ · t→`range_tiles` · d→`flight_sec`(칸 결합 탄) · `telegraph` | `projectile_id` | 3/0/0 |
| SelfTileAoe | m→`damage`★ · t→`radius_tiles` · d→`flight_sec` | `projectile_id` | 7/1/1 |
| NextAttackDoubleFire | 칸 없음 | | 1/0/0 |
| SelfBuffLethal | m→`percent`(공속) · d→`duration_sec` | | 2/0/0 |
| SelfBlink | m→`density_radius_tiles` · t→`landing_ring_tiles` · `slamDamage`→`damage`★ · `slamTileRange`→`radius_tiles` | `projectile_id`(연출 · 선택) | 0/0/2 |
| PlacementAura | m→`percent`(공속) · d→`duration_sec`(수면 · 0 = 없음) | | 1/0/0 |
| AllyMoveSpeedAura | m→`percent` · t→`radius_tiles` · d→`duration_sec`(TTL) | `projectile_id`(선택) | 0/0/1 |
| ApplyCcToTarget | `ccKind`→`cc_kind` · d→`duration_sec` · m→`speed`(Impulse 만) | | 3/0/0 |
| ApplyStackToTarget | `stackKind`→`stack_kind` · m→`count` · d→`duration_sec`(겹당) · t→`stack_cap`(0 = 기본 5) | | 2/0/0 |
| SelfStatBuff | `buffStat`→`buff_stat` · m→`percent` · d→`duration_sec`(≤0 영구) · t→`stack_cap`(최대 중첩) | | 4/0/0 |
| HeavyStrike | m→`mul`(>1) — 공격 수식자로 접힘 | | 3/0/0 |
| DreamCocoon | m→`percent` · d→`duration_sec`(잠) · `buffStat`→`buff_stat` | | 1/0/0 |
| BountyMark | m→`mul`(각성 배율 >1) · t→`percent`(받는 피해 감소 0~99) | | 1/0/0 |
| AreaSleep | m→`count`(인원) · t→`radius_tiles` · d→`duration_sec` | `projectile_id`(선택) | 1/0/1 |
| EmitProjectilePattern | 패턴 `damage`→`damage`★(U10 · 탄이 길막을 세우면 **길막 폭발 피해**) · t→`range_tiles`(방향 패턴 필수) | `pattern_id` | 2/7/2 |
| UltimateLeap | m→`density_radius_tiles` · t→`landing_ring_tiles` · d→`flight_sec`(예고) · `slamDamage`→`damage`★ · `slamTileRange`→`radius_tiles` | `projectile_id` | 0/0/1 |
| GrantShield | m→`shield`★ · t→`radius_tiles`(0 = 자기만) · (실드 캐스트: `targetCount`→`count` · `filter`→`shield_filter` · `includes_self`=true) | | 0/1+1/2 |
| AreaBreath | m→`damage`★ · t→`range_tiles` · `coneHalfAngleDeg`→`cone_half_deg` | | 0/0/1 |
| SelfOrbitProjectile | m→`damage`★ · t→`radius_tiles`(궤도) · d→`duration_sec` · `orbitCount`→`count` | `projectile_id` | 1/0/0 |
| AreaTaunt | t→`radius_tiles` · d→`duration_sec` | | 0/1/0 |
| SpawnHazard | 장판 DoT `param1`→`damage`★(U10 · §6) | `hazard_id` | 1/0/0 |
| RecallAttachedToFront | 칸 없음(손패 선언) | | 1/0/0 |
| AllyStatAura · OpponentStatAura | `buffStat`→`buff_stat` · m→`percent` · t→`radius_tiles` · d→`duration_sec` | | 0/1/0 · 0/1/0 |
| GainCost | m→`count` | | 0/1/0 |
| ReduceSkillCooldown | m→`duration_sec`(줄일 초) | | 0/1/0 |
| AreaApplyStack | `stackKind`→`stack_kind` · m→`count` · t→`radius_tiles` · d→`duration_sec`(겹당) | | 0/1/0 |
| AreaCc | m→`damage`★(부수 · 0 = CC 만) · t→`radius_tiles` · d→`duration_sec` · `ccKind`→`cc_kind` | | 0/1/0 |
| AreaDot | m→`damage`★(틱당) · t→`radius_tiles` · d→`duration_sec` · `tickIntervalSec`→`tick_sec` | | 0/1/0 |
| **표에 들지 않음** | `None`(센티넬) · `AreaBarrage`(이관 — 거절) · `SelfWarmupBuff`(죽은 값) · `SplitOnDeath`(→ `Enemies` 열 · §8) | | 적 2 = Split |

**액티브 6(새 종류 — `SkillEffectType` 1:1 · 저작 enum append 는 unit 4)**: 옛 `SkillData` 칸 → 새 칸. 주인 없는 시전이라 비율형 거절(계약 9).

| kind | 옛 → 새 | 라이브 |
|---|---|---|
| ActiveMeteor | `magnitude`→`damage`★(거절) · `range`→`radius_tiles`(= `RangeToTiles`) · `warningSec`→`flight_sec` · `projectile`→`projectile_id`(필수) | 1 |
| ActiveSlowField | `magnitude`→`mul`(이속 ×) · `range`→`radius_tiles` · `durationSec`→`duration_sec` | 1 |
| ActivePowerSurge · ActiveRapidFire | `magnitude`→`mul` · `range`→`radius_tiles` · `durationSec`→`duration_sec` | 1 · 1 |
| ActiveTornado | `magnitude`→`speed`(당김) · `range`→`radius_tiles` · `durationSec`→`duration_sec` | 1 |
| ActivePortal | `durationSec`→`duration_sec`(두 칸 조준은 `Cards.needs_two_tiles`) | 1 |

**줄 수 추정**: 저작 효과 64(카드 34 + 유닛 17 + 적 13) − Split 2 + 실드 캐스트 1 + 액티브 6 = 69 → 같은 값 병합 시 −3(`HeavyStrike ×2` 3장 → 1 · 짱쎈 `SelfBlink` 2줄 → 1 — 「사용자 확인」 2) → **≈ 66**.

## 4. `Skills` (소유 줄)

| 열 | 형 | 허용 값 | 기본 | 필수 |
|---|---|---|---|---|
| `owner_kind` | enum | `card` · `unit` · `enemy` | — | 필수 |
| `owner_id` | string | `Cards` · `Units` · `Enemies` 의 `id` | — | 필수 |
| `slot` | int | ≥ 0 · 소유자 안 유일 · 굽는 순서 = slot 오름차순 | — | 필수 |
| `trigger` | enum | 트리거 종류 이름(코어 `TriggerKind`) + `Cast`(액티브 시전 — 표 어휘 · 저작 append 는 unit 4) | — | 필수 |
| `period` | int | ≥ 1 | 0 | `AttackN` · `OnDamagedN` |
| `period_sec` | float | > 0 | 0 | `PeriodicTimer` |
| `fraction` | float | (0, 1) | 0 | `HealthThreshold` |
| `subject` | enum | `Self` · `OthersPlacement` | `Self` | — |
| `gate` · `gate_subject` · `gate_value` | enum · enum · float | `None`·`HpBelow` · `Self`·`EventTarget` · (0,1) | `None` · `Self` · 0 | — |
| `fire_cap` | int | ≥ 0 · 0 = 무제한 | 0 | — |
| `effect_id` | string | `Effects` 키 · 폐기 아님 | — | 필수 |

- **수명은 칸이 아니다** — 소유자 종류에서 파생(계약 3): 카드·유닛·적 = 주인 수명(`BindingLifetime.Owner`) · `Cast` = `UntilFireCap`. 저작 손잡이는 `fire_cap` 하나. 오늘 빌더가 종류로 박는 `UltimateLeap → FireCap 1`(`BindingDefinitionBuilder.cs:351`)은 이 칸 값 1 로 옮기고, 0 이면 경고.
- 인스턴스 값(`StackId` · 부착 캐스트 FireCap/Lifetime)은 칸이 아니다(unit 1a).
- 검증(코어 `EffectComboRule` 하나 — 카드는 `host_kinds` 의 종류마다): 트리거별 필수 칸 · 쓰지 않는 칸 ≠ 0 경고 · `subject = OthersPlacement` ⇒ `OnPlace` · `trigger = None` ⇒ Unit 카드 × {SelfBuffLethal · DreamCocoon · BountyMark · PlacementAura} · `Cast` ⇔ Active 카드(줄 정확히 1 · `fire_cap` 1) · Squad 카드 = 줄 0 · `BountyMark` 카드는 그 한 종류만(`CardDefinitionBuilder.cs:121`) · 나머지 조합 거절은 census 표 3 을 그대로(unit 2 가 정리).
- 줄 수: 카드 34 + 액티브 6 + 유닛 17 + 실드 캐스트 1 + 적 11 = **69**.

## 5. `Projectiles` · `Patterns` · `PatternShots`

`Projectiles`(한 줄 = 탄 한 종 · 키 `id` = 오늘 `ProjectileData.id`)

| 열 | 형 | 기본 | 비고 |
|---|---|---|---|
| `id` | string | — | 필수 · 유일 |
| `speed` · `hit_threshold` | float | 10 · 0.3 | |
| `flight_mode` | enum `ProjectileFlightMode` | Homing | 궤적 결합 = `CombatDefinitionBuilder.Translate` |
| `arc_height` · `min_flight_sec` · `impact_tile_range` | float · float · int | 2 · 0.3 · 1 | |
| `pierce_count` · `rehit_cooldown_sec` | int · float | 1 · 0 | |
| `knockback_distance` · `knockback_sec` | float | 0 | |
| `bezier_lateral` · `bezier_forward_bias` | float | 1.2 · 0.35 | |
| `splash_radius` · `splash_damage_mul` | float | 0 · 0.5 | `onHitEffect == Splash` 를 반경으로 접는다(`CombatDefinitionBuilder.cs:389`) — `splash_damage_mul` 은 실린 피해의 **배율**이라 피해 칸이 아니다 |
| `blocker_id` | string | — | `BallisticBlocker` 필수 · `Blockers` 키 |

`Patterns`(키 `id` = `ProjectilePatternData.id`) — **`damage` 칸 없음(U10)**: `barrel_id`(→ `Projectiles` · 필수) · `selection`(`PatternSelectionRule`) · `min_angle_deg` · `max_angle_deg` · `randomize_shots` · `random_interval_min_sec` · `random_interval_max_sec` · `reselect_per_shot` · `telegraph_sec`(착탄 지연 = 모양 · U1 `telegraph` 와 별개) · `scope_tile_range` · `fan_out_all` · `fan_out_stagger_sec`. `PatternShots`: `pattern_id` · `idx` · `direction_t`(0~1) · `interval_after_prev_sec`. 검증 = 오늘 `TryToSpec`(발 1~15 · min ≤ max · 방향 탄 ⇔ `selection None`) + `BindPattern` 경고.
- 평타 다연발 패턴(`Pattern_Defender_*` 2)의 `damage` 는 오늘도 출력 피해로 덮인다(`ProjectilePatternData.cs` 툴팁) → 손실 없음.

## 6. `Hazards` · `HazardEffects` · `Blockers`

`Hazards`(키 `id` = **에셋 이름** — 계약 10 · 개명 안 함): `shape`(`HazardShape`) · `radius` · `lifetime_sec` · `target_factions`(`Faction` 비트 · 기본 EnemyUnit). `HazardEffects`: `hazard_id` · `idx` · `kind`(`CcKind`) · `value`(**비피해 수치만** — 감속 배율 · 넉백 속도 · DoT 면 비움) · `rest_sec` · `tick_sec` · `element`(`DotElement`).
`Blockers`(키 `id` = 에셋 이름): `shape` · `max_hp` · `decay_per_sec` · `explode_tile_range` · `explode_target_cap` · `explode_projectile_id`. 폭발 여부 = 실려 온 피해 > 0(오늘 `explodeDamage > 0` 과 같다).

**U10 결정 — 장판 수치의 자리**: DoT 수치(`param1`) · 길막 폭발 피해 = 피해 → 그 장판을 까는 **효과 줄 `damage`**. 감속 배율 등 **비피해 수치는 장판 줄에 남는다**. 이유: (a) U10 의 대상은 피해이고 비율형 적용 칸도 피해·실드뿐이라 비피해 수치를 옮겨 얻는 것이 없다 (b) 장판은 하위 효과 합성(`effects[]`)이라 효과 줄 칸 하나에 담을 수 없는 쪽이 비피해 다수이고, 피해는 「효과가 까는 장판의 DoT ≤ 1」로 묶으면 칸 하나로 충분하다 (c) 같은 장판을 피해만 다르게 여러 효과가 쓰는 U10 의 목적이 그대로 선다.
- 검증: 효과가 까는 장판의 DoT 하위 효과 ≤ 1 · `damage > 0` 인데 DoT 없음 → 거절 · 길막을 세우는 패턴 효과의 `damage` = 폭발 피해.
- 결과: 효과 줄 없이 까이는 장판(씬 디버그 스폰 `Command.DebugSpawnHazard` — Fire/Poison/Ice 3x3 · `HazardCastAbility` 의 1x1 — 둘 다 라이브 플레이 경로 아님)은 피해 원천이 없어진다 → unit 1b 에서 디버그 명령이 `damage` 를 싣는다. 옮겨지는 옛 값: Ember 12 · Fire_3x3 20 · Fire_1x1 10 · Poison_1x1 20 · Poison_3x3 10 · BombBarrel 120.

## 7. `Cards` · `CardStatEffects` · `CardAttackMods`

| 열 | 형 | 기본 | 비고 |
|---|---|---|---|
| `id` · `display_name` · `description` | string | — | `id` 필수 · 문안은 formatter 가 이긴다(에셋 문안 = 폴백) |
| `visible` | int 0/1 | 1 | |
| `type` · `category` · `axis` | enum `CardType` · `CardCategory` · `CardTargetAxis` | Squad · Normal · ClassRanger | |
| `attach_type` · `attach_value` | enum `DcAttachType` · string | None · — | |
| `host_kinds` | flags `Defender` · `Enemy` | **Defender**(U5) | `BountyMark` 카드 ⇔ `Enemy` 만(오늘 `HasBountyMark()` 파생을 값으로) |
| `leak_allowance_cost` | int | 0 | ⚠ 코어 소비 0 — 문안만 읽는다(§12 발견) |
| `cooldown_sec` · `needs_two_tiles` | float · bool | 0 · false | Active 만(옛 `SkillData.cooldownSec` · `needsTwoTiles`) |

`CardStatEffects`(Squad 만): `card_id` · `slot` · `buff_stat`(`CardBuffKind`) · `percent`. `CardAttackMods`(Unit 만 · 트리거 없는 상시 수식자 — 효과 id 를 주지 않는다: 재사용 소유자 0 · §11): `card_id` · `slot` · `kind`(`DcAttackModKind`) · `count` · `range_tiles` · `damage_mul`. 스킬 칸은 없다 — 카드의 규칙은 `Skills`.

## 8. `Units` · `Enemies`

- 스탯 열 = 현행 스탯 시트(`UnitStatImportDto.DefenderStatDto` · `EnemyStatDto`) 그대로. 이 spec 이 보태거나 빼는 것만:
- `Units`: 스킬 칸 없음. `abilities` 중 규칙 레일 2종(`UnitSkillAbility` · `ShieldCastAbility`) → `Skills`. 평타 경로 능력 4종은 SO 참조로 남는다(시트 밖 · §11).
- `Enemies`: `split_unit_id`(→ `Enemies` 키) · `split_count`(≥1 · ≤ `SplitMaxChildren`) — 옛 `SplitOnDeath` 메커닉(빌더가 규칙 줄을 안 만든다 `BindingDefinitionBuilder.cs:104-112` · 코어는 적 정의 `SplitChain.NextInChain` 으로 읽는다). 검증 = `SplitChain.Validate`(순환·과길이·자손 총수).

## 9. 비율 적용 칸 (U7 · U11)

| 종류 | 비율 칸 | 비고 |
|---|---|---|
| ProjectileToTarget · SelfTileAoe · SelfOrbitProjectile · AreaBreath · AreaCc · AreaDot · EmitProjectilePattern · SelfBlink · UltimateLeap · SpawnHazard | `damage` | AreaDot = 틱당 · 패턴 = 발당 · 도약 = 슬램 |
| GrantShield | `shield` | |
| ActiveMeteor | `damage`(칸은 있으나 **거절** — 주인 없는 시전) | 계약 9 |
| 나머지 전부(버프·오라·CC·스택·배율·도발·메타·표식·손패·액티브 5) | 없음 → **비율형 거절** | |

- 규칙: `magnitude_mode = OwnerStatRatio` ⇒ 그 종류의 비율 칸이 있어야 · 그 칸은 비움(값 = 시전 순간 `basis_stat` 최종값 × `ratio`) · `Cast` 소유 줄은 거절 · 사망·퇴근 트리거는 감지 순간 스냅샷(계약 9). 회복 종류는 오늘 없다.

## 10. id 규칙 (README 계약 10)

- 네임스페이스 = 표마다(`Effects` · `Projectiles` · `Patterns` · `Hazards` · `Blockers` · `Cards` · `Units` · `Enemies`). 참조 칸은 이름에 표를 밝힌다(`projectile_id` · `pattern_id` …) — 겹쳐도 모호하지 않다.
- **겹침 3건(허용 · 개명 안 함)**: `cannon_strike` · `nightmare_barrage` · `nightmare_missile` = 탄 id 이자 패턴 id.
- 장판 · 길막 id = 에셋 이름(`Hazard_Ember` 등 — 스네이크 규칙의 유일한 예외).
- 새 `effect_id` = 소문자 스네이크 · 첫 공개 뒤 개명 금지 · 삭제 대신 `deprecated`(서버 어휘 — 계약 6). 이전 기본값(unit 4 dry-run 에서 바꿀 수 있다): 유닛 능력 = 능력 id(`sky_strike_cannon` · `shield_shield_shuttle` …) · 카드 = 카드 id(메커닉 여럿이면 `_{slot}`) · 적 = `{enemy_id}_{slot}` · 액티브 = `SkillData.id`(`meteor` …). 오늘 이 후보들 사이 충돌 0.
- 기존 id 전부 비지 않고 표 안 유일(카드 54 · 탄 83 · 패턴 13 · 유닛 27 · 적 24 — 재확인).

## 11. 필드 → 열 전수 대조

분류: **열** = 새 표 칸 · **고유** = 소유자 표(Cards · Units · Enemies 와 자식) · **뷰** = SO 에 남고 시트 밖 · **밖** = 이 spec 이 옮기지 않음(SO 그대로 — 손실 아님).

| 저작 | 필드 → 분류 · 새 자리 |
|---|---|
| `DcTriggerSpec`(8) | `kind`→`Skills.trigger` · `period`→`period` · `periodSeconds`→`period_sec` · `fraction`→`fraction` · `gate` · `gateSubject` · `gateValue` → 같은 이름 · `subject`→`subject` — **열 8** |
| `DcPayloadSpec`(20) | `kind`→`Effects.kind` · `magnitude` · `tileRange` · `duration` → 종류별 칸(§3) · `projectile`→`projectile_id` · `pattern`→`pattern_id` · `hazard`→`hazard_id` · `ccKind` · `stackKind` · `buffStat` → 같은 이름 · `slamDamage`→`damage` · `slamTileRange`→`radius_tiles` · `tickIntervalSec`→`tick_sec` · `orbitCount`→`count` · `coneHalfAngleDeg`→`cone_half_deg` · `telegraph`→`telegraph` — **열 16** · `splitUnit`(+ Split 의 `magnitude`) → `Enemies.split_*` — **고유 1** · `auraPrefab` · `auraScale` · `stackModifier` — **뷰 3** |
| `DreamcatcherCard`(15) | `mechanics`→`Skills` · `skill`→`Skills`(`Cast`) + `Effects`(액티브) — **열 2** · `id` · `displayName` · `description` · `visible` · `axis` · `category` · `type` · `attachType` · `attachValue` · `leakAllowanceCost` → `Cards` · `effects`→`CardStatEffects` · `attackMods`→`CardAttackMods` — **고유 12** · `art` — **뷰 1** |
| `CardEffect`(2) · `DcAttackModSpec`(4) | `kind`→`buff_stat` · `percent` / `kind` · `count` · `tileRange`→`range_tiles` · `damageMul` — **고유 6** |
| `UnitSkillAbility`(2) | `mechanics`→`Skills`(owner = 그 방어유닛) · `id`→`effect_id` 기본값 — **열 2** |
| `ShieldCastAbility`(5) | `cooldown`→`Skills.period_sec`(PeriodicTimer) · `amount`→`shield` · `targetCount`→`count` · `filter`→`shield_filter` · `id`→`effect_id` — **열 5**. 반경 = 오늘 유닛 `attackRange` 파생(「사용자 확인」 3) |
| `AttackUnitData.nightmareMechanics`(1) · `DefenderUnitData.abilities`(1) | → `Skills`(owner = 그 적) — **열 1** · 능력 목록(평타 4종만 남음) — **고유 1** |
| `SkillData`(13) | `id`→`effect_id` · `effect`→`kind` · `range`→`radius_tiles` · `magnitude` · `durationSec` · `warningSec`→`flight_sec` · `projectile`→`projectile_id` — **열 7** · `cooldownSec` · `needsTwoTiles` → `Cards` — **고유 2** · `displayName` · `description` · `uiTint`(소비 0) · `cost`(문안만 — §12) — **밖 4** |
| `ProjectileData`(39) | §5 의 17 칸(`onHitEffect` 는 `splash_radius` 로 접힘 · `spawnBlocker`→`blocker_id`) — **열 17** · `visualScale` · `visualHeightOffset` · `projectilePrefab` · `hitPrefab` · `facing` · `spinSpeed` · `preserveVfxColors` · `tintColor` · `emissionMultiplier` · `scaleJitter` · `hueJitter` · `rotationJitter` · `textureVariants` · `selectMode` · `hitVfxLifetime` · `hitVfxScale` · `castPrefab` · `castVfxLifetime` · `dropHeight` · `fallPortion` — **뷰 20**(`visualScale` 은 규칙 줄에 실려 사건으로 나르지만 원천은 SO) · `onHitMagnitude` · `onHitDuration`(소비 0 · 라이브 0) — **밖 2** |
| `ProjectilePatternData`(15 + 발 2) | `damage`→`Effects.damage`(U10) · 나머지 14 + `shots[]` 2 → `Patterns` · `PatternShots` — **열 17** |
| `HazardSO`(6) · `HazardEffect`(6) | `shape` · `radius` · `lifetime` · `zoneTargetFactions` · `effects`→`HazardEffects` — **열 5** · `visualPrefab` — **뷰 1** · `kind` · `restDuration` · `tickInterval` · `element` · `param1`(DoT → `Effects.damage` / 그 밖 → `value`) — **열 5** · `param2`(소비 0 — 빌더가 안 옮긴다 `BoardEffectDefinitionBuilder.cs:80-89`) — **밖 1** |
| `BlockingHazardSO`(11) | `shape` · `maxHp` · `healthDecayPerSec` · `explodeTileRange` · `explodeTargetCap` · `explodeProjectile` → `Blockers` · `explodeDamage`→`Effects.damage` — **열 7** · `visualPrefab` · `spawnVfxPrefab` · `destructionVfxPrefab` · `overheadHeight` — **뷰 4** |
| `DreamstoneData`(5) | `id` · `displayName` · `icon` · `grade` · `effect` — **밖 5**(효과 = 코어 로컬 고정 스탯 `DreamstoneStat` · 트리거 저작 없음 · 오늘 시트에 없다 → 후속 후보) |
| 평타 경로 능력(21) | `HazardCastAbility` 8(코어 소비 0 — census 발견 4) · `BombThrowAbility` 7 · `DirectionalVolleyAbility` 2 · `SummonPatrolAbility` 4 — **밖 21**(`CombatDefinitionBuilder` 평타 경로 · 이 spec 무관) |
| 코드가 만드는 규칙 | 기믹 `GimmickBindings` · 퇴근 임계 운석 `ResignationBarrage`(`Gimmick_ClockOut.asset` 의 탄) — 저작 필드 아님 · 밖(unit 1a: 런타임 조립 줄은 효과 표 밖) |

**합계 176 필드 = 열 92 · 고유 22 · 뷰 29 · 밖 33.**
**손실 검증**: 라이브 경로가 읽는 필드 중 새 자리가 없는 것 = 0. 옮겨지며 뜻이 바뀌는 칸 3 — 패턴 `damage`(효과로) · 장판 DoT `param1`(효과로 · 비라이브 장판 5개는 §6 기록값) · 길막 `explodeDamage`(효과로). `SplitOnDeath` 는 규칙이 아니라 적 고유 값으로 자리를 옮긴다(오늘도 규칙 줄이 없다).

## 12. 발견

1. **U10 목록에 길막 폭발이 빠져 있었다** — 폭탄맨 배럴 피해 120 은 `Blocker_BombBarrel.explodeDamage`(`BlockingHazardDef.ExplodeDamage`)에 있고 패턴 `damage` 는 0 이다. README U10 · unit 1 문서에 추가(이 커밋).
2. **실드 캐스트는 넷째 저장처다** — `ShieldCastAbility` 를 코드가 규칙으로 굽는다(`BindingDefinitionBuilder.cs:66-83`). README 계약 2 · unit 4 에 추가(이 커밋).
3. **「회복」 효과 종류는 없다** — unit 0 · 3 문서의 비율 칸 예시에서 뺐다(이 커밋).
4. `SkillData.cost`(2~4)는 **문안만** 읽는다(`DreamcatcherCardText.cs:549`) — 실비용은 `AwakeningConfig.costActive`(20). 카드 문안이 다른 값을 보일 수 있다 → 후속 후보(이 spec 밖).
5. `DreamcatcherCard.leakAllowanceCost` 코어 소비 0 — 문안(`DreamcatcherCardText.cs:149-151`)과 테스트만 읽는다. 열은 유지(값 보존) · 처리는 후속 후보.
6. census(2026-09-26)의 Unit 카드 33 · 메커닉 32 는 이후 `star_strike` · `gaesagi` 추가로 35 · 34 — census 는 날짜 스냅샷이라 고치지 않는다.

## 사용자 확인 (2026-09-28 답: 표 모양 승인 U12 · 같은 값 병합 안 함 U13 · 실드 반경 고정값 U14 — README)

1. **표 모양 전체**(0 완료 기준 — 저작자가 쓰는 도구). 특히 ① 옛 겸직 칸(`magnitude`·`tileRange`·`duration`)을 뜻 이름 칸으로 푼 것 ② 액티브를 효과 표에 넣은 것(종류 6 append · 트리거 `Cast`) ③ 공격 수식자 · 스쿼드 효과 · 드림스톤은 효과 id 를 주지 않은 것.
2. **같은 값 효과 병합** — `heavy_strike` · `execution_strike` · `calamity_heart` 의 강타 ×2 가 한 효과, 짱쎈 순간이동 2줄(0.9 · 0.5)이 한 효과가 된다. 병합하면 **한 줄 조정이 모든 소유자에 번진다**. 병합 / 소유자별 분리(값이 같아도 다른 id) 중 택일(unit 4 dry-run 에서 줄 단위로 다시 확인).
3. **실드 셔틀 실드 캐스트 반경** — 오늘은 유닛 사거리에서 파생(사거리를 바꾸면 실드 반경도 바뀐다). 효과 줄 고정값으로 굳히면 이 연동이 끊긴다. 굳힘(계약 1 — 소유자별 덮어쓰기 없음) / 파생 유지 중 택일.
