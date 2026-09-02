# 18 — 드림캐쳐 전수조사: 전투 판정의 격자 양자화 전폐

> **사용자 지시 2026-09-01**: 「드림캐쳐 전수조사 후 타일 기반을 없애라. 전투는 모두 거리 기반이다.」

## 전수조사 결과 (카드 47종 · 페이로드 33종)

**이미 거리 기반(unit 12~14)**: 스킬 광역 멤버십 전부(AreaCc/Sleep/Dot/Stack/Taunt/Breath/
StatAura/GrantShield/TileStatBurst — 몸 걸침) · 사거리 · 투사체 splash/스윕.

**격자 잔존 = 이 unit 이 바꾸는 것 6곳**:

| # | 어디 | 종전 자 | 신규 자 |
|---|---|---|---|
| a | 에미터 탄별 대상(불나방떼·융단폭격 풀·배치 volley) — `PatternScope`/`PatternTargeting` | 후보를 **셀로 접어** 체비셰프 스코프 + row-major 셀 rank | 위치+몸(사거리 자 `InBodyReach`) 스코프 · 거리² 최근접 · **simId rank** 결정론 |
| b | 자장가 「내가 때릴 대상 제외」(AreaSleep 부속) | 셀 체비셰프 × `RangeToTiles` | `InBodyReach`(연속 사거리 + 양쪽 몸) — `UnitStat.BodyRadius` 포트 신설 |
| c | 회오리 당김 멤버십(MovementSystem) | `TileAoe.IsInTileRange`(정사각) | 원 + 몸 — 「화면은 원인데 판정은 사각」 마지막 1곳 |
| d | 투사체 광역 착탄 피해자(ProjectileHitSystem) | 피해자 **셀** vs 착탄 셀(unit 4b 원이지만 셀 양자화) | 피해자 **위치** vs 착탄점 |
| e | 버프장 멤버십(AllyBuffFieldSystem) | 셀 vs 셀 `IsInRadius` | 위치 + 몸 |
| f | 통통구슬 재조준(BounceRetarget) | 후보 셀 `IsInRadius` | 후보 위치 + 몸 |

**존치 (격자가 정당한 곳 — 사유 기록)**:
- **조준 입력**(액티브 칸 찍기) — 저작이 칸 단위(외부 확정: 격자는 배치/저작에서 산다)
- **fan 시차 슬롯**(융단폭격 「같은 칸 = 같은 착탄 순간」) — 멤버십이 아니라 착탄 리듬 저작 단위
- **어그로 타일 필드** — 설계 어휘가 타일 필드(이동 유도 축, aggro-tile-chase 재설계)
- **보스 블링크/궁극기 착지 셀** — 지형(어느 칸에 서나) 축
- **브리지 표기 2곳**(프리뷰 영향 수 등) — 표기 관용
- **해저드 존 틱** — unit 19 (구조 재설계라 분리)
- 겸직 tileRange 2장(살찐 제물 30=−30% · 광란 10=잔존값) — 공간 아님(후속 후보의 겸직 해소 항목)

## 결정론 재정의 (a 의 대가)

row-major **셀 키** rank 는 격자 없이는 정의되지 않는다 → **`SimEntityId` 오름차순 rank** 로
교체(구조적 결정론 선호 — 시드가 아니라 index). Nearest 동률도 낮은 simId. 골든이 움직인다
(패턴 발사 시나리오) — 재베이크 + 귀속.

## 완료 기준

- [ ] 위 6곳 격자 어휘 참조 0 (grep: 해당 파일의 WorldToCell/IsInTileRange/IsInRadius/ChebyshevDistance)
- [ ] `PatternScope`/`PatternTargeting` 테스트가 연속 자로 재작성되어 초록
- [ ] EditMode 전건 초록(선행 2건 제외) · 골든 재베이크 + simId 결정론 귀속
