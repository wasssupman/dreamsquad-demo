# skill-data-table unit 4 — 이전 dry-run 표(헤드리스 · 승인 전)

> 생성: `python3 tools/skill-data-table/dry_run.py` — 에셋 YAML 위에서 `LegacyBindingMigration` 과 같은 규칙을 돌렸다(에셋 0).
> 정본 = Unity 메뉴 `Wassup/BattleCore/Skill Data Table/이전 dry-run (표만 쓴다)` → `dry_run_table.md`. 둘이 다르면 Unity 쪽이 맞다.

소유자 77 · 효과 줄 69(카드 40 · 유닛 18 · 적 11 · **병합 0** — U13) · 깃발 달린 줄 3

**왕복 검사**(이전 뒤 굽기가 싣는 겸직 칸 magnitude · tileRange · duration + 피해 = 라이브 굽기 스냅샷): 대조 56 줄 · 불일치 0 · 스냅샷에 없는 줄 0 (카드 %·배율 인코딩 5종 · 강공 · 인수인계(손패 선언)는 굽기가 값을 접어 대조 밖 — `BindingSpecBakeTests` 가 Unity 에서 전 칸을 잰다)


깃발 = 손실 · 해시 변화 · 확인 필요 · 버림. 「라벨 변화」는 해시 밖(굽기 스냅샷 텍스트만). 효과 에셋 = `Assets/_Project/Data/Effects/Effect_{effect_id}.asset`.

## card `active_meteor` — Assets/_Project/Data/Dreamcatcher/Active_Meteor.asset

- cooldown_sec = 18 · needs_two_tiles = 0
- SkillData 표시 칸(displayName · description · uiTint · cost=4)은 옮기지 않는다(`tables.md` §11 밖)

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | skill | `meteor` | ActiveMeteor | Cast · fire_cap 1 | damage 40 · radius_tiles 2 · flight_sec 1.5 · projectile_id meteor |  |

## card `active_portal` — Assets/_Project/Data/Dreamcatcher/Active_Portal.asset

- cooldown_sec = 14 · needs_two_tiles = 1
- SkillData 표시 칸(displayName · description · uiTint · cost=3)은 옮기지 않는다(`tables.md` §11 밖)

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | skill | `portal` | ActivePortal | Cast · fire_cap 1 | duration_sec 8 |  |

## card `active_power_surge` — Assets/_Project/Data/Dreamcatcher/Active_PowerSurge.asset

- cooldown_sec = 30 · needs_two_tiles = 0
- SkillData 표시 칸(displayName · description · uiTint · cost=3)은 옮기지 않는다(`tables.md` §11 밖)

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | skill | `power_surge` | ActivePowerSurge | Cast · fire_cap 1 | mul 2 · radius_tiles 1 · duration_sec 8 |  |

## card `active_rapid_fire` — Assets/_Project/Data/Dreamcatcher/Active_RapidFire.asset

- cooldown_sec = 25 · needs_two_tiles = 0
- SkillData 표시 칸(displayName · description · uiTint · cost=2)은 옮기지 않는다(`tables.md` §11 밖)

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | skill | `rapid_fire` | ActiveRapidFire | Cast · fire_cap 1 | mul 2 · radius_tiles 1 · duration_sec 6 |  |

## card `active_slow_field` — Assets/_Project/Data/Dreamcatcher/Active_SlowField.asset

- cooldown_sec = 20 · needs_two_tiles = 0
- SkillData 표시 칸(displayName · description · uiTint · cost=2)은 옮기지 않는다(`tables.md` §11 밖)

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | skill | `slow_field` | ActiveSlowField | Cast · fire_cap 1 | mul 0.6 · radius_tiles 2 · duration_sec 5 |  |

## card `active_tornado` — Assets/_Project/Data/Dreamcatcher/Active_Tornado.asset

- cooldown_sec = 12 · needs_two_tiles = 0
- SkillData 표시 칸(displayName · description · uiTint · cost=3)은 옮기지 않는다(`tables.md` §11 밖)

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | skill | `tornado` | ActiveTornado | Cast · fire_cap 1 | radius_tiles 2 · duration_sec 2 · speed 6 |  |

