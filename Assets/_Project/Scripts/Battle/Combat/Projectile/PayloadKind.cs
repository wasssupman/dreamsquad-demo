namespace Wassup.Battle.Combat.Projectile
{
    // Payload axis of a projectile (orthogonal to MovementKind). Selects what
    // happens at impact. Default (0) is SingleSplash so existing spawns keep the
    // legacy single-target (+ optional splash) resolution with no code change.
    public enum PayloadKind : byte
    {
        // Apply outputs to the direct target, plus the existing OnHitEffectType.Splash
        // bonus to nearby entities. Legacy projectile impact behavior.
        SingleSplash = 0,

        // Flat AOE to every enemy within impactTileRange (Chebyshev tiles) of the
        // impact cell — no direct target, no falloff. Shares the tile-membership
        // primitive the legacy Meteor resolver used (removed unit 8).
        TileAoe = 1,

        // Sweep the prev→current segment every frame and damage targets on the
        // path, each at most once (hit-set buffer), until the pierce budget runs
        // out. No point-arrival resolution (defender-directional-volley unit 1;
        // hit arm lands in unit 2).
        PathHit = 2,

        // bomb-barrel-on-place unit 2 — 착탄 지점 칸에 **길막 설치물을 세운다**. 피해는 0 이다
        // (배럴은 폭탄이 아니라 물건이고, 터지는 것은 부서질 때다 — unit 0).
        // 스폰은 기존 해저드 스폰 요청 채널을 타므로 신규 채널 0.
        // ⚠ 끝에 append — 중간에 끼우면 직렬화된 기존 값이 밀린다.
        SpawnBlocker = 3,
    }
}
