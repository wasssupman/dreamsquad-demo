# 18 — 드림캐쳐 전수조사: 전투 판정의 격자 양자화 전폐

> **사용자 지시 2026-09-01**: 「드림캐쳐 전수조사 후 타일 기반을 없애라. 전투는 모두 거리 기반이다.」

## 전수조사 결과 (카드 47종 · 페이로드 33종)

**unit 14 잔여 해소(2fa3d149, 리뷰 M3 기록)**: 도형 스킬 사각의 중심을
`CellOfPosition` 스냅에서 **받은 center 그대로**로 — 칸 조준 concrete 는 byte-identical,
자기시전(대다수 광역이 hostPos 를 넘김)은 몸 중심 정중앙이 되고 이동 캐스터의 도형이
칸 경계에서 튀지 않는다. 골든 델타 1건(광역 CC 경계 이동)이 이 변경의 귀속.

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
- **셀 고정 착탄점**(아틸러리 `BallisticArcToPoint`·에미터 `BindingClass.Cell`) — 착탄 **예고가
  칸에 고정**되는 조준 저작(예고가 움직이면 안 된다는 기존 설계). 피해자 판정은 그 점 기준
  연속(d 항). 리뷰 M1 이 존치 목록 누락을 잡아 추가
- **해저드 존 틱** — unit 19 (구조 재설계라 분리)
- 겸직 tileRange 2장(살찐 제물 30=−30% · 광란 10=잔존값) — 공간 아님(후속 후보의 겸직 해소 항목)

## 결정론 재정의 (a 의 대가)

row-major **셀 키** rank 는 격자 없이는 정의되지 않는다 → **`SimEntityId` 오름차순 rank** 로
교체(구조적 결정론 선호 — 시드가 아니라 index). Nearest 동률도 낮은 simId. 골든이 움직인다
(패턴 발사 시나리오) — 재베이크 + 귀속.

## 완료 기준

- [x] 위 6곳의 판정 경로에서 격자 어휘 소멸(fan 시차·조준 저작 등 존치분은 사유 기록).
- [x] `PatternScope.FilterByReach`/`PatternTargeting.Select`(simId) 테스트 재작성 초록.
- [x] EditMode 2669건 전건 초록(선행 2건 제외) · **골든 8건 바이트 무변** — 이 시드들에선
      셀 양자화가 승자를 바꾼 적이 없었다는 실측. simId 결정론도 결과 동일.

### 진행 기록 — 2026-09-01 (d706a096)

- Burst 함정 5번째 재발을 **내가 또 밟았다**: 신규 lookup 3개를 로컬 형태로 추가 →
  MovementSystem NRE 로 이동 계열 전멸. MovementSystem 자기 주석이 이미 「로컬 형태
  금지·필드로」라고 경고하고 있었다. 셋 다 필드 형태(OnCreate + Update)로 교정 후 초록.
  교훈 확장: 지우는 것도 더하는 것도 로컬 형태는 건드리지 말 것 — 신규는 항상 필드.
