using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct StackModifierSlot : IBufferElementData
    {
        public ModifierHeader header;   // remaining = perAppDuration 까지 남은 시간 (S1)
        public StackKind kind;
        public byte stackCount;
        public byte maxStack;
        public byte lastTriggeredStack; // edge 검출 캐시 (4번 단위에서 사용)
    }
}
