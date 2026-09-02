# 0b — 표기: 링 전용 경로 `SetAreaRange` + 액티브 조준 원 + `squareShape` 삭제

> 선행: 0a. 결정 D1(채움 주신호) · D6(조준 셀 중심 원) · Q7(사각 채움 결함은 삭제로 해소). **sim 무변 · 골든 무변.**

## 목적

0a 가 판정을 원으로 바꿨으니 같은 표기가 즉시 따라가야 한다(unit 5 교훈 「판정만 바꾸면 화면이 거짓말한다」).
액티브 셀 조준·메테오 텔레그래프가 사각 타일 채움(`squareShape`)을 쓰고 있어 이 단위가 원으로 옮기고,
그 뒤 unit 2 가 재사용할 **링 전용 경로**를 만든다.

## 변경 대상

- `Core/TilemapMapView.cs` — **신설**
  `public void SetAreaRange(Vector2 centerTiles, float radiusTiles, in RangeRingStyle style)`.
  `RangeRingStyle { Color color; float fillAlpha; float lineAlpha; bool pulse; }` (뷰 소유 plain struct).
  - 가드 체인은 형제와 같다: `grid == null || _tileSet == null || radiusTiles <= 0` 조기 return
    (`ApplyRingTint` 가 `_tileSet` 을 null 검사 없이 역참조한다 — 우회하면 NRE).
  - 순서 고정: **`ClearPlacementRange()` → 스타일 오버라이드 설정 → `ShowRangeRing`**. 거꾸로면 방금 넣은
    스타일이 지워진다. 타일은 칠하지 않는다(`_rangeCells` 비어 있음).
  - `ApplyRingTint`(`:911`, 배치 링과 **공유**) — 오버라이드가 있으면 그 색·채움·선 알파를 쓰고 `pulse` 면
    채움 알파를 타일 펄스와 같은 위상으로 흔든다. 오버라이드 리셋은 `ClearPlacementRange` 에서.
  - **`squareShape` 파라미터·분기 삭제**(`:979, :1000, :1010`, 헤더 `:959~975`). 소비처 0 이 되고 `(2N+5)²` 결함도
    함께 사라진다. 되살릴 일이 생겨도 SDF 셰이더가 `_HalfExtent` 로 정확한 사각을 그리므로 타일 경로는 불필요.
- `Data/TileSetData.cs` — `aimRingStyle`(조준·텔레그래프용, 초기 채움 0.4 근처 · `pulse = true`) 저작 필드.
  배치 링은 종전 값(`rangeColor` · `rangeFillAlphaUnderRing` · `rangeRingAlpha`) 그대로.
- `Bridge/BattleBridge.cs:8122 PinCenteredRange` — `SetAreaRange(new Vector2(cell.x, cell.y), tileRange + 0.5f,
  _tileSet.aimRingStyle)` 로. 주석 「squareShape … 여기만 예외다」 삭제. `SetSkillAimCells`(포탈 단일 셀)는 무변.
- **VFX 반경 2곳도 판정에 맞춘다**(표기 동시 변경의 연장): `BattleBridge.cs:~2642` 회오리 `SpawnTornado(…,
  tornadoTiles * tileSize, …)` → `(tornadoTiles + CellHalfWidthTiles) * tileSize` · `ProjectileHitSystem.cs:~796`
  운석 착탄 `radiusWorld = tileRange * tileSize` → `(tileRange + CellHalfWidthTiles) * tileSize`. 둘 다 뷰 값이라
  sim 판정·골든 무변(`radiusWorld` 는 이벤트 페이로드지만 판정에 쓰이지 않는다 — 구현 시 소비처 확인).
- stale 주석 정리: `BattleBridge.cs:8124~8126` · `TilemapMapView.cs:963~968`(「멤버십은 `IsInTileRange` 정사각형,
  결정 4」 — 결정 4 는 unit 14 에서 폐기).

## 구현

- **D6 그대로**: 액티브 스킬 = 터치/커서 타일 **중심**에서 `N + 0.5` 원. 원과 접하는 유닛이 대상 — 0a 의
  `TileStatBurstSkill` 이 `ctx.CellCenter` 중심으로 같은 식을 쓰므로 표기와 판정이 한 자다.
- **신호량**: 종전 조준은 채움 0.85 펄스, 배치 링 규칙(선 0.95 + 채움 0.12 무펄스)을 그대로 쓰면 신호의 85%
  가 사라진다. 조준·텔레그래프는 **작은 반경 + 손가락 중심** 조건이라 채움이 주신호다(D1 과 같은 논리).
  값은 SO, 실기기에서 튠.
- 배치 링 경로(`SetPlacementRange`)는 **바이트 단위로 무변**이어야 한다 — `ApplyRingTint` 수정이 유일한 접점.
- `IsPlacementRangeCell` 포워더(`BattleBridge:7451`)는 소비처 0 — 이 단위에서 건드리지 않고 후속 후보.

## 완료 기준

- [ ] Play: 액티브 셀 조준(`TileStatBurst` 계열, 토네이도)이 조준 셀 중심 **원**으로 뜨고 채움이 펄스한다.
      손가락이 중심을 가린 상태에서도 범위가 읽힌다. 발동 결과와 링이 일치.
- [ ] Play: 메테오·보스 착탄 텔레그래프가 원 링으로 뜨고 위험 구간이 읽힌다(채움이 유일한 신호였던 경로).
- [ ] Play: 회오리 링 VFX · 운석 착탄 VFX 의 원이 조준 링(N + 0.5)과 같은 크기다.
- [ ] Play: 배치 드래그 사거리 링이 종전과 동일(스크린샷 A/B) — 오버라이드 없을 때 `ApplyRingTint` 경로 무변.
- [ ] `squareShape` 문자열 0건 · `_rangeCells` 가 `SetAreaRange` 뒤 비어 있음(EditMode 또는 로그 단언).
- [ ] `RangeDisplayContractTests` 초록 · sim 파일 변경 0 · 골든 바이트 무변.
