using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackUnit", menuName = "Wassup/AttackUnit", order = 10)]
    public class AttackUnitData : ScriptableObject, ISpineUnitVisualData
    {
        // unit-stat-spreadsheet-schema Unit 1 — stable id for spreadsheet import
        // matching. Fixed once assigned; independent of asset/display name.
        // Mirrors DefenderUnitData.id.
        public string id;
        public string displayName;

        // traversal-layers unit 2 — 이 유닛이 **지날 수 있는** 층.
        //
        // ⚠ 타입이 `PlacementLayer` 인 것은 이름이 거짓말하는 것이다. 통행 판정은
        // «(셀 층 & 유닛 층) != 0» 이라 **셀과 같은 비트 공간**이어야 하고, 셀 층은
        // `PlacementLayers.Derive(tiles)` 가 만든다. 같은 비트를 가진 병렬 enum 을 만드는 건
        // 순수 중복이므로(제약 8) 타입을 재사용한다. 리네임(`PlacementLayer` → `CellLayer`)은
        // 참조가 40곳 넘어 후속 후보다.
        //
        // **배치 층과 다른 축이다.** 배치는 «여기 설 수 있나», 통행은 «여기 지날 수 있나».
        // 실제로 갈리는 예가 있다 — 스폰·골 칸은 배치가 닫히지만 통행은 반드시 열려야 한다
        // (README §0 의 회귀가 정확히 이 혼동이었다).
        //
        // 폴백 = `Path`(현행 재현). «방어유닛 = 자기 `placementLayers`» 폴백은 방어유닛을
        // 실제로 움직이는 별도 spec 의 몫이다 — 지금 그렇게 하면 순찰 소환물이 `Ground` 로
        // 떨어져 앵커가 자기 마스크 밖이 되고 굳는다(README §4).
        public PlacementLayer traversalLayers = PlacementLayer.None;
        public PlacementLayer EffectiveTraversalLayers
            => traversalLayers == PlacementLayer.None ? PlacementLayer.Path : traversalLayers;


        // enemy-class-system Unit 0 — enemy archetype LABEL only. Behavior is NOT
        // derived from this; it comes from the Behavior fields below (enemy-behavior-components).
        [Header("Class")]
        public EnemyClass enemyClass = EnemyClass.None;

        // wave-pattern unit 12 — 등장 게이트. 이 유닛이 나올 수 있는 가장 이른 웨이브(1부터).
        // 1 = 제한 없음(기본, 기존 자산 전부 불변). 예: Runner=2 → 첫 웨이브에 절대 안 나온다.
        // 적용 범위는 seed 생성 경로(WavePatternGenerator.Generate)뿐 — 작성 플랜(WavePlanAsset)은
        // 디자이너가 명시한 배치를 그대로 존중한다.
        [Header("Wave Gating")]
        [Tooltip("이 유닛이 등장할 수 있는 가장 이른 웨이브(1부터). 1 = 제한 없음. seed 생성 웨이브에만 적용.")]
        [Min(1)] public int minWaveNumber = 1;

        // structure-hunter-enemy unit 1 — 종류별 동시 등장 상한.
        // minWaveNumber 가 «언제부터» 라면 이쪽은 «한 번에 몇 마리까지» 다.
        // 생성기에는 이 축이 없었다: 일반 웨이브는 countA = rng.NextInt(1, total) 로 한 종류가
        // 최대 maxUnitsPerWave 까지, 보스 웨이브 호위는 3~4기까지 나온다. 기존 적 12종은 전부
        // 몸으로 막을 수 있어 문제가 되지 않았지만, 유인·차단이 통하지 않는 적(마음사냥꾼)은
        // 수량이 곧 «막을 수 없는 시간» 이라 상한 없이는 웨이브 하나가 판을 끝낸다.
        // 0 = 무제한(기본) → 기존 자산 전부 불변. 적용 범위는 minWaveNumber 와 같다
        // (seed 생성 경로만 — 작성 플랜은 디자이너 명시 배치를 존중한다).
        [Tooltip("한 웨이브에 이 유닛이 나올 수 있는 최대 수. 0 = 무제한. seed 생성 웨이브에만 적용.")]
        [Min(0)] public int maxPerWave = 0;

        // enemy-behavior-components Unit 0 — behavior-as-data. Selected per-SO and
        // baked to ECS (attackMethod → attack components; EnemyBehavior/EnemyTargetFilter).
        // Decouples function from visuals; lets one class have sub-variants.
        [Header("Behavior")]
        public EnemyAttackMethod attackMethod = EnemyAttackMethod.Melee;
        public EnemyTargetMode targetMode = EnemyTargetMode.Nearest;
        // enemy-ai-fsm — Engaging 이동 정책(구 aimMode 대체). Halt=멈춰서 공격, Advance=이동하며 공격.
        public EngageMovement engageMovement = EngageMovement.Halt;
        public DefenderClass targetPriorityClass = DefenderClass.None; // None = no priority
        public DefenderClassFlags targetClassMask = DefenderClassFlags.Everything;
        // battle-structures unit 1 — 저작 타겟 마스크(진영 × 종류). «이 적은 무엇을 노리는
        // 놈인가». targetClassMask(어느 **직업**의 방어유닛)와 다른 축이다 — 거점에는
        // DefenderClassTag 가 없어 classMask 가 애초에 적용되지 않는다.
        // None(0) = 미저작 → 베이크 시 EnemyTargetDefaults.LegacyEnemyMask 로 폴백. 이는
        // 마이그레이션이 아니라 «인스펙터에서 마스크를 비웠을 때 무장 해제되는 것» 방어선이다
        // (기존 에셋 무회귀는 아래 이니셜라이저가 보장한다).
        // 예: DefenderCore 단독으로 두면 «거점만 때리는 적» 이 된다.
        // 기본값 = **상대 진영 전부**(`EnemyTargetDefaults.DefaultEnemyMask`). 방어측
        // `DefenderUnitData.targetFactions`(= AnyEnemy)와 같은 shape 이다 — 한쪽만 비트를
        // 열거하면 방어측 종류가 늘 때 조용히 «무적 대상» 이 생긴다(2026-08-12 방어 본능 사고).
        //
        // ⚠ **미저작 적의 실제 기본값은 이 이니셜라이저다**(`Resolve` 의 «0 = 폴백» 이 아니다).
        // YAML 에 키가 없으면 이 값이 남아 폴백 분기를 안 탄다 — 두 값을 같은 상수로 묶어둔 이유.
        [Tooltip("이 적이 노리는 대상(진영 × 종류). 비우면(None) 기본값 = 상대 진영 전부.")]
        public Wassup.Battle.Units.Faction targetFactions =
            (Wassup.Battle.Units.Faction)Wassup.Battle.Combat.EnemyTargetDefaults.DefaultEnemyMask;

        public float health = 100f;
        public float moveSpeed = 2f;

        public float attackRange = 1f;
        public float attackCooldown = 1f;
        // distance-based-range unit 3 — 이 유닛의 **몸 반경**(칸). 0 = 점(오늘과 동일).
        // 사거리·투사체 충돌이 이 값만큼 유효 반경을 넓힌다 — 큰 몸은 큰 표적이라는 물성.
        // ⚠ **시트에 컬럼이 없다** → 임포터가 스킵하므로 SO 저작으로 끝난다(고아 컬럼
        // `aggroRange` 와 대칭). 반대로 같이 조정될 보스 HP 는 **시트가 정본**이라
        // `.asset` 만 고치면 다음 로그인 임포트가 되돌린다.
        // ⚠ **unit 9 에서 기본값이 0 → 0.25 로 바뀌었다.** 「점」이던 시절엔 공격자 쪽 상수
        // 0.5 가 몸을 대신했고 대상은 몸이 없었다. 이제 양쪽이 자기 몸을 들고 오며,
        // 일반 유닛끼리는 0.25 + 0.25 = 0.5 로 **종전과 도달 거리가 같다.**
        // 저작 키가 있는 에셋(보스 3종)만 자기 값을 유지하므로 그쪽은 재기준이 필요했다.
        [Min(0f)] public float bodyRadius = 0.25f;
        // unit 13 (계약 1 rev 3, 2026-09-01 외부 세션) — 적 몸 크기는 **티어**다:
        // 소 0.25 / 중 0.5 / 대 1.0 / 보스 개별(위 bodyRadius float). 표를 타입으로 강제해
        // 「사거리 N」의 상대별 실거리를 유한하게 만든다 — 근거 없는 소수(0.4)가 저작될 수
        // 없다. Large 는 예약(오늘 소비 0) — 첫 대형 적이 올 때 코드 무변으로 켠다.
        // ⚠ bodyRadius 는 Boss 티어에서만 읽힌다. 시트에 두 컬럼 다 없음 — SO 저작으로 끝.
        public enum BodySize { Small = 0, Medium = 1, Large = 2, Boss = 3 }
        public BodySize bodySize = BodySize.Small;
        // unit 16 — **임팩트 소켓**(월드 유닛, 지면 위 높이). 투사체 «뷰» 가 마지막 구간에서
        // 이 높이로 꽂히고 임팩트 VFX 도 여기서 터진다 — 명중 판정은 접지 원 그대로(sim 무변).
        // 0 = 미저작 → 종전 경로(투사체 저작 visualHeightOffset). 몸통 중앙쯤을 저작한다.
        [Min(0f)] public float impactSocketHeight = 0f;
        // enemy-detection-range unit 1 — 계약 8 의 이행 지점. 감지 반경이 공격 사거리보다 좁으면
        // 무의미하다(그 안이면 이미 사거리 안이라 교전 중이다). 저작 실수를 조용히 통과시키지 않는다.
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (detectionRange >= MinDetectionRange && detectionRange <= attackRange)
                Debug.LogWarning(
                    $"[AttackUnitData] {name}: detectionRange({detectionRange}) 가 attackRange({attackRange}) 이하다 — " +
                    "감지가 아무 일도 하지 않는다(그 안이면 이미 사거리 안이다).", this);

            // 리뷰 L6 — 무기 없이 구워질 적에 감지를 저작하면 `DetectionSystem` 이 fail-closed 로
            // 막지만(계약 11) **`DefenderHunterTag` 부착은 못 막는다.** 그 태그 하나로
            // `DefenderFieldSystem` 의 매 프레임 그리드 BFS 가 켜지고 소스 반경은 인접 폴백으로
            // 떨어진다 — 기능은 안 깨지고 낭비만 남으므로 저작 시점에 말해 준다.
            if (UsesDetection
                && attackMethod != EnemyAttackMethod.None
                && (outputs == null || outputs.Length == 0))
                Debug.LogWarning(
                    $"[AttackUnitData] {name}: 감지를 저작했는데 outputs 가 비었다 — walk-only 로 구워져 " +
                    "감지는 fail-closed 로 막히고 사냥 필드 재빌드만 켜진다(순수 낭비).", this);
        }
