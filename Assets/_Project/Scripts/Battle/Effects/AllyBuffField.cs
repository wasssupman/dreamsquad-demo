using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // active-ally-zone unit 0 — 액티브(공용 드림캐쳐) 아군 버프 장판.
    //
    // 캐리어 엔티티다(TornadoField/PortalLink 패턴): 컴포넌트가 곧 장판이고 수명이 끝나면
    // 엔티티째 파괴된다(EffectTickSystem). 멤버십은 **스냅샷이 아니다** —
    // AllyBuffFieldSystem 이 매 프레임 안에 있는 방어 유닛에게 짧은 모디파이어를 재발행하므로
    // 이탈·만료·사망이 전부 자연 소멸로 처리된다(revoke 프리미티브 불요).
    //
    // 중심을 월드가 아니라 **셀**로 든다 — 멤버십 판정 상대가 DefenderTile.cell(int2)이라
    // 월드↔셀 변환을 매 프레임 반복할 이유가 없다.
    public struct AllyBuffField : IComponentData
    {
        // modifier 슬롯 네임스페이스: on-place=0 · 시너지=1 · 효과타일=2 · **스킬 아군 버프=3** ·
        // 드림캐쳐=100+. 전용 슬롯이라 배치 오라(0)와 합산되고, 같은 장판의 반복 갱신은 refresh.
        public const ushort StackId = 3;

        public int2     centerCell;
        public int      tileRange;
        public StatKind stat;
        // 배율 그대로(×2.0). op/magnitude 분류는 ModifierAuthoring.FromMultiplier 가 단독 소유.
        public float    magnitude;
        public float    remaining;
    }
}
