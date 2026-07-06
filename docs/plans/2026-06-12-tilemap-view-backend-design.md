# Tilemap View Backend — Design (thin)

**작성일**: 2026-06-12
**spec**: `docs/spec/tilemap-view-backend/`

## 목표

Unity Tilemap 을 **뷰 백엔드**로 도입해, 타일 에셋 교체(TileSetData SO swap)와 보드 레이아웃(Rectangle / Isometric) 실험을 빠르게 반복할 수 있는 프레임웍을 만든다.

## 아키텍처 요약

```
MapDocument / GeneratedMap / FlowFieldSingleton / GridMath   ← 시뮬레이션 (불변)
        │ (sim 공간 = 현행 rect XZ 월드)
   BoardSpace (sim ↔ view 변환의 유일 지점, Legacy 모드 = identity)
        │
Legacy3D: MapView (기존 region mesh)        ← 기존 경로 그대로 보존
TilemapRect / TilemapIso: TilemapMapView    ← Grid + Tilemap, XY 평면 + ortho 카메라
```

- Tilemap 은 source of truth 가 아니다. Tilemap 셀 상태로 게임 로직 판정 금지.
- ECS 코드 무변경. 변환은 프레젠테이션 write(~10곳)와 입력 read(2곳)에서만.
- Hexagonal 은 인접성/경로 계약이 달라 범위 밖 (후속 후보).

## 결정 근거 (대화 요약)

- Tilemap 은 잡/Burst 에서 직접 소비 불가 → 시뮬레이션 대체가 아니라 뷰/저작 계층으로 한정.
- matchSeed 결정론 계약(`match-seed-unification`)과 `GeneratedMap` 런타임 모델은 보존.
- iso 는 논리적으로 동일한 정수 사각 그리드의 렌더 변환 → 논리 계층 무변경으로 토글 가능.

상세 계약과 작업 단위는 spec README 참조.
