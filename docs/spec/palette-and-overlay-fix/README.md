# Palette And Overlay Fix Spec

**작성일**: 2026-04-27
**상태**: **완료 2026-04-27**. 사용자 시각 합격. 인계는 `5_handoff_summary.md` 참조.
**근거 인계 문서**: `docs/spec/board-visualization/29_final_handoff.md`, `docs/spec/board-visualization/audit/VISUAL_AUDIT.md` V-007 갱신본.

## 목적

board-visualization rev4 의 22 palette pass (커밋 `6c88007`) 코드는 들어갔으나 화면에 zone tint 와 Place edge / inner corner / outer corner overlay 가 반영되지 않는 문제를 결정 실험 기반으로 좁혀 fix.

## 비목적

- board-visualization 컨셉 변경 (rev4 의 보드게임 타일 컨셉 유지).
- `RuntimeMaterialFactory` 의 URP transparent surface mode / DOTS_INSTANCING / overlay sortingOrder — 사후 트레이스에서 무관 판명. **건드리지 않는다.**
- prop 분포 (V-001) — 별도 spec.
- 맵 생성기 재설계 — 별도 결정 필요.

## 작업 단위

| 번호 | 파일 | 목적 | 상태 |
|---|---|---|---|
| 0 | `0_pipeline_sanity_scene.md` | 격리된 새 씬에서 `RuntimeMaterialFactory` + URP/Tile_Unlit 셰이더 path 단독 검증 (`PaletteSanityProbe.cs`). 6 quad 시각 비교 | **통과 2026-04-27** |
| 1 | `1_red_tint_decision_test.md` | 본 게임 씬에서 forest.asset placeBaseTint = (1,0,0,1) 로 MapView 경로 검증. Place 빨개짐으로 Bug A 발화 확정 | **결과 A 확인 2026-04-27 — Bug A 폐기** |
| 2 | `2_overlay_alpha_tuning.md` | Bug C — `placeEdgeOpacity` 0.25→0.55, `placeOuterCornerOpacity` 0.42→0.7. inner 는 0.62 유지 | **완료 2026-04-27** |
| 3 | `3_place_edge_mask_widen.md` | Bug B — `MapView.cs` line 242 `envNeighborMask` → `transitionMask`. Place ↔ Walk 경계도 fringe | **완료 2026-04-27** |
| 4 | `4_region_uv_continuity.md` | region 내부 셀 격자선 제거. `CreateTiledSurfaceMesh` single-quad refactor 후 row seam 잔존 → `BuildRegionSurfaceMesh` per-region single mesh 로 escalate | **완료 2026-04-27** |
| 5 | `5_handoff_summary.md` | 종료 인계 | 작성됨 |

## 공통 계약

- board-visualization 의 baseline (`6c88007` 시점) 그대로 유지. rev4 컨셉 (보드게임 타일, 격자 수용) 도 유지.
- 결정 실험 (`0_`) 의 결과는 사용자 Play 캡처. Unity MCP unavailable 이면 사용자에게 사진 의뢰.
- 후속 작업 단위는 0 의 결과 확정 후에만 spec 화. 추측 의심선을 spec 에 박지 않는다 (board-visualization 의 가장 비싼 교훈).
- `RuntimeMaterialFactory` 와 `Tile_Unlit.shader` 는 **수정 금지**. 이미 정상.

## 후속 후보 (현 spec 범위 밖)

- `BoardSortOrder` 를 overlay quad 에 부여 (보험성). 우선순위 매우 낮음.
- 22 palette pass 의 톤 값 자체를 다시 평가해 더 어두운 톤 / 더 따뜻한 톤으로 가는 컨셉 튜닝.
- volcano theme 채움 (`docs/spec/board-visualization/` 의 23 후보 그대로).
- `BattleBridge.StartBattle` 반복 시 Persistent leak 추적 (별도 spec).
