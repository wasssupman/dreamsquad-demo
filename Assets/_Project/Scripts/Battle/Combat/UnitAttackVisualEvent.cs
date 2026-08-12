using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // ECS→MonoBehaviour visual trigger for any attacker (defender or enemy) firing
    // an attack. SpineUnitPool consumes this to play attack animation + face the
    // target. Defender-specific side effects (cast VFX, attack VFX prefab) are
    // applied in BattleBridge by checking whether the attacker has DefenderUnitData.
    public struct UnitAttackVisualEvent
    {
        public Entity attacker;
        public float3 targetWorld;
        // attack-anim-speed-match — 이번 공격의 **실제 발사 주기**(초) = max(cooldownDuration/attackSpeedMul,
        // hitDelaySec). 뷰가 공격 애니를 이 주기에 맞춰 압축 재생(compress-to-fit)한다. hitDelay 가 다음
        // START 를 막으므로(AttackSystem) 애니가 실발사보다 빨라지지 않게 둘의 max 를 쓴다. 0 이하 = 폴백.
        public float attackAnimPeriod;
        // beam-ranger-defender unit 1 rev — 이 공격이 겨눈 대상. targetWorld 는 발사 순간의
        // **스냅샷**이라 사건 사이(주기 0.2s)에 적이 걸어가면 어긋난다. 지속 연출(빔)은 대상을
        // 매 프레임 따라가야 하므로 위치가 아니라 엔티티가 필요하다. Entity.Null = 대상 없음.
        public Entity target;

        // elite-enemy-tier unit 4 — 화염 브레스 연출. **이 채널을 재사용하는 이유**: 피해는
        // `[BurstCompile] ISystem` 인 AttackSystem 에서 적용되므로 managed `VfxSpawner` 를 부를 수
        // 없고, 브리지 드레인이 필요하다. 이 이벤트는 이미 브리지가 드레인하고 `attacker` 를
        // 실어서 위치를 뷰 좌표로 풀 수 있다 → 신규 NativeQueue 채널 0.
        //
        // ★**이 플래그가 켜진 이벤트는 «공격 사건» 이 아니라 VFX 캐리어다.** 공격 시작 이벤트는
        // RESOLVE 보다 앞서 별도로 발행되므로(같은 이벤트에 얹을 수 없다) 브레스는 두 번째
        // 이벤트로 온다. 드레인은 이 플래그를 보면 **애니 재생(NotifyAttack)을 건너뛴다** —
        // 안 그러면 한 프레임에 공격 애니가 두 번 트리거된다.
        public bool hasAreaBreath;
        public float2 breathDir;        // 정규화된 조준 방향(월드 XZ)
        public float breathRangeWorld;  // 사거리(월드) = tileRange × tileSize
        public float breathHalfAngleDeg;// 부채꼴 폭(뷰가 VFX 를 벌리는 데 쓴다)
    }
}
