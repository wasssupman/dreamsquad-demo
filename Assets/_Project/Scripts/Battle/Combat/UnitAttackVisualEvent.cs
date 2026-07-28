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
    }
}
