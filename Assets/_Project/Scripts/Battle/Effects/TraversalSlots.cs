using Unity.Burst;
using Unity.Collections;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    // traversal-layers unit 1b — 통행 슬롯의 순수 계산.
    //
    // 슬롯 하나 = 통행 마스크 하나 = 라우팅 한 벌. 이 클래스는 «그 마스크로 어느 칸을
    // 걸을 수 있나»만 정하고, 장애물 합성·BFS·필드 저장은 호출자가 한다.
    //
    // 순수 함수. plain 값만 받는다.
    [BurstCompile]
    public static class TraversalSlots
    {
        // 슬롯이 하나뿐일 때의 마스크. 현재 라우팅은 `walkMask`(= tiles == Walk)로 굽는데
        // `Walk` 는 `Path` 층을 여므로(`PlacementLayers.Derive`) **두 집합이 같다** —
        // 이것이 unit 1b 가 «행동 변화 0» 인 근거다.
        public const byte DefaultMask = (byte)PlacementLayer.Path;

        // 셀 층 비트 ∩ 슬롯 마스크 → 0/1 walk 마스크.
        //
        //     걸을 수 있다  ⇔  (셀 층 & 슬롯 마스크) != 0
        //
        // 이 spec 의 정의식 그 자체이고, 여기 한 곳에만 있다. 장애물은 포함하지 않는다 —
        // 그건 지형이 아니라 별개 층이고 `NavGrid` 가 합성한다(계약 4).
        public static void FillWalkMask(
            in NativeArray<byte> cellLayers, byte slotMask, NativeArray<byte> outMask)
        {
            for (int i = 0; i < outMask.Length; i++)
                outMask[i] = (byte)((cellLayers[i] & slotMask) != 0 ? 1 : 0);
        }
    }
}
