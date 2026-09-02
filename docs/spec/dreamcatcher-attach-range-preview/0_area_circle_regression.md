# 0 — 스킬 광역 사각 → 원 (마지막 격자 잔존 정리)

> 사용자 결정 2026-09-02 Q2/Q2-b/Q8: 「타일 기반은 지운다, N 거리 이내 원으로」. 반경 = `N + 0.5`.
> `Chebyshev` arm 은 **기능은 남기고 비활성화**. 범위 = `RangeMetric` 소비처만.
> 이 spec 에서 **유일하게 sim 을 바꾸는 단위**다. 이후 unit 은 sim 무변.

## 목적

`distance-based-range` 결정 4 가 남긴 「같은 N 인데 사거리는 원, 스킬 광역은 사각」 공존을 닫는다.
이 spec 이 그릴 도형이 원 하나가 되어 표기 어휘가 하나(SDF 링, `_HalfExtent = 0`)로 접힌다.
**판정을 바꾸는 커밋에서 표기도 함께 바꾼다** — 액티브 셀 조준·텔레그래프가 사각을 그리는 동안
판정만 원이면 화면이 거짓말한다(unit 5 교훈 「4만 하고 멈추면 거짓말 구간이 생긴다」).

## 변경 대상

- `Scripts/Skills/ISkillContext.cs:35` — `RangeMetric { Chebyshev = 0, Euclidean = 1 }` 에
  **`AreaCircle = 2`** 추가. `Chebyshev` 는 남기고 주석으로 dormant 선언(소비처 0).
- `Scripts/Battle/Skills/EcsSkillContext.cs:450~468` — `AreaCircle` arm 신설:
  `InBodyReach((p−center)/t, tileRange, CellHalfWidthTiles, targetR)`. `Chebyshev` arm 본문은 그대로.
- concrete 10곳 → `RangeMetric.AreaCircle`: `AreaSleepSkill:46` · `AreaCcSkill:34` · `AreaDotSkill:32`
  · `AreaStackSkill:33` · `AreaTauntSkill:42` · `StatAuraSkill:60,65` · `GrantShieldSkill:74`
  · `TileStatBurstSkill:26`(액티브 칸 조준 — 조준 입력은 칸, 범위는 조준 셀 중심 원)
  · `ConeBreathSkill:52`(콘 사전필터 — 콘 판정 `SkillCone.IsInCone` 은 무변).
  ⚠ `EmitPatternSkill:91` 은 `Euclidean` **유지** — 탄 비행 거리라 칸 반폭을 더하면 사거리 밖 후보를
  고르고 탄이 도중 소멸한다(그 파일 주석).
- `Tests/EditMode/TestSkillContext.cs:167~176` — 페이크에 `AreaCircle` arm 추가(`Euclidean` 과
  같은 식에 `+ 0.5`). 페이크와 어댑터가 갈리면 도메인 테스트가 초록인데 라이브가 다르다.
- **표기** `Bridge/BattleBridge.cs:8122 PinCenteredRange` — `squareShape: true` 를 버리고
  `TilemapMapView.SetAreaRange(center, tileRange + 0.5f)`(신설, 아래) 로. 링 = 원 가장자리.
- `Core/TilemapMapView.cs` — **`SetAreaRange(Vector2 centerTiles, float radiusTiles, Color? color = null)`**
  신설: 타일을 칠하지 않고 `ShowRangeRing` 만 띄운다(표준 상대 항 없음 = 정확한 가장자리).
  `ClearPlacementRange` 가 수명을 공유(기존 규칙). `squareShape` 경로는 dormant — **경계 가드 1줄**
  (`squareShape && max(|dx|,|dz|) > ceil(tileRange)` 스킵)만 얹는다(Q7). unit 2 가 `color` 를 쓴다.
- `Tests/Golden/*.trace.txt` 8건 + `docs/spec/battle-sim-extraction/golden-corpus.md` — 재베이크.

## 구현

- **식 하나.** 광역 원 = 투사체 TileAoe 착탄(`ProjectileHitSystem:747`)과 같은
  `InBodyReach(d, N, 0.5, targetR)`. 자기 자리 폭발(`SelfAreaBlastSkill`)은 이미 이 식이라 무변.
- **Chebyshev 는 지우지 않는다.** enum 값·`EcsSkillContext` arm·`SkillMath.BodyOverlapsSquare`·
  `AttackReachTests` 의 사각 단언·`TilemapMapView.squareShape` 전부 존치. 대신 **dormant 가드
  테스트** 1건: `Skills/Concrete/*.cs` 에 `RangeMetric.Chebyshev` 문자열 0건(`facingLookupRetired`
  관용구 — 되살릴 때 의식적으로 테스트를 고치게 한다).
- **골든은 격리 커밋.** 코드 커밋 → Play 중 `Wassup/Battle/Sim Harness/Regenerate Golden Corpus`
  → 골든만 담은 커밋. 무관 dirty(머티리얼·Spine Examples)가 기준선에 구워지지 않게 스테이징은
  경로 명시. 2회 실행 일치로 결정론 확인. diff 귀속: 스킬 광역 사건이 있는 시나리오만 움직여야 한다.
- **`SetAreaRange` 는 `_rangeRing` 재사용.** 새 렌더러·정렬 상수 없음(`RangeRingOrder = -8` 그대로).
  `CellToLocalInterpolated + local.z = −PropGroundLift` 관용구 유지(`GetCellCenterLocal` 금지 —
  0.5 유닛 뜬 사고). 색은 `color ?? _tileSet.rangeColor`, `ApplyRingTint` 가 매 프레임 적용하므로
  오버라이드 슬롯은 `ClearPlacementRange` 에서 리셋.
- 액티브 조준의 `includeCenter`·타일 채움은 사라진다 — `IsPlacementRangeCell` 소비처는 배치 경로만
  (`BattleBridge:7451` 경유, 액티브는 안 씀 — 확인 완료).

## 완료 기준

- [ ] `RangeMetric.Chebyshev` 소비처 0(concrete grep) · dormant 가드 테스트 초록.
- [ ] `AreaSleepSkillTests`·`StatAuraSkillTests`·`GrantShieldSkillTests`·`SkillLayerEndpointTests` 초록
      (대각 N=1 후보 포함 단언이 있으면 유지돼야 함 — `N + 0.5` 의 존재 이유).
- [ ] 페이크·어댑터 동치 단언: 같은 후보 배치에서 `TestSkillContext` 와 `EcsSkillContext` 의
      `AreaCircle` 결과 집합 일치(EditMode, 기존 parity 테스트 위치).
- [ ] 골든 8건 재베이크 + 2회 일치 + diff 귀속 기록(어느 시나리오가 왜 움직였나).
- [ ] Play: 액티브 카드 셀 조준이 **원 링**으로 뜨고, 발동 시 걸린 적이 링 안(가장자리 걸침 포함).
- [ ] EditMode 코어·에셋 lane 전건 초록(선행 실패 2건 `bomb_man`·`boomerang` 문안 제외).
