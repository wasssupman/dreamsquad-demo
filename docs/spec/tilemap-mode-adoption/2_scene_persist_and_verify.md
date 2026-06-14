# 2. 영속 씬 배선 + 검증

## 목적

`_TilemapBoard` GameObject 와 `BattleBridge` 의 Tilemap 필드/프리셋을 `BattleScene.unity` 에 **영속 저장**해, 빌드/실기기에서 인스펙터 값으로 Legacy3D ↔ TilemapRect ↔ TilemapIso 토글이 가능하게 한다. 선행: dirty 씬 정리(완료 — 실험물 837줄 되돌림 2026-06-14).

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity` — `_TilemapBoard`(Grid + Ground/Overlay Tilemap+Renderer) 추가, `BattleBridge` 필드 배선, 저장.

## 구현

- `_TilemapBoard`: Grid + 자식 Ground/Overlay (각 Tilemap + TilemapRenderer, Sprites/Default 머티리얼). `TilemapMapView` 컴포넌트 + grid/ground/overlay 배선.
- `BattleBridge` SerializeField 배선: `tilemapMapView`=뷰, `tileSet`=`TileSet_PlaceholderIso`(또는 Rect), `tilemapCameraPresetRect/Iso`, `tilemapHiddenEnvironment`(되돌림 후 실험 스프라이트 없음 → 비움; skybox 는 카메라 clearFlags 가 처리).
- `boardViewMode` 기본값: **Legacy3D(비파괴) — 사용자가 인스펙터에서 토글**. (iso 를 게임 기본으로 할지는 별도 product 결정.)
- `EditorSceneManager.SaveScene` 로 영속화.

## 완료 기준

> ✅ 검증 2026-06-14 — dirty 실험물 837줄 `git checkout` 되돌림 + 씬 재로드(메모리 정리). `_TilemapBoard`
> (Grid+Ground/Overlay Tilemap+Renderer, Sprites-Default) + `TilemapMapView` 추가, `BattleBridge` 필드 배선
> (tilemapMapView fileID 172395674, tileSet/프리셋 guid, env 비움), `boardViewMode: 0`(Legacy3D 비파괴). SaveScene.
> 씬 diff = **327줄 순수 추가, 내 객체만**(실험물 0). Play(boardViewMode→TilemapIso 인메모리, 저장X): 저장된
> `_TilemapBoard` 로 painted=200 + clearFlags=Solid + size=6.22 깔끔한 iso 보드 렌더(임시객체 아님) — 영속 배선 동작 확정.

- 씬 YAML 에 `_TilemapBoard` + `TilemapMapView` 존재(`grep` fileID 비-0), `BattleBridge` 필드 fileID 비-0.
- Legacy3D 기본 Play: 본 spec 이전과 동일(회귀 0).
- 인스펙터 `boardViewMode`=TilemapIso 로 바꿔 Play(또는 동등 검증): 저장된 `_TilemapBoard`/refs 로 깔끔한 iso 보드 렌더(임시 객체 아님). 스크린샷.
- 커밋에 `BattleScene.unity` 포함(이번엔 내 변경만 — baseline 위 깨끗한 diff).
