using UnityEngine;

namespace Wassup.Core
{
    // Lightweight global SFX + BGM player (sanctioned singleton — CLAUDE.md §5 /
    // TRD §5.2). Scene-local (BattleScene). Round-robins a small AudioSource pool for
    // overlapping one-shots (score tick, projectile fire) + a dedicated looping BGM
    // source. All clips are authored-time assets (ElevenLabs) played locally — no
    // runtime API calls. BGM auto-plays during the Battle phase (GameManager.PhaseChanged).
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Score tick")]
        [Tooltip("처치 틱 클립. Null → no-op.")]
        [SerializeField] private AudioClip scoreTickClip;
        [Range(0f, 1f)] [SerializeField] private float scoreTickVolume = 0.6f;

        [Header("Projectile fire")]
        [Tooltip("방어유닛 투사체 발사 클립. Null → no-op.")]
        [SerializeField] private AudioClip projectileFireClip;
        [Range(0f, 1f)] [SerializeField] private float projectileFireVolume = 0.4f;
        [Tooltip("발사음 최소 간격(초) — 다발 발사 시 과중첩 방지")]
        [SerializeField] private float projectileFireMinInterval = 0.045f;

        [Header("BGM")]
        [Tooltip("전투 배경음. Null → 무음.")]
        [SerializeField] private AudioClip bgmClip;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.35f;
        [Tooltip("Battle 페이즈에만 자동 재생/정지")]
        [SerializeField] private bool bgmOnlyInBattle = true;

        [Header("Voice pool")]
        [Tooltip("동시 겹침용 라운드로빈 보이스 수")]
        [SerializeField] private int voiceCount = 6;

        private AudioSource[] _voices;
        private int _next;
        private AudioSource _bgmSource;
        private float _lastProjectileFire = -100f;
        private bool _subscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            int n = Mathf.Max(1, voiceCount);
            _voices = new AudioSource[n];
            for (int i = 0; i < n; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.loop = false;
                _voices[i] = src;
            }

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.loop = true;
            _bgmSource.volume = bgmVolume;
            _bgmSource.clip = bgmClip;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        // Lazy-subscribe to phase changes (GameManager.Instance may not exist in Awake).
        private void Update() => EnsureSubscribed();

        private void EnsureSubscribed()
        {
            if (_subscribed || !bgmOnlyInBattle) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            OnPhaseChanged(GameManager.Instance.CurrentPhase);
        }

        private void Unsubscribe()
        {
            if (_subscribed && GameManager.Instance != null) GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Battle) PlayBgm();
            else StopBgm();
        }

        // Score tick at the given pitch (caller raises pitch on rapid streaks).
        public void PlayScoreTick(float pitch = 1f)
        {
            if (scoreTickClip == null || _voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = Mathf.Clamp(pitch, 0.5f, 3f);
            src.PlayOneShot(scoreTickClip, scoreTickVolume);
        }

        // Defender projectile launch. Throttled so dense volleys don't machine-gun.
        public void PlayProjectileFire()
        {
            if (projectileFireClip == null || _voices == null) return;
            float t = Time.unscaledTime;
            if (t - _lastProjectileFire < projectileFireMinInterval) return;
            _lastProjectileFire = t;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = 1f;
            src.PlayOneShot(projectileFireClip, projectileFireVolume);
        }

        public void PlayBgm()
        {
            if (_bgmSource == null || bgmClip == null) return;
            if (_bgmSource.isPlaying && _bgmSource.clip == bgmClip) return;
            _bgmSource.clip = bgmClip;
            _bgmSource.volume = bgmVolume;
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            if (_bgmSource != null && _bgmSource.isPlaying) _bgmSource.Stop();
        }
    }
}
