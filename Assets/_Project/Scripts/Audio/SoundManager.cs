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

        [Header("Attack SFX (melee)")]
        [Tooltip("공격 실행 효과음 볼륨. 클립은 유닛별 DefenderUnitData.attackSfxClip.")]
        [Range(0f, 1f)] [SerializeField] private float attackSfxVolume = 0.9f;
        [Tooltip("공격음 최소 간격(초) — 다수 근접 유닛 동시 타격 과중첩 방지")]
        [SerializeField] private float attackSfxMinInterval = 0.04f;

        [Header("Card absorb")]
        [Tooltip("카드 흡수 찰싹 틱 클립(card-fly-to-target-absorb). Null → no-op.")]
        [SerializeField] private AudioClip cardAbsorbClip;
        [Range(0f, 1f)] [SerializeField] private float cardAbsorbVolume = 0.7f;

        [Header("Deck / UI")]
        [Tooltip("손패 딜인(덱 드로우) 리플. Null → no-op.")]
        [SerializeField] private AudioClip cardDealClip;
        [Range(0f, 1f)] [SerializeField] private float cardDealVolume = 0.6f;
        [Tooltip("카드 집기(press-to-lift). Null → no-op.")]
        [SerializeField] private AudioClip cardPickupClip;
        [Range(0f, 1f)] [SerializeField] private float cardPickupVolume = 0.5f;
        [Tooltip("카드 손패 복귀(취소/실패). Null → no-op.")]
        [SerializeField] private AudioClip cardReturnClip;
        [Range(0f, 1f)] [SerializeField] private float cardReturnVolume = 0.5f;
        [Tooltip("UI 버튼 틱(게이지 토글 등). Null → no-op.")]
        [SerializeField] private AudioClip uiTickClip;
        [Range(0f, 1f)] [SerializeField] private float uiTickVolume = 0.5f;

        [Header("Deploy voice")]
        [Tooltip("배치 추임새 볼륨. 클립은 캐릭터별 DefenderUnitData.deployVoiceClip.")]
        [Range(0f, 1f)] [SerializeField] private float deployVoiceVolume = 0.85f;

        [Header("Deploy place SFX")]
        [Tooltip("유닛 배치(드롭) 통일 효과음. Null → no-op.")]
        [SerializeField] private AudioClip deployPlaceClip;
        [Range(0f, 1f)] [SerializeField] private float deployPlaceVolume = 0.4f;

        [Header("Boss warning")]
        [Tooltip("보스 경보 배너 스팅어(~2s). Null → no-op.")]
        [SerializeField] private AudioClip bossWarningClip;
        [Range(0f, 1f)] [SerializeField] private float bossWarningVolume = 0.85f;

        [Header("Gimmick reveal")]
        [Tooltip("기믹 리빌 등장음 볼륨. 클립은 GimmickRevealConfig 소유(기믹별 sfxClip → 공용 defaultSfxClip). PlayAttack/PlayDeployVoice 와 같은 '볼륨만 여기, 클립은 호출측' 형태.")]
        [Range(0f, 1f)] [SerializeField] private float gimmickRevealVolume = 0.8f;

        [Header("Cost tick")]
        [Tooltip("코스트가 자연 충전으로 다음 정수에 도달한 순간 블립(BGM 위로 뚫리게 밝은 톤). Null → no-op.")]
        [SerializeField] private AudioClip costTickClip;
        [Range(0f, 1f)] [SerializeField] private float costTickVolume = 0.7f;

        [Header("Next wave button")]
        [Tooltip("다음 웨이브 조기 소환 버튼 프레스(전용 만족감 사운드). Null → no-op.")]
        [SerializeField] private AudioClip nextWaveClip;
        [Range(0f, 1f)] [SerializeField] private float nextWaveVolume = 0.7f;

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
        private float _lastAttackSfx = -100f;
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

        // 근접 등 공격 실행 SFX(유닛별 DefenderUnitData.attackSfxClip). 다수 동시 타격 과중첩 방지 스로틀.
        public void PlayAttack(AudioClip clip)
        {
            if (clip == null || _voices == null) return;
            float t = Time.unscaledTime;
            if (t - _lastAttackSfx < attackSfxMinInterval) return;
            _lastAttackSfx = t;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = 1f;
            src.PlayOneShot(clip, attackSfxVolume);
        }

        // card-fly-to-target-absorb unit 1 — 카드가 유닛에 찰싹 흡수되는 임팩트 틱.
        public void PlayCardAbsorb()
        {
            if (cardAbsorbClip == null || _voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = 1f;
            src.PlayOneShot(cardAbsorbClip, cardAbsorbVolume);
        }

        // 덱/UI 원샷 (손패 딜인·집기·복귀·UI 틱). 라운드로빈 보이스 재사용, 미할당 시 no-op.
        private void PlayOneShot(AudioClip clip, float vol)
        {
            if (clip == null || _voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = 1f;
            src.PlayOneShot(clip, vol);
        }

        public void PlayCardDeal()   => PlayOneShot(cardDealClip, cardDealVolume);
        public void PlayCardPickup() => PlayOneShot(cardPickupClip, cardPickupVolume);
        public void PlayCardReturn() => PlayOneShot(cardReturnClip, cardReturnVolume);
        public void PlayUiTick()     => PlayOneShot(uiTickClip, uiTickVolume);

        // Per-character casual deploy interjection (clip from the deployed DefenderUnitData).
        public void PlayDeployVoice(AudioClip clip)
        {
            if (clip == null || _voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = 1f;
            src.PlayOneShot(clip, deployVoiceVolume);
        }

        // battle-audio: unit placement (drop) voice. clip != null → 유닛별 배치 보이스,
        // null → 통합 폴백(deployPlaceClip). 볼륨은 공통(deployPlaceVolume).
        public void PlayDeployPlace(AudioClip clip = null) => PlayOneShot(clip != null ? clip : deployPlaceClip, deployPlaceVolume);

        // boss-wave-cadence: 보스 경보 배너 슬램 순간 스팅어(~2s).
        public void PlayBossWarning() => PlayOneShot(bossWarningClip, bossWarningVolume);

        // gimmick-recognition-upgrade unit 2: 리빌 ① 도장(아이콘이 찍히는 순간) 등장음.
        // 클립은 GimmickRevealConfig 가 소유하고 여기로 넘어온다 — null 이면 PlayOneShot 이 no-op.
        public void PlayGimmickReveal(AudioClip clip) => PlayOneShot(clip, gimmickRevealVolume);

        // 코스트 물통이 자연 충전으로 다음 정수에 도달한 순간 블립. pitch 로 상승감(가득 찰수록 높게).
        public void PlayCostTick(float pitch = 1f)
        {
            if (costTickClip == null || _voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            src.PlayOneShot(costTickClip, costTickVolume);
        }

        // 다음 웨이브 조기 소환 버튼 프레스(전용 만족감 사운드).
        public void PlayNextWave() => PlayOneShot(nextWaveClip, nextWaveVolume);

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
