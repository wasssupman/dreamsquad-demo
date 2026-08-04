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
        // dreamcatcher-card-visibility unit 4 — 숨김(visible == 0) Active 카드를 래핑하는
        // 스킬을 풀 설정 시점에 제외하기 위한 카탈로그. null = 무필터(배선 없는 씬/테스트 하위호환).
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;

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
            if (_pool.Count == 0 && defaultPool.Count > 0) _pool = FilterHiddenSkills(defaultPool, cardCatalog);
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
            _pool = FilterHiddenSkills(pool, cardCatalog);
            _count = count;
            _seed = seed;
        }

        public void Configure(SkillData[] pool)
        {
            _pool = FilterHiddenSkills(pool, cardCatalog);
            if (_count == 0) _count = defaultCount;
            _seed = 0;
        }

        // dreamcatcher-card-visibility unit 4 — 풀 설정 시점의 숨김 스킬 제외. 순수 값
        // 연산(제약 10, DeckPrune 과 같은 shape) — EditMode 테스트 대상. Roll 이 아니라
        // 풀 자체를 걸러 Pool 프로퍼티(BattleLogger 의 pool+seed 기록)와 롤 대상을 일치시킨다.
        //
        // 제외 조건: 카탈로그에 이 스킬을 래핑하는 Active 카드가 존재하고 그 **전부**가
        // visible == 0. 래핑 카드가 없는 스킬은 보존한다 — 기존 "No Active card wraps
        // skill" 경고 경로(gift 시점) 그대로. catalog/pool null 은 무필터/빈 리스트.
        public static List<SkillData> FilterHiddenSkills(IEnumerable<SkillData> pool, DreamcatcherCardCatalog catalog)
        {
            var result = new List<SkillData>();
            if (pool == null) return result;
            var cards = catalog != null ? catalog.cards : null;
            foreach (var skill in pool)
            {
                if (skill == null || cards == null) { result.Add(skill); continue; }
                bool wrapped = false, anyVisible = false;
                for (int i = 0; i < cards.Length; i++)
                {
                    var c = cards[i];
                    if (c == null || c.type != CardType.Active || c.skill != skill) continue;
                    wrapped = true;
                    if (c.visible != 0) { anyVisible = true; break; }
                }
                if (!wrapped || anyVisible) result.Add(skill);
            }
            return result;
        }

        // battle-sim-extraction — 시드를 명시해 굴린다. 매치 진입 경로는 **반드시 이 오버로드**를
        // 쓴다: 인자 없는 Roll() 은 미설정 시 벽시계로 폴백해 같은 matchSeed 가 매 실행 다른
        // 로드아웃을 내고, 그 값이 MatchConfigSnapshot 에 캡처돼 configHash 를 흔든다.
        // 호출처는 GameManager.NextSkillRollSeed() 가 준 값을 넘긴다.
        public IReadOnlyList<SkillData> Roll(int seed)
        {
            if (seed != 0) _seed = seed;
            return Roll();
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
