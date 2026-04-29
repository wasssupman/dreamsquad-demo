# BlockingHazardSO + Rock_3x3 Sample

**작업 구분**: 6

## 목적

차단형 hazard 의 ScriptableObject 정의 + 샘플 1종 (`Hazard_Rock_3x3`) 작성. path-zone-hazards 의 `HazardSO` 와 평행한 패턴.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/BlockingHazardSO.cs`
- Add: `Assets/_Project/Data/Hazards/Hazard_Rock_3x3.asset` (ScriptableObject instance)
- Add: `Assets/_Project/Prefabs/Hazards/BlockingHazard_Placeholder.prefab` (placeholder visual cube)

## 구현

### BlockingHazardSO.cs

```csharp
using UnityEngine;

namespace Wassup.Battle.Effects
{
    [CreateAssetMenu(menuName = "Wassup/Hazards/Blocking Hazard SO", fileName = "Hazard_Blocking_New")]
    public class BlockingHazardSO : ScriptableObject
    {
        [Header("Visual")]
        [Tooltip("Spawned by BattleBridge as the visual representation. Self-managed lifetime via BlockingHazardPresenter.")]
        public GameObject visualPrefab;

        [Header("Shape")]
        [Tooltip("Cell shape sampled at spawn. Reuses HazardShapeSampler from path-zone-hazards.")]
        public HazardShape shape = HazardShape.Square3x3;

        [Header("Combat")]
        [Min(1f)]
        public float maxHp = 100f;

        [Header("Destruction VFX")]
        [Tooltip("Optional. If set, BattleBridge spawns this on destruction. Lifetime self-managed.")]
        public GameObject destructionVfxPrefab;

        // 후속 후보: tauntRadius, onDestroyHazardSO (composition), counterAttack 등
    }
}
```

### Hazard_Rock_3x3 asset

Inspector 값:
- visualPrefab: `BlockingHazard_Placeholder` (3×3 cube cluster)
- shape: `Square3x3`
- maxHp: 100f
- destructionVfxPrefab: null (Unit 8 에서 placeholder 또는 후속)

### BlockingHazard_Placeholder prefab

placeholder visual:
- 부모 GameObject + 9개 unit cube child (3×3 격자, 셀 크기 = 1f)
- 또는 단일 큰 cube (scale = 3,1,3) 으로 단순화 (멀티셀 점유는 ECS 에서, visual 은 단일 mesh)
- material: 회색 또는 갈색 (Rock 느낌)
- `BlockingHazardPresenter` MonoBehaviour 부착 (Unit 8 에서 정의)

## 단위 테스트 (EditMode)

없음 — SO 데이터만. spawn 통합은 Unit 7 에서.

## 완료 기준

- 컴파일 성공.
- Hazard_Rock_3x3 asset 가 Inspector 에서 정상 표시.
- placeholder prefab 가 Editor 에서 정상 렌더 (3×3 영역 시각).
- 기존 테스트 회귀 0.
- 콘솔 에러/경고 0.

검증: 2026-04-29 — `BlockingHazardSO`, `Hazard_Rock_3x3.asset`, `BlockingHazard_Placeholder.prefab`, placeholder material 생성. 컴파일 성공. 커밋 미작성.
