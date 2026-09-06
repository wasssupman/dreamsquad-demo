using Unity.Entities;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // AttackSystem stages one of these on the shooter when cooldown expires; a
    // MonoBehaviour drain inside BattleBridge consumes them to create the actual
    // projectile entity (managed RenderMesh setup is not safe inside ISystem).
    //
    // Carries both trajectory axes' parameters; the drain copies the relevant
    // subset onto ProjectileState per `movement`/`payload`. Defaults (0/0) =
    // HomingToEntity / SingleSplash reproduce the legacy path.
    public struct ProjectileSpawnRequest : IComponentData
    {
        // ── Axis discriminators ──────────────────────────────────────────────
        // skill-layer-migration unit 7d — **착탄 예고를 거나.** 0 = 안 건다.
        // ⚠ 반환값이 아니라 **앞으로 흐르는 값**이다. 예전엔 시전 지점이 스폰된 엔티티를
        // 돌려받아 예고를 걸었는데, 실행이 스킬 레이어로 가면서 그 반환이 사라졌다.
        // 드레인은 이미 그 엔티티를 손에 들고 있으므로 요청이 「걸어라」만 말하면 된다.
        public int telegraphTileRange;
        public MovementKind movement;
        public PayloadKind payload;

        // ── Shared ───────────────────────────────────────────────────────────
        public float3 origin;
        public float damage;
        public float speed;   // homing: flight speed · ballistic: derives flightTime in the drain
        public float visualScale;
        public int dataIndex;

        // waypoint-routing unit 4 rev 4 — moving victim traversal-layer mask.
        // Copied to ProjectileState so splash/bounce/path-hit/TileAoe obey the
        // same contract after launch. 0 = legacy unfiltered.
        public byte targetTraversalLayers;

        // ── Homing trajectory ────────────────────────────────────────────────
        public Entity target;
        public float hitThreshold;

        // ── Ballistic-arc trajectory ─────────────────────────────────────────
        // impact = cell-locked strike point. For BallisticArc, flightTime is
        // derived at drain time (distance/speed) so `flightTime` below is ignored
        // there; elapsed always starts at 0 and accumulates in MoveSystem.
        public float3 impact;
        public float arcHeight;

        // ── Bezier homing (projectile-emission-pattern unit 1) ───────────────
        // 스윙 소스만 싣는다 — 제어점 자체는 드레인이 SO(bezierLateral/ForwardBias)를
        // 읽어 산출한다(SkyFall 의 dropHeight 보충과 같은 번역자 seam). 덕분에 발사
        // 주체(AttackSystem·emitter·캐스트)는 어느 쪽도 SO 를 알 필요가 없고, 요청
        // struct 가 궤적마다 비대해지지 않는다. 기본 0 = 첫 발(중앙 스윙).
        public int swingIndex;

        // ── Sky-fall trajectory ──────────────────────────────────────────────
        // Request-carried flight time (seconds). SkyFall has zero travel distance
        // so the drain cannot derive it from speed; Meteor maps warningSec here.
        public float flightTime;

        // ── Grenade fuse (bomb-thrower-defender unit 1) ──────────────────────
        // Extra hold after travel before the GrenadeToCell arm arrives. Copied
        // verbatim onto ProjectileState. Default 0 = no fuse (non-grenade arms).
        public float fuseSec;

        // ── Directional trajectory (defender-directional-volley unit 1) ──────
        // Fire direction (unit vector on the sim plane) + max flight distance
        // (world units). Zero on homing/ballistic requests — ignored there. The
        // drain copies these onto ProjectileState for DirectionalLinear.
        public float2 direction;
        public float maxDistance;

        // ── Single-splash payload ────────────────────────────────────────────
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;

        // ── Tile-AOE payload ─────────────────────────────────────────────────
        public int impactTileRange;
        // distance-based-range unit 23b — **착탄 자리의 «주인» 의 몸**(타일).
        // > 0 = 자기 자리 폭발(그 몸이 터진다) · **0 = 진짜 날아온 탄의 착탄점**(칸 반폭 = 종전).
        // ⚠ 즉발 폭발(`flightTime = 0`)이 이 파이프라인을 타면 판정 지점의 국소 문맥에서는
        // 「어떤 착탄점」으로 보여 칸 반폭이 옳아 보인다 — 그러나 그 자리는 트리거 대상의
        // 몸 중심이다. 원점 정보를 **이 경계 너머까지 실어 보내는 것**이 이 필드의 존재 이유다
        // (그게 안 실려서 unit 23 초판 감사가 이 결함을 원리적으로 못 봤다).
        public float originBodyRadius;

        // ── Bomb TileAoe extensions (bomb-thrower-defender unit 2) ───────────
        // Copied verbatim onto ProjectileState by the drain. Defaults 0 = legacy
        // TileAoe(uncapped, damage-only). Set by AttackSystem bomb branch(unit 4).
        public int aoeTargetCap;
        public byte ccKind;
        public float ccDuration;

        // bomb-barrel-on-place unit 2 — PayloadKind.SpawnBlocker 가 세울 길막 설치물의
        // 레지스트리 index(브리지가 SO→index 로 풀어 싣는다). -1 = 미배선.
        public int blockerDataIndex;

        // ── Bounce (dreamcatcher-attack-mod-bounce) ──────────────────────────
        // Copied verbatim onto ProjectileState by the drain. Defaults 0 = no-op.
        public int bounceRemaining;
        public int bounceTileRange;
        public float bounceDamageMul;

        // 대상 소멸 시 재조준 반경(Chebyshev 타일). 0 = 기존 동작(소멸).
        public int retargetTileRange;

        // ── Shooter attribution (nightmare-catcher unit 1) ───────────────────
        // Copied verbatim onto ProjectileState by the drain. Default Entity.Null
        // (bridge-cast skills) = no threat attribution.
        public Entity owner;

        // ── TileAoe victim faction (nightmare-catcher unit 4) ────────────────
        // Copied verbatim onto ProjectileState by the drain. Default Enemy(0) =
        // legacy pool (player Meteor / defender ballistic unchanged, N3).
        public ProjectileTargetFaction targetFaction;

        // ── Frontmost priority damage (dreamcatcher-content-2 끝을 보는 눈) ───
        // Copied verbatim onto ProjectileState by the drain. Defaults
        // Entity.Null / 0 = no bonus, so every existing spawn is inert without
        // touching legacy producers. ProjectileHitSystem multiplies only when the
        // actual Damage-kind victim == priorityTarget, using
        // (priorityDamageMul > 0 ? priorityDamageMul : 1). Wired in unit 3.
        public Entity priorityTarget;
        public float priorityDamageMul;

        // ── Heavy strike all-victim mul (dreamcatcher-heavy-strike 응축된 일격) ──
        // Copied verbatim onto ProjectileState by the drain. priorityDamageMul
        // multiplies only the priority victim; this one multiplies EVERY Damage
        // victim of the shot (근접 cleave/splash/bounce 포함 — 강공은 한 방 통째).
        // Default 0 = inert (실적용 mul>0?mul:1). Wired in unit 1, applied unit 2.
        public float heavyDamageMul;

        // ── Orbit phase (content-4 unit 8) ───────────────────────────────────
        // 궤도 구슬의 각도 오프셋(rad). **per-shot 값이라 탄 SO 가 아니라 요청이 나른다** —
        // 같은 SO 로 여러 구슬을 서로 다른 위상에 띄우는 것이 이 값의 존재 이유다.
        // 기본 0 = 단일 구슬/비궤도 = 종전 동작.
        public float orbitPhase;
    }

    public struct ProjectileSpawnOutputElement : IBufferElementData
    {
        public AttackOutput value;
    }
}
