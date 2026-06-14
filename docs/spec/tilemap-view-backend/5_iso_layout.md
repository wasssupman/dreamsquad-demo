# 5. Isometric 레이아웃 검증

## 목적

`boardViewMode = TilemapIso` 에서 마름모 보드 + 유닛 정렬 + 입력이 정상 동작함을 확인하고, 세 모드의 **sim 결정론 동등성**을 못 박는다. 셀↔월드 정합의 권위는 unit 0/1 에서 이미 Grid 로 고정됐으므로, 본 unit 은 검증 + 카메라/타일 에셋 튜닝만 담는다. 신규 시스템 없음.

## 변경 대상

- `Assets/_Project/Data/TileSets/TileSet_Placeholder.asset` — iso 마름모 placeholder 튜닝 (unit 1 에서 생성된 것의 시각 보정)
- `Assets/_Project/Data/Camera/CameraPreset_TilemapIso.asset` — 수치 튜닝
- 0~4 산출물의 iso 경로 버그 수정 (발견 시 — 수정 지점은 해당 파일에 한정, unit 1 정합 테스트가 먼저 빨개지는지 확인 후 수정)

## 구현

- Grid `cellLayout = Isometric` + `cellSize = TileSetData.isoCellSize` 적용 확인 (unit 1 구현 경로).
- 유닛이 마름모 셀 중심에 서는지 — 어긋나면 unit 1 의 `GetCellCenterWorld` 정합 테스트부터 확인 (테스트가 green 인데 시각이 어긋나면 anchor 설정 의심).
- 드래그 배치: iso 마름모 경계에서 의도 셀에 떨어지는지 — 특히 셀 모서리 근처 클릭. hover/reject 피드백 표시 확인.
- 투사체 방향, 메테오 경고 링/낙하, 토네이도/힐 VFX 위치가 iso 보드 위에 정렬되는지 (unit 3 산출의 iso 경로 확인).
- **sim 결정론 확인 (README 검증 질문 2)**: 동일 matchSeed 로 Legacy3D / TilemapRect / TilemapIso 각 1판 Play. `LogMap` 시드 로그 + 판 종료 결과(킬 수/도달 수)가 3모드에서 동일함을 로그로 비교. 이 비교는 sim 결정론 검증이며, 시각 동등성(헬스바 carve-out, 3D VFX 정합)은 포함하지 않는다.

## 완료 기준

> ✅ 검증 2026-06-14 (코드 변경 없음 — 검증 전용. iso 에셋/카메라 추가 튜닝 불필요 판단). TilemapIso Play(메모리 배선):
> Grid `cellLayout=Isometric` `cellSize=(1,0.5,1)`(isoCellSize 적용), 마름모 보드 렌더 + 유닛 보드 위 정렬(스크린샷).
> **sim 결정론(검증 질문 ②)**: matchSeed=9999 로 Legacy3D/Rect/Iso 3모드 `_generatedMap` **byte-identical**
> (seed=-1298266927, grid 20×10, goal(19,6), spawns 2, tilesFNV=1422606019213334008 전부 동일) — 맵 생성이
> boardViewMode 미참조 확정. 웨이브는 matchSeed 파생(mode 무관, match-seed-unification 기증명), 전투 sim 도
> mode 미참조 → 판 결과 결정론 보장. EditMode **325/323 pass**(정합 테스트 포함), 콘솔 신규 에러 0.
> 제외 항목 의도대로: 헬스바 게이팅(unit 2), backdrop/prop 미표시, 한글 TMP 폰트 경고(기존 백로그), 3D VFX 미세정합(후속).

- Unity compile 0 errors. EditMode 전체 green (BoardSpace + TilemapMapView 정합 테스트 포함).
- `TilemapIso` Play: 마름모 보드 렌더, 유닛 셀 중심 정렬, 경로 이동이 마름모 축을 따라 자연스러움, 드래그 배치 + hover/reject 정상. 스크린샷 1장 확보.
- 동일 seed 3모드 판 결과 로그 동일 (sim 결정론).
- 콘솔 error/warning 0.
- 알려진 제외 항목이 모두 의도대로인지 확인: Tilemap 모드 헬스바 미표시(게이팅 로그), backdrop/prop 미표시, 3D VFX 미세 정합은 후속 후보.