## card `all_atk` — Assets/_Project/Data/Dreamcatcher/Card_AllAtk8.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `all_move` — Assets/_Project/Data/Dreamcatcher/Card_AllMove10.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `boomerang` — Assets/_Project/Data/Dreamcatcher/Card_Boomerang.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `boomerang` | ProjectileToTarget | AttackN · period 1 | damage 25 · range_tiles 4 · projectile_id boomerang |  |

## card `bouncy_bead` — Assets/_Project/Data/Dreamcatcher/Card_BouncyBead.asset

- hostKinds = Defender
- 공격 수식자 1 줄 = 카드 자식 값(`CardAttackMods`) — 그대로

## card `sub_butterfly_dream` — Assets/_Project/Data/Dreamcatcher/Card_ButterflyDream.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `sub_butterfly_dream` | DreamCocoon | None | percent 35 · duration_sec 4 · buff_stat AttackDamage |  |

## card `calamity_heart` — Assets/_Project/Data/Dreamcatcher/Card_CalamityHeart.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `calamity_heart_0` | SelfBuffLethal | None | percent 100 · duration_sec 6 |  |
| 1 | mechanics[1] | `calamity_heart_1` | HeavyStrike | AttackN · period 3 | mul 2 |  |
| 2 | mechanics[2] | `calamity_heart_2` | SelfTileAoe | OnDeath | damage 400 · radius_tiles 2 · projectile_id meteor |  |

## card `cornered_burst` — Assets/_Project/Data/Dreamcatcher/Card_CorneredBurst.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `cornered_burst` | SelfTileAoe | OnDamagedN · period 1 · gate HpBelow/Self 0.3 | damage 20 · radius_tiles 1 · projectile_id meteor |  |

## card `corpse_burst` — Assets/_Project/Data/Dreamcatcher/Card_CorpseBurst.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `corpse_burst` | SelfTileAoe | OnKill | damage 25 · radius_tiles 1 · projectile_id meteor |  |

## card `cost1_as` — Assets/_Project/Data/Dreamcatcher/Card_Cost1As5.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `cost1_hp` — Assets/_Project/Data/Dreamcatcher/Card_Cost1Hp10.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `cracked_grail` — Assets/_Project/Data/Dreamcatcher/Card_CrackedGrail.asset

- 스쿼드 스탯 효과 2 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `devouring_craving` — Assets/_Project/Data/Dreamcatcher/Card_DevouringCraving.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `devouring_craving` | SelfStatBuff | OnKill | percent 50 · duration_sec 3 · buff_stat AttackSpeed |  |

## card `ember_bite` — Assets/_Project/Data/Dreamcatcher/Card_EmberBite.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `ember_bite` | ApplyStackToTarget | AttackN · period 3 | count 1 · duration_sec 4 · stack_kind Bleed |  |

## card `ember_field` — Assets/_Project/Data/Dreamcatcher/Card_EmberField.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `ember_field` | SpawnHazard | OnKill | damage 12 · hazard_id Hazard_Ember | U10 — 장판 'Hazard_Ember' DoT 12 → 효과 damage |

## card `execution_strike` — Assets/_Project/Data/Dreamcatcher/Card_ExecutionStrike.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `execution_strike` | HeavyStrike | AttackN · period 1 · gate HpBelow/EventTarget 0.25 | mul 2 |  |

## card `eye_on_the_end` — Assets/_Project/Data/Dreamcatcher/Card_EyeOnTheEnd.asset

- hostKinds = Defender
- 공격 수식자 1 줄 = 카드 자식 값(`CardAttackMods`) — 그대로

## card `farewell` — Assets/_Project/Data/Dreamcatcher/Card_Farewell.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `farewell` | SelfTileAoe | OnDeath | damage 500 · radius_tiles 2 · projectile_id meteor |  |

## card `sub_fattened_offering` — Assets/_Project/Data/Dreamcatcher/Card_FattenedOffering.asset

- hostKinds = Enemy

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `sub_fattened_offering` | BountyMark | None | percent 30 · mul 3 |  |

