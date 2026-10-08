// salvaged from Assets/_Project/Scripts/Battle/Effects/TraversalSlots.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `NativeArray<byte>` → `byte[]`, `Unity.Burst`·`Unity.Collections` 제거.
namespace Somnia.Battle.BattleCore.Map
{
    // 통행 슬롯의 순수 계산.
    //
    // 슬롯 하나 = 통행 마스크 하나 = 라우팅 한 벌. 이 클래스는 «그 마스크로 어느 칸을
    // 걸을 수 있나»만 정하고, 장애물 합성·BFS·필드 저장은 호출자가 한다.
    public static class TraversalSlots
    {
        // 슬롯이 하나뿐일 때의 마스크.
        //
        // ⚠ **Ground 통행 슬롯은 만들지 않는다**(M23 · 현행 유지). 층이 셋 선언돼 있는데
        // 통행은 둘만 쓴다 — 라이브에 Ground 통행 슬롯이 존재한 적이 없다. 설계인지
        // 디오라마 전환 부작용인지 이력이 없어 재결정 전까지 현행을 그대로 옮긴다.
        public const byte DefaultMask = LayerBits.Path;

        // 셀 층 비트 ∩ 슬롯 마스크 → 0/1 walk 마스크.
        //
        //     걸을 수 있다  ⇔  (셀 층 & 슬롯 마스크) != 0
        //
        // 정의식은 여기 한 곳에만 있다. 장애물은 포함하지 않는다 — 그건 지형이 아니라
        // 별개 층이고 `NavGrid` 가 합성한다.
        public static void FillWalkMask(byte[] cellLayers, byte slotMask, byte[] outMask)
        {
            for (int i = 0; i < outMask.Length; i++)
                outMask[i] = (byte)((cellLayers[i] & slotMask) != 0 ? 1 : 0);
        }
    }
}
