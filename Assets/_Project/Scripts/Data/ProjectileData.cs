using UnityEngine;

namespace Wassup.Data
{
    // Phase 3은 None만 소비한다. Phase 4에서 DoT/Splash/Slow 등으로 확장.
    public enum OnHitEffectType
    {
        None,
        Poison,
        Fire,
        Splash,
        Slow,
    }

    public enum TextureSelectMode
    {
        Random,
        Sequential,
        First,
    }

    public enum ProjectileFacing
    {
        AlongVelocity,
        FixedUp,
        SpinAroundUp,
        // bomb-thrower-defender unit 5 — 데굴데굴: 진행방향에 수직인 수평축 기준 tumble
        // (이동 중일 때만 굴러 착지/퓨즈 시 정지). spinSpeed = 굴림 속도. 구르는 폭탄 뷰용.
        RollAlongPath,
    }

    // Trajectory mode. Homing tracks the target entity (legacy). BallisticToCell
    // lobs an arc to the target's cell locked at fire time and resolves as a
    // tile-AOE. BattleBridge maps this to (MovementKind, PayloadKind) at convert.
    public enum ProjectileFlightMode
    {
        Homing,
        BallisticToCell,
        // defender-directional-volley unit 1 — 방향 벡터 직선 비행 + 경로 스윕 히트.
        // 타겟 엔티티/착탄 셀 없음. BattleBridge 가 (DirectionalLinear, PathHit) 로 매핑.
        Directional,
        // projectile-emission-pattern unit 1 — 3차 베지어 곡선 + 타겟 추적.
        // (BezierHomingToEntity, SingleSplash) 로 매핑.
        BezierHoming,
        // projectile-emission-pattern unit 4 — 낙하 텔레그래프 후 착탄 셀 AoE.
        // 지금까지 이 축은 BattleBridge.ApplyMeteor 가 하드코딩으로만 만들었고
        // flightMode 어휘엔 없었다 — 발사 명세(패턴)가 데이터로 SkyFall 탄을
        // 지정할 수 있어야 하므로 개통한다. (SkyFall, TileAoe) 로 매핑.
        SkyFall,
        // dreamcatcher-content-5 unit 0 — 발사 축을 따라 나갔다 돌아오는 왕복.
        // (BoomerangReturn, PathHit) 로 매핑 — 경로를 훑는 궤적이라 페이로드는
        // Directional 과 같은 짝이다.
        Boomerang,
        // on-place-skill-rework unit 10 — 낙하 텔레그래프 × **적 하나**.
        // (SkyFallOnEntity, SingleSplash) 로 매핑. 위 `SkyFall` 과 그림은 같고
        // **조준이 다르다**(칸 ↔ 적) — 「반경 안 적 전원에게 1발씩」처럼 발수가 적 수를
        // 따라야 하는 폭격이 이 축을 쓴다. 칸 폭격(메테오·보스 barrage)은 `SkyFall` 이다.
        // ⚠ 새 멤버는 **끝에 붙인다** — 중간에 끼우면 기존 에셋의 직렬화 값이 밀린다.
        SkyFallOnTarget,
    }

    [CreateAssetMenu(fileName = "Projectile", menuName = "Wassup/Projectile", order = 13)]
    public class ProjectileData : ScriptableObject
    {
        public string id;
        public float speed = 10f;
        public float hitThreshold = 0.3f;
        public float visualScale = 0.3f;

        [Tooltip("비행체 시각을 카메라 평면 up으로 띄우는 높이(월드 유닛). ECS/속도엔 영향 없이 Presentation에만 적용한다. 타일에 깔리는 것 방지용.")]
        public float visualHeightOffset = 0f;

        public GameObject projectilePrefab;
        public GameObject hitPrefab;

        public ProjectileFacing facing = ProjectileFacing.AlongVelocity;
        public float spinSpeed = 0f;

        [Header("As-is VFX")]
        [Tooltip("true 면 tint/emission/texture 변종 recolor 를 건너뛰고 프리팹 머티리얼 고유 색을 " +
                 "그대로 쓴다. 이미 색이 완성된 VFX(예: GabrielAguiar) 용. false 면 기존 데이터 recolor.")]
        public bool preserveVfxColors = false;

        [Header("Variation - deterministic")]
        public Color tintColor = Color.white;
        public float emissionMultiplier = 1f;

        [Header("Variation - per-shot random")]
        [Range(0f, 1f)] public float scaleJitter = 0f;
        [Range(0f, 0.5f)] public float hueJitter = 0f;
        [Range(0f, 360f)] public float rotationJitter = 0f;

        [Header("Texture variants")]
        public Texture2D[] textureVariants;
        public TextureSelectMode selectMode = TextureSelectMode.Random;

        [Header("Hit VFX")]
        public float hitVfxLifetime = 0f;
        [Tooltip("hit VFX 스케일 배수. GA muzzle-hit 처럼 원본이 작으면 키운다.")]
        public float hitVfxScale = 1f;

        [Header("Cast VFX")]
        public GameObject castPrefab;
        public float castVfxLifetime = 0f;

