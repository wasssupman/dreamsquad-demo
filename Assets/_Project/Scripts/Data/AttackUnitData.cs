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

        public float health = 100f;
        public float moveSpeed = 2f;

        public float attackRange = 1f;
        public float attackCooldown = 1f;
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

        // nightmare-catcher unit 5 — 나이트매어캐쳐 메커닉 선언(정의 계층 DcMechanic,
        // ECS 무참조). 비어있지 않으면 이 적이 곧 보스: 스폰 베이크가 BossTag +
        // ThreatEntry(위협 테이블) + DcTriggerSlot 을 부착한다. 빈 배열/null =
        // 일반 적 무변경. Appended last (직렬화 back-compat).
        [Header("Nightmare Catcher")]
        public DcMechanic[] nightmareMechanics;

        // battle-score-formula unit 0 — 이 적을 **처치**했을 때 얻는 점수.
        // 유출당하면 얻지 못한다(EnemyKilledEvent 는 goal-reach 경로에서 발화하지
        // 않는다 — EnemyKilledEvent.cs 주석). 티어 enum 을 만들지 않고 이 필드의
        // 값 구간으로만 잡몹/보스를 구분한다(제약 8). Appended last (직렬화 back-compat).
        [Header("Score")]
        [Tooltip("처치 시 획득 점수. 유출당하면 얻지 못한다. 잡몹 100 / 보스 2000 기준.")]
        public int killScore = 100;

        // spine-weapon-trail unit 3 — 무기 궤적. 적/보스도 대상이다(디펜더 종속 해제).
        // 미할당 = 무궤적이라 잡몹 전원은 현행 그대로. Appended last (직렬화 back-compat).
        [Header("Weapon Trail")]
        public GameObject weaponTrailPrefab;
        [Range(0.05f, 1f)] public float weaponTrailEndNormalized = 0.31f;

        public GameObject SpineWeaponTrailPrefab => weaponTrailPrefab;
        public float SpineWeaponTrailEndNormalized => weaponTrailEndNormalized;
    }
}
