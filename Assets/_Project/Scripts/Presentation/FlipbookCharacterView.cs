using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // sprite-character-preview unit 0 — 캐릭터 상태 5개를 플립북 5개에 매핑하고 전이를 소유한다.
    //
    // 프레임 진행·클럭·스프라이트 쓰기는 SpriteFlipbookPlayer 가 이미 한다. 여기서 다시 하지 않는다.
    // 뷰가 얹는 것은 상태 매핑(폴백 포함)과 "원샷 완주 → Idle 복귀" 전이뿐이다.
    //
    // 확인용 도구다 — 게임 로직(스폰/전투 이벤트/사망 처리)에 연결되지 않는다.
    // 프리팹을 씬에 직접 배치하고 인스펙터 버튼으로 상태를 바꿔 눈으로 본다.
    public enum FlipbookCharacterState { Idle, Attack, Death, Deploy, Drag }

    [RequireComponent(typeof(SpriteFlipbookPlayer))]
    public class FlipbookCharacterView : MonoBehaviour
    {
        [Header("필수")]
        [SerializeField] private SpriteFlipbookData idle;
        [SerializeField] private SpriteFlipbookData attack;
        [SerializeField] private SpriteFlipbookData death;

        [Header("선택 — 비면 Idle 로 폴백")]
        [SerializeField] private SpriteFlipbookData deploy;
        [SerializeField] private SpriteFlipbookData drag;

        [Tooltip("활성화 시 Idle 재생. 프리팹을 씬에 놓자마자 보이게 하려면 켠다.")]
        [SerializeField] private bool playIdleOnEnable = true;

        private SpriteFlipbookPlayer _playerCache;
        private FlipbookCharacterState _current = FlipbookCharacterState.Idle;

        public FlipbookCharacterState Current => _current;

        // 재생기와 같은 이유로 lazy 조회 — RequireComponent 로 붙는 순서에 기대지 않는다.
        private SpriteFlipbookPlayer Player =>
            _playerCache != null ? _playerCache : (_playerCache = GetComponent<SpriteFlipbookPlayer>());

        public bool IsPlaying => Player != null && Player.IsPlaying;

        public static bool ShouldLoop(FlipbookCharacterState state) =>
            state == FlipbookCharacterState.Idle || state == FlipbookCharacterState.Drag;

        // Death 는 원샷이지만 복귀하지 **않는다** — 마지막 프레임을 유지한다.
        // 그래서 !ShouldLoop 로 복귀를 판정하면 안 된다(사망 캐릭터가 되살아난다).
        // 두 술어가 분리돼 있는 유일한 이유가 이것이다.
        public static bool ReturnsToIdle(FlipbookCharacterState state) =>
            state == FlipbookCharacterState.Attack || state == FlipbookCharacterState.Deploy;

        // 폴백 없는 원본 슬롯. Play 는 "폴백이 일어났는가" 를 알아야 해서 이걸 본다.
        private SpriteFlipbookData Own(FlipbookCharacterState state) => state switch
        {
            FlipbookCharacterState.Idle => idle,
            FlipbookCharacterState.Attack => attack,
            FlipbookCharacterState.Death => death,
            FlipbookCharacterState.Deploy => deploy,
            FlipbookCharacterState.Drag => drag,
            _ => null,
        };

        // 선택 상태(Deploy/Drag)가 비면 Idle 로 떨어진다 — Spine 쪽 ResolveAnimation 폴백 체인과 같은 정신.
        // Idle 자체가 비면 null 이 나가고, 재생기가 FrameCount == 0 에서 알아서 정지한다.
        public SpriteFlipbookData Resolve(FlipbookCharacterState state)
        {
            var data = Own(state);
            return data != null ? data : idle;
        }

        private void OnEnable()
        {
            if (playIdleOnEnable) Play(FlipbookCharacterState.Idle);
        }

        private void Update() => PollPlayback();

        // 선택 상태가 비어 Idle 데이터로 떨어지면 **상태까지** Idle 로 접는다.
        // 상태만 Deploy 로 남기면 ReturnsToIdle(Deploy) 는 참인데 실제로 도는 건 루프하는 idle 이라
        // IsPlaying 이 영원히 참 → 복귀가 영영 일어나지 않고 Current 가 Deploy 에 갇힌다.
        // 화면에는 idle 이 정상 재생돼 보여서 증상이 드러나지도 않는다.
        public void Play(FlipbookCharacterState state)
        {
            var data = Own(state);
            if (data == null)
            {
                state = FlipbookCharacterState.Idle;
                data = idle;
            }

            _current = state;
            WarnIfLoopPolicyViolated(state, data, this);

            var player = Player;
            if (player != null) player.Play(data);
        }

        // Update 본문에서 분리한 이유는 재생기의 Tick(dt) 과 같다 — 비포커스 에디터나 EditMode
        // 테스트에는 프레임이 없어서, 검증 툴이 전이를 프레임 없이 전진시킬 수 있어야 한다.
        //
        // 재생기의 자가 tick 은 그대로 둔다. 뷰가 클럭까지 소유하면 프레임이 두 배로 진행한다.
        // 대가로 전이가 최대 1프레임 늦지만 눈에 보이지 않는다.
        public void PollPlayback()
        {
            if (!ReturnsToIdle(_current)) return;

            var player = Player;
            if (player == null || player.IsPlaying) return;

            Play(FlipbookCharacterState.Idle);
        }

        // 원샷 상태에 루프 데이터가 들어오면 IsPlaying 이 영원히 참이라 상태가 갇힌다.
        // 원인이 컴포넌트가 아니라 **에셋의 체크박스**라, 로그가 에셋을 지목하지 않으면 추적이 오래 걸린다.
        //
        // 감지만 하고 고치지 않는다 — 재생기가 SpriteFlipbookData.Loop 를 직접 읽으므로 런타임에
        // 뒤집으려면 SO 에 써야 하고, 그건 확인용 도구가 사용자의 에셋을 조용히 바꾸는 것이다.
        private static void WarnIfLoopPolicyViolated(FlipbookCharacterState state, SpriteFlipbookData data, Object context)
        {
            if (data == null) return;

            bool wantLoop = ShouldLoop(state);
            if (data.Loop == wantLoop) return;

            if (wantLoop)
                Debug.LogError($"FlipbookCharacterView: '{state}' 는 루프 상태인데 '{data.name}' 의 loop 가 꺼져 있다 " +
                               "— 1회 재생 후 마지막 프레임에서 멈춘다.", context);
            else
                Debug.LogError($"FlipbookCharacterView: '{state}' 는 원샷 상태인데 '{data.name}' 의 loop 가 켜져 있다 " +
                               "— IsPlaying 이 영원히 참이라 상태가 갇히고 Idle 로 복귀하지 못한다.", context);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 재생 전에 오소링 시점에서 잡는다. 5슬롯을 각자의 정책과 대조한다.
            WarnIfLoopPolicyViolated(FlipbookCharacterState.Idle, idle, this);
            WarnIfLoopPolicyViolated(FlipbookCharacterState.Attack, attack, this);
            WarnIfLoopPolicyViolated(FlipbookCharacterState.Death, death, this);
            WarnIfLoopPolicyViolated(FlipbookCharacterState.Deploy, deploy, this);
            WarnIfLoopPolicyViolated(FlipbookCharacterState.Drag, drag, this);
        }
#endif
    }
}
