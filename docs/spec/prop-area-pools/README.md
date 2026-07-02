# prop-area-pools — 근경/원경 프랍 풀 분리

상태: 완료 2026-07-02 (units 0~3, Play 검증 PASS)

## 문제

배경 프랍 배치가 단일 풀 `MapThemeData.tileProps` (`PropData[]`) 하나를 근경(플레이 영역)·원경(외곽 링)이 **둘 다** 순회한다. 영역 구분은 `PropData` 에셋에 박힌 opt-out 플래그(`excludeFromDistantRing`, `distantRingWeight`)로만 이뤄진다. 결과:

- 플레이 전용으로 의도한 프랍이 원경에도 등장 (역방향 opt-out 부재)
- 가중치가 프랍 에셋에 고정 → 테마별/영역별 인스펙터 조절 불가
- "가용 목록"이 명시 리스트가 아니라 *전체 − 제외 플래그*의 암묵 형태

## 목표

근경·원경을 **각각의 명시적 가중치 리스트**로 분리한다. 인스펙터에서 영역별로 어떤 프랍이 어느 가중치로 등장할지 직접 authoring 한다.

## feature-wide 계약

- `MapThemeData` 는 프랍 풀을 **두 리스트로 소유**한다: `playAreaProps`(근경), `distantRingProps`(원경). 둘 다 `WeightedProp[]`.
- `WeightedProp = { PropData prop; float weight }`. weight 는 룰렛 base weight. `weight <= 0` 또는 `prop == null` / `prop.prefab == null` 항목은 등장하지 않는다.
- 같은 `PropData` 를 양쪽에 다른 weight 로 등록 가능. 한쪽에만 넣으면 그 영역 전용.
- **근경**(`BackgroundPropPlacer`)은 `playAreaProps` 만, **원경**(`TilemapMapView.InstantiateRingProps`)은 `distantRingProps` 만 순회한다. 교차 참조 없음.
- `PropPlacement.propIndex` 는 `playAreaProps` 인덱스다 (근경 전용 경로). 원경은 propIndex 를 영속화하지 않고 즉시 인스턴스화.
- 공유 유지: `OccludesPlay` occlusion 판정, `RingDistance` falloff. 이 둘은 영역 무관 기하 판정이라 그대로 둔다.
- `PropData` 의 배치 소스 필드(`placementWeight` 는 검토, `distantRingWeight`·`excludeFromDistantRing` 는 제거)와 `TilemapMapView.RingWeight` 헬퍼는 이관 완료 후 retire.

## 작업 단위

| # | 문서 | 작업 | 완료 기준 |
|---|---|---|---|
| 0 | `0_data_model_and_migration.md` | `WeightedProp` + `playAreaProps`/`distantRingProps` 추가, `tileProps` 임시 유지, 일회성 에디터 마이그레이션 | compile + forest/desert 에셋 두 리스트 populate 확인 |
| 1 | `1_near_placer_switch.md` | 근경 placer(`BackgroundPropPlacer` + `MapView`/`TilemapMapView` 인스턴스화 + `BattleBridge` 가드)를 `playAreaProps` 로 이관, EditMode 테스트 갱신 | `BackgroundPropPlacerTests` green |
| 2 | `2_ring_placer_switch.md` | 원경 `InstantiateRingProps` 를 `distantRingProps` 로 이관 | Play→스크린샷: 전용 프랍 영역 격리 확인 |
| 3 | `3_cleanup.md` | `tileProps` 필드 + PropData 잔여 필드 + `RingWeight` + 마이그레이션 코드 제거, `placementWeight` 처리 결정 | compile + 재검증, dead ref 0 |

## 검증 질문

> "playAreaProps 에만 넣은 프랍이 원경에 안 나타나고, distantRingProps 전용 프랍이 플레이 영역에 안 나타나는가?"

배경/프랍 변경은 Play→게임뷰 스크린샷 육안 검증 필수 (memory: `feedback_background_screenshot_verify`).

## 후속 후보

- 영역별 밀도/falloff 를 리스트 단위로 이관 (현재 `tilePropDensity`/`ringPropDensity` 는 테마 전역 유지)
- 카테고리 회피(`sameCategoryMinDistanceCells`)를 원경에도 적용 (현재 근경 전용)
