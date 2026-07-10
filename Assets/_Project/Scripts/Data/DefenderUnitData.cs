using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "DefenderUnit", menuName = "Wassup/DefenderUnit", order = 11)]
    public class DefenderUnitData : ScriptableObject, ISpineUnitVisualData, IDefenderSpineExtras
    {
        // outgame-scene-and-flow Unit 0 — stable id for save/load. Fixed once
        // assigned (it is a persistence key); independent of asset/display name.
        public string id;
        // ingame-dreamcatcher Unit 0 — class/role for buff targeting axes.
        public DefenderClass role = DefenderClass.None;
        public string displayName;
        public float health = 50f;
        public float attackRange = 3f;
        public float attackCooldown = 1f; // seconds between attacks
        // attack-hit-delay — 공격 시작 후 타격 판정까지 지연(초). 0 = 즉시.
        public float hitDelaySec = 0f;
        // attack-hit-delay 2 — 배치 직후 공격 시작까지 지연(초). 그동안 idle(공격 X). 0 = 즉시.
        public float deployDelaySec = 0f;

        // Phase 8 §13 follow-up — melee-only AoE cap. Projectile defenders
        // still hit a single target (splash is handled by ProjectileData).
        // Melee (projectile == null) defenders hit up to `attackTargetCount`
        // nearest in-range attackers per cooldown tick. Default 1 preserves
        // single-target behavior; Bastion/Bruiser type tanks benefit from 3+.
        public int attackTargetCount = 1;
        public Mesh visualMesh;
        public Material visualMaterial;

        // battle-audio: 배치 시 캐릭터별 캐주얼 추임새(클래스 어울리는 짧은 라인, TTS 보이스).
        public AudioClip deployVoiceClip;

        // Phase 3: when set, the AttackSystem queues a ProjectileSpawnRequest rather
        // than appending IncomingDamage immediately. Leaving this null keeps the
        // Phase 0-2 direct-damage path for regression coverage.
        public ProjectileData projectile;

        // modifier-legacy-migration unit 0: hit outputs are the runtime source of
        // truth for defender attacks. Defenders with no outputs deal no runtime damage.
        [Header("Attack Outputs")]
        public AttackOutput[] outputs;

        // Phase 4: fires once at placement moment. None means no on-place effect.
        public OnPlaceEffectType onPlaceEffect;
        public float onPlaceRange;
        public float onPlaceMagnitude;
        public float onPlaceDuration;

        // Phase 6: placement cost subtracted from CostRuntime on PlaceDefenderAs.
        public int cost = 1;

        [Header("Targeting")]
        // When true, AttackState.targetMask is set to Faction.Defender (ally targeting).
        // Use for healers and buff-appliers that target friendly units instead of enemies.
        public bool targetAllies;

        [Header("Hazard Cast")]
        public bool hazardCastEnabled;
        public float hazardCastRange;
        public float hazardCastCooldown;
        public HazardCastKind hazardCastKind;
        public HazardSO zoneHazard;
        public BlockingHazardSO blockingHazard;
        public int hazardFootprintWidth = 1;
        public int hazardFootprintHeight = 1;

        [Header("Rarity")]
        public DefenderRarity rarity = DefenderRarity.Common;

        // defender-portraits 0 — 스쿼드/배치 UI 표시용 클래스 포트레이트. null 이면
        // 텍스트/단색 폴백. ECS 런타임/전투 로직은 참조하지 않는 순수 프레젠테이션 데이터.
        [Header("Presentation")]
        public Sprite portrait;

        // aggro-targeting Unit 10 — magnet aggro (히트 구동). aggroCapacity = max
        // enemies this unit can hold at once. 0 = no aggro (Fighter/Ranger); only
        // Guardian-role units set > 0. 획득 트리거는 가디언의 공격 명중 —
        // AttackSystem RESOLVE 가 히트 적을 AggroHitEvent 로 넘긴다(별도 range 없음:
        // 획득 범위 = 공격 사거리). 구 aggroRange 폐기(근접 즉시 배정 산물).
        // Concrete numbers are delegated to the balancing spec.
        [Header("Aggro")]
        public int aggroCapacity = 0;

        // Phase 8: Spine skeleton skin + animation names. When spineSkinName is
        // empty or skeletonDataAsset is null, BattleBridge falls back to the
        // Phase 5 billboard path, so skeletons can be rolled out incrementally
        // one unit type at a time without breaking the rest of the roster.
        [Header("Phase 8 — Spine")]
        public SkeletonDataAsset skeletonDataAsset;
        public string spineSkinName;
        public string idleAnimation = "idle";
        public string attackAnimation = "attack";
        public string deathAnimation = "die";
        // Visual scale applied to the spawned SkeletonAnimation GameObject.
        // Spine rigs ship in their own unit space (often pixels); map into our
        // tile-based world so a single SO knob is enough to normalise rig size.
        public float spineVisualScale = 1f;

        // unit-parts-appearance 0 — 파츠 조합 외형. 비어 있으면 spineSkinName 단일 스킨.
        // 순서가 의미를 갖는다(뒤 파츠가 슬롯 단위로 덮음). 조립은 인스펙터 드롭다운
        // 또는 Layer Lab 데모 → 임포트 도구 경유.
        [Header("Parts Appearance")]
        [SpineSkin(dataField: "skeletonDataAsset")] public List<string> partSkins = new List<string>();
        public List<SpineSlotColor> slotColors = new List<SpineSlotColor>();

        [Header("Deployment Presentation")]
        public string dragAnimation = "idle";
        public string deployAnimation = "deploy";
        public GameObject placementVfxPrefab;
        public GameObject attackVfxPrefab;

        [Header("Knockback (per attack)")]
        public float knockbackDistance;   // world units. 0 = disabled
        public float knockbackDuration;   // seconds. velocity = direction * distance / duration

        [Header("On-place Push")]
        public float onPlacePushDistance; // world units. 0 = disabled
        public float onPlacePushDuration; // seconds
        public float onPlacePushRadius;   // world units, radial from defender center

        [Header("Cast Anchor")]
        public string castAnchorBone = "";
        public Vector3 castAnchorLocalOffset = new Vector3(0.5f, 1f, 0f);
        public float deploymentDuration = 0.45f;
        public float placementSkillDelay = 0f;

        public string SpineDisplayName => displayName;
        public SkeletonDataAsset SpineSkeletonDataAsset => skeletonDataAsset;
        public string SpineSkinName => spineSkinName;
        public string SpineIdleAnimation => idleAnimation;
        // enemy-walk-anim-speed unit 4 — 디펜더는 타일 고정(이동 없음) → 걷기 애니 불요.
        // 항상 idle 단일 루프(현행). 빈 문자열 = 스위칭 비활성.
        public string SpineWalkAnimation => "";
        public string SpineAttackAnimation => attackAnimation;
        public string SpineDeathAnimation => deathAnimation;
        public float SpineVisualScale => spineVisualScale;
        // enemy-spawn-positioning 0 — 방어 유닛은 본 spec 범위 밖. 계약 기본값(오프셋 없음).
        public Vector3 SpineVisualOffset => Vector3.zero;
        public IReadOnlyList<string> SpinePartSkins => partSkins;
        public IReadOnlyList<SpineSlotColor> SpineSlotColors => slotColors;
        public string SpineDragAnimation => dragAnimation;
        public string SpineDeployAnimation => deployAnimation;
        public string SpineCastAnchorBone => castAnchorBone;
        public Vector3 SpineCastAnchorLocalOffset => castAnchorLocalOffset;

        // dreamcatcher-awakening-hand unit 0 — awakening currency granted when
        // this defender dies ("death is income": sacrificing units feeds the
        // dreamcatcher economy). Appended last to keep serialization order
        // stable; existing assets pick up the initializer (4) until re-saved.
        [Header("Awakening")]
        public int awakeningReward = 4;
    }

    public enum OnPlaceEffectType
    {
        None,
        SlowPulse,
        BoostNearbyDefenders,
        BindNearby,
        MeleeBurst,
        ForwardProjectile,
        GainCost,
        ReduceSkillCooldown,
    }
}
