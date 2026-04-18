using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // Phase 8 §12 — singleton entity holding the event queue MeteorResolutionSystem
    // writes into when a Meteor warning finishes and the AoE actually burns. The
    // MonoBehaviour drain on BattleBridge dequeues each frame so the VfxSpawner
    // can fire the visual burst in world space.
    public struct MeteorBurstEventsSingleton : IComponentData
    {
        public NativeQueue<MeteorBurstEvent> queue;
    }
}
