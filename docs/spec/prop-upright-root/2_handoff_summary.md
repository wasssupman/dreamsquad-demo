# 2 — Handoff Summary

## Commit

- `94443bd` unit 0 — audit + frame contract
- `178abac` unit 1 (code) — 루트 flip + AttachAuthoredBlob upright + EditMode 테스트
- `630b0fa` unit 1 (prefabs) — 28개 프랍 블롭 upright 마이그레이션

## Implemented

- background/ring props 루트에 `localRotation = Euler(-90,0,0)` → 프랍 저작 프레임이 **월드-upright(+Y=위)**. 타일맵 렌더 회전·배치 로직·빌보드 무변경.
- 검증: 인게임 propRoot 월드 회전 = `(0,0,0)`(기존 90°에서 전환), 블롭 월드 = `(90,0,0)`(접지 보존).
- `PropDataEditor.AttachAuthoredBlob` 기본값을 upright 프레임으로 정정(블롭 `Euler(90,0,0)` + 높이=+Y·깊이=+Z). 이후 authoring 되는 프랍은 자동으로 upright.
- 기존 28개 프랍 프리팹 블롭을 월드-보존 수식(`p_new=(x,-z,y)`, rot `Euler(90,0,0)`)으로 일회성 마이그레이션. 시각적으로 블롭 위치 변화 없음(순수 프레임 전환).
- EditMode `PropUprightRootTests` 3종: upright basis 불변식 + 블롭 월드보존 위치/회전 수식.

## Key Files

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `_backgroundPropsRoot`/`_ringPropsRoot` 루트 flip
- `Assets/_Project/Editor/PropDataEditor.cs` — `AttachAuthoredBlob` upright 프레임
- `Assets/_Project/Tests/EditMode/PropUprightRootTests.cs`
- `Assets/_Project/Prefabs/Props/forest/*.prefab` (28) — 마이그레이션된 블롭

## Verified

- compile 클린. `PropUprightRootTests` green.
- Play(forest) 스크린샷 `Assets/Screenshots/upright_flip_verify.png` — 프랍 기립·블롭 접지, 시각 회귀 없음.
- 라이브: propRoot 월드=(0,0,0), 블롭 월드=(90,0,0) 확인.

## Notes / 주의점

- **backdrop 프랍(prop_concept_*·prop_edge_*)은 스코프 밖** — identity 루트(BackdropMounter, Legacy3D), flip 루트 미경유. 마이그레이션에서 제외/되돌림. 건드리지 말 것.
- **desert 테마 접지 결함은 미해결** — desert 풀의 `prop_style_*`/`prop_dummy_*` 가 아직 FullCamera + nonzero offset(접지 fix 의 desert 미적용). 프레임 flip 과 별개. → Follow-up Backlog "desert 테마 접지 fix".
- pine(`1_1`/`1_4`) 리네임은 사용자 WIP — 블롭 마이그레이션만 포함, 리네임 정리는 사용자 몫.
- 블롭 마이그레이션은 regen 이 아니라 직접 transform 변환(M2 preservation 트랩 회피).

## Follow-up

- **ObstaclePlacer 테스트 기존 실패** — `Place_PreservesWalkAndMinimumPlaceRatio`(≥36 기대, 31). dea2733 커밋 테스트, 맵 생성, 이번 작업과 무관. 별도 조사 필요.
- desert 테마 접지 fix → `docs/spec/README.md` Follow-up Backlog.
