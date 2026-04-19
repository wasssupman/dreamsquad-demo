using UnityEngine;

namespace Wassup.Presentation
{
    // Phase 8 §13 — Meteor falling streak. Attached to Meteor_Falling_SKELETON
    // prefab root. Launched by VfxSpawner.SpawnMeteorFall at the moment the
    // warning ring appears; travels from `startWorld + up*height` down to the
    // impact point over `durationSec`, accelerating into the strike with a
    // quadratic ease-in. Destroys itself on landing so the Meteor_Burst prefab
    // (separate VFX) takes over the impact moment.
    [DisallowMultipleComponent]
    public class MeteorFall : MonoBehaviour
    {
        [SerializeField, Range(1f, 30f)] private float startHeight = 10f;

        private Vector3 _start;
        private Vector3 _target;
        private float _duration;
        private float _elapsed;
        private bool _launched;

        public void Launch(Vector3 targetWorld, float durationSec)
        {
            _target = targetWorld;
            _start = targetWorld + Vector3.up * startHeight;
            _duration = Mathf.Max(0.05f, durationSec);
            _elapsed = 0f;
            _launched = true;
            transform.position = _start;
        }

        private void Update()
        {
            if (!_launched) return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            // Quadratic ease-in: slower at top, fastest on landing.
            transform.position = Vector3.Lerp(_start, _target, t * t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
