# Artillery Defender — 곡사포 유닛 (후속)

> 상태: 대기 (선행 spec `projectile-trajectory-payload` 완료 후 착수) · 2026-07-06

## 목표

`projectile-trajectory-payload` 리팩터가 깔아둔 **BallisticArc 궤적 + TileAoe 페이로드** 를 실제 authored 곡사포 유닛으로 만든다. 이 spec 은 **콘텐츠/저작만** 담당 — 엔진 능력은 선행 spec 이 이미 제공한다.

## 전제 (선행 spec 산출물)

- `ProjectileData` 에 `flightMode`/`arcHeight`/`impactTileRange` 필드 존재
- MoveSystem `BallisticArc` arm + ImpactSystem `TileAoe` payload arm 동작
- AttackSystem RESOLVE 가 `flightMode` 로 궤적/페이로드/셀고정 배선
- 데미지 = `DefenderUnitData.outputs` Damage 합산

## 예상 작업 단위 (선행 완료 시 확정)

| # | 작업 | 목적 |
|---|---|---|
| 0 | ProjectileData(ballistic) 에셋 | flightMode=BallisticArc, arcHeight, impactTileRange 세팅 |
| 1 | DefenderUnitData 곡사포 SO | outputs=Damage, attackRange 길게, attackCooldown 느리게, projectile 참조 |
| 2 | 프리팹 + 아이콘 | 셸 뷰(ProjectileViewPool 재사용) + 유닛 아이콘(maxTextureSize 256) |
| 3 | draft 편입 + Play 검증 | 실매치 발사→arc 비행→비행중 타겟 사망 시 셀 낙하→반경 AOE |

## 검증 질문

> 실매치에서 곡사포가 **발사 시점에 고정된 타일** 로 포탄을 곡사 발사하고, 비행 중 타겟이 죽거나 이동해도 그 타일에 착탄해 **지정 반경의 적(악몽) 전원** 에게 데미지를 넣는가?

## 후속 후보 (이 유닛 spec 밖)

- **slow-곡사포** [M] · 착탄 시 Damage + ApplyStat:Slow AOE. 선행 spec 의 "non-Damage payload" 확장 의존.
- **임팩트 knockback** [S] · `DefenderCcData` 를 AOE 대상에 적용.
- **arcHeight 거리 비례** [S] · 먼 표적일수록 높은 포물선.
- **per-target HitFlash** [S] · AOE 대상 피격 플래시(현재 Meteor 선례로 미적용).
