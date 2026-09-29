# skill-data-table unit 8 — 상시 효과 이전 dry-run 표(2부)

> 생성: 헤드리스 하네스 `tools/skill-data-table/dry_run_part2.sh`(워크트리 에셋 YAML 추출 → 실제 소스 AlwaysOnEffectMigration.Build/Report 컴파일 실행 · Unity 없음 · 에셋 경로 순) · 기준 커밋 33a8f3a58 · Unity 메뉴 dry-run 과 같은 함수

카드 54 장 검사 · 옮기는 카드 16 · 새 효과 줄 18(병합 0 — U13) · 배치 오라 수혜 대상 쓰기 1 · 깃발 0

소유 줄 = 트리거 `None`(보유 시작 순간부터) · 게이트 없음 · fire_cap 0 · 카드의 다음 빈 slot. 효과 에셋 = `Assets/_Project/Data/Effects/Effect_{effect_id}.asset`. 깃발 = 손실 · 해시 변화 · 확인 필요 · 충돌.

## 새 효과 줄 + 카드 소유 줄

| 카드 | 옛 항목 | 새 effect_id | kind | 값 | slot | 깃발 |
|---|---|---|---|---|---|---|
| `all_atk` | effects[0] — AttackDamage +8% · 카드 axis All | `all_atk` | FactionStatBuff | buff_stat AttackDamage · percent 8 · ally_filter All | 0 |  |
| `all_move` | effects[0] — MoveSpeed +10% · 카드 axis All | `all_move` | FactionStatBuff | buff_stat MoveSpeed · percent 10 · ally_filter All | 0 |  |
| `bouncy_bead` | attackMods[0] — ProjectileBounce count 2 · tileRange 3 · damageMul 1 | `bouncy_bead` | ProjectileBounce | count 2 · range_tiles 3 · mul 1 | 0 |  |
| `cost1_as` | effects[0] — AttackSpeed +5% · 카드 axis Cost1 | `cost1_as` | FactionStatBuff | buff_stat AttackSpeed · percent 5 · ally_filter Cost1 | 0 |  |
| `cost1_hp` | effects[0] — EffectiveHealth +10% · 카드 axis Cost1 | `cost1_hp` | FactionStatBuff | buff_stat EffectiveHealth · percent 10 · ally_filter Cost1 | 0 |  |
| `cracked_grail` | effects[0] — AttackDamage +70% · 카드 axis All | `cracked_grail_0` | FactionStatBuff | buff_stat AttackDamage · percent 70 · ally_filter All | 0 |  |
| `cracked_grail` | effects[1] — EffectiveHealth -40% · 카드 axis All | `cracked_grail_1` | FactionStatBuff | buff_stat EffectiveHealth · percent -40 · ally_filter All | 1 |  |
| `eye_on_the_end` | attackMods[0] — FrontmostTarget count 0 · tileRange 0 · damageMul 1.2 | `eye_on_the_end` | FrontmostTarget | mul 1.2 | 0 |  |
| `guardian_as` | effects[0] — AttackSpeed +8% · 카드 axis ClassGuardian | `guardian_as` | FactionStatBuff | buff_stat AttackSpeed · percent 8 · ally_filter ClassGuardian | 0 |  |
| `guardian_fortress` | effects[0] — EffectiveHealth +50% · 카드 axis ClassGuardian | `guardian_fortress_0` | FactionStatBuff | buff_stat EffectiveHealth · percent 50 · ally_filter ClassGuardian | 0 |  |
| `guardian_fortress` | effects[1] — AttackSpeed -50% · 카드 axis ClassGuardian | `guardian_fortress_1` | FactionStatBuff | buff_stat AttackSpeed · percent -50 · ally_filter ClassGuardian | 1 |  |
| `guardian_hp` | effects[0] — EffectiveHealth +15% · 카드 axis ClassGuardian | `guardian_hp` | FactionStatBuff | buff_stat EffectiveHealth · percent 15 · ally_filter ClassGuardian | 0 |  |
| `sub_incubus_pact` | effects[0] — AttackDamage +25% · 카드 axis All | `sub_incubus_pact` | FactionStatBuff | buff_stat AttackDamage · percent 25 · ally_filter All | 0 |  |
| `nightmare_hunt` | attackMods[0] — DamageVsSleeping count 0 · tileRange 0 · damageMul 2 | `nightmare_hunt` | DamageVsSleeping | mul 2 | 0 |  |
| `ranger_as` | effects[0] — AttackSpeed +10% · 카드 axis ClassRanger | `ranger_as` | FactionStatBuff | buff_stat AttackSpeed · percent 10 · ally_filter ClassRanger | 0 |  |
| `ranger_atk` | effects[0] — AttackDamage +10% · 카드 axis ClassRanger | `ranger_atk` | FactionStatBuff | buff_stat AttackDamage · percent 10 · ally_filter ClassRanger | 0 |  |
| `ranger_hp` | effects[0] — EffectiveHealth +12% · 카드 axis ClassRanger | `ranger_hp` | FactionStatBuff | buff_stat EffectiveHealth · percent 12 · ally_filter ClassRanger | 0 |  |
| `shatter_hymn` | effects[0] — DamageVsCc +50% · 카드 axis All | `shatter_hymn` | FactionStatBuff | buff_stat DamageVsCc · percent 50 · ally_filter All | 0 |  |

## 배치 오라 효과의 수혜 대상(`ally_filter`)

| 카드 | 효과 id | 지금 | 쓸 값 | 깃발 |
|---|---|---|---|---|
| `slow_awakening` | `slow_awakening` | ClassRanger(기본값 = 카드 축으로 폴백) | All |  |

## 메모

없음
