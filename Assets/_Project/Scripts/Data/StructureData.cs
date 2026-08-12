using UnityEngine;
using Wassup.Battle.Units;

namespace Wassup.Data
{
    // battle-structures unit 3 — 거점 정의. 마음(Core)과 본능(Instinct)이 같은 SO 타입을
    // 쓰고 kind 로 갈린다.
    //
    // **진영은 여기 없다.** 방어 본능과 적 본능이 같은 스탯일 수 있어 SO 를 두 벌 만들게
    // 되고 «진영만 다른 같은 거점» 이 데이터 중복이 된다. 진영은 배치가 정한다
    // (MapDocument.structures 의 side × 이 kind → StructurePlacements.DeriveFaction).
    [CreateAssetMenu(fileName = "Structure", menuName = "Wassup/Map/StructureData", order = 2)]
    public class StructureData : ScriptableObject
    {
        public string displayName;

        [Header("Identity")]
        [Tooltip("마음(Core) = 진영당 1, 지켜야 할 것 · 본능(Instinct) = 맵당 N, 공격하는 것")]
        public StructureKind kind = StructureKind.Core;

        [Header("Stats")]
        [Tooltip("거점 체력. 거점은 각자 체력을 갖고 각자 무너진다(계약 7).")]
        [Min(1f)] public float health = 1000f;

        [Header("View")]
        [Tooltip("거점 프랍. unit 4 가 소비한다. 후보: KayKit Platformer Pack.")]
        public GameObject viewPrefab;
        // instinct-content unit 0 rev — 프랍 스케일 knob. 시험용 프리팹이 footprint 3×3 대비
        // 과대해(사용자 지적) 프리팹 복제 대신 저작값으로 줄인다(제약 6 — 수치는 SO).
        [Tooltip("프랍 스케일 배율. 1 = 프리팹 원본.")]
        [Min(0.05f)] public float viewScale = 1f;

        // ── 본능 공격 (unit 5 가 소비) ─────────────────────────────────────────
        // 마음은 공격하지 않는다 — AttackState 를 부여받지 않으므로 아래는 무시된다.
        // targetFactions 는 AttackUnitData.targetFactions 와 **같은 축·같은 의미**다.
        // 본능이 «유닛과 같은 파이프라인» 을 타는 근거(계약 10).
        [Header("Attack (Instinct only)")]
        [Tooltip("이 본능이 노리는 대상(진영 × 종류). 기본 = 방어 유닛만(포탑).")]
        public Faction targetFactions = Faction.DefenderUnit;
        [Min(0f)] public float attackRange = 3f;
        [Min(0.01f)] public float attackCooldown = 1.5f;
        [Min(0f)] public float attackDamage = 10f;
        [Tooltip("본능의 투사체. 없으면 unit 5 가 발사를 건너뛴다.")]
        public ProjectileData projectile;
    }
}
