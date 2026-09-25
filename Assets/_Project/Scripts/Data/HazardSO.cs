using UnityEngine;
using Wassup.Data.Authoring;

namespace Wassup.Data
{
    [CreateAssetMenu(menuName = "Wassup/Hazard", fileName = "Hazard_New")]
    public class HazardSO : ScriptableObject
    {
        [Header("Shape")]
        public HazardShape shape = HazardShape.SingleCell;
        public int radius = 1;

        [Header("Lifetime")]
        public float lifetime = 5f;

        [Header("Visual (decoupled)")]
        public GameObject visualPrefab;

        [Header("Effects (composition)")]
        public HazardEffect[] effects;

        // battle-core-rebuild unit 6b — **존 효과가 거는 대상 진영**(F34). 옛 전투는 적 전용
        // 하드 게이트였고 CLAUDE.md 제약 8 이 그것을 「축 없이 하드코딩한 실수」로 지목했다.
        // 기본값이 오늘의 게이트(적만)라 기존 에셋은 판이 안 바뀐다. 옛 전투는 이 필드를 안 읽는다.
        [Header("Targets (battle core)")]
        [Tooltip("존 효과가 걸리는 진영 비트. 기본 = 적만(옛 하드 게이트와 같다).")]
        public Wassup.Skills.Faction zoneTargetFactions = Wassup.Skills.Faction.EnemyUnit;
    }
}
