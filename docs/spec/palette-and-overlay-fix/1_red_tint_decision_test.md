# 1. Red-Tint Decision Test (in-game)

**전제**: `0_pipeline_sanity_scene.md` 통과. factory + shader path 자체는 정상으로 확인된 상태에서 진행.

## 목적

본 게임 씬에서 zone tint 가 `MapView` 경로를 통해 화면에 도달하는지 확인. Sanity 씬에서 정상이었으므로, 본 게임에서 안 보이면 `MapView._tileTextureMaterials` 캐시 (Bug A) 또는 다른 MapView 측 path 가 막힌 것.

## 변경 대상

- `Assets/_Project/Map/Theme/forest/forest.asset` 의 `placeBaseTint` 한 필드만 임시 빨강으로 변경.

원본:
```yaml
placeBaseTint: {r: 0.9, g: 0.85, b: 0.72, a: 1}
```

실험:
```yaml
placeBaseTint: {r: 1, g: 0, b: 0, a: 1}
```

본 작업 단위 종료 시 **반드시 원본 복귀**.

## 절차

1. forest.asset placeBaseTint 를 (1, 0, 0, 1) 로 변경.
2. Unity Editor → Play (배틀 씬).
3. Place 영역 (배치 가능 슬랩) 색을 관찰.
4. 결과 보고:
   - **A. 모든 Place 면이 빨강 또는 빨강 강하게 섞임** → MapView 경로의 tint path 정상. Bug A 영향 없음.
   - **B. Place 면이 베이지/원래 색 그대로** → MapView 측 path 가 tint 를 무시. Bug A 발화 의심.
   - **C. 일부 Place cell 만 빨강, 나머지 그대로** → Bug A 부분 발화 (캐시 우선 hit 한 자리만).
5. 같이 관측: Place 외곽 흰 fringe 가 보이는지 / inner corner overlay 가 보이는지 / Walk 옆 Place 와 Env 옆 Place 의 외곽 차이 (Bug B 진단 보조).
6. forest.asset 원본 복귀.
7. 결과에 따라 spec 작업 단위 갱신.

## 결과 분기

| 결과 | 다음 작업 |
|---|---|
| A (전부 빨강) | Bug A 폐기. 작업 단위 `2_place_edge_mask_widen.md` (Bug B) + `3_overlay_alpha_tuning.md` (Bug C) 만 spec 화 |
| B (전혀 안 변함) | Bug A 우선. 작업 단위 `2_tile_material_cache_zone_key.md` 로 캐시 키 (zone, texture) 교체. 그 후 다시 본 실험 재돌입 |
| C (부분만 빨강) | Bug A 발화 확인. B 와 동일 fix 후속 |

## 완료 기준

- forest.asset 이 원본 값으로 복귀.
- 결과 (A / B / C) 가 사용자에 의해 보고됨.
- 본 spec README 의 작업 단위 표가 결과에 맞춰 갱신.
- 다음 작업 단위 spec 화 + 사용자 승인 대기.

## 주의

- Sanity 씬 (`0`) 결과가 정상이 아니면 본 작업 진행 금지. 0 의 결과 분기에 따라 추가 진단 먼저.
- forest.asset 의 다른 필드 건드리지 않음.
- Unity MCP 가능하면 빨강 + 원본 두 캡처 첨부.

확인 일자: 2026-04-27 — 결과 A. 모든 Place 슬랩 빨강 확인 (사용자 캡처 스크린샷 2026-04-27 오후 4.16.10.png). MapView tint path 정상, Bug A 영향 없음. forest.asset 원복 완료. 추가 관측: Place 외곽 fringe 거의 안 보임 → Bug B + Bug C 둘 다 영향 가능성. 후속 작업 단위 `2` (Bug C alpha) + `3` (Bug B edge mask) 로 진행.
