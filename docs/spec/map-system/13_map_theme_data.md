# MapThemeData SO

**작업 구분**: Phase 10B

## 목적

테마별 배경 오브젝트 prefab 목록과 밀도 파라미터를 보관. Codex 축소 권고대로 **2필드만** (prefab 목록 + 최소 Place 비율). multi-cell footprint / rotate / weight 는 Phase 11+ 이관.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/MapThemeData.cs`
- 새 asset: `Assets/_Project/Map/Theme/forest/forest.asset`
- 새 폴더: `Assets/_Project/Map/Theme/forest/` (prefab 추가는 task 14)

## 구현

```csharp
using UnityEngine;

namespace Wassup.Data
{
    // H-2 fix: themeId 필드 제거 (scope 축소 "2필드만" 원칙 준수)
    // 테마 식별은 asset 파일명 + 폴더 경로로 충분 (Phase 10B v1 = forest 1개)
    [CreateAssetMenu(fileName = "MapThemeData", menuName = "Wassup/MapThemeData")]
    public class MapThemeData : ScriptableObject
    {
        [Header("Obstacle Prefabs (single-cell)")]
        [Tooltip("Place → Deco 전환된 타일에 배치. 각 타일당 prefab 1개 랜덤 선택.")]
        public GameObject[] obstaclePrefabs;

        [Header("Density")]
        [Range(0.2f, 0.6f)]
        [Tooltip("전체 Place 타일 중 obstacle 로 전환 후에도 유지할 최소 비율 (defender 배치 공간).")]
        public float minPlaceableRatio = 0.4f;
    }
}
```

## Phase 11+ 확장 예약

Phase 10B 미구현 (명시적 제외):
- `int2 footprint`, `bool canRotate`, `int weight` — multi-cell obstacle 시스템
- `SpawnGoalConstraint` — spawn/goal 위치 theme 제약
- `PathConstraints` — 경로 길이/커브 수 theme 제약
- `int2 gridSize` — theme 별 고정 크기 (v1 은 MapGenerationSettings 의 값 사용)
- obstacleDensityPct — Place-ratio 대안. Phase 10B 는 단일 `minPlaceableRatio` 로 축소
- `spawnRule` / `goalRule` — spawn/goal 배치 규칙 (v1 은 `ProceduralMapGenerator.DecideSpawnsAndGoal` 의 하드코드)

## Forest theme v1 구성 (task 14 에서 실제 생성)

- 파일 경로: `Assets/_Project/Map/Theme/forest/forest.asset`
- obstaclePrefabs: 3~4개 단일 셀 prefab (나무/바위/덤불)
- minPlaceableRatio: 0.4

## 완료 기준

- `MapThemeData.cs` 컴파일.
- `Assets/_Project/Map/Theme/forest/forest.asset` 생성 (obstaclePrefabs 는 task 14 에서 채움).
- Inspector 에서 Range slider 동작.
- `ProceduralMapGenerator.Generate` 가 `MapThemeData` 파라미터 받는 구조 (task 11 에 이미 반영).
