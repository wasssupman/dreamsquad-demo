using Unity.Entities;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // nightmare-catcher unit 4 — which faction pool a TileAoe impact damages.
    // Zero value = Enemy so every existing spawn (player Meteor, defender
    // ballistic) keeps the legacy enemy-target pool by construction (N3);
    // only the boss AreaBarrage arm sets Defender. Other payload kinds
    // (SingleSplash/bounce) stay enemy-only and ignore this.
    public enum ProjectileTargetFaction : byte { Enemy = 0, Defender = 1 }

    // Per-projectile flight data, decomposed into two orthogonal axes:
    // `movement` (trajectory) and `payload` (impact resolution). Defaults
    // (HomingToEntity / SingleSplash) reproduce the legacy homing projectile so
    // existing spawns need no change.
    //
    // `damage` is a snapshot taken at launch (already multiplied by
    // ModifierStats.damageMul on the shooter at fire time); it does not change in
    // flight even if the buff expires before the projectile lands.
    public struct ProjectileState : IComponentData
    {
        // ── Axis discriminators ──────────────────────────────────────────────
        public MovementKind movement;
        public PayloadKind payload;

        // ── Shared ───────────────────────────────────────────────────────────
        public float damage;
        // Hit-event channel: the impact system enqueues this index into the
        // ProjectileHitEventsSingleton so the Presentation layer can resolve a
        // hit-VFX prefab without ECS lookups. Populated from
        // ProjectileSpawnRequest.dataIndex at launch.
        public int dataIndex;

        // waypoint-routing unit 4 rev 4 — launch-time attack target layer mask.
        // ProjectileHitSystem/MoveSystem use it for direct and secondary victims.
        // 0 = legacy unfiltered.
        public byte targetTraversalLayers;

        // Runtime arrival flag — set by ProjectileMoveSystem when the trajectory
        // reaches its endpoint; consumed by ProjectileHitSystem to resolve the
        // payload. Each trajectory knows its own arrival condition, so arrival
        // lives on the movement side, not the impact side. Defaults false at spawn.
        public bool impactReached;

        // ── Homing trajectory (MovementKind.HomingToEntity) ──────────────────
        public Entity target;
        public float speed;
        public float hitThreshold;

        // ── Ballistic-arc trajectory (MovementKind.BallisticArcToPoint) ──────
        // Shared by SkyFall, which also locks impact and ticks elapsed.
        // impact is cell-locked at fire time; the target entity is not tracked.
        // flightTime: BallisticArc derives it from distance/speed at spawn;
        // SkyFall carries it on the request (warningSec). elapsed ticks up.
        public float3 origin;
        public float3 impact;
        public float flightTime;
        public float elapsed;
        public float arcHeight;

        // ── Bezier homing trajectory (MovementKind.BezierHomingToEntity) ─────
        // 발사 시 결정론 산출된 제어점 2개(드레인이 SO 파라미터로 계산 — ISystem 은
        // SO 를 못 읽는다). P0 = origin, P3 = 타겟 live 위치라 여기 없다. flightTime/
        // elapsed/arcHeight 는 위 슬롯 재사용(arcHeight = 카메라 평면 up 아치 높이).
        public float3 control1;
        public float3 control2;

        // ── Orbit trajectory (MovementKind.OrbitAroundPoint, content-4) ───────
        // 전용 필드는 `orbitPhase` 하나뿐이고 나머지는 위/아래 슬롯을 빌려 쓴다.
        // 빌린 의미를 여기 모아 둔다:
        //   origin      = 궤도 **중심**(발사 시점 고정, 타겟 엔티티 무참조)
        //   maxDistance = 궤도 **반경**(월드) ← DirectionalLinear 의 사거리 슬롯
        //   speed       = **각속도**(rad/s, 음수 = 역회전) ← 월드 속도가 아니다.
        //                 발사 arm 이 «탄 SO 선속도 ÷ 반경» 으로 변환해 보낸다
        //   flightTime  = 지속 초 · elapsed = 누적 (도착 = 수명 종료)
        //   prevPos     = 직전 위치(PathHit 스윕) · direction = 접선(front-most 정렬)
        //   hitThreshold= **피격 반경** — 궤도 반경과 다른 축이다(굵기 vs 넓이)
        //   orbitPhase  = 각도 오프셋(rad). 여러 구슬을 같은 궤도에 균등 배치한다.
        //                 elapsed 오프셋으로 대신할 수 없다 — 그건 수명도 앞당긴다.
        public float orbitPhase;

        // ── Boomerang trajectory (MovementKind.BoomerangReturn, content-5) ────
        // **전용 필드 0** — 전부 아래/위 슬롯을 빌린다:
        //   origin      = 발사점(= 귀환점, 고정)  ·  speed = 선속도
        //   direction   = **발사 축(불변)** ← DirectionalLinear 과 같은 슬롯·같은 의미
        //   maxDistance = 편도 거리(월드)   ·  elapsed = 누적(도착 = 왕복 완료)
        //   prevPos     = 직전 위치(PathHit 스윕)  ·  hitThreshold = 피격 반경
        //
        // ⚠ `direction` 은 이 궤적에서 **위치 계산의 입력**이다. 「돌아오는 중이니 뒤집자」로
        // 갱신하면 다음 프레임이 발사점 뒤를 계산한다 — 궤도가 direction 을 매 프레임 쓰는
        // 것과 정반대이므로(거긴 접선 = 파생값) 그 arm 을 복제하지 말 것.
        // 「지금 어느 다리인가」는 **어디에도 저장하지 않는다**(content-5 계약 5).

        // ── Grenade fuse (MovementKind.GrenadeToCell, bomb-thrower-defender) ──
        // Extra hold at the cell after travel completes (elapsed >= flightTime)
        // before arrival fires at elapsed >= flightTime + fuseSec. Default 0 = no
        // fuse (every non-grenade arm arrives exactly at flightTime). Movement owns
        // this timing; ProjectileHitSystem never sees it (contract 3).
        public float fuseSec;

        // ── Directional trajectory (MovementKind.DirectionalLinear) ──────────
        // direction: normalized fire vector on the sim plane, locked at launch
        // (facing is immutable, so it never changes in flight). maxDistance:
        // world-space range; reaching it sets impactReached — for this arm that
        // flag means "flight ended", the cue for PathHit to despawn after its
        // final sweep. prevPos: last frame's position, written by the move arm
        // so the payload can sweep the segment it just crossed.
        public float2 direction;
        public float maxDistance;
        public float3 prevPos;

        // ── Path-hit payload (PayloadKind.PathHit) ───────────────────────────
        // Remaining victims this shot may damage before despawning (1 = stop at
        // the first). Owned write after launch: ProjectileHitSystem only.
        public int pierceRemaining;

        // dreamcatcher-content-4 unit 0 — 같은 피해자를 다시 때리기까지의 간격(초).
        // 값은 탄 SO(ProjectileData.rehitCooldownSec)에서 드레인이 채운다.
        // 0 = 피해자당 영구 1회(기존 방향탄 동작 — 무회귀 기본값).
        // >0 이면 ① PathHitRecord 의 nextHitAt 으로 재타격을 허용하고
        //         ② **pierceRemaining 을 소모하지 않는다**(유일한 종료 조건 = 수명).
        // 시계는 이 투사체의 `elapsed` 다 — Battle 도메인 시계를 그대로 따라가고
        // 리플레이 결정론이 투사체 안에서 닫힌다. 소비는 unit 2(ProjectileHitSystem).
        public float rehitCooldownSec;

        // dreamcatcher-content-5 unit 2 — 스친 적을 **그 프레임 진행 방향**으로 미는 넉백.
        // 값은 탄 SO(ProjectileData.knockbackDistance/Duration)에서 드레인이 채우며,
        // 여기에는 이미 **속도로 환산된** 값이 온다(거리÷시간). 0 = 꺼짐(기존 관통탄 무변화).
        // 방향은 상태가 아니라 스윕(pos−prevPos)에서 뽑는다 — 왕복이 두 다리에서 반대 힘이
        // 되는 것은 그 결과다. 소비는 ProjectileHitSystem 의 PathHit arm.
        public float knockbackSpeed;
        public float knockbackDuration;

        // ── Single-splash payload (PayloadKind.SingleSplash) ─────────────────
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;

        // ── Tile-AOE payload (PayloadKind.TileAoe) ───────────────────────────
        public int impactTileRange;

        // ── Bomb TileAoe extensions (bomb-thrower-defender unit 2) ───────────
        // aoeTargetCap: nearest-B cap (0 = 무제한 = 레거시 메테오/스킬/보스 경로).
        // ccKind/ccDuration: 수면/스턴탄 CC (0=None, Combat→Effects EnemyCcEvents).
        // bombType: 뷰 변종 인덱스(0 데미지/1 수면/2 스턴) — sim 무해 캐리어,
        // Presentation 이 색으로 해석(계약 8). 전부 기본 0 = 레거시 TileAoe.
        public int aoeTargetCap;
        public byte ccKind;
        public float ccDuration;
        public byte bombType;

        // ── Bounce (dreamcatcher-attack-mod-bounce) ──────────────────────────
        // Post-resolution survival for SingleSplash: while bounceRemaining > 0
        // and a retarget candidate exists, the impact system re-homes instead of
        // destroying. Defaults 0/0/0 = every existing spawn keeps the legacy
        // destroy path. Owned write after launch: ProjectileHitSystem only.
        public int bounceRemaining;
        public int bounceTileRange;   // retarget search radius (Chebyshev tiles)
        public float bounceDamageMul; // per-bounce decay applied to damage sources

        // 대상이 죽거나 사라졌을 때 **파괴 대신 재조준**할 반경(Chebyshev 타일). 0 = 비활성.
        // 기본 호밍 동작(대상 소멸 = 소멸)은 화살 등 기존 투사체의 감각/밸런스라 그대로 두고,
        // 이 값을 실은 투사체만 opt-in 한다. bounce(맞고 나서 남은 홉, 소비형·감쇠 있음)와는
        // 다른 축이다 — 이건 "맞히기도 전에 대상이 사라진 경우"(비소비형·감쇠 없음)를 다룬다.
        // 한 투사체가 둘 다 가질 수 있어 합치지 않는다.
        //
        // ⚠ 원점이 다르다: 카드의 tileRange 는 **캐스터 기준** 사거리인데 이 값은 **투사체
        // 현재 위치 기준**으로 재해석된다. 같은 숫자라도 니들이 날아간 만큼 실효 도달 범위가
        // 늘어난다. 후보 풀의 진영은 호출부가 고정한다(현재 전부 적 전용).
        public int retargetTileRange;

        // ── Shooter attribution (nightmare-catcher unit 1) ───────────────────
        // Entity that fired this projectile, filled at the AttackSystem launch
        // arms (base shots and dc-trigger carriers alike); Entity.Null for
        // bridge-cast skills (player Meteor — intentionally not threat-credited).
        // ProjectileHitSystem reads it to enqueue ThreatHitEvent on victims that
        // carry a ThreatEntry buffer (boss). Survives bounce re-homing.
        public Entity owner;

        // ── TileAoe victim faction (nightmare-catcher unit 4) ────────────────
        // Default Enemy(0) = legacy pool; boss AreaBarrage sets Defender.
        public ProjectileTargetFaction targetFaction;

        // ── Frontmost priority damage (dreamcatcher-content-2 끝을 보는 눈) ───
        // Filled at launch from ProjectileSpawnRequest. Defaults Entity.Null / 0
        // = no bonus (all legacy spawns inert). ProjectileHitSystem applies the
        // multiplier only to the Damage-kind victim that equals priorityTarget,
        // to both IncomingDamage and ThreatTable.TryCredit (no desync). Survives
        // bounce re-homing but only fires while the direct victim == priorityTarget.
        public Entity priorityTarget;
        public float priorityDamageMul;

        // ── Heavy strike all-victim mul (dreamcatcher-heavy-strike 응축된 일격) ──
        // Filled at launch from ProjectileSpawnRequest. Unlike priorityDamageMul
        // (one victim), this multiplies EVERY Damage-kind victim of this shot, to
        // both IncomingDamage and ThreatTable.TryCredit (no desync). Default 0 =
        // inert. Survives bounce re-homing. Applied in unit 2 (ProjectileHitSystem).
        public float heavyDamageMul;
    }
}