#endif

        public float BodyRadiusTiles => bodySize switch
        {
            BodySize.Medium => 0.5f,
            BodySize.Large => 1.0f,
            BodySize.Boss => bodyRadius,
            _ => Wassup.Skills.SkillMath.StandardBodyRadiusTiles,   // Small = 표준 소형 상대
        };
        // enemy-behavior-components Unit 6 — melee AoE. Nearest N in-range targets hit
        // per attack (melee/outputs path). 1 = single-target. Aggroed enemies are
        // forced to 1 (guardian-only) by AttackSystem.
        public int attackTargetCount = 1;
        public ProjectileData projectile;
        // attack-hit-delay — 공격 시작 후 타격 판정까지 지연(초). 0 = 즉시.
        public float hitDelaySec = 0f;

        // aggro-targeting Unit 0 — taunt attack. Used ONLY while aggroed, by
        // enemies that have no normal outputs (Runner/Swift) so they can still
        // hit the guardian holding them. Ignored during normal (non-aggro)
        // movement. Concrete numbers delegated to the balancing spec.
        [Header("Aggro (Taunt) Attack")]
        public float aggroAttackDamage = 0f;
        public float aggroAttackCooldown = 1f;
        public float aggroAttackRange = 1f;

        // modifier-legacy-migration unit 1: hit outputs are the runtime source
        // of truth for enemy attacks. Enemies with no outputs deal no runtime damage.
        [Header("Attack Outputs")]
        public AttackOutput[] outputs;

        // elite-whirlpot unit 1 — 적의 «유닛별 공격 VFX». `DefenderUnitData.attackVfxPrefab`
        // 대칭이며, 적 쪽에는 지금까지 이 축이 아예 없었다(적 연출은 Spine 애니와 투사체뿐).
        //
        // ★**어느 적이 이 연출을 갖는지는 이 슬롯의 유무가 결정한다.** id·이름 분기 금지이고
        // `attackTargetCount > 1` 로도 판정하지 않는다 — 그러면 Basic·Tanker·짱쎈까지 회오리가
        // 생긴다. 빔 유닛 판정("빔 유닛인가는 SO 의 프리팹 유무가 결정한다")과 같은 규율.
        //
        // 미할당이 **기본값이자 정상**이므로(적 17종 전부) 브리지는 경고 없이 넘어간다.
        // 브레스의 「빈 슬롯 경고」와 다른 이유: 그쪽은 전역 슬롯 하나여서 비어 있으면 곧
        // 버그이지만, 이쪽은 유닛별 opt-in 이라 비어 있는 것이 대다수의 정상 상태다.
        [Header("Attack VFX (optional — 슬롯 유무가 곧 opt-in)")]
        public GameObject attackVfxPrefab;
        // **타일당** 스케일 — 최종 = 이 값 × `attackRange`. 광역 반경이 `attackRange` 이고
        // 그게 튜닝 knob 이라, 고정 스케일로 저작하면 반경을 바꿀 때 연출이 조용히 어긋난다.
        // `VfxSpawner.areaBreathScalePerTile` 이 같은 벤 자리에서 나온 같은 관례다.
        public float attackVfxScalePerTile = 1f;

        public Mesh visualMesh;
        public Material visualMaterial;

        [Header("Spine")]
        public SkeletonDataAsset skeletonDataAsset;
        public string spineSkinName;
        public string idleAnimation = "idle";
        // enemy-walk-anim-speed unit 4 — 이동 중 걷기 애니. 비면 idleAnimation 단일 루프(현행).
        // 정지가 잦은 적(예: 헌터 보스)은 idle 을 진짜 idle 로, walk 를 여기에 두어 슬로모 걷기 회피.
        public string walkAnimation = "";
        public string attackAnimation = "attack";
        public string deathAnimation = "die";
        public float spineVisualScale = 1f;

        // unit-parts-appearance 0 — 파츠 조합 외형. 비어 있으면 spineSkinName 단일 스킨.
        // 순서가 의미를 갖는다(뒤 파츠가 슬롯 단위로 덮음).
        [Header("Parts Appearance")]
        [SpineSkin(dataField: "skeletonDataAsset")] public List<string> partSkins = new List<string>();
        public List<SpineSlotColor> slotColors = new List<SpineSlotColor>();

        // enemy-spawn-positioning 0 — 비주얼 피봇 미세조정(view-space). 기본 0 = 피봇이 이동타일 중심(sim 좌표)에 정렬.
        public Vector3 visualOffset;

        public string SpineDisplayName => displayName;
        public SkeletonDataAsset SpineSkeletonDataAsset => skeletonDataAsset;
        public string SpineSkinName => spineSkinName;
        public string SpineIdleAnimation => idleAnimation;
        public string SpineWalkAnimation => walkAnimation;
        public IReadOnlyList<string> SpineIdleVariants => idleVariants;
        public string SpineAttackAnimation => attackAnimation;
        public string SpineDeathAnimation => deathAnimation;
        public float SpineVisualScale => spineVisualScale;
        public Vector3 SpineVisualOffset => visualOffset;
        public IReadOnlyList<string> SpinePartSkins => partSkins;
        public IReadOnlyList<SpineSlotColor> SpineSlotColors => slotColors;

        // dreamcatcher-awakening-hand unit 0 — awakening currency granted when
        // this nightmare dies. Class-scale backfill: small/runner 1, mid 2,
        // large/special 3. Appended last to keep serialization order stable;
        // existing assets pick up the initializer (1) until backfilled.
        [Header("Awakening")]
        public int awakeningReward = 1;

        // nightmare-catcher unit 5 — 특수 메커닉 선언(정의 계층 DcMechanic, ECS 무참조).
        // 비어있지 않으면 스폰 베이크가 `DcTriggerSlot` 을 부착한다. 빈 배열/null = 무변경.
        //
        // ★**«비어있지 않으면 이 적이 곧 보스» 는 더 이상 참이 아니다**(elite-enemy-tier unit 0).
        // BossTag·ThreatEntry·등장경보는 이제 `tier == EnemyTier.Boss` 에서만 나온다. 그래서
        // **특수 메커닉을 가진 «보스가 아닌 적»**(엘리트)이 성립한다 — 그게 그 spec 의 요점이다.
        // 이 필드 이름이 이제 실제 범위보다 좁다(보스 전용이 아니다). rename 하지 않는 이유는
        // 라이브 에셋들이 이 YAML 키를 들고 있어서다.
        [Header("Nightmare Catcher")]
        public DcMechanic[] nightmareMechanics;

        // three-minute-kill-race unit 1 — **`killScore` 필드는 은퇴했다.** 개체 1킬 = 1점이고
        // 예외가 없다(보스도 분열체도 1). 점수를 등급으로 가르던 축이 사라졌으므로 이 자리에
        // 밸런스 값을 다시 만들지 말 것 — 강함의 차이는 체력·공격력·등장 빈도로 표현한다.
        // 처치 보상 중 **적별로 다른 것은 `awakeningReward` 하나**다.

        // spine-weapon-trail unit 3 — 무기 궤적. 적/보스도 대상이다(디펜더 종속 해제).
        // 미할당 = 무궤적이라 잡몹 전원은 현행 그대로. Appended last (직렬화 back-compat).
        [Header("Weapon Trail")]
        public GameObject weaponTrailPrefab;
        [Range(0.05f, 1f)] public float weaponTrailEndNormalized = 0.31f;

        // three-minute-survival unit 0 — 이 적이 골을 뚫었을 때 골 안정도에서 깎는 양.
        // 처치 보상과 **반대 축**이다: 이쪽은 놓쳤을 때의 대가다. ~~티어 enum 을
        // 만들지 않고 값 구간으로 일반/엘리트/보스를 구분한다(제약 8).~~
        // → elite-enemy-tier unit 0 이 `tier` 필드를 만들었다(위 killScore 주석과 같은 근거).
        // 이 필드는 여전히 밸런스 값이며 `tier` 와 서로 검증하지 않는다.
        // heart-stress-axis unit 0 rev 2 — **돌격형(`attackMethod: None`)의 마음 직격.**
        //
        // 이 값을 소비하는 조건은 **`canSiege == false`** 다 — 「마스크에 `DefenderCore` 가 없다」,
        // 즉 «마음을 조준할 수 없어 도달하면 소멸하는 적». 라이브에서는 Runner·Swift 2종이고
        // 둘은 **일반 공격을 갖는다**(unit 7 — `attackMethod: Melee`). 「공격이 없는 적」이 아니라
        // 「마음만 못 때리는 적」이다. 마음을 조준할 수 있는 적은 이 값을 **쓰지 않는다**. 즉 이 필드는 「유출 피해」가
        // 아니라 **돌격형의 한 방**이고, 그래서 값 대역이 공격력과 같은 축이어야 한다.
        //
        // 마음 HP 는 덱의 `goalStabilityMax`(라이브 **1500** — unit 5)다. 옛 값 대역(일반 1 /
        // 엘리트 2 / 보스 5)은 마음 HP 가 1~5 이던 시절의 유물이라 1000 에서는 0.1% = 무해였다.
        // 현재 저작: 돌격형 50. 마음 1500 기준 **한 마리당 3.3%**, 30마리 통과 = 판 종료.
        //
        // ⚠ unit 5 에서 HP 를 1000→1500 으로 올릴 때 이 값을 **같이 올리지 않았다**(75 후보 기각).
        // 밸런스 1패스는 손잡이를 하나만 돌린다 — 여기를 함께 스케일하면 HP 변경이 이 채널에
        // 한해 무효가 되고, 다른 모든 피해원이 1.5배 물러진 판에서 돌격형만 옛 무게로 남아
        // **상대적으로 1.5배 강해진다**. 통과가 시시하게 느껴지면 그때 이 값만 따로 올린다.
        // 공성형의 값은 **inert** 다.
        // Appended last (직렬화 back-compat).
        [Header("Goal Stability")]
        [Tooltip("돌격형(targetFactions 에 마음이 없는 적)이 마음에 도달했을 때 한 번에 꽂는 피해. " +
                 "마음을 조준할 수 있는 적은 이 값을 쓰지 않는다(공격력으로 때린다).")]
        [Min(0)] public int stabilityDamage = 1;

        public GameObject SpineWeaponTrailPrefab => weaponTrailPrefab;
        public float SpineWeaponTrailEndNormalized => weaponTrailEndNormalized;

        // waypoint-routing unit 3 — 맵 waypointPaths 인덱스. -1 = 기존 골 직행.
        // 스폰 시 맵 경로 존재 여부를 검증하고, 유효할 때만 Movement 컴포넌트를 부착한다.
        [Header("Waypoint Routing")]
        [Tooltip("따를 맵 웨이포인트 경로 인덱스. -1 = 사용 안 함(골 직행).")]
        [Min(-1)] public int waypointPathIndex = -1;

        // waypoint-routing unit 4 — sim 높이가 아니라 view-space 상시 lift.
        // 이동 규칙은 traversalLayers=Air 가 소유하고, 이 값은 떠 보이는 표현만 맡는다.
        // 0 = 지상(기존 적 전부 무변경). Appended last (직렬화 back-compat).
        [Header("Flight View")]
        [Tooltip("비행 적의 상시 화면 높이. 0 = 지상. 이동/타게팅 규칙에는 관여하지 않는다.")]
        [Min(0f)] public float flightLift = 0f;

        // summon-patrol-defender unit 10 — idle 변형 풀. 적도 가질 수 있는 성질이라 공용
        // 인터페이스에 있고, 여기선 저작 슬롯만 연다. 비어 있음 = 현행(단일 idle 루프).
        // 지금 이 값을 채운 적 에셋은 없다.
        [Header("Idle Variants")]
        [Tooltip("대기 중 번갈아 재생할 애니 이름들. 비우면 idleAnimation 단일 루프.")]
        public List<string> idleVariants = new List<string>();

        // elite-enemy-tier unit 0 — 등급 축. **BossTag·위협테이블·등장경보의 유일한 출처**다
        // (그 앞까지는 「nightmareMechanics 가 비어있지 않으면 곧 보스」였다 —
        // BattleBridge.BakeNightmareMechanics). 폴백 Normal 이라 기존 에셋 17종 중 보스 3종에만
        // Boss 를 찍으면 무회귀다. 값 대역(stabilityDamage)과는 독립 축이다 —
        // 근거는 EnemyTier.cs 주석. Appended last (직렬화 back-compat).
        [Header("Tier")]
        [Tooltip("일반/엘리트/보스. BossTag·위협테이블·등장경보는 Boss 에서만 나온다. " +
                 "엘리트는 특수 메커니즘을 갖되 보스 특권(CC·어그로 면역)은 받지 않는다.")]
        public EnemyTier tier = EnemyTier.Normal;

        // enemy-detection-range unit 1 — **감지 반경.** 「이 적은 몇 칸 안의 방어유닛을
        // 발견하는가」. 발견하면 진행 경로를 벗어나 그쪽으로 향하고, 사거리에 들면 기존대로
        // 멈춰 싸운다.
        //
        //   0  = 감지 안 함(기본). 사거리에 들어온 것만 친다 — 24종 중 20종의 값이다.
        //   >0 = 반경(칸).
        //   <0 = 무제한. 맵 어디든 방어유닛이 있으면 사냥한다.
        //
        // ⚠ **구 `huntsDefenders`(bool)를 흡수했다.** 그쪽은 「무제한 사냥」 한 점만 표현할 수
        // 있었고, 그 사이의 유한 반경이 이 축의 값이다. 축을 둘로 두지 않는다.
        // ⚠ **`tier == Boss` 는 더 이상 사냥을 주지 않는다.** 예전 부착 조건이
        // `Boss || huntsDefenders` 라 보스는 저작 없이 사냥을 공짜로 받았는데, 그러면
        // 「저작을 안 하면 티어가 대신 말한다」는 숨은 규칙이 생겨 축이 다시 둘이 된다.
        // 보스 3종은 `-1` 을 **명시 저작**한다(누락은 `EnemyTierBakeTests` 가 잡는다).
        // ⚠ **시트에 컬럼이 없다** → 임포터가 스킵하므로 SO 저작으로 끝난다
        // (`bodyRadius`·`bodySize` 와 같은 처지). Appended last (직렬화 back-compat).
        [Tooltip("몇 칸 안의 방어유닛을 발견하면 경로를 벗어나 달려드는가. " +
                 "0 = 감지 안 함(기본), 음수 = 무제한 사냥.")]
        public float detectionRange = 0f;

        // 감지를 쓰는 적인가. 부착·게이트가 이 술어 하나를 지난다 — 「!= 0」을 호출처마다 다시
        // 쓰면 음수(무제한)의 취급이 조용히 갈린다.
        //
        // ⚠ **`!= 0f` 가 아니라 임계 비교다**(리뷰 지적). float 이라 `0.001` 저작이 `!= 0` 을
        // 통과해 `DefenderHunterTag` 를 붙이는데 반경은 무의미해진다 — 「태그는 붙었는데 아무것도
        // 감지 못 하는」 적이 생기고, 그건 `DefenderFieldSystem` 의 재빌드 게이트까지 켠다.
        public const float MinDetectionRange = 0.05f;
        public bool UsesDetection => detectionRange < 0f || detectionRange >= MinDetectionRange;
        public bool HasUnlimitedDetection => detectionRange < 0f;
    }
}