        // Phase 3에서 enum 필드만 존재. Phase 4부터 Splash가 실 사용 (Poison/Fire/Slow는
        // 여전히 미구현 자리표시).
        public OnHitEffectType onHitEffect = OnHitEffectType.None;
        public float onHitMagnitude;
        public float onHitDuration;
        public float splashRadius;
        public float splashDamageMul = 0.5f;

        [Header("Ballistic (flightMode = BallisticToCell)")]
        public ProjectileFlightMode flightMode = ProjectileFlightMode.Homing;
        [Tooltip("카메라 평면 기준 포물선 정점 높이(월드 유닛). BallisticToCell 전용.")]
        public float arcHeight = 2f;
        [Tooltip("착탄 셀 기준 AOE 반경(타일, Chebyshev 거리). BallisticToCell 전용.")]
        public int impactTileRange = 1;
        [Tooltip("최소 비행 시간(초). 근거리에서도 arc 가 보이게 하한. BallisticToCell 전용.")]
        public float minFlightTime = 0.3f;

        [Header("Directional (flightMode = Directional)")]
        [Tooltip("관통 예산. 1 = 첫 피격 대상에서 소멸, N = N기 히트 후 소멸. Directional 전용.")]
        public int pierceCount = 1;

        // dreamcatcher-content-4 unit 0 — 탄의 성질이므로 여기 산다(바로 위 pierceCount 와
        // 같은 자리·같은 역할: 드레인 SpawnProjectile 이 읽어 ProjectileState 로 옮긴다).
        // 카드 payload / DcTriggerSlot / ProjectileSpawnRequest 로 관통시키지 않는다.
        [Header("Path-hit 재타격 (PathHit 페이로드)")]
        [Tooltip("같은 적을 다시 때리기까지의 간격(초). 0 = 적당 1회(기존 방향탄 동작). " +
                 ">0 이면 관통 예산을 소모하지 않고 수명이 다할 때까지 반복 타격한다 — 궤도 화염구용.")]
        public float rehitCooldownSec = 0f;

        // dreamcatcher-content-5 unit 0 — 넉백도 탄의 성질이라 같은 자리에 둔다
        // (위 rehitCooldownSec·pierceCount 와 동일: 드레인이 읽어 ProjectileState 로).
        // **부메랑 전용이 아니라 PathHit 공통 축**이며 0 = 꺼짐이라 기존 관통탄 전부 무변화.
        //
        // 저작 단위가 「거리 ÷ 시간」인 것은 근접 넉백(CcData.knockbackDistance /
        // knockbackDuration)의 기존 관례를 그대로 따른 것이다 — 속도를 직접 저작하게 하면
        // 같은 개념에 어휘가 둘 생긴다. 드레인이 속도로 환산해 싣는다.
        //
        // ⚠ 거리는 짧게(비틀거리는 정도), 시간은 넉넉히. 근거는 **밀기가 적을 골 쪽으로
        // 보낼 수 있다**는 것이지 이동 시스템의 프레임 변위 상한이 아니다 — 그 상한은
        // 저작값의 수십 배라 실제로는 걸리지 않는다.
        [Tooltip("스친 적을 진행 방향으로 미는 거리(월드). 0 = 넉백 없음(기존 관통탄 동작).")]
        public float knockbackDistance = 0f;
        [Tooltip("넉백이 지속되는 시간(초). 거리÷시간이 밀리는 속도가 된다. 0 = 넉백 없음.")]
        public float knockbackDuration = 0f;

        // projectile-emission-pattern unit 1 — 탄의 성질이므로 여기 산다(패턴 SO 는
        // 이 값을 복제하지 않는다 — README 계약 3). 드레인이 읽어 제어점을 산출한다.
        [Header("Bezier homing (flightMode = BezierHoming)")]
        [Tooltip("좌우 스윙 폭(월드 유닛). 0 = 직선에 가깝게. 여러 발이면 발마다 좌우 교대로 더 크게 벌어진다.")]
        public float bezierLateral = 1.2f;
        [Tooltip("제어점을 진행 방향으로 밀어내는 비율(전체 거리 대비). 클수록 곡선이 늦게 휜다.")]
        [Range(0f, 0.5f)] public float bezierForwardBias = 0.35f;

        [Header("SkyFall (스킬 낙하 투사체 — Meteor)")]
        [Tooltip("낙하 시작 높이(월드 유닛, 카메라 평면 up). flightTime(warningSec) 동안 이 높이에서 착탄 셀로 떨어진다. 내부적으로 state 의 arcHeight 슬롯을 재사용.")]
        public float dropHeight = 6f;

        [Tooltip("낙하가 차지하는 비행 후반 비율(0~1). 1=전체 구간 등속 낙하, 0.35=마지막 35% 에 압축(상공 대기 후 내리꽂힘 — 텔레그래프 시간은 그대로, 시각만 빨라짐). 순수 뷰 파라미터.")]
        [Range(0.05f, 1f)] public float fallPortion = 1f;
    }
}
