// Introduced in spec unit 1 (apply_channels_and_lifecycle) — producer-agnostic StatModifier attachment channel.
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct StatModifierApplyEvent
    {
        public Entity target;
        public StatKind stat;
        public CombineOp op;
        public float magnitude;
        public float duration;
        public Entity source;
        public ushort stackId;       // producer 가 부여, 디폴트 0
        public ModifierOrigin origin; // dreamcatcher-empower-aura unit 1 — 출처 태그(디폴트 Unspecified)
        // dreamcatcher-berserker unit 0 — 누적 상한. >0 이면 같은 키의 재적용이 **덮어쓰기가
        // 아니라 누적**이 된다(magnitude = min(cap, 기존 + 새 값)). 0 = 기존 덮어쓰기 규칙
        // 그대로라 이 필드를 안 싣는 기존 생산자(오라·시너지·존·스택 파생 등)는 무변화다.
        //
        // ⚠ **버프를 지우는 이벤트는 이 값을 실으면 안 된다.** 이 엔진의 회수는 슬롯 삭제가
        // 아니라 «항등값으로 덮어쓰기» 다(BattleBridge.RevokeDreamcatcherEffects). 상한을 실으면
        // min(cap, 기존 + 항등) = 기존 이 되어 **지우기가 조용히 실패한다.**
        public float magnitudeCap;
    }

    public struct StatModifierApplyEventsSingleton : IComponentData
    {
        public NativeQueue<StatModifierApplyEvent> queue;
    }
}
