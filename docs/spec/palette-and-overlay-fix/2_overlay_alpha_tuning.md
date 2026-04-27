# 2. Overlay Alpha Tuning (Bug C)

**전제**: `0_pipeline_sanity_scene` 통과, `1_red_tint_decision_test` 결과 A 확인 (MapView tint path 정상). 본 작업은 화면에서 fringe / corner overlay 가 안 보이는 원인 중 alpha 부분을 다룸.

## 목적

`forest.asset` 의 `placeEdgeOpacity` / `placeOuterCornerOpacity` / `placeInnerCornerOpacity` 가 너무 낮아 베이지/녹색 텍스처 위에 흰 fringe 가 시각 인지 한계 미만으로 사라지는 문제 해소. **단일 asset 편집** 이며 코드 변경 없음. 시각 검증 후 적정값 확정.

## 변경 대상

- `Assets/_Project/Map/Theme/forest/forest.asset` 의 3 개 alpha 필드.

현재 값 (확인됨):
```yaml
placeInnerCornerOpacity: 0.62
placeOuterCornerOpacity: 0.42
placeEdgeOpacity: 0.25
```

`placeInnerCornerOpacity` 는 이미 0.62 로 충분히 높음. 그런데도 사용자 보고에서 inner corner overlay 가 안 보였다면 alpha 가 원인이 아님 (mask 가 안 맞거나 inner corner cell 자체가 맵에 적음). **inner 는 그대로 두고**, edge / outer 만 올린다.

목표 값 (1차 시도):
```yaml
placeInnerCornerOpacity: 0.62  # 유지
placeOuterCornerOpacity: 0.7
placeEdgeOpacity: 0.55
```

이 값이 너무 강하면 V-004 (edge fringe 가 grid 강조) 회귀 위험. 사용자 시각 평가 후 0.45~0.7 범위에서 조정.

## 절차

1. forest.asset 의 세 alpha 필드를 위 1차 시도 값으로 수정.
2. Editor → Play (Battle 씬).
3. Place 영역 외곽 + L자 inner corner + outer corner 시각 평가:
   - Env 옆 Place 외곽에 흰 fringe 가 인지 가능한 굵기/밝기로 보이는가?
   - Place L자 안쪽에 inner corner overlay (작은 회전 사각) 가 보이는가?
   - Outer corner (Place 끝나는 모서리) 가 다른 sprite 로 보이는가?
4. 결과에 따라 값 조정 (0.45~0.7 범위) 또는 합격 결정.

## 결과 분기

| 관찰 | 판정 | 다음 |
|---|---|---|
| Env-adjacent Place 외곽에 fringe 가 명확히 보임 | Bug C 해소 | 작업 단위 `3` (Bug B edge mask 확대) 로 Walk-adjacent 측 fringe 보강 |
| 모든 Place 외곽에 fringe 가 안 보임 (Env 옆도) | alpha 만으로는 불충분. mask 또는 텍스처 자체 의심. `3` 단계에서 mask 확대 후 재평가. 그래도 안 보이면 텍스처 alpha 채널 자체 문제 의심 | 0d_overlay_texture_alpha_diagnose 추가 |
| fringe 너무 강해 grid 가 강조됨 | V-004 회귀. 값 0.4~0.5 사이 재시도 | 본 작업 단위 안에서 반복 |

## 완료 기준

- forest.asset 의 세 alpha 필드가 새 값으로 저장됨.
- Play 시 fringe / corner overlay 가 시각 인지 가능 + V-004 회귀 없음 둘 다 만족.
- 사용자 OK + 캡처 첨부.
- 본 spec README 의 작업 단위 표 갱신.

## 주의

- alpha 만 변경. 다른 필드, 텍스처, 코드 건드리지 않음.
- 적정값 결정은 사용자 시각 평가에 위임. 1 회 시도로 안 맞으면 같은 작업 단위 안에서 재조정.
