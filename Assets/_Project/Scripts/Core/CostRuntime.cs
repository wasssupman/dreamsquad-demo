using UnityEngine;

namespace Wassup.Core
{
    // Phase 6 resource manager. MonoBehaviour so Update() drives continuous
    // regen via Time.deltaTime, but intentionally NOT a singleton — GameManager
    // is the only allowed Instance in the project (CLAUDE.md rule). External
    // code accesses this via GameManager.CostRuntime or a wired SerializeField.
    //
    // Phase contract:
    //  - Configure(start, max, regen) at battle session start.
    //  - ResetToStart() at placement phase entry / Restart / Redraft.
    //  - BeginRegen() at battle phase entry. StopRegen() at result / teardown.
    //  - TrySpend returns false if insufficient; caller skips the action.
    public class CostRuntime : MonoBehaviour
    {
        private float _current;
        private float _max = 15f;
        private float _startingCost = 10f;
        private float _regenPerSec = 1f;
        private bool _regenActive;
        // dreamstone-loadout Unit 6 — CostRate stone multiplier, default 1 (no buff).
        private float _regenRateMultiplier = 1f;

        public float Current => _current;
        public float Max => _max;
        public int CurrentInt => Mathf.FloorToInt(_current);
        public bool RegenActive => _regenActive;
        public float RegenRateMultiplier => _regenRateMultiplier;

        public void Configure(float startingCost, float max, float regenPerSec)
        {
            _startingCost = startingCost;
            _max = Mathf.Max(1f, max);
            _regenPerSec = Mathf.Max(0f, regenPerSec);
        }

        // dreamstone-loadout Unit 6 — ownership contract (mirrors unit 3's
        // set-then-apply lesson, applied in reverse): ResetToStart()/Configure() run
        // on EVERY placement entry, including a mid-match Restart — and Restart is
        // contractually required to KEEP the squad's stone buffs. If either method
        // touched _regenRateMultiplier, a Restart would silently wipe the player's
        // equipped CostRate stones. So neither may ever set it. Only three call
        // sites own this value: GameManager.StartSquadMatch/StartTestModeMatch set
        // it from equipped stones at match-entry; DraftController.TryConfirm resets
        // it to 1 (a drafted match carries no squad stone buffs). RestartBattle
        // does not call SetRegenRateMultiplier at all — no-contact is the point.
        public void SetRegenRateMultiplier(float multiplier) => _regenRateMultiplier = Mathf.Max(0f, multiplier);

        public void ResetToStart()
        {
            _current = Mathf.Clamp(_startingCost, 0f, _max);
            _regenActive = false;
        }

        public void BeginRegen() => _regenActive = true;
        public void StopRegen() => _regenActive = false;

        public bool CanAfford(int amount) => _current >= amount;

        public int AddCost(int amount)
        {
            if (amount <= 0) return 0;
            float before = _current;
            _current = Mathf.Min(_max, _current + amount);
            return Mathf.FloorToInt(_current - before);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (_current < amount) return false;
            _current -= amount;
            return true;
        }

        // Optional: rollback a spend if the downstream operation failed.
        public void RefundSpend(int amount)
        {
            if (amount <= 0) return;
            _current = Mathf.Min(_max, _current + amount);
        }

        private void Update() => Tick(Time.deltaTime);

        // dreamstone-loadout Unit 6 — regen step extracted from Update() so EditMode
        // tests can drive it directly (MonoBehaviour.Update never runs in EditMode).
        // Behavior unchanged from the prior inline version, with regenPerSec now
        // scaled by the CostRate stone multiplier.
        public void Tick(float dt)
        {
            if (!_regenActive || _current >= _max) return;
            _current += _regenPerSec * _regenRateMultiplier * dt;
            if (_current > _max) _current = _max;
        }
    }
}