## card `flame_spinner` — Assets/_Project/Data/Dreamcatcher/Card_FlameSpinner.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `flame_spinner` | SelfOrbitProjectile | PeriodicTimer · period_sec 5 | damage 20 · count 2 · radius_tiles 1 · duration_sec 5 · projectile_id flame_orb |  |

## card `frenzy` — Assets/_Project/Data/Dreamcatcher/Card_Frenzy.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `frenzy` | SelfStatBuff | AttackN · period 1 | percent 8 · duration_sec 4 · stack_cap 10 · buff_stat AttackSpeed |  |

## card `frost_arrow` — Assets/_Project/Data/Dreamcatcher/Card_FrostArrow.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `frost_arrow` | ApplyCcToTarget | AttackN · period 3 | duration_sec 0.6 · cc_kind Stun |  |

## card `frostbite` — Assets/_Project/Data/Dreamcatcher/Card_Frostbite.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `frostbite` | ApplyStackToTarget | AttackN · period 1 | count 1 · duration_sec 4 · stack_cap 5 · stack_kind Ice |  |

## card `gaesagi` — Assets/_Project/Data/Dreamcatcher/Card_Gaesagi.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `gaesagi` | EmitProjectilePattern | OnPlace · subject Any | damage 100 · pattern_id gaesagi | U10 — 명세 'Pattern_Gaesagi' damage 100 → 효과 damage |

## card `gale_shove` — Assets/_Project/Data/Dreamcatcher/Card_GaleShove.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `gale_shove` | ApplyCcToTarget | AttackN · period 4 | duration_sec 0.35 · speed 6 · cc_kind Impulse |  |

## card `guardian_as` — Assets/_Project/Data/Dreamcatcher/Card_GuardianAs8.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `guardian_fortress` — Assets/_Project/Data/Dreamcatcher/Card_GuardianFortress.asset

- 스쿼드 스탯 효과 2 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `guardian_hp` — Assets/_Project/Data/Dreamcatcher/Card_GuardianHp15.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `handover` — Assets/_Project/Data/Dreamcatcher/Card_Handover.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `handover` | RecallAttachedToFront | OnRetire | — |  |

## card `heavy_strike` — Assets/_Project/Data/Dreamcatcher/Card_HeavyStrike.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `heavy_strike` | HeavyStrike | AttackN · period 5 | mul 2 |  |

## card `sub_incubus_pact` — Assets/_Project/Data/Dreamcatcher/Card_IncubusPact.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `last_flame` — Assets/_Project/Data/Dreamcatcher/Card_LastFlame.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `last_flame` | SelfBuffLethal | None | percent 90 · duration_sec 5 |  |

## card `last_stand` — Assets/_Project/Data/Dreamcatcher/Card_LastStand.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `last_stand` | SelfStatBuff | HealthThreshold · fraction 0.7 | percent 30 · buff_stat AttackDamage |  |

## card `lullaby_dart` — Assets/_Project/Data/Dreamcatcher/Card_LullabyDart.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `lullaby_dart` | ApplyCcToTarget | AttackN · period 5 | duration_sec 2.5 · cc_kind Sleep |  |

## card `moth_swarm` — Assets/_Project/Data/Dreamcatcher/Card_MothSwarm.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `moth_swarm` | EmitProjectilePattern | PeriodicTimer · period_sec 3 | damage 7.2 · pattern_id moth_swarm | U10 — 명세 'Pattern_MothSwarm' damage 7.2 → 효과 damage |

## card `nightmare_afterglow` — Assets/_Project/Data/Dreamcatcher/Card_NightmareAfterglow.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `nightmare_afterglow` | SelfStatBuff | OnKill | percent 50 · duration_sec 3 · buff_stat AttackDamage |  |

## card `nightmare_hunt` — Assets/_Project/Data/Dreamcatcher/Card_NightmareHunt.asset

- hostKinds = Defender
- 공격 수식자 1 줄 = 카드 자식 값(`CardAttackMods`) — 그대로

## card `poke_needle` — Assets/_Project/Data/Dreamcatcher/Card_PokeNeedle.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `poke_needle` | ProjectileToTarget | AttackN · period 1 | damage 20 · range_tiles 4 · projectile_id needle_flame |  |

