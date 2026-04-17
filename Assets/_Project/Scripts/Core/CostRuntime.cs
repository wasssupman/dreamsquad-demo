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

        public float Current => _current;
        public float Max => _max;
        public int CurrentInt => Mathf.FloorToInt(_current);
        public bool RegenActive => _regenActive;

        public void Configure(float startingCost, float max, float regenPerSec)
        {
            _startingCost = startingCost;
            _max = Mathf.Max(1f, max);
            _regenPerSec = Mathf.Max(0f, regenPerSec);
        }

        public void ResetToStart()
        {
            _current = Mathf.Clamp(_startingCost, 0f, _max);
            _regenActive = false;
        }

        public void BeginRegen() => _regenActive = true;
        public void StopRegen() => _regenActive = false;

        public bool CanAfford(int amount) => _current >= amount;

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

        private void Update()
        {
            if (!_regenActive || _current >= _max) return;
            _current += _regenPerSec * Time.deltaTime;
            if (_current > _max) _current = _max;
        }
    }
}
