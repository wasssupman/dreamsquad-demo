using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // distance-based-range unit 10 PR2 — 유닛의 **사각 몸**. 맥락 = Units:
    // `HitRadius`·`Health`·`FactionTag` 와 같은 「그 유닛이 무엇인가」다.
    // 쓰기는 스폰 1회, 읽기는 Combat(사거리 술어)·Bridge(표기).
    //
    // ⚠ **`defender-footprint` 결정 1(sim 무변)을 여는 컴포넌트다**(사용자 결정 2026-09-01).
    // 그 spec 은 footprint 를 배치·UX 로 한정했는데, 같은 문서 결정 2 가 「추후 타일 판정을
    // 거리 기반 중심으로 확장할 계획」이라 예고했다. 그 확장이 이것이다.
    //
    // 값이 둘인 이유:
    //   · `halfExtent` — 사각의 반폭 `(W−1)/2, (H−1)/2`. **비정사각이 있어 축이 둘**이다
    //     (2×3 → (0.5, 1.0)). 한 숫자로 접으면 3×3 으로 오독하고 가로가 0.5칸 과대평가된다.
    //   · `centerOffset` — **대표 셀이 아니라 앵커** 기준의 몸 중심(`(W−1)/2`).
    //     sim 위치를 기하 중심으로 옮기면 0 이 되지만, 위치와 몸이 갈릴 수 있는 소비처
    //     (표기·프리뷰 등 엔티티가 없는 시점)를 위해 값을 들고 있는다.
    //
    // 1×1 이면 둘 다 0 이고 술어가 종전과 **byte-identical** 이다.
    public struct BodyExtent : IComponentData
    {
        public float2 halfExtent;
        public float2 centerOffset;
    }
}
