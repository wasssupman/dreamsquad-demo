# 3. Place Edge Mask Widen (Bug B)

**전제**: `2_overlay_alpha_tuning` 적용됨 (alpha 0.55 / 0.7). Place ↔ Env 경계에서 fringe 가 시각 인지 가능. **Place ↔ Walk 경계 fringe 가 누락된 상태가 잔존 문제로 확인됨** (사용자 캡처에서 Place 와 Walk 갈색 path 가 하드 컷).

## 목적

Place edge overlay 가 Env neighbor 한정으로 그려지는 현재 동작을 **모든 zone transition (Walk 포함)** 에 대해 그리도록 확장. 사용자 보고 "결국 풀, 이동경로, 코블스톤 타일이 엣지와 코너가 자연스럽게 이어진다고 보여지나? — 아니다" 의 첫 번째 원인 해소.

## 변경 대상

- `Assets/_Project/Scripts/Core/MapView.cs` line 242.

현재:
```csharp
int edgeMask = visualCell.envNeighborMask;
```

변경:
```csharp
int edgeMask = visualCell.transitionMask;
```

`transitionMask` 는 `BoardVisualPlanBuilder` 가 채우는 4-bit cardinal 필드로, 이 cell 의 zone 과 다른 zone 인 cardinal neighbor 를 비트로 표시. board edge 는 제외 (rev3 계약 유지). Place ↔ Walk + Place ↔ Env 둘 다 비트가 들어옴.

## 절차

1. `MapView.cs` line 242 한 줄 수정.
2. 컴파일 통과 확인 (Unity console error 0).
3. Editor → Play (Battle 씬).
4. 시각 평가:
   - Place 슬랩이 Walk path (갈색) 와 맞닿는 부분에 fringe 가 들어왔는가?
   - Env 옆 fringe 는 그대로 유지되는가?
   - V-004 (edge 가 grid 과강조) 회귀 여부?
5. 결과에 따라 alpha 추가 미세 조정 또는 합격.

## 결과 분기

| 결과 | 다음 |
|---|---|
| Place ↔ Walk fringe 들어옴 + Env 측 유지 + 자연스러움 | **본 spec 종료**. handoff summary 작성 |
| Walk 측 fringe 너무 강함 / 부자연스러움 | placeEdgeOpacity 미세 조정 (예: 0.55 → 0.45) |
| 여전히 부자연스러움 | outer corner 미발화 또는 inner grid 잔존 등 다른 원인. 별도 진단 work unit (`4_outer_corner_diagnose.md` / `5_inner_grid_diagnose.md`) |

## 완료 기준

- `MapView.cs` line 242 가 `transitionMask` 를 사용하도록 수정됨.
- 컴파일 / Play 진입 / Place ↔ Walk fringe 가시 확인.
- inner corner / outer corner / Env 측 fringe / Place 내부 region mesh 는 회귀 없음.
- 사용자 OK.

## 주의

- `transitionMask` 는 BoardVisualPlanBuilder 가 채우는 기존 필드. 새 필드 추가 / 빌더 수정 불필요.
- 본 변경은 **Place 의 edge fringe 만** 영향. inner corner (sameZoneMask 기반), outer corner (shapeClass 기반) 는 무관.
- 기존 `envNeighborMask` 필드는 다른 용도로 남겨두거나, 아무도 안 쓰게 되면 별도 정리. 본 spec scope 밖.
