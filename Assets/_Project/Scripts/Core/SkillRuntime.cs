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

        public int ReduceAllCooldowns(float seconds)
        {
            if (seconds <= 0f || _cooldownRemaining.Count == 0) return 0;

            int affected = 0;
            var keysSnapshot = new List<SkillData>(_cooldownRemaining.Keys);
            foreach (var key in keysSnapshot)
            {
                if (!_cooldownRemaining.TryGetValue(key, out var rem) || rem <= 0f) continue;
                float next = Mathf.Max(0f, rem - seconds);
                if (next <= 0f) _cooldownRemaining.Remove(key);
                else _cooldownRemaining[key] = next;
                affected++;
            }
            return affected;
        }

        // Called by BattleBridge at StartBattle / Restart / Redraft so stale
        // cooldown values never bleed across sessions.
        public void ResetAll()
        {
            _cooldownRemaining.Clear();
        }

        private void Update()
        {
            // battle-sim-extraction M0 unit 2 — 하네스 구동 중에는 `BattleBridge.StepOneTick`
            // 이 `Tick(StepDt)` 을 부른다. 여기서 막지 않으면 한 스텝에 두 번 깎인다.
            if (Wassup.Core.TimeControl.SimHarnessClock.Active) return;
            Tick(Time.deltaTime);
        }

        // 쿨다운 전진. 라이브는 `Update` 가 프레임 델타로, 하네스는 스텝이 고정 dt 로 부른다.
        // 라이브 dt 원천은 **바꾸지 않았다** — `Time.deltaTime` → 배틀 도메인 델타 전환은
        // 「슬로우모에서 쿨다운도 느려진다」는 별개의 게임 결정이라 이 unit 밖이다.
        public void Tick(float dt)
        {
            if (_cooldownRemaining.Count == 0) return;

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
