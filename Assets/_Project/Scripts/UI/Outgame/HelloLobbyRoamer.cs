using UnityEngine;
using UnityEngine.EventSystems;

namespace Wassup.UI
{
    // 로비 hello 캐릭터 좌우 로밍 + 클릭/터치 리액션.
    // 이동 중에는 walk 애니메이션, 정지 시 idle(0프레임). 클릭하면 attack/interaction 중
    // 하나를 랜덤 재생하고, 재생 중 추가 입력은 무시한다. 걷기/대기 중에는 언제든 리액션 진입 가능.
    // Animator 는 IsWalking bool + 리액션은 state 직접 Play(원샷, exit time 으로 idle 복귀).
    // 튜닝값은 전부 프리팹 SerializeField (canvas 좌표 단위).
    public class HelloLobbyRoamer : MonoBehaviour, IPointerClickHandler, ILobbyKeyringTarget
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        // hello_attack 상태는 컨트롤러에 남아 있지만 풀에서 제외 (2026-07-07 사용자 결정)
        private static readonly string[] ReactionStates = { "hello_interaction" };

        [SerializeField] private float minX = -600f;
        [SerializeField] private float maxX = 600f;
        [SerializeField] private float moveSpeed = 160f;
        [SerializeField] private Vector2 idleDurationRange = new Vector2(1.5f, 3.5f);
        [SerializeField] private float arriveThreshold = 4f;

        private RectTransform _rt;
        private Animator _animator;
        private float _targetX;
        private float _idleRemaining;
        private bool _walking;
        private float _reactionRemaining;
        private bool _keyringSuspended;
        private readonly float[] _reactionLengths = new float[ReactionStates.Length];

        public bool IsWalking => _walking;
        public bool IsReacting => _reactionRemaining > 0f;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _animator = GetComponent<Animator>();
            _idleRemaining = Random.Range(idleDurationRange.x, idleDurationRange.y);
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                for (int i = 0; i < ReactionStates.Length; i++)
                    if (clip.name == ReactionStates[i])
                        _reactionLengths[i] = clip.length;
            }
        }

        private void OnDestroy()
        {
            LobbyReactionLock.Release(this);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerReaction();
        }

        // 리액션 시작. 자신 또는 다른 캐릭터가 재생 중이면 무시(전역 잠금). 검증 툴 호출용 public.
        public void TriggerReaction()
        {
            if (IsReacting || !LobbyReactionLock.TryAcquire(this)) return;
            int pick = Random.Range(0, ReactionStates.Length);
            _walking = false;
            _animator.SetBool(IsWalkingHash, false);
            _animator.Play(ReactionStates[pick], 0, 0f);
            _reactionRemaining = _reactionLengths[pick];
        }

        // lobby-keyring-drag — 드래그 픽업: 진행 중 리액션 강제 종료 + 로밍 정지.
        // 리액션 클립은 exit time 으로 idle 에 자연 복귀하므로 애니는 건드리지 않는다.
        public void SuspendForKeyring()
        {
            if (IsReacting)
            {
                _reactionRemaining = 0f;
                LobbyReactionLock.Release(this);
            }
            _walking = false;
            _animator.SetBool(IsWalkingHash, false);
            _keyringSuspended = true;
        }

        // 착지 완료: 새 위치에서 idle 타이머 재추첨 후 로밍 자연 재개.
        public void ResumeFromKeyring()
        {
            _keyringSuspended = false;
            _idleRemaining = Random.Range(idleDurationRange.x, idleDurationRange.y);
        }

        // Update 에서 분리된 이유: 비포커스 에디터에선 프레임이 안 흘러, 에디터 검증 툴이
        // dt 를 직접 주입해 로직을 전진시킬 수 있어야 한다.
        public void Tick(float dt)
        {
            if (_keyringSuspended) return;
            if (IsReacting)
            {
                _reactionRemaining -= dt;
                if (!IsReacting)
                {
                    LobbyReactionLock.Release(this);
                    _idleRemaining = Random.Range(idleDurationRange.x, idleDurationRange.y);
                }
                return;
            }

            if (_walking)
            {
                Vector2 pos = _rt.anchoredPosition;
                float next = Mathf.MoveTowards(pos.x, _targetX, moveSpeed * dt);
                _rt.anchoredPosition = new Vector2(next, pos.y);
                if (Mathf.Abs(next - _targetX) <= arriveThreshold)
                {
                    _walking = false;
                    _idleRemaining = Random.Range(idleDurationRange.x, idleDurationRange.y);
                    _animator.SetBool(IsWalkingHash, false);
                }
                return;
            }

            _idleRemaining -= dt;
            if (_idleRemaining > 0f) return;

            _targetX = Random.Range(minX, maxX);
            if (Mathf.Abs(_targetX - _rt.anchoredPosition.x) <= arriveThreshold)
                return; // 현재 위치와 사실상 같으면 다음 tick 에 재추첨

            _walking = true;
            _animator.SetBool(IsWalkingHash, true);
            Vector3 s = _rt.localScale;
            s.x = Mathf.Abs(s.x) * (_targetX > _rt.anchoredPosition.x ? 1f : -1f);
            _rt.localScale = s;
        }
    }
}
