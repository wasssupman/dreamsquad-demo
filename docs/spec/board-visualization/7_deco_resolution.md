# 7. Deco Resolution (rev3 선행 결정)

## 목적

`MapTileType.Deco` 의 정체성을 **mask / shape 작업 이전에 확정**한다. Deco 가 어떤 zone 으로 folding 되는지에 따라 `sameZoneMask` / `innerCornerMask` 의 모든 판정이 달라진다. 이 결정이 늦어지면 8~11 작업이 재작업 대상이 된다.

## 조사 결과

`MapTileType.Deco` 의 **실사용처가 코드에 존재한다**:

- `Assets/_Project/Scripts/Data/BattleMapBuilder.cs:145,208` — 맵 생성기에서 Deco 타일 직접 생성
- `Assets/_Project/Scripts/Data/ObstaclePlacer.cs:40` — Place → Deco 변환 (obstacle 배치)
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs::IsBackgroundTile` — 현재 Deco 를 background 로 취급

즉 Deco 는 "현재 사용 없음 → 삭제" 가 아니라 **obstacle placement 결과물**이다.

## 선택지

- (A) `BoardZoneType` 에 `Deco` 추가, 시각적으로 Env 와 분리  
  → 큰 리팩터. mask / shape / anchor / theme 자산 전부 4-zone 대응 필요.
- (B) Deco → Env folding 유지, 시각 동일  
  → 현 구현 상태 그대로. Deco 가 놓인 셀은 Env 와 같은 surface 로 렌더되고, obstacle prefab 이 그 위를 덮는다.
- (C) `MapTileType.Deco` 제거, obstacle 경로를 Env + prop 으로 대체  
  → 가장 깨끗. 하지만 `BattleMapBuilder` / `ObstaclePlacer` / 관련 테스트 전면 재작성. 본 spec scope 를 넘음.

## Decision (rev3)

**선택: (B) Deco → Env folding 유지. 시각 분리 없음.**

근거:
- rev3 의 시각 목표는 "Env 와 Place 의 경계감 + 프랍 유기화" 이지 "Deco 의 별도 시각 언어 확립" 이 아니다.
- Deco 셀은 obstacle prefab 이 그 위를 덮으므로, 배경 surface 가 Env 와 같아도 화면상 구분된다.
- (A) 는 8~11 을 4-zone 대응으로 재설계해야 함. 비용 불균형.
- (C) 는 gameplay 맵 생성 로직 수정. board-visualization spec scope 밖.

결과:
- `BoardVisualPlanBuilder.ToBoardZoneType` 의 `MapTileType.Deco → BoardZoneType.Env` folding 유지.
- `sameZoneMask` / `innerCornerMask` / `regionId` 는 Deco 를 Env 로 간주해 계산.
- `sourceTileType` 은 셀에 원본 `MapTileType.Deco` 를 보존 (필요 시 renderer 가 구분 가능).
- theme `decoTileTexture` / `decoTileVariants` / `decoSurfaceRules` 는 `14` 에서 deprecated 로 격리.

## 변경 대상

**코드**: 없음. 현재 상태 유지.

**문서**:
- 본 문서가 결정 기록 source of truth.
- `1_board_visual_plan.md` 는 Deco folding 을 전제로 서술됨.
- `14_theme_asset_contract.md` 에서 `deco*` 필드 deprecated 격리.

## 완료 기준

- 본 문서에 Decision 문단이 존재하고 (B) 가 명시됨.
- `8` ~ `13` 에서 Deco 관련 재작업이 필요 없음.
- 향후 (A) / (C) 로 재결정이 필요하면 rev4 spec 을 별도로 연다.

## 주의

- Deco 가 시각적으로 Env 와 구분되어야 한다는 디자인 요구가 생기면 이 결정 전체를 다시 한다. "조금만 분리" 같은 부분 구현 금지.
- rev3 는 본 결정을 **사용자가 다른 지시를 주지 않는 한 유지**. Codex 가 (B) 이외의 경로로 임의 변경 금지.

확인 일자: 2026-04-24 / 커밋 해시: 8501c43
