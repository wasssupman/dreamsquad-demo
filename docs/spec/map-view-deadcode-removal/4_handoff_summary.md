# 4. Handoff Summary

## Commit

| 커밋 | 내용 |
|---|---|
| `3f12376` | docs — spec 작성 (unit 0~3) |
| `81dd23f` | unit 0 — 은퇴한 카메라 프리셋 계통 제거 |
| `da07d74` | unit 1 — `BoardViewMode` 접기 (iso 경로 제거) |
| `5089fbb` | unit 2 — 소비자 없는 맵 메타 3배열 제거 |
| `48196f9` | unit 3 — 단수 `goal` 폴백 제거 + 재감사 |
| `9097be6` | unit 3 보강 — 심볼 재감사로 드러난 `TryGetBoardWorldBounds` 제거 |

## Implemented

- `ApplyTilemapCameraPreset()`(호출부 0) + `tilemapCameraPresetRect/Iso` + `BoardCameraPreset.cs` + `CameraPreset_Tilemap{Rect,Iso}.asset` 제거. 카메라 소유자는 `CameraDirector` 하나로 정리.
- `BoardViewMode` enum 삭제 → `BoardSpace.Configure` / `TilemapMapView.Initialize` / `ConfigureGrid` 시그니처에서 mode 제거. `BoardSpace.Mode`(readers 0), `TileSetData.isoCellSize` 동반 제거.
- `GeneratedMap` / `MapDocument` / `MapDocumentBuilder` / `MapPainterWindow` 에서 `mergeDegree`·`chokepoint`·`propLayerId` 제거 — 판마다 셀수×3 `NativeArray` 할당이 사라졌다.
- `MapDocument.goal`(단수) 폴백 → **loud 계약**으로 전환: `OnValidate` 에러 + `ToGeneratedMap` 의 `MapGenerationFailedException`.
- placeholder 타일 에셋 8종(`PH_Iso_*` 4, `PH_Rect_*` 4) 제거 — 전부 참조 0.
- iso 정합 테스트 3개를 **회전 + 비균일 cellSize** 그리드 테스트로 교체(계약 유지). 페인트 정합은 3축으로 강화.

## Key Files

- `Assets/_Project/Scripts/Core/BoardSpace.cs` — sim↔view 단일 변환 지점. 권위 위임 계약이 여기 주석에 있다.
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `ConfigureGrid` 가 grid 구성(레이아웃/셀크기/90°X 회전/anchor)의 유일 소유자.
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` — 저작 축과 `OnValidate` 계약.
- `Assets/_Project/Tests/EditMode/BoardSpaceTests.cs` — "권위는 Grid" 를 못 박는 테스트. 변이로 유효성 확인됨.

## Verified

- compile 0 errors. EditMode **2193 / 실패 0** (unit 0 시점 2192 → 대체 테스트 순증 +1).
- **변이 검증**: `BoardSpace.ToView` 를 rect 수식으로 하드코딩하면 7개가 빨개진다(신규 회전 테스트 3개 포함). 되돌린 뒤 green.
- **프로덕션 문서 왕복**: 실제 `MapDocument` 9장 전부 `ToGeneratedMap → WriteToDocument` 보존 확인(임시 테스트, 확인 후 제거).
- **guard 발화**: 빈 goals 에 `OnValidate` 에러 + `ToGeneratedMap` 예외 둘 다 확인(임시 테스트, 확인 후 제거).
- Play(BattleScene) 4회 정상, 보드/프레이밍 불변, 콘솔 error/warning 0.
- `BattleScene.unity` · 맵 9장 `.asset` **전부 무변경**.

## Notes

- **씬을 편집하지 않는 것이 이 spec 의 핵심 계약이었다.** SerializeField 를 지우면 `BattleScene.unity` 의 `boardViewMode` / `tilemapCameraPreset*` 키는 orphan 으로 남고, 대응 필드가 없어 역직렬화되지 않는다(씬 로드 시 경고 0 으로 확인). 되돌려서 씬을 정리하려 들지 말 것.
- 맵 9장의 `mergeDegree`/`chokepoint`/`propLayerId` YAML 키도 orphan 으로 남는다. `ForceReserializeAssets` 는 이를 떨구지 않으므로(기존 판례) 방치가 정답이다.
- 단수 `goal` 폴백은 **지운 게 아니라 계약으로 승격**했다. 다시 폴백을 넣지 말 것 — 조용한 폴백은 다른 격자계의 골을 만든다.

## ⚠ 함정 — 다음 세션이 반드시 알아야 할 것

`MapDocument.cs` 에 `goal` 참조가 한 줄 남아 `Wassup.Runtime` 이 컴파일에 실패했는데, **테스트 러너는 stale 어셈블리로 계속 green(2193/0)을 냈다.** `read_console` 에도 안 잡혔다(반복 refresh 가 소거).

판별법: `editor_state.compilation.last_domain_reload_after` 가 `last_compile_finished` 보다 **과거**면 그 테스트 결과는 믿으면 안 된다. 새 테스트 파일을 넣었는데 총 개수가 그대로인 것도 같은 신호다.

확실한 진단은 `Editor.log` grep 하나뿐이었다:

```bash
tail -c 300000 "$LOCALAPPDATA/Unity/Editor/Editor.log" | grep -a "error CS" | tail
```

`validate_script` 는 "Duplicate method signature" 오탐을 냈다 — 신뢰하지 말 것.

## Follow-up

- **`MapTileType.Env` 제거** — 9장 전부 0개. `envTile` 슬롯은 `TileSet_Desert` 에서 `decoTile`/`terrainTile` 과 같은 타일을 가리키고 있어 지워도 그림이 안 바뀐다. EditMode 테스트 여럿이 Env 를 픽스처로 쓰므로 함께 손봐야 한다.
- **`ObstaclePlacer.DesignateDeco` + `RederivePlaceMask`** — 게이트를 9장 전부 통과 못 한다(라이브 6장 authored Deco, dev 3장 authored mask). 두 테마의 `mapGridBuildableKeepRatio: 0.6` 도 무의미한 저작값.
- **프랍 시스템 2벌** — `TilemapPropScatter`(씬 배선·활성, `groundTilemap.GetTile` 역판정)와 `BoardVisualPlan`+`BackgroundPropPlacer` 가 독립적으로 돈다. 의도적 레이어링인지 확인 필요.
- **`PaintMarkers` → `InstantiateStructureProps` paint-then-erase** — 무해하나 낭비.
- **`DirectionAim{Controller,Logic}` 의 iso 근거 주석** — "iso 보드에서 화면 위가 두 레인 사이" 라는 설계 근거가 남아 있다. 결정 자체는 유효하나 근거가 사라진 구성을 가리킨다. 설계 재검토 없이 문구만 고치면 근거가 약해지므로 별도 판단 필요.
- **[별건 · 버그] `BoardSortOrder.Compute`** — `(gridSize.y - cellY) * 10 + cellX` 에서 행 간격 10 < 맵 폭(13~30)이라 **먼 행 유닛이 가까운 행 유닛 위에 그려진다**. 사장 코드가 아니라 결함이며 순수 함수라 EditMode 테스트 대상. 맵 개편(격자 축소/원화)과 무관하게 선행 가치 있음.
