using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LayerLab.ArtMaker
{
    /// <summary>
    /// 사운드 이름 상수 및 유틸리티 클래스
    /// Sound name constants and utility class
    /// </summary>
    public class SoundList
    {
        public const string Ambient = "amb";
        public const string ButtonArrow = "btn_arrow";
        public const string ButtonClose = "btn_close";
        public const string ButtonDefault = "btn_default";
        public const string ButtonEye = "btn_eye";
        public const string ButtonRandom = "btn_random";

        // 문자열 보간 방지용 정적 배열 / Static arrays to avoid string interpolation
        private static readonly string[] StepRightNames = { "character_step_right1", "character_step_right2", "character_step_right3" };
        private static readonly string[] StepLeftNames = { "character_step_left1", "character_step_left2", "character_step_left3" };

        /// <summary>
        /// 오른발 발자국 사운드 이름 랜덤 반환
        /// Return random right footstep sound name
        /// </summary>
        public static string StepRight ()
        {
            return StepRightNames[Random.Range(0, StepRightNames.Length)];
        }

        /// <summary>
        /// 왼발 발자국 사운드 이름 랜덤 반환
        /// Return random left footstep sound name
        /// </summary>
        public static string StepLeft ()
        {
            return StepLeftNames[Random.Range(0, StepLeftNames.Length)];
        }
    }
    
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        private AudioSource _audioEffect;
        private AudioSource _audioAmb;
        private int _soundIndex;

        // AudioClip 캐시 - Resources.Load 반복 호출 방지 / AudioClip cache to avoid repeated Resources.Load calls
        private readonly Dictionary<string, AudioClip> _clipCache = new();
        
        private void Awake()
        {
            Instance = this;
            _audioEffect ??= gameObject.AddComponent<AudioSource>();
            _audioAmb ??= gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            PlayBGM(SoundList.Ambient);
        }

        /// <summary>
        /// 배경음악 재생
        /// Play background music
        /// </summary>
        /// <param name="soundName">사운드 이름 / Sound name</param>
        public void PlayBGM(string soundName)
        {
            var clip = Resources.Load<AudioClip>($"@Sound-Forge/{soundName}");
            if(clip == null) return;
            _audioAmb.clip = clip;
            _audioAmb.loop = true;
            _audioAmb.Play();
        }
        
        /// <summary>
        /// 발자국 사운드 재생
        /// Play footstep sound
        /// </summary>
        public void PlayStepSound()
        {
            _soundIndex++;
            PlaySound(_soundIndex % 2 == 0 ? SoundList.StepLeft() : SoundList.StepRight());
        }
        
        /// <summary>
        /// 사운드 효과 재생
        /// Play sound effect (OneShot)
        /// </summary>
        /// <param name="soundName">사운드 이름 / Sound name</param>
        public void PlaySound(string soundName, float volume = 1.0f)
        {
            // 캐시에서 먼저 조회, 없으면 로드 후 캐시에 저장 / Check cache first, load and store on miss
            if (!_clipCache.TryGetValue(soundName, out var clip))
            {
                clip = Resources.Load<AudioClip>($"@Sound-Forge/{soundName}");
                if (clip != null) _clipCache[soundName] = clip;
            }
            if(clip == null) return;
            _audioEffect.PlayOneShot(clip, volume);
        }
    }
}