## card `ranger_as` — Assets/_Project/Data/Dreamcatcher/Card_RangerAs10.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `ranger_atk` — Assets/_Project/Data/Dreamcatcher/Card_RangerAtk10.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `ranger_hp` — Assets/_Project/Data/Dreamcatcher/Card_RangerHp12.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `severance_meteor` — Assets/_Project/Data/Dreamcatcher/Card_SeveranceMeteor.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `severance_meteor` | SelfTileAoe | OnRetire | damage 120 · radius_tiles 1 · flight_sec 0.8 · projectile_id meteor |  |

## card `shatter_hymn` — Assets/_Project/Data/Dreamcatcher/Card_ShatterHymn.asset

- 스쿼드 스탯 효과 1 줄 = 카드 자식 값(`CardStatEffects`) — 그대로

## card `shield_burst` — Assets/_Project/Data/Dreamcatcher/Card_ShieldBurst.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `shield_burst` | SelfTileAoe | OnShieldBreak | damage 80 · radius_tiles 1 · projectile_id meteor |  |

## card `shield_lull` — Assets/_Project/Data/Dreamcatcher/Card_ShieldLull.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `shield_lull` | AreaSleep | OnShieldBreak | count 2 · radius_tiles 1 · duration_sec 2.5 |  |

## card `slow_awakening` — Assets/_Project/Data/Dreamcatcher/Card_SlowAwakening.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `slow_awakening` | PlacementAura | None | percent 50 · duration_sec 2 |  |

## card `star_strike` — Assets/_Project/Data/Dreamcatcher/Card_StarStrike.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `star_strike` | ProjectileToTarget | AttackN · period 1 | damage 30 · range_tiles 1 · flight_sec 0.5 · telegraph 1 · projectile_id star_strike_meteor |  |

## card `thornmail` — Assets/_Project/Data/Dreamcatcher/Card_Thornmail.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `thornmail` | NextAttackDoubleFire | OnDamagedN · period 5 | — |  |

## card `tremor_plate` — Assets/_Project/Data/Dreamcatcher/Card_TremorPlate.asset

- hostKinds = Defender

| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `tremor_plate` | SelfTileAoe | HealthThreshold · fraction 0.7 | damage 15 · radius_tiles 1 · projectile_id meteor |  |

## unit `archer` — Assets/_Project/Data/Defenders/Defender_Archer.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_SlowAura_Archer'.mechanics[0] | `slow_aura_archer` | OpponentStatAura | OnPlace | percent -90 · radius_tiles 3 · duration_sec 1.5 · buff_stat MoveSpeed |  |

## unit `bastion` — Assets/_Project/Data/Defenders/Defender_Bastion.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_Taunt_Bastion'.mechanics[0] | `taunt_bastion` | AreaTaunt | OnPlace | radius_tiles 2 · duration_sec 5 |  |

## unit `bomb_man` — Assets/_Project/Data/Defenders/Defender_BombMan.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_UnitSkill_BombMan'.mechanics[0] | `onplace_barrel_bomb_man` | EmitProjectilePattern | OnPlace | damage 120 · range_tiles 2 · pattern_id bombman_barrel | U10 — 길막 'Blocker_BombBarrel' 폭발 피해 120 → 효과 damage(명세 damage 0 는 안 쓰였다) |

## unit `bruiser` — Assets/_Project/Data/Defenders/Defender_Bruiser.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_MeleeBurst_Bruiser'.mechanics[0] | `melee_burst_bruiser` | SelfTileAoe | OnPlace | damage 70 · radius_tiles 2 · projectile_id bruiser_shock | U15 — 해시 변화: 착탄 연출 배율 0 → 탄 'Projectile_BruiserShock' 배율 1(카드와 같게 · 배율 1 이면 화면 무변) |

## unit `busters` — Assets/_Project/Data/Defenders/Defender_Busters.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_OpeningBeam_Busters'.mechanics[0] | `opening_beam_busters` | AreaDot | OnPlace | damage 7 · radius_tiles 2 · duration_sec 2 · tick_sec 0.2 · (뷰) aura |  |

