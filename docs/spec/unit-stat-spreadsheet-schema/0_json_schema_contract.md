# 0. JSON Schema Contract

## 목적

기획파트가 관리하는 Defender/Enemy 스프레드시트 → REST API → JSON 변환 결과물이 어떤 형태여야 하는지 확정한다. 이 JSON을 소비하는 Unity Editor 임포터는 후속 유닛에서 구현한다.

## 변경 대상

문서만 (코드 변경 없음). 이 계약을 실제로 반영하려면 후속 유닛에서 `Assets/_Project/Scripts/Data/AttackUnitData.cs`(id 필드 추가)와 신규 Editor 임포터 스크립트를 건드리게 된다.

## 구현 (스키마 정의)

### 최상위 구조

```json
{
  "defenders": [ { "id": "archer", "health": 50, ... } ],
  "enemies": [ { "id": "basic", "health": 100, ... } ]
}
```

### `defenders[]` — `DefenderUnitData` 매핑

| JSON 키 | 타입 | SO 필드 | 비고 |
|---|---|---|---|
| `id` | string | `id` | 매칭키 (기존 필드) |
| `displayName` | string | `displayName` | 참조용 |
| `role` | string (enum) | `role` | `None/Ranger/Guardian/Fighter/Caster/Support` |
| `rarity` | string (enum) | `rarity` | `Common/Rare/Epic/Ego` |
| `health` | number | `health` | |
| `attackRange` | number | `attackRange` | |
| `attackDamage` | number | `attackDamage` | 레거시 스칼라 (README 참고) |
| `attackCooldown` | number | `attackCooldown` | |
| `hitDelaySec` | number | `hitDelaySec` | |
| `deployDelaySec` | number | `deployDelaySec` | |
| `attackTargetCount` | integer | `attackTargetCount` | |
| `cost` | integer | `cost` | |
| `aggroCapacity` | integer | `aggroCapacity` | |
| `aggroRange` | number | `aggroRange` | |

제외: `visualMesh`/`visualMaterial`/`projectile`/Spine 전체/VFX prefab, `outputs`, `onPlaceEffect` 계열 4개, `targetAllies`, `hazardCast` 계열 8개, `knockback` 계열 2개, `onPlacePush` 계열 3개, `castAnchor*`/`deploymentDuration`/`placementSkillDelay`.

### `enemies[]` — `AttackUnitData` 매핑

| JSON 키 | 타입 | SO 필드 | 비고 |
|---|---|---|---|
| `id` | string | `id` (**신규 추가 필요**) | 매칭키 |
| `displayName` | string | `displayName` | 참조용 |
| `enemyClass` | string (enum) | `enemyClass` | `None/Tanker/Runner/Bruiser/Shooter` |
| `attackMethod` | string (enum) | `attackMethod` | `None/Melee/Projectile` |
| `targetMode` | string (enum) | `targetMode` | `None/Nearest/FocusUntilDead` |
| `engageMovement` | string (enum) | `engageMovement` | `Halt/Advance/Pulse` |
| `targetPriorityClass` | string (enum) | `targetPriorityClass` | `DefenderClass` 값 |
| `targetClassMask` | string[] (enum 배열) | `targetClassMask` | 비트마스크 → 배열. `[]`=None, `["Everything"]`=전체 |
| `health` | number | `health` | |
| `moveSpeed` | number | `moveSpeed` | |
| `attackDamage` | number | `attackDamage` | 레거시 스칼라 (README 참고) |
| `attackRange` | number | `attackRange` | |
| `attackCooldown` | number | `attackCooldown` | |
| `attackTargetCount` | integer | `attackTargetCount` | |
| `hitDelaySec` | number | `hitDelaySec` | |
| `aggroAttackDamage` | number | `aggroAttackDamage` | |
| `aggroAttackCooldown` | number | `aggroAttackCooldown` | |
| `aggroAttackRange` | number | `aggroAttackRange` | |

제외: `visualMesh`/`visualMaterial`/`projectile`/Spine 전체/`visualOffset`, `outputs`.

### 공통 컨벤션

- Enum은 C# 멤버명 문자열 그대로 (서수 아님).
- 매칭은 `id` 기준 업데이트만 (upsert 없음, 미매칭 id는 무시).
- 빈 셀 → JSON 키 생략 → 임포트 시 기존 SO 값 유지 (부분 갱신).
- 스키마 버전 필드는 1차 범위에 포함하지 않음.

## 완료 기준

- [x] 사용자와 브레인스토밍 세션에서 섹션별 확인 완료 (2026-07-02)
- [ ] (후속 유닛) `AttackUnitData.id` 필드 추가 후 compile 확인
- [ ] (후속 유닛) 임포터 구현 후 실제 스프레드시트 1건 왕복 테스트

이 유닛은 문서 계약 확정만을 완료 기준으로 삼는다. 코드/컴파일/Play 검증은 후속 유닛(임포터 구현)의 완료 기준이다.
