# 3 — Card_SlowAwakening → PlacementAura

## 목적

느린 각성 카드를 SelfWarmupBuff(no-op) 에서 PlacementAura 오라로 교체.

## 변경 대상
- `Assets/_Project/Data/Dreamcatcher/Card_SlowAwakening.asset`

## 구현 (필드)
| 필드 | 이전 | 이후 |
|---|---|---|
| `type` | Unit(1) | 유지 Unit(1) |
| `binding` | Unit(1) | 유지 Unit(1) |
| `mechanics[0].payload.kind` | SelfWarmupBuff(5) | **PlacementAura(6)** |
| `mechanics[0].payload.magnitude` | 50 | 유지 50 (공속 %) |
| `mechanics[0].payload.duration` | 2 | 유지 2 (warmup 초) |
| `mechanics[0].trigger.kind` | None(0) | 유지 None(0) |
| `axis` | All(3) | 유지 All(3) — 전 유닛 대상 오라 |
| `category` | Subconscious(2) | 유지 (무의식 프레임) |
| `description` | (부착 유닛…) | **"부착한 유닛 생존 중, 새로 배치되는 유닛이 2초 뒤 깨어나 공속 +50%"** |

## 완료 기준
- [ ] payload kind=6 반영. grep/read 로 확인.
- [ ] 덱빌더 팝업: 무의식 보랏빛 프레임 + `UNIT` 헤더 + 갱신된 description.
- [ ] `DreamcatcherCatalogSyncTests` 그린(등록 유지).
