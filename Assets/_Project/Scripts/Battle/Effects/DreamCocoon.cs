using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // subconscious-curse-expansion unit 0 (호접몽) — 잠 완주 감시 상태. 부착 시
    // bridge 가 Sleep(duration)과 함께 부여한다(bridge 의 부착 시점 컴포넌트 쓰기는
    // LethalTimer/CcEffect 선례). DreamCocoonSystem 이 소비: 피격 wake 로 Sleep 이
    // 사라지면 파탄(버프 없음), remaining 완주 시 self 영구 스탯버프
    // (stat×mult, origin=Dreamcatcher)를 부여하고 소멸한다.
    public struct DreamCocoon : IComponentData
    {
        // 완주 판정 타이머. bake 가 잠 duration − Epsilon 으로 설정 — 완주 프레임이
        // CcDecay 자연만료 프레임과 겹치지 않게 하는 보조 안전핀(실제 파탄/완주
        // disambiguator 는 시스템의 remaining>0 가드 + 시스템 순서 핀).
        public float remaining;
        public StatKind stat;
        // 완주 버프 배율(예 1.35 = +35%). 발화 시 FromMultiplier 로 op/mag 분해
        // (HealthThreshold last_stand 선례 — +% 는 Additive 버킷).
        public float mult;
        // StatModifier 네임스페이스의 단일 할당자(_dcStackCounter)에서 부여.
        public ushort stackId;

        // 내부 상수 — 튜닝 노브가 아니다(제약 6 대상 아님). bake 가
        // duration <= Epsilon 을 거절하므로 remaining 은 항상 양수로 시작한다.
        public const float Epsilon = 0.05f;
    }
}
