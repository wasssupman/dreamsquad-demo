# MapGenerationSettings SO

**작업 구분**: Phase 10A

## 목적

맵 생성 파라미터 (그리드 크기, seed) 를 ScriptableObject 로 관리. 사용자 bullet 2 "X×Y 유동, 기본 20×20" 을 반영. 하드코딩 20×10 제거의 토대.

## 변경 대상

- 새 파일: `Assets/_Project/Scripts/Data/MapGenerationSettings.cs`
- 새 asset: `Assets/_Project/Data/MapGenerationSettings.asset`

## 구현

```csharp
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "MapGenerationSettings", menuName = "Wassup/MapGenerationSettings")]
    public class MapGenerationSettings : ScriptableObject
    {
        [Header("Grid")]
        [Min(4)] public int gridWidth  = 20;
        [Min(4)] public int gridHeight = 20;

        [Header("Seed")]
        [Tooltip("0 이면 매 판 System.DateTime.Now.Ticks 기반 새 seed. 고정값이면 재현 가능 매 판 동일 맵.")]
        public int defaultSeed = 0;

        [Header("Generator")]
        [Tooltip("알고리즘/상수 변경 시 수동 증가. 버그 재현 로그에 포함.")]
        public int generatorVersion = 1;

        public int EffectiveSeed => defaultSeed != 0 ? defaultSeed : (int)(System.DateTime.Now.Ticks & int.MaxValue);
    }
}
```

## 사용 규약

- `BattleBridge` 가 판 시작 시 `MapGenerationSettings` SerializeField 참조 → `EffectiveSeed` 얻음
- 개발/테스트 중 defaultSeed 를 nonzero 로 두면 고정 맵 재현 가능
- 프로덕션 기본값: defaultSeed = 0 (랜덤)

## 완료 기준

- `MapGenerationSettings.cs` 컴파일 + asset 생성.
- Inspector 에서 gridWidth/Height 조정 가능, Min 4 제약 동작.
- EffectiveSeed: defaultSeed=0 → 매 호출 다른 값 / defaultSeed=N → 항상 N 반환.
- BattleBridge scene 에 SerializeField 연결 (와이어링은 task 4 에서).
