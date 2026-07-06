# Artillery Defender — 곡사포 유닛

> 상태: **완료 2026-07-06** — units 0~3, Play 검증 OK. handoff: `3_handoff_summary.md`. 선행 `projectile-trajectory-payload` 엔진(`e5836bc`~`b84f2da`)의 첫 실증. 콘텐츠/저작만(엔진은 런타임 활성이었음).

## 목표

엔진이 깔아둔 곡사 경로(BallisticArc + TileAoe)를 **독립 로스터 유닛**으로 만든다. `flightMode=BallisticToCell` ProjectileData + DefenderUnitData + DefenderCatalog 등록. 새 유닛이므로 기존 Cannon 등 무변경.

## 검증 질문

> 실매치에서 곡사포가 **발사 시점 고정 타일**로 돌덩이를 포물선 발사하고, 비행 중 타겟이 죽거나 이동해도 그 타일에 착탄해 **반경 내 적 전원**에게 데미지를 넣는가? (= projectile-trajectory-payload 리팩터의 첫 실증)

## 확정 결정

- **새 유닛** (Cannon repurpose 아님). Cannon 임시 곡사화는 원복 완료.
- **투사체 비주얼 = Rock**(날아가는 바위, 공성 느낌). GA `Projectile_Rock_GA` 의 prefab/hit/cast VFX 재사용.
- **몸체 = Cannon Spine 재사용**(전용 rig은 후속). 아이콘 필드 없음(드래프트는 Spine/이름 표시) → 아이콘 저작 불필요.
- **데미지 = outputs(Damage)** — 엔진이 합산해 flat AOE. 새 필드 없음.

## 작업 단위

| # | 작업 | 내용 | 상태 |
|---|---|---|---|
| 0 | 에셋 | `Projectile_ArtilleryShell` — Rock 비주얼 + flightMode=BallisticToCell, arcHeight 3.5, impactTileRange 2, speed 6, minFlightTime 0.3 | ✅ |
| 1 | 에셋 | `Defender_Artillery` — id/name/스탯 + outputs=Damage + projectile 참조 + Cannon Spine/mat/placementVfx | ✅ |
| 2 | 등록 | `DefenderCatalog.units` 추가 + 프로필 보유(테스트) | ✅ |
| 3 | 검증 | 실매치 arc→셀낙하→반경 AOE. Play 육안 OK. → `3_handoff_summary.md` | ✅ |

> 저작은 코드가 아닌 에셋(execute_code 로 SO 생성) — 상세/스탯은 본 README + handoff 가 source of truth. 별도 번호 문서 없음.

## 제안 스탯 (Cannon 기반, 곡사포답게)

| 필드 | 값 | 근거 |
|---|---|---|
| attackRange | 7 | 장거리 후방 |
| attackCooldown | 3.5 | 느린 발사 |
| health | 350 | 약한 후방 |
| cost | 5 | 느림+AOE 보상 |
| damage(output) | 60 | 느림+AOE 단발 보상 |
| arcHeight / impactTileRange | 3.5 / 2 | 포물선 높이 / 5×5 AOE |

Play에서 육안 튜닝(unit 3).

## 후속 후보

- slow-곡사포(착탄 slow) · 임팩트 knockback · arcHeight 거리비례 · 전용 Spine rig · 전용 아이콘(아이콘 시스템 도입 시).
