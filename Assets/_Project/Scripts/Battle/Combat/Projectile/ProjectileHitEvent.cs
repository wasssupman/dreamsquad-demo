using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // Combat→Presentation crossing payload. ProjectileHitSystem enqueues one of
    // these per direct-target impact so the MonoBehaviour view pool can play the
    // configured hit prefab without any ECS reference. Splash secondary targets
    // are intentionally not represented here — the visual is one impact per shot.
    public struct ProjectileHitEvent
    {
        public float3 position;
        public int dataIndex;

        // 발사체 엔티티(소멸 직전 스냅샷) — bridge 가 "이 착탄이 스킬 텔레그래프의
        // 착탄인가" 를 정확 판별하는 용도(unit 9). visual 라우팅(hitPrefab 유무)과
        // 판별을 분리해, meteor 에 hitPrefab 을 달아도 텔레그래프 해제가 깨지지 않는다.
        public Entity source;

        // Payload discriminator + world radius so the drain can route AOE bursts
        // (TileAoe with no hitPrefab → VfxSpawner.SpawnMeteorBurst) without ECS
        // lookups. radiusWorld = impactTileRange * tileSize, snapshotted at
        // resolve time because the radius is per-cast (skill range), not a
        // ProjectileData constant (unit 7).
        public PayloadKind payload;
        public float radiusWorld;
    }
}
