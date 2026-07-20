using UnityEngine;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Presentation
{
    // sprite-flipbook-player unit 2 — SpriteFlipbookData 를 SpriteRenderer 위에서 재생하는 얇은 컴포넌트.
    // 프레임 판정은 FlipbookMath(순수), 재생 속성은 SO 에 위임하고 여기는 시간 누적 + 스프라이트 반영 +
    // 수명만 소유한다. 정렬(sortingLayer/order)과 GameObject 수명은 소비자가 소유한다 — 재생기는 안 건드린다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlipbookPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteFlipbookData flipbook;

        [Tooltip("클럭 도메인. 전투 이펙트는 Battle(슬로우모 동반), UI/연출은 Interaction.")]
        [SerializeField] private TimeDomain timeDomain = TimeDomain.Battle;

        [Tooltip("활성화 시 자동 재생.")]
        [SerializeField] private bool playOnEnable = true;

        [Tooltip("원샷 완주 후 SpriteRenderer 를 끈다. 해제 시 마지막 프레임이 그대로 남는다.")]
        [SerializeField] private bool disableRendererWhenFinished;

        private SpriteRenderer _renderer;
        private float _elapsed;
        private bool _playing;
        private int _shown = -1;

        public bool IsPlaying => _playing;

        // 완료 콜백 대신 IsPlaying 폴링이 계약이라, 루프 플립북(영원히 IsPlaying == true)을
        // 소비자가 구분할 수단이 필요하다. 이게 없으면 풀 회수 코루틴이
        // `WaitWhile(() => player.IsPlaying)` 에서 영구 대기하며 인스턴스를 샌다.
        public bool IsLooping => flipbook != null && flipbook.Loop;

        // 비활성 GameObject 에 AddComponent 직후 Play() 하면 Awake 전이라 필드가 비어 있다 — lazy 조회.
        private SpriteRenderer Renderer =>
            _renderer != null ? _renderer : (_renderer = GetComponent<SpriteRenderer>());

        // flipbook 이 아직 없으면 자동 재생을 건너뛴다 — 소비자가 런타임에 Play(data) 로 주입하는
        // 경로가 정상이라, 여기서 경고하면 정상 사용에 거짓 경고가 뜬다.
        private void OnEnable()
        {
            if (playOnEnable && flipbook != null) Play();
        }

        // 재활성화 시 Play 가 처음부터 다시 시작하도록 상태만 내린다(렌더러는 건드리지 않는다).
        private void OnDisable() => _playing = false;

        // 클럭은 도메인 시간. Time.deltaTime 을 쓰면 전투 슬로우모 중 이펙트만 정속으로 돌아 어긋난다.
        // 정지 상태에서는 도메인 스케일 조회조차 하지 않는다(완주한 재생기가 씬에 남아도 매 프레임 일하지 않게).
        private void Update()
        {
            if (!_playing) return;
            Tick(TimeManager.Instance.DeltaTime(timeDomain));
        }

        public void Play(SpriteFlipbookData data)
        {
            flipbook = data;
            Play();
        }

        // 처음부터 재생(재트리거 = 재시작).
        public void Play()
        {
            _elapsed = 0f;
            _shown = -1;

            if (flipbook == null || flipbook.FrameCount == 0)
            {
                _playing = false;
                return;
            }

            _playing = true;
            // 끄는 기능과 켜는 기능이 같은 플래그를 소유한다 — 이 플래그를 안 쓰는 소비자는
            // 렌더러 enabled 를 온전히 자기가 소유한다.
            SetRendererEnabled(true);

            // 첫 프레임 즉시 반영 — 한 프레임 동안 이전 스프라이트가 남는 깜빡임 방지.
            Tick(0f);
        }

        // 중도 취소. disableRendererWhenFinished 로 렌더러 가시성 소유권을 재생기에 넘긴 소비자는
        // 여기서도 렌더러가 정리될 것을 기대한다 — 안 그러면 취소된 원샷의 중간 프레임이 화면에
        // 그대로 멈춰 남는다(소비자는 소유권을 넘겼다고 믿어 직접 끄지 않는다).
        public void Stop()
        {
            _playing = false;
            SetRendererEnabled(false);
        }

        // 렌더러 가시성은 disableRendererWhenFinished 가 켜졌을 때만 재생기 소유다.
        // teardown 중 호출(소비자의 OnDisable/OnDestroy → Stop())이면 SpriteRenderer 가 먼저 파괴돼
        // lazy 조회가 null 을 돌려줄 수 있으므로 여기서 한 번에 막는다.
        private void SetRendererEnabled(bool value)
        {
            if (!disableRendererWhenFinished) return;
            var r = Renderer;
            if (r != null) r.enabled = value;
        }

        // Update 에서 분리된 이유: 비포커스 에디터에선 프레임이 안 흘러, 검증 툴이 dt 를 직접 주입해
        // 로직을 전진시킬 수 있어야 한다(로비 캐릭터 선례).
        public void Tick(float dt)
        {
            if (!_playing || flipbook == null) return;

            _elapsed += dt;

            int count = flipbook.FrameCount;
            float fps = flipbook.Fps;
            bool loop = flipbook.Loop;

            int index = FlipbookMath.FrameAt(_elapsed, fps, count, loop);
            if (index != _shown)
            {
                _shown = index;
                var sprite = flipbook.FrameAt(index);
                // 빈 슬롯은 직전 프레임 유지 — 렌더러가 순간 비는 것보다 낫다.
                // 원인 자체는 SpriteFlipbookData.OnValidate 경고가 잡는다.
                if (sprite != null) Renderer.sprite = sprite;
            }

            // 완료 판정은 프레임 반영 **뒤에** 온다. 순서가 뒤집히면 마지막 프레임이 한 번도 안 그려진다.
            if (!FlipbookMath.IsFinished(_elapsed, fps, count, loop)) return;

            _playing = false;
            SetRendererEnabled(false);
        }
    }
}
