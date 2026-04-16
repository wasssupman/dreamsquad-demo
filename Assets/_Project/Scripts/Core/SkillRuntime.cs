using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // Tracks per-skill cooldown state for the duration of one battle session.
    // MonoBehaviour by design (Time.deltaTime tick) but intentionally NOT a
    // singleton — GameManager remains the sole static Instance in the project
    // (CLAUDE.md). External code accesses this via GameManager.skillRuntime or
    // the inspector-wired reference on BattleBridge.
    public class SkillRuntime : MonoBehaviour
    {
        private readonly Dictionary<SkillData, float> _cooldownRemaining = new();
        private readonly List<SkillData> _expireBuffer = new();

        public bool IsReady(SkillData skill)
        {
            if (skill == null) return false;
            return !_cooldownRemaining.TryGetValue(skill, out var rem) || rem <= 0f;
        }

        // Caller is responsible for having checked IsReady first; Consume is the
        // commit half of the ready→cast transaction.
        public void Consume(SkillData skill)
        {
            if (skill == null) return;
            _cooldownRemaining[skill] = skill.cooldownSec;
        }

        public float GetRemainingSeconds(SkillData skill)
        {
            if (skill == null) return 0f;
            return _cooldownRemaining.TryGetValue(skill, out var rem) ? Mathf.Max(0f, rem) : 0f;
        }

        public float GetRemainingNormalized(SkillData skill)
        {
            if (skill == null || skill.cooldownSec <= 0f) return 0f;
            var rem = GetRemainingSeconds(skill);
            return Mathf.Clamp01(rem / skill.cooldownSec);
        }

        // Called by BattleBridge at StartBattle / Restart / Redraft so stale
        // cooldown values never bleed across sessions.
        public void ResetAll()
        {
            _cooldownRemaining.Clear();
        }

        private void Update()
        {
            if (_cooldownRemaining.Count == 0) return;
            var dt = Time.deltaTime;

            _expireBuffer.Clear();
            foreach (var kv in _cooldownRemaining)
            {
                var next = kv.Value - dt;
                if (next <= 0f) _expireBuffer.Add(kv.Key);
            }
            foreach (var key in _expireBuffer) _cooldownRemaining.Remove(key);

            // Second pass updates survivors; avoids mutating during the earlier enumeration.
            var keysSnapshot = new List<SkillData>(_cooldownRemaining.Keys);
            foreach (var key in keysSnapshot)
            {
                _cooldownRemaining[key] -= dt;
            }
        }
    }
}
