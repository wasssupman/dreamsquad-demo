# Background Props Spec

**작성일**: 2026-04-22  
**상태**: v1 1차 구현 진행. 기본 프리팹 prototype, theme 이미지 매칭, footprint placement, runtime instantiate 연결까지 구현. Play smoke 는 아직 미확인.
**목표**: 맵 생성 후 배경 타일 영역에 1x1, 2x1, 1x2, 2x2 등 X * Y footprint 프랍을 자동 배치하고, 동일한 `PropData`/prefab 구조를 맵 외곽 장식용 프랍에도 사용할 수 있게 한다.

## 요구사항 충족 현황

| 요구사항 | 현재 문서/구현 상태 |
|---|---|
| 맵 배경 타일 위 오브젝트 배치 | `BackgroundPropPlacer` + `MapView.InstantiateBackgroundProps` 구현 |
| 1x1, 2x1 등 X * Y 크기 지원 | `PropData.footprintX/Y` 기반 `CanFit` / occupancy 검증 구현 |
| `_Project/Data` 에 각 프랍 SO 관리 | prototype 경로 유지, 최종 경로 `Assets/_Project/Data/Theme/{themeName}/` 문서화 및 generator 매칭 구현 |
| Sprite/Spine 프랍을 billboard prefab 화 | prototype 구현 존재 |
| 디자이너가 주변 꾸미기용 단순 prefab 배치 | prototype prefab 수동 배치 가능. decor placement 데이터화는 미구현 |
| Data/Theme SO 와 Art/Theme PNG 이름 매칭 | `PropDataEditor` 에 theme PNG 매칭 구현 |
| 맵 생성 후 배경 타일 영역 순회 + footprint 적합 프랍 필터 + 랜덤 배치 | `BackgroundPropPlacer.Generate` 구현 |

## 구현 문서 목록

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_prop_data.md` | Data | `PropData` 필드, naming, footprint 계약 |
| `1_prop_billboard.md` | Runtime Prefab | Sprite/Spine billboard prefab 계약 |
| `2_prop_data_editor.md` | Editor Generator | `PropData -> prefab` 생성 버튼과 이미지 매칭 |
| `3_prototype_sample.md` | Prototype | 현재 존재하는 `prop_prototype_1_1` 기본 프리팹 |
| `4_theme_asset_layout.md` | Asset Pipeline | `Data/Theme` SO 와 `Art/Theme` PNG 매칭 규칙 |
| `5_footprint_placement_algorithm.md` | Placement | 배경 타일 영역 순회, footprint 적합성 검사, 랜덤 배치 알고리즘 |
| `6_runtime_instantiation.md` | Runtime Integration | placement 결과를 prefab instantiate 로 연결하는 구조 |
| `7_implementation_plan.md` | Plan | 실제 구현 순서와 완료 기준 |
| `8_handoff_summary.md` | Handoff | 현재 prototype 상태와 미구현 범위 |

## 전체 기능 흐름

```text
Theme 선택
  -> Assets/_Project/Data/Theme/{themeName}/prop_{name}_{x}_{y}.asset 로 PropData 로드
  -> Assets/_Project/Art/Theme/{themeName}/prop_{name}_{x}_{y}.png 와 이름 매칭
  -> PropData Generator 로 billboard prefab 생성/갱신
  -> 맵 생성
  -> 배경 타일 영역 후보 순회
  -> 각 후보 좌표에서 들어갈 수 있는 footprint 프랍 필터
  -> 룰 기반 선택(v1: seeded random)
  -> occupancy 기록 + PropPlacement 생성
  -> MapView/PropSpawner 에서 PropData.prefab instantiate
```

## 공통 원칙

- `PropData` 는 프랍 1종의 source of truth 이다.
- `PropData.name` 은 asset/prefab/image 매칭의 기본 키다.
- Unity 자산 확장자는 실제로 `.asset` 이며, 문서에서 말하는 SO 는 `ScriptableObject asset` 을 뜻한다.
- 권장 basename 은 `prop_{name}_{x}_{y}` 이다. `{x}` 와 `{y}` 는 `footprintX/Y` 와 일치해야 한다.
- Data 경로와 Art 경로는 같은 themeName 과 같은 basename 으로 매칭한다.
- footprint 기준점은 좌하단 셀이다. 프랍은 `(x, y)` 좌하단 셀에서 +X/+Y 방향으로 영역을 차지한다.
- 배경 타일 영역에 배치되는 Tile Prop 은 footprint 안의 모든 셀이 허용 타일이어야 한다.
- 같은 셀은 둘 이상의 Tile Prop 이 점유할 수 없다.
- v1 선택 룰은 seeded random 이다. 같은 seed, theme, prop set, map 이면 같은 placement 결과가 나와야 한다.
- Walk/path 타일 침범은 기본 금지다.
- 맵 외곽 Decor Prop 은 같은 prefab 구조를 쓰지만 tile occupancy 와 분리한다.

## 핵심 결정

- 초기 구현은 “맵 생성 후 후처리 배치”로 한다. 즉, 맵 타일을 만든 뒤 배경 타일 영역을 순회하면서 프랍 placement 를 만든다.
- 프랍 배치가 맵 경로 생성에 영향을 주지 않는 형태로 시작한다. 경로/배치 충돌을 줄이기 위해 허용 타일을 배경/장식 타일로 제한한다.
- `MapThemeData.obstaclePrefabs` 는 즉시 제거하지 않는다. v1 에서 `PropData[] tileProps`, `PropData[] decorProps` 를 추가하고, 기존 obstacle 은 호환 또는 migration 대상으로 둔다.
- Generator 는 기본 prefab 생성까지만 담당한다. 어떤 타일에 배치할지는 placement 알고리즘 책임이다.

## 완료 확인

- [x] v0 기본 프리팹 prototype 생성 (`prop_prototype_1_1`)
- [x] Sprite/Texture 기반 `PropData -> billboard prefab` generator prototype
- [x] Theme 경로 기반 `PropData`/PNG 매칭
- [x] `MapThemeData.tileProps / decorProps` 추가
- [x] 배경 타일 영역 순회 + footprint 적합성 검사
- [x] seeded random placement
- [x] placement 결과 runtime instantiate
- [x] 1x1, 2x1, 1x2, 2x2 test coverage
- [ ] Play smoke: 생성 맵에 background prop 자동 배치 확인
