# map-stage-tile-scale — 맵마다 다른 타일 크기 (스테이지가 런타임 tileSize 를 정한다)

상태: **초안 2026-08-27 — 사용자 승인 대기** (map-diorama-stage 후속. 발단: 사용자가 Street·Subway·StreetDay 를 15×6 으로 축소 저작하며 `previewTileSize` 가 1.81/1.87/1.48 이 됐고, 계약 1「previewTileSize == 런타임 tileSize(1)」이 배틀 진입을 하드 실패시킴)

## 검증 질문

**같은 아트 위에 «큰 칸 15×6»으로 저작한 스테이지가, 코드의 타일 가정을 깨지 않고 라이브에서 돈다 — 이동·사거리·배치·카메라·정렬이 전부 그 맵의 타일 크기로 흐르는가?**

## 현재 구조 (왜 가능한가)

- 런타임 타일 크기의 단일 소스 = `BattleBridge.tileSize`(SerializeField 1). 맵 빌드 시점에 하류로 **그때 주입**된다: `MapStageScanner.Scan(…, tileSize)` · `SimFieldInstaller` → `FlowFieldSingleton.tileSize`(심 소비 59곳: Movement/Combat/Effects 전부 이 값을 읽음) · `TilemapMapView.Initialize`(오버레이 격자 cellSize) · `PlacementInput.Initialize` · `BoardSpace.Configure` · 정렬/게이지/VFX 반경. 시작 시 캐시하는 곳 없음.
- 심은 **셀 단위**(사거리·속도·반경이 타일 배수)라 타일이 커져도 게임 규칙은 불변 — 월드 크기만 커진다.
- 따라서 `BuildMapForBattle` 에서 스캔 직전 `tileSize = stage.previewTileSize` 한 줄이 핵심이고, 나머지는 «타일 1 을 가정한 곳» 정리다.

## 작업 단위 (초안)

| # | 작업 | 목적 |
|---|---|---|
| 0 | `MapStage.previewTileSize` → `tileSize` 의미 승격 + 브리지 주입 | 기즈모 전용 → **이 맵의 타일 크기(런타임 정본)**. `DioramaMapBuilder.Validate` 의 계약 1 검사(preview≠runtime 하드 실패) 은퇴. `StageScan.runtimeTileSize` 도 스테이지 값에서 옴 |
| 1 | 타일 1 가정 정리 | `StagePoolBuildabilityTests`(`Scan(…, 1f)`) · `DioramaStagePlayTests.CellOf`(floor(xz)) · `BattleBridgeTestAccess` 셀 헬퍼 · `MapStageCameraFraming`/`RenderPrefabPreview` 바운즈(`playAreaCells` × tile) · `MapStageAuthoringTools.Host`(cell+0.5 → ×tile) · `MapStageEditorUtil.SnapToCellCenter`/`NormalizeGridOrigin` · 가이드 «양자화 규칙» |
| 2 | 시각 스케일 결정 | 캐릭터(`tilemapCharacterScale` 0.42 월드 고정)·투사체·그림자·VFX 반경이 타일 1.8 에서는 **칸 대비 절반 크기**. 선택: ⓐ 그대로(맵마다 캐릭터 체감 크기 다름) ⓑ 맵별 캐릭터 스케일 knob(`MapStage.characterScale` 또는 tile 비례) — 사용자 결정 |
| 3 | 세 맵 마커 재저작 | Street/Subway/StreetDay 의 스폰·골·포탈·차단을 1.8 격자 기준으로 다시 놓기(현재 워크트리의 15×6 프리팹은 마커가 옛 30폭 좌표) + 빌더빌리티·프리뷰 |
| 4 | 라이브 캡 재정의 | 「가로 ≤ 30 셀」은 타일 1 기준 — 월드 폭(셀 × tile) 또는 셀 수 중 어느 축을 카메라 상한으로 삼을지 |

## 계약 (초안)

1. 타일 크기는 **스테이지 프리팹이 정한다** — 씬/브리지 값은 폴백(스테이지 미지정 시 1).
2. 심은 타일 크기를 모른다 — 셀 단위 규칙 불변. 타일 크기는 sim↔world 변환(`GridMath`·`BoardSpace`·`FlowFieldSingleton.tileSize`)에만 나타난다.
3. 한 판 안에서 타일 크기는 상수 — 맵 교체(재빌드) 시에만 바뀐다.

## 후속 후보

- 맵별 캐릭터/VFX 스케일 knob(unit 2 ⓑ 로 결정될 경우 이 spec 안에서, 아니면 별도).
