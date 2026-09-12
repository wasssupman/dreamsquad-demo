# 6 — 배치 프리뷰 공격 가이드 (사용자 요청 2026-09-12)

## 목적

배치 중 사거리(원) 안에 적이 있으면, **sim 과 같은 타겟 규칙**으로 고른 최종 타겟 방향으로 **공격 도형 가이드**
(부채꼴)를 바닥에 그린다. 「이 자리에 놓으면 지금 이 방향으로 휘두른다 — 이 부채꼴 안이 같이 맞는다」를 배치 전에 보여준다.
정적 가이드(절반의 시간 거짓말)가 아니라 **라이브 타겟을 매 프레임 따라가는** 가이드라 계약 9(표기 = 판정)를 지킨다.

## 규칙

- **대상 유닛**: `attackShape` 가 Omni 가 아닌 유닛만(오늘 브루저·말파이트). Omni 는 가이드 없음(원 링이 전부).
- **타겟 선정**: 프리뷰 후보(마스크·통행 층·`InReach` 원 — `RefreshRangeTargetMarks` 가 이미 모으는 집합) 중
  **최근접**, 동거리는 **낮은 `simId`**(`AttackSystem:615` 와 같은 tie-break). 락·frontmost·어그로는 배치 전이라 없다.
- **방향**: 베이스(발밑) → 타겟 sim 위치. 가이드 원점 = 베이스, 반경 = 사거리 + 내 몸(링과 같은 값), 각 = `angleDeg`.
- **없으면 숨김**: 사거리 안 적이 없으면 가이드를 그리지 않는다(기본 방향 없음 — 「방향은 타겟이 정한다」).
- **마크는 무변**: 마크는 「때릴 수 있는 적」(원)이고 가이드는 「이번 휘두르기에 같이 맞는 범위」다 — 두 표기의 뜻이
  다르므로 마크를 도형으로 자르지 않는다.
- **Rect(띠)**: 2026-09-12 사용자 요청(「이쑤시개를 rect 형으로」)으로 **띠 가이드도 구현** — `SetShapeGuide(band: true, halfWidthTiles)`,
  `BuildBand/BuildBandOutline`(sim `BandGate` 상자와 같은 도형: 길이 = 사거리 + 내 몸, 좌우 반폭). 라이브 검증 `rect_d.png`.

## 변경 대상

- `Core/TilemapMapView.cs` — `SetShapeGuide(centerTiles, dirTiles, radiusTiles, angleDeg)` / `ClearShapeGuide()`.
  **부채꼴 메시**(채움 fan + 테 띠, 24분할)를 grid 자식으로 눕힌 `MeshRenderer` 2개 — 링과 같은 정렬 대역(`RangeRingOrder`)·
  높이(`PropGroundLift`). **사용자 지시(2026-09-12)로 색·정렬 변경**: 색 = 마크와 같은 `rangeTargetMarkColor`(빨강 —
  링과 같은 라임이면 원과 한 덩어리로 읽혀 방향이 죽는다), 채움 α0.22 · 테 α0.85 · 정렬 `PlacementShapeGuideOrder(−7)` =
  링(−8)·범위 타일(−12) **위**, 마크(−6 으로 상향) 아래. 머티리얼은 `RuntimeMaterialFactory.CreateTransparent`.
  자가 리뷰(2026-09-12)로 바꾼 것: ① 참격 텍스처(반각 30° 고정) 재사용 → **메시**(각·반경이 인자라 저작 각도가 바뀌어도 참말,
  「60° 아니면 경고」 분기 자체가 사라짐 — 불필요한 확장 제거) ② 색을 참격 주황이 아니라 **링 색**으로 — 「예고」와 「타격」이
  색으로 갈린다(비주얼 비대칭 제거) ③ 바깥 호 = 링 원과 정확히 겹친다(반경 동일).
- `Bridge/BattleBridge.cs` `RefreshRangeTargetMarks` — 후보 루프에서 `NearestTargeting.RanksBefore`(sim 과 **같은 함수**)로
  최근접 추적 → 도형 유닛이면 `SetShapeGuide`, 없으면 `ClearShapeGuide`. `TilemapMapView.ClearPlacementRange` 가 가이드도 회수
  (드롭·취소·재배치 경로는 전부 거길 지난다). 재배치(`DefenderRelocationController`)·peek 경로도 같은 `SetPlacementRange` 라 자동 포함.
- 자가 리뷰에서 확인한 구멍 없음: 힐러(`targetAllies`)는 마크 경로가 이미 건너뛰어 가이드도 없음(Omni 라 무관) · 같은 자리 타겟
  (방향 0)은 숨김 · Rect 는 이쑤시개 확정(unit 4 rev 3b) 뒤 띠 가이드로 그린다.

## 완료 기준

- [x] 라이브 검증 2026-09-12(`scratchpad/live/guide_f.png`): 브루저 프리뷰(11,7) — 원 링 안에 라임 부채꼴이 최근접 적(오른쪽 위,
      빨간 마크) 쪽을 향하고 바깥 호가 링과 겹친다. 검증 레시피: Play → `SceneTransition.Go(Battle)` → `StartBattle` →
      **첫 적 스폰 즉시** `TimeManager.Request(Battle, 0)` 동결(수비 0 이면 5초 안에 스트레스 100 → 결과창) → `SetPlacementRange`
      → `ScreenCapture`. 「적이 벗어나면 사라진다 · Omni 엔 안 뜬다」는 코드 경로(`guideHas`/`SectorKind` 게이트)로 보장, 육안은 사용자.
- [~] 가이드 방향의 타겟 = sim 의 첫 획득 타겟 — 같은 함수(`NearestTargeting.RanksBefore`)를 지나므로 구조로 보장. 배치 직후 첫
      공격 방향과의 일치는 사용자 Play 육안.
- [x] 드롭·취소·재배치 경로 — 전부 `TilemapMapView.ClearPlacementRange` 를 지나고 거기서 `ClearShapeGuide`. EditMode 코어+에셋
      2837건 무회귀(선행 실패 2건 외 0).
- [x] EditMode: 순수 함수는 신설하지 않고 `NearestTargeting.RanksBefore` 를 **재사용**(sim 과 같은 tie-break, 기존 테스트가 고정).
