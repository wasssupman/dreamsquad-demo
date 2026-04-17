using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wassup.Core
{
    // Phase 7: deterministic per-session skill loadout. Given a pool of SkillData
    // (6 in the shipping config) and a target count (2), rolls a subset using a
    // seeded Fisher-Yates partial shuffle so identical seeds reproduce identical
    // picks — same contract shape as DraftController's seed field for the audit log.
    //
    // Non-singleton by design; GameManager holds the SerializeField reference and
    // Draft/Redraft flows trigger Roll explicitly. Restart path deliberately skips
    // Roll so the player retries the same conditions (Phase 7 decision Q6=a).
    public class SkillLoadoutController : MonoBehaviour
    {
        [SerializeField] private List<SkillData> defaultPool = new();
        [SerializeField] private int defaultCount = 2;

        private List<SkillData> _pool = new();
        private int _count;
        private readonly List<SkillData> _picked = new();
        private int _seed;
        private bool _hasRolled;

        public IReadOnlyList<SkillData> Picked => _picked;
        public int Seed => _seed;
        public bool HasRolled => _hasRolled;
        public IReadOnlyList<SkillData> Pool => _pool;

        private void Awake()
        {
            if (defaultPool.Count == 0)
                PopulateEditorFallbackPool();
            if (_pool.Count == 0 && defaultPool.Count > 0) _pool = new List<SkillData>(defaultPool);
            if (_count == 0) _count = defaultCount;
        }

        private void PopulateEditorFallbackPool()
        {
#if UNITY_EDITOR
            const string skillsRoot = "Assets/_Project/Data/Skills";
            var guids = AssetDatabase.FindAssets("t:SkillData", new[] { skillsRoot });
            if (guids == null || guids.Length == 0) return;

            defaultPool.Clear();
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (skill != null) defaultPool.Add(skill);
            }
#endif
        }

        // Replaces the pool / count used by subsequent Roll calls. Pass seed=0 to
        // let Roll pick a fresh time-based seed.
        public void Configure(IEnumerable<SkillData> pool, int count, int seed = 0)
        {
            _pool = new List<SkillData>(pool);
            _count = count;
            _seed = seed;
        }

        public void Configure(SkillData[] pool)
        {
            _pool = pool != null ? new List<SkillData>(pool) : new List<SkillData>();
            if (_count == 0) _count = defaultCount;
            _seed = 0;
        }

        // Produces a new random picked set. Idempotent per seed: same seed always
        // yields same picks, so logs can replay sessions exactly.
        public IReadOnlyList<SkillData> Roll()
        {
            if (_pool.Count == 0 || _count <= 0)
            {
                _picked.Clear();
                _hasRolled = true;
                return _picked;
            }

            if (_seed == 0) _seed = unchecked((int)System.DateTime.UtcNow.Ticks) | 1;
            var rng = new System.Random(_seed);

            var working = new List<SkillData>(_pool);
            int take = Mathf.Min(_count, working.Count);
            _picked.Clear();
            for (int i = 0; i < take; i++)
            {
                int j = i + rng.Next(working.Count - i);
                (working[i], working[j]) = (working[j], working[i]);
                _picked.Add(working[i]);
            }

            _hasRolled = true;
            return _picked;
        }

        // Called by BattleBridge teardown paths that preserve the picked set
        // (Restart). Redraft instead calls Roll again with a new seed.
        public void ResetRollState()
        {
            _picked.Clear();
            _hasRolled = false;
            _seed = 0;
        }
    }
}
