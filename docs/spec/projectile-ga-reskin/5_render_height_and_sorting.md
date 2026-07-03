# 투사체 렌더링 — 높이 오프셋 + sorting order

**작업 구분**: 5

## 목적

플레이 피드백 2건 해결:
1. 투사체가 타일에 깔림 → 시각을 타일 위로 띄우는 높이 오프셋.
2. 투사체가 적 근접 시 적 스프라이트 뒤로 깔림 → sorting order 를 유닛 위로.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs` (`visualHeightOffset` 필드)
- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (오프셋 적용 + sorting)
- Modify: `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` (`ProjectileOffset` 상수)

## 구현

### 1) 높이 오프셋 (visualHeightOffset)

- `ProjectileData.visualHeightOffset`(기본 0 → 기존 투사체 불변).
- ViewPool 이 **렌더 Y 에만** 더한다(Spawn 초기 위치 + SyncTransforms). ECS 위치·velocity·AlongVelocity 회전엔 미반영(순수 시각 부양).
- `ProjectileViewState.heightOffset` 로 스폰 시 캐시. SyncTransforms 의 state 재구성 시 반드시 보존(안 하면 첫 프레임 후 0 리셋).
- `lastPosition` 은 오프셋 미포함(velocity 정확).

### 2) sorting order (ProjectileOffset)

- 원인: 프로젝트 sorting layer 는 `Default` 하나. 유닛은 `SpineUnitView` 가 위치 기반 동적 order(`BoardSortOrder.Compute + CharacterOffset`, 수백대) 세팅. 투사체는 ViewPool 이 order 를 안 건드려 프리팹 기본 0~2 고정 → 겹치면 유닛이 위.
- `BoardSortOrder.ProjectileOffset = 1000`(유닛 최대 order 위, 데미지 숫자 32000·UI 아래).
- `ViewPool.GetOrCreate`(Instantiate 당 1회, 풀 재사용 스킵 → 누적 없음)에서 뷰의 전 렌더러 `sortingOrder += ProjectileOffset`. 렌더러 간 상대 순서(mesh/trail/flare) 보존.

## 완료 기준

- 투사체가 타일 위로 떠서 안 깔림(높이). 값은 GA SO 에 `visualHeightOffset=0.7` 적용(유닛 5 튜닝 대상).
- 투사체가 적 근접·명중 시 적 위로 렌더(sorting).
- 기존 투사체(offset 0, sorting 은 전역 적용) 회귀 없음.
- compile 클린. **실플레이 육안 확인 대기.**
