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
        // placement-mask unit 4 — 이 유닛이 설 수 있는 배치 층(비트). 판정은 (셀 층 & 이 값) != 0 뿐이고
        // 코드는 role 을 보지 않는다 — 클래스별 배정은 이 필드를 어떻게 적느냐는 저작 선택이다.
        // 기존 asset 은 누락 필드를 **이니셜라이저**(Ground)로 채우고, None(0) 이 들어와도 폴백이
        // Ground 로 맞춘다 — 두 경로 다 안 건드리면 기존 동작 그대로다(이니셜라이저를 바꾸면 폴백이
        // 유일한 방어선이 되므로 둘을 함께 유지할 것). 참고: 그래서 "손으로는 못 놓는 유닛"은 표현 불가.
        public PlacementLayer placementLayers = PlacementLayer.Ground;
        public PlacementLayer EffectivePlacementLayers
            => placementLayers == PlacementLayer.None ? PlacementLayer.Ground : placementLayers;
        public string displayName;
        // squad-character-page unit 6 — 유닛 설명(시트-동기 plain 필드, 체력 등과 동형).
        // 비어 있으면 UnitKitSummary.Describe 가 자동 요약문으로 폴백. 시드값 = 자동 요약문.
        [TextArea] public string desc;
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

        // battle-audio: 공격 실행 시 효과음(근접 클래스 등 per-unit). Null → 무음.
        // 투사체 유닛은 PlayProjectileFire 가 별도로 울리므로 보통 비워둔다.
        public AudioClip attackSfxClip;
        // 히트 VFX 크기. ProjectileViewPool.PlayHit 이 프리팹 스케일을 덮으므로 크기는 **여기서**
        // 정한다(프리팹에서 줄여도 무시된다 — 실제로 한 번 헛수고했다). 1 = 원본 크기.
        public float attackVfxScale = 1f;
        // 히트 VFX 를 타격 방향으로 회전시킬지. 방향성 있는 폭발(흙 분출 등)만 true.
        public bool attackVfxFacesTarget;
        // 히트 VFX 기본 자세 보정(로컬 오일러, 계산된 회전 **뒤에** 곱해진다).
        // 필요한 이유: 벤더 VFX 는 대개 "바닥 = 월드 XZ" 관례로 저작되는데, 이 게임의 보드는
        // Tilemap 그리드라 **바닥이 월드 XY 평면**이다(BoardSpace.ToView 가 sim XZ → 셀 XY).
        // 그래서 지면형 이펙트는 X축 -90° 같은 보정이 필요하다. 값은 눈으로 맞추는 knob —
        // 코드가 추측하지 않는다(실제로 네 번 헛짚었다).
        public Vector3 attackVfxEulerOffset;
        // beam-ranger-defender unit 1 — 지속 빔 유닛의 판별자 겸 프리팹. 비어 있으면 빔 없음.
        // "빔 유닛인가"를 id/kind 로 분기하지 않기 위한 데이터 선언(메커닉 연출은 메커닉이 소유).
        public GameObject beamVfxPrefab;

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
        // bleed-fighter-defender unit 1 — ApplyStackNearby 전용. 스택 종류를 분기에
        // 하드코딩하지 않기 위한 슬롯(제약 6). 다른 onPlaceEffect 에서는 무시된다.
        public Wassup.Battle.Effects.StackKind onPlaceStackKind;
        // beam-ranger-defender unit 2 — DotNearby 전용. 이산 tick 간격(초).
        // 이때 onPlaceMagnitude 는 **틱당 피해**다(DPS 아님 — CcEffect.tickInterval 계약).
        public float onPlaceTickInterval;

        // Phase 6: placement cost subtracted from CostRuntime on PlaceDefenderAs.
        public int cost = 1;

        // defender-placement-cooldown 0 — 배치 성공 후 이 타입 재배치가 막히는 시간(초).
        // 0 = 쿨타임 없음(기존 동작 유지, opt-in). 유닛 타입 단위.
        public float placementCooldown = 0f;

        [Header("Targeting")]
        // When true, AttackState.targetMask is set to Faction.DefenderUnit (ally targeting).
        // Use for healers and buff-appliers that target friendly units instead of enemies.
        public bool targetAllies;

        // defender-ability-assets unit 2 — 능력별 flat 필드 그룹(volley 4·hazard 8·
        // shield 4·bomb 9)은 능력 서브에셋(Data/Abilities/)으로 이관·삭제됨. 파라미터는
        // DirectionalVolleyAbility/HazardCastAbility/ShieldCastAbility/BombThrowAbility 소유.
        // 같은 구체 타입 중복 부착 금지(GetAbility = 첫 매치, 저작 규율 — spec 계약 1).
        [Header("Abilities")]
        public List<DefenderAbilityData> abilities = new List<DefenderAbilityData>();

        // 첫 매치 or null. null 원소(리스트 빈 슬롯)는 스킵.
        public T GetAbility<T>() where T : DefenderAbilityData
        {
            for (int i = 0; i < abilities.Count; i++)
                if (abilities[i] is T match) return match;
            return null;
        }

        // 배치 시 공격방향 지정(조준 페이즈) 필요 여부 — 능력이 선언(spec 계약 4).
        // 구 directionalAttack flag 의 대체. cut-over(unit 2)에서 UX 소비처가 이걸 읽는다.
        public bool RequiresFacing
        {
            get
            {
                for (int i = 0; i < abilities.Count; i++)
                    if (abilities[i] != null && abilities[i].RequiresFacing) return true;
                return false;
            }
        }

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

        // defender-deploy-cutscene unit 1 — 드래그 배치 스와이프 시 좌상단에 재생하는
        // 원샷 스프라이트 플립북. 비어 있으면 컷신 없음(유닛별 옵트인). 순수 프레젠테이션.
        [Header("Deploy Cutscene")]
        public Sprite[] deployCutsceneFrames;
        public float deployCutsceneFps = 24f;
        // 유닛별 표시 배율(재생기 displayScale 에 곱). 프레임 원본 크기가 유닛마다 달라도
        // 화면 노출 크기를 맞추거나 특정 유닛만 키울 때 사용. 1 = 재생기 기본 크기.
        public float deployCutsceneScale = 1f;
        // 유닛별 도착 위치 오프셋(px). 재생기 공유 baseline(cornerMarginPx)에 더해진다.
        // 프레임마다 캐릭터 위치/크기가 달라 컷신별로 안착 지점을 미세조정. (0,0)=baseline.
        public Vector2 deployCutsceneOffset;
        // depth-parallax unit 7 — 뎁스맵 프레임(패럴랙스용). 길이 1=정적 공유(기본), N=프레임별, 빈=색만(패럴랙스 없음).
        public Texture2D[] deployCutsceneDepth;
        // 유닛별 틸트 세기(컨트롤러가 SetTilt 전에 곱함). 게인은 컨트롤러가 단독 소유.
        public float deployCutsceneTiltGain = 1f;

        [Header("Knockback (per attack)")]
        public float knockbackDistance;   // world units. 0 = disabled
        public float knockbackDuration;   // seconds. velocity = direction * distance / duration

        [Header("Sleep on hit (per attack)")]
        // sleep-fighter-defender — RESOLVE 주 타겟(bestTarget 1체)에게 Sleep N초.
        // 근접(무투사체) 전제 — 투사체 유닛은 넉백과 동일하게 발사 시점 적용됨.
        public float sleepOnHitSec;       // seconds. 0 = disabled

        [Header("Knockup on hit (per attack)")]
        // knockup-fighter-defender — 히트한 **전 대상**에게 Stun N초 = 공중 띄우기.
        // ⚠ sleepOnHitSec 과 스코프가 다르다: 저쪽은 주 타겟 1체, 이쪽은 hitTargets 전원.
        // 다중 타겟 유닛에서 둘의 결과가 갈리므로 하나의 필드로 통합하지 말 것.
        // 떠오르는 연출은 뷰(unit 3)가 이 필드 보유를 보고 재생한다 — 심은 Stun 그대로다.
        public float knockupOnHitSec;     // seconds. 0 = disabled
        // 띄우기 연출 최고 높이(view 공간 단위). 심은 안 쓰고 뷰만 해석한다.
        // ⚠ sim-Y 가 아니다 — 평면 tilemap 보드라 BoardSpace 가 sim-Y 를 버린다(화면에 안 보임).
        public float knockupVisualHeight = 1.2f;

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
        // summon-patrol-defender unit 5 — 단 거점 수비 아군(Patrol)은 이동한다. 그 유닛만
        // walkAnimation 을 채운다. 기존 에셋은 null/"" 이라 현행 동작 그대로다(무회귀).
        public string SpineWalkAnimation => walkAnimation;
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
        public GameObject SpineWeaponTrailPrefab => weaponTrailPrefab;
        public float SpineWeaponTrailEndNormalized => weaponTrailEndNormalized;

        // dreamcatcher-awakening-hand unit 0 — awakening currency granted when
        // this defender dies ("death is income": sacrificing units feeds the
        // dreamcatcher economy). Appended last to keep serialization order
        // stable; existing assets pick up the initializer (4) until re-saved.
        [Header("Awakening")]
        public int awakeningReward = 4;

        // spine-weapon-trail unit 1 — 무기 궤적. 직렬화 순서를 흔들지 않게 맨 뒤에 덧붙인다.
        [Header("Weapon Trail")]
        // 프리팹 유무가 유일한 게이트다(빔 유닛의 beamVfxPrefab 과 같은 관례 — id/kind 분기 없음).
        // 미할당 = 무궤적이라 활·총을 든 원거리 유닛에 참격이 뜨지 않는다.
        public GameObject weaponTrailPrefab;
        // 방출 종료 지점 = 공격 애니 길이 대비 비율. 방출은 **스윙 구간에만** 걸어야 한다 —
        // Attack3 는 전체 0.867초 중 앞 0.267초만 스윙이고 나머지는 복귀 동작이라,
        // 끝까지 방출하면 칼을 되돌리는 자국이 남는다(0.267/0.867 ≈ 0.31).
        // 애니마다 다르므로 상수로 박지 않는다.
        [Range(0.05f, 1f)] public float weaponTrailEndNormalized = 0.31f;

        // summon-patrol-defender unit 2 — 거점 수비 아군(Patrol)의 이동 속도.
        // 일반 방어유닛은 타일 고정이라 PathFollowState 자체가 안 붙어서 이 값이 읽히지
        // 않는다. 직렬화 순서를 흔들지 않게 맨 뒤에 덧붙인다(기존 에셋은 초기값 유지).
        [Header("Patrol")]
        public float moveSpeed = 1.5f;
        // 이동하는 유닛만 채운다. 빈 값 = 단일 idle 루프(기존 방어유닛 동작, 무회귀).
        // 채우면 enemy-walk-anim-speed 의 발 미끄러짐 보정이 로코모션 루프에 함께 붙는다.
        public string walkAnimation = "";
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
        // 아래는 항상 **맨 뒤에** 추가한다 — 기존 유닛 에셋이 이 enum 을 int 로 직렬화해 두었다.
        ApplyStackNearby,   // bleed-fighter-defender unit 1 — 반경 내 적에게 onPlaceStackKind 스택 도포
        StunNearby,         // knockup-fighter-defender unit 1 — 반경 내 적 전원 Stun(착지 충격 = 넉업)
        DotNearby,          // beam-ranger-defender unit 2 — 반경 내 적 전원에 이산 tick DoT(개점 일제 조사)
    }
}