## unit `cannon` — Assets/_Project/Data/Defenders/Defender_Cannon.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_SkyStrike_Cannon'.mechanics[0] | `sky_strike_cannon` | EmitProjectilePattern | OnPlace | damage 200 · pattern_id cannon_strike | U10 — 명세 'Pattern_Cannon_Strike' damage 200 → 효과 damage |

## unit `guardian` — Assets/_Project/Data/Defenders/Defender_Guardian.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_AllyDamageAura_Guardian'.mechanics[0] | `ally_damage_aura_guardian` | AllyStatAura | OnPlace | percent 30 · radius_tiles 2 · duration_sec 6 · buff_stat AttackDamage |  |

## unit `machine_gunner` — Assets/_Project/Data/Defenders/Defender_MachineGunner.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_OnPlaceShot_MachineGunner'.mechanics[0] | `onplace_shot_machine_gunner` | EmitProjectilePattern | OnPlace | damage 70 · range_tiles 6 · pattern_id machine_gunner_onplace | U10 — 명세 'Pattern_MachineGunner_OnPlace' damage 70 → 효과 damage |

## unit `malphite` — Assets/_Project/Data/Defenders/Defender_Malphite.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_Quake_Malphite'.mechanics[0] | `quake_malphite` | AreaCc | OnPlace | damage 40 · radius_tiles 2 · duration_sec 3 · cc_kind Stun |  |

## unit `marksman` — Assets/_Project/Data/Defenders/Defender_Marksman.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_OnPlaceShot_Marksman'.mechanics[0] | `onplace_shot_marksman` | EmitProjectilePattern | OnPlace | damage 70 · range_tiles 6 · pattern_id marksman_onplace | U10 — 명세 'Pattern_Marksman_OnPlace' damage 70 → 효과 damage |

## unit `piercer` — Assets/_Project/Data/Defenders/Defender_Piercer.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_OnPlaceShot_Piercer'.mechanics[0] | `onplace_shot_piercer` | EmitProjectilePattern | OnPlace | damage 90 · range_tiles 5 · pattern_id piercer_onplace | U10 — 명세 'Pattern_Piercer_OnPlace' damage 90 → 효과 damage |

## unit `ranger` — Assets/_Project/Data/Defenders/Defender_Ranger.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_ReduceCooldown_Ranger'.mechanics[0] | `reduce_cooldown_ranger` | ReduceSkillCooldown | OnPlace | duration_sec 2 |  |

## unit `scout` — Assets/_Project/Data/Defenders/Defender_Scout.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_GainCost_Scout'.mechanics[0] | `gain_cost_scout` | GainCost | OnPlace | count 1 |  |

## unit `shield_shuttle` — Assets/_Project/Data/Defenders/Defender_ShieldShuttle.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_AreaShield_ShieldShuttle'.mechanics[0] | `area_shield_shield_shuttle` | GrantShield | OnPlace | shield 250 · radius_tiles 2 |  |
| 1 | ShieldCastAbility 'Ability_Shield_ShieldShuttle' | `shield_shield_shuttle` | GrantShield | PeriodicTimer · period_sec 4 | shield 150 · count 2 · radius_tiles 1 · shield_filter MinHealth · includes_self 1 | U14 — 실드 반경 = 사거리 1 파생 1칸을 **고정값**으로(사거리를 바꿔도 안 따라간다)<br>해시 변화 1칸: coneSinCos 0,0 → 0,1(옛 전용 굽기가 반각을 안 구웠다 · 실드는 반각을 안 읽는다 — 동작 무변)<br>라벨 변화(해시 밖): 「Defender_ShieldShuttle 실드 캐스트」 → 「Defender_ShieldShuttle mechanic 1」 |

## unit `shotgunner` — Assets/_Project/Data/Defenders/Defender_Shotgunner.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_OnPlaceBlast_Shotgunner'.mechanics[0] | `onplace_blast_shotgunner` | EmitProjectilePattern | OnPlace | damage 100 · range_tiles 4 · pattern_id shotgunner_blast | U10 — 명세 'Pattern_Shotgunner_Blast' damage 100 → 효과 damage |

