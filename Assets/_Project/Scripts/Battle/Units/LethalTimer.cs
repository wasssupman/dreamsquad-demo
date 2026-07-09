using Unity.Entities;

namespace Wassup.Battle.Units
{
    // dreamcatcher-content-1 unit 2 (③ 마지막 불꽃) — kamikaze self-destruct timer.
    // Owned by Units because expiry adds DeadTag (생성/소멸/Health = Units domain);
    // placing this in Effects would make an Effects system structurally write a
    // Units component. LethalTimerSystem (Units) ticks it and adds DeadTag on expiry,
    // reusing the existing death path. Attach via BattleBridge on card bind.
    public struct LethalTimer : IComponentData
    {
        public float remaining;
    }
}
