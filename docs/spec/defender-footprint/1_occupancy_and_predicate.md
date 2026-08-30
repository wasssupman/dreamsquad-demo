# 1 — 브리지 점유 모델 + footprint 판정

## 목적

배치 점유와 공간 판정을 「앵커+W×H 셀 집합」으로 확장한다. `_defenderByTile` 은 **대표 셀 키 · 유닛당 1엔트리**를 유지해 «엔트리 수 = 기수» 소비자(DeployedCountOf·뷰 동기·순회)를 지키고, 셀→유닛 해석은 신규 owner 맵이 담당한다. 전 유닛 1×1 무회귀가 완료 경계 — UX(고스트·자석)는 unit 2 몫.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/PlacementRejectReason.cs` — `FootprintCellReason` 구조체(타일별 사유)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_defenderCellOwner` 맵 · `SpatialFootprintCheck` · `GetPlacementCellReasons`(UI seam) · 점유 등록/해제 헬퍼 · 배치 2경로/사망/퇴근/리셋/활성화/조회 rewiring
- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — `RelocationFootprintCheck`(자기 footprint 제외) · 스왑/Finish rewiring
- `Assets/_Project/Tests/EditMode/FootprintPlacementCheckTests.cs` **신규**

## 구현

- **셀 파라미터 의미**: 배치 진입(`CanPlaceDefenderAt`·`PlaceDefenderAs`·`TryBeginDefenderDeployment`)의 (x,y) = **footprint 앵커**(min 코너). 바인딩·`DefenderTile`·sim 위치·시너지/on-place/로그는 전부 **대표 셀**. 1×1 은 앵커=대표 셀이라 기존과 동일.
- **판정 단일 술어 유지**: `SpatialFootprintCheck` 는 셀마다 기존 `SpatialPlacementCheck` 를 재호출한다(규칙 이중화 금지). 종합 사유 우선순위 = **Occupied > NotBuildable > OutOfBounds**(조치 가능한 사유 우선 — 거부 라벨 문자화와 정합). `perCell` 리스트로 타일별 사유 전달, UI 는 재판정하지 않고 이 결과를 그린다.
- **셀-키 공개 API 는 footprint 투명**: `TryResolveDefenderKey`(owner 맵)로 footprint 안 어느 칸이 와도 대표 셀로 해석 — `TryGetDefenderAt`·`ActivateDeployedDefender`·`RetireDefender`·재배치 from/Finish 적용.
- **해제는 등록 스냅샷 기준**: `ReleaseDefenderFootprint` 는 SO 를 다시 읽지 않고 owner 맵을 스캔해 등록된 칸들을 반납한다 — 배치 후 시트 임포트로 footprint 값이 바뀌어도 유령 점유가 안 남는다.
- **재배치 자기 겹침 허용**: `RelocationFootprintCheck` 는 from-rect 안 셀의 Occupied 를 무시한다(2×2 를 한 칸 옮기기가 자기 점유에 막히면 안 됨). 같은 앵커 = 제자리 재정비(None) 유지.
- **효과 타일 무변경 = 규약**: `AddEffectTile`/`ApplyEffectTileOnce` 의 정확 일치 조회가 곧 「효과 타일은 **대표 셀에 있을 때만** 발동」(README 계약 2 파생, 과발동 방지 보수 기본값).

## 완료 기준

- [x] 컴파일 에러 0
- [x] `FootprintPlacementCheckTests` 그린 — 1×1 동치 · 다중 셀 통과/부분 실패 per-cell 사유 · 사유 우선순위 · 재배치 자기 겹침 · 제자리 (8/8 + FootprintMathTests 12/12)
- [x] 기존 EditMode 코어 lane 무회귀 — 2474 전건 실패 0 (스킵 3 은 선행 Ignore)
- [x] 라이브 동작 무변 — 전 유닛 1×1 (앵커=대표 셀 항등)

확인 2026-08-28 — 사용자 일괄 진행 승인 흐름. 커밋 해시는 handoff 에 기록.