## unit `slasher` — Assets/_Project/Data/Defenders/Defender_Slasher.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_BleedBurst_Slasher'.mechanics[0] | `bleed_burst_slasher` | AreaApplyStack | OnPlace | count 5 · radius_tiles 2 · duration_sec 2 · stack_kind Bleed |  |

## unit `sniper` — Assets/_Project/Data/Defenders/Defender_Sniper.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | UnitSkillAbility 'Ability_OnPlaceShot_Sniper'.mechanics[0] | `onplace_shot_sniper` | EmitProjectilePattern | OnPlace | damage 120 · range_tiles 8 · pattern_id sniper_onplace | U10 — 명세 'Pattern_Sniper_OnPlace' damage 120 → 효과 damage |

## enemy `boss_jjangssen` — Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `boss_jjangssen_0` | SelfTileAoe | HealthThreshold · fraction 0.2 | damage 60 · radius_tiles 2 · projectile_id jjangssen_quake | U15 — 해시 변화: 착탄 연출 배율 0 → 탄 'Projectile_JjangssenQuake' 배율 1(카드와 같게 · 배율 1 이면 화면 무변) |
| 1 | mechanics[1] | `boss_jjangssen_1` | SelfBlink | HealthThreshold · fraction 0.5 | damage 50 · radius_tiles 1 · density_radius_tiles 2 · landing_ring_tiles 6 · projectile_id jjangssen_leap |  |
| 2 | mechanics[2] | `boss_jjangssen_2` | SelfBlink | HealthThreshold · fraction 0.9 | damage 50 · radius_tiles 1 · density_radius_tiles 2 · landing_ring_tiles 6 · projectile_id jjangssen_leap |  |
| 3 | mechanics[3] | `boss_jjangssen_3` | UltimateLeap | HealthThreshold · fraction 0.8 · fire_cap 1 | damage 100 · radius_tiles 2 · flight_sec 2 · density_radius_tiles 2 · landing_ring_tiles 6 · projectile_id jjangssen_leap |  |

## enemy `boss_mamemo` — Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `boss_mamemo_0` | AreaSleep | PeriodicTimer · period_sec 3.5 | count 3 · radius_tiles 4 · duration_sec 2.5 |  |
| 1 | mechanics[1] | `boss_mamemo_1` | GrantShield | HealthThreshold · fraction 0.34 | shield 350 |  |
| 2 | mechanics[2] | `boss_mamemo_2` | GrantShield | PeriodicTimer · period_sec 2.5 | shield 60 · radius_tiles 4 |  |

## enemy `boss_nightmare` — Assets/_Project/Data/Enemies/Enemy_Boss_Nightmare.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `boss_nightmare_0` | EmitProjectilePattern | PeriodicTimer · period_sec 10 | damage 150 · pattern_id nightmare_barrage | U10 — 명세 'Pattern_NightmareBarrage' damage 150 → 효과 damage |
| 1 | mechanics[1] | `boss_nightmare_1` | AllyMoveSpeedAura | PeriodicTimer · period_sec 0.5 | percent 20 · radius_tiles 3 · duration_sec 0.6 · (뷰) aura |  |
| 2 | mechanics[2] | `boss_nightmare_2` | EmitProjectilePattern | PeriodicTimer · period_sec 0.1 | damage 2 · pattern_id nightmare_missile | U10 — 명세 'Pattern_NightmareMissile' damage 2 → 효과 damage |

## enemy `dragon` — Assets/_Project/Data/Enemies/Enemy_Dragon.asset


| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 · 메모 |
|---|---|---|---|---|---|---|
| 0 | mechanics[0] | `dragon_0` | AreaBreath | AttackN · period 3 | damage 50 · range_tiles 3 · cone_half_deg 50 |  |

## enemy `slime` — Assets/_Project/Data/Enemies/Enemy_Slime.asset

- split_unit = slime_mid · split_count = 2
- mechanics[0] SplitOnDeath → 적 고유 값

## enemy `slime_mid` — Assets/_Project/Data/Enemies/Enemy_Slime_Mid.asset

- split_unit = slime_small · split_count = 2
- mechanics[0] SplitOnDeath → 적 고유 값
