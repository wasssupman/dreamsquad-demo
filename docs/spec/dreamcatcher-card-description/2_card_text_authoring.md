# 2 — 카드 description 텍스트 authoring

## 목적

카드 에셋의 `description` 필드에 설명 텍스트를 기입한다. 팝업에 노출되는 **카탈로그 16장**
(Squad 11 + Unit 5)이 필수, Active 6장은 provisional(팝업 미노출, 손패 peek 후속용).

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_*.asset` (16장) — 필수
- `Assets/_Project/Data/Dreamcatcher/Active_*.asset` (6장) — provisional

## 문안 (초안 — 사용자 검토)

### Squad (11) — 수치는 자동 라인이 표시. description = 대상/플레이버 보완
| 에셋 | description |
|---|---|
| Card_RangerAtk10 | 레인저 계열 아군의 공격력을 강화한다. |
| Card_RangerAs10 | 레인저 계열 아군의 공격 속도를 끌어올린다. |
| Card_RangerHp12 | 레인저 계열 아군의 생존력을 높인다. |
| Card_GuardianAs8 | 가디언 계열 아군의 공격 속도를 높인다. |
| Card_GuardianHp15 | 가디언 계열 아군의 체력을 늘려 전선을 지탱한다. |
| Card_GuardianFortress | 요새처럼 버틴다. 체력이 크게 늘지만 공격 속도는 절반으로 떨어진다. |
| Card_Cost1As5 | 1코스트 유닛의 공격 속도를 높인다. |
| Card_Cost1Hp10 | 1코스트 유닛의 체력을 높인다. |
| Card_AllAtk8 | 모든 아군의 공격력을 강화한다. |
| Card_AllMove10 | 모든 아군의 이동 속도를 높인다. |
| Card_SlowAwakening | 배치 후 2초간 잠들어 있다가 깨어나 폭발적인 공격 속도를 얻는다. |

### Unit (5) — effects 없음. description 이 유일한 설명 (현재 빈칸 해소 대상)
> 사용자 지시: 메커니즘에 써둔 내용 그대로(수치 포함).
| 에셋 | description | 메커니즘 근거 |
|---|---|---|
| Card_PokeNeedle | 5회 공격마다 대상에 투사체 20 데미지 | AttackN(5) × ProjectileToTarget(20) |
| Card_BouncyBead | 상시: 공격 투사체가 3타일 내 2회 튕김 (감쇠 없음) | ProjectileBounce count2/tile3/mul1 |
| Card_Thornmail | 5회 피격 시 다음 공격 2연발 | OnDamagedN(5) × NextAttackDoubleFire |
| Card_Farewell | 사망 시 2타일 범위 100 폭발 | OnDeath × SelfTileAoe(100, tile2) |
| Card_LastFlame | 즉발 공속버프(+90%, 5초) + 5초 후 자폭 | SelfBuffLethal(mag90→+90% AS, dur5) |

### Active (6) — provisional (팝업 미노출, 손패 peek 후속에서 확정)
| 에셋 | description (초안) |
|---|---|
| Active_Meteor | 지정한 지역에 운석을 떨어뜨려 광역 피해를 준다. |
| Active_Portal | 포탈을 열어 적을 경로 뒤쪽으로 되돌린다. |
| Active_PowerSurge | 아군의 공격력을 일시적으로 크게 끌어올린다. |
| Active_RapidFire | 아군의 공격 속도를 폭발적으로 높인다. |
| Active_SlowField | 지정 지역에 감속 장판을 만들어 적을 느리게 한다. |
| Active_Tornado | 회오리를 일으켜 적을 끌어당기며 피해를 준다. |

> Active 문안은 실제 `SkillData` 효과값과 대조 후 확정. 이번 spec 필수 아님.

## 구현

- 각 `.asset` 의 MonoBehaviour 블록 끝(`skill:` 뒤)에 `description:` 키를 추가.
  유닛 0 이 필드를 추가하면 Unity 가 재직렬화 시 자동으로 키를 쓰지만, 값 authoring 은
  텍스트 편집 또는 `manage_scriptable_object` 로 일괄 기입.
- 여러 줄이 필요하면 YAML 블록 스칼라 사용. 현재 문안은 1줄이라 인라인 문자열로 충분.

## 완료 기준

- [ ] 16장 카탈로그 카드 전부 `description` 채워짐.
- [ ] 덱빌더에서 Unit 카드 탭 시 본문에 설명이 보인다(빈칸 해소).
- [ ] 에셋 재직렬화 후 다른 필드 손상 없음(`DreamcatcherCatalogSyncTests` 그린 유지).
