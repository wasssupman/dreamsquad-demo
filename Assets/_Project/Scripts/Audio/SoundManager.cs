using UnityEngine;

namespace Wassup.Core
{
    // Lightweight global SFX player (sanctioned singleton — CLAUDE.md §5 / TRD §5.2,
    // 2026-07-07). Scene-local (no DontDestroyOnLoad); lives in BattleScene alongside
    // the score HUD. Round-robins a small pool of AudioSources so rapid ticks overlap
    // cleanly at their own pitch. Clips are authored-time assets (generated via
    // ElevenLabs Text-to-Sound-Effects) played locally — no runtime API calls.
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Score tick")]
        [Tooltip("처치 틱 클립. Null → no-op(무음).")]
        [SerializeField] private AudioClip scoreTickClip;
        [Range(0f, 1f)]
        [SerializeField] private float scoreTickVolume = 0.6f;

        [Header("Voice pool")]
        [Tooltip("동시 재생 겹침용 라운드로빈 보이스 수")]
        [SerializeField] private int voiceCount = 6;

        private AudioSource[] _voices;
        private int _next;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            int n = Mathf.Max(1, voiceCount);
            _voices = new AudioSource[n];
            for (int i = 0; i < n; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // 2D
                src.loop = false;
                _voices[i] = src;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Play the score tick at the given pitch (caller raises pitch on rapid streaks).
        // No-op if the clip is unassigned, so wiring is safe before a clip is picked.
        public void PlayScoreTick(float pitch = 1f)
        {
            if (scoreTickClip == null || _voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = Mathf.Clamp(pitch, 0.5f, 3f);
            src.PlayOneShot(scoreTickClip, scoreTickVolume);
        }
    }
}
