using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-onsen unit 2 [ECS] — 온천 "열기" 누적 타이머+카운터 (Effects 소유).
    // HeatAccrualSystem 이 기믹 활성 시 모든 유닛(아군+적)에 lazy-attach — 스폰 경로 무수정
    // (FatigueAccrual 전례). elapsed 가 heatInterval 을 넘을 때마다 stacks++ 후 HeatMath.Delta
    // 로 회복/손실을 힐/피해 채널에 append. stacks 는 heatMaxStack 에서 멈춘다.
    public struct HeatAccrual : IComponentData
    {
        public float elapsed;
        public byte stacks;
    }
}
