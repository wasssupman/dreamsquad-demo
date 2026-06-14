# 3 — Handoff Summary (tilemap-mode-adoption)

## Commit

- `016a29a` 0 모드별 유닛 스케일(const 제거) + mode-aware billboard tilt
- `4a3fc91` 1 카메라 보드 bounds 프레이밍 + solid 배경 + 환경 게이팅 scaffold
- `3531660` 2 `_TilemapBoard` + BattleBridge Tilemap 필드 영속 씬 배선
- 문서: `a9ff36a`(0) · `ae2ecba`(1) · 본 커밋(2+handoff)

## Implemented

- `CharacterVisualScale` const → static 프로퍼티. SerializeField `legacyCharacterScale(0.7)`/`tilemapCharacterScale(0.42)`/`tilemapBillboardTilt(0)`, 맵 빌드 시 모드 기준 설정. Legacy3D=0.7/35, Tilemap=0.42/0.
- `ApplyTilemapCameraPreset`: 페인트된 보드 실측 bounds(iso 마름모 정확) 로 orthographicSize/center, `clearFlags=SolidColor`(skybox 제거).
- `ApplyEnvironmentGating(tilemapHiddenEnvironment[])` — Tilemap 모드 SetActive 토글(현재 빈 배열, 되돌림으로 숨길 실험물 없음).
- `_TilemapBoard`(Grid+Ground/Overlay Tilemap) + BattleBridge 필드 영속 저장. **인스펙터 `boardViewMode` 1값으로 Legacy3D/Rect/Iso 토글**.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (모드 스케일/틸트, ApplyTilemapCameraPreset, ApplyEnvironmentGating, 필드)
- `Assets/_Project/Scripts/Data/BoardCameraPreset.cs` (배경 필드), `Assets/_Project/Scripts/Core/TilemapMapView.cs` (TryGetBoardWorldBounds, sorting)
- `Assets/_Project/Scenes/BattleScene.unity` (_TilemapBoard + 배선)
- `Assets/_Project/Data/Camera/CameraPreset_Tilemap{Rect,Iso}.asset`

## Verified

- compile 0, EditMode **325/323 pass**(도메인 리로드 후, 회귀 0).
- TilemapIso Play(저장된 _TilemapBoard, boardViewMode 인메모리 전환): 깔끔한 마름모 보드 painted=200, 정자세·축소 유닛, solid 배경, bounds-fit 카메라. 스크린샷 다수.
- 모드별 스케일/틸트 0.42·0 / 0.70·35 토글 정확. 씬 diff 327줄 순수 추가(실험물 0).

## Notes

- **boardViewMode 기본 = Legacy3D(비파괴)**. 게임을 iso/rect 로 "기본 채택"할지는 **미결 product 결정** — 인스펙터에서 값만 바꾸면 됨.
- dirty `BattleScene.unity` 실험물 837줄은 **사용자 승인 하 git checkout 되돌림**(복구 불가). 배경 스프라이트 4 + 평면 4 + 카메라 이동.
- 검증 함정: 다회 Play 후 EditMode 거짓 실패 → `RequestScriptReload` 후 재실행. (memory 기록)

## Follow-up

- **boardViewMode 기본 모드 product 결정** (iso/rect 채택 여부).
- iso/rect 전용 **타일 아트**(RuleTile/스프라이트) + 유닛 스케일 미세 튜닝(`tilemapCharacterScale`).
- Tilemap 모드 전용 2D 배경 연출. 해저드/장애물 Tilemap 정렬. (tilemap-view-backend / 본 spec README 후속 후보)
