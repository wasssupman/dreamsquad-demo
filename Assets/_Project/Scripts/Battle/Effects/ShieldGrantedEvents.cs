using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // shield-guardian-defender unit 4 — Effects→Presentation 실드 부여 원샷 시그널.
    // ShieldCastSystem 이 실드를 부여한 대상 위치마다 1건 enqueue, BattleBridge 가
    // drain 해 VfxSpawner.SpawnShieldGranted 로 단발 VFX 를 띄운다.
    // payload = sim 좌표 위치 값 타입만 (managed/scene 참조 금지 — VFX 스킬 계약).
    public struct ShieldGrantedEvent
    {
        public float3 position;
    }

    // Queue owned by BattleBridge (기존 NativeQueue 싱글턴 lifecycle 패턴).
    public struct ShieldGrantedEventsSingleton : IComponentData
    {
        public NativeQueue<ShieldGrantedEvent> queue;
    }
}
