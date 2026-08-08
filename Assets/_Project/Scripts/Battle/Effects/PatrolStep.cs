using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // summon-patrol-defender unit 1 — 이번 틱의 자기주도 이동 방향(단위 cardinal 또는 zero).
    //
    // Effects 소유. writer = PatrolFieldSystem 단독, MovementSystem 은 RO 소비 —
    // AggroChaseCell(Effects 가 굽고 Movement 가 하강)과 같은 관계다.
    //
    // AggroChaseCell 처럼 dist 버퍼를 실어 나르지 않고 **방향 하나로 접는** 이유:
    // 어그로는 목적지(가디언)가 정적이라 획득 시 1회 계산해 두면 되지만, 순찰병의
    // 목적지는 움직이는 적이라 필드가 매 틱 무효가 된다. 매 틱 굽는 필드를 피해자당
    // grid 크기 버퍼로 들고 있는 건 순수한 낭비다.
    public struct PatrolStep : IComponentData
    {
        public float2 dir;
    }
}
