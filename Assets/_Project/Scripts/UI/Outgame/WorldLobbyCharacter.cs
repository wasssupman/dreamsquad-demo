using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wassup.UI
{
    // 로비 world 캐릭터: 제자리 idle 루프 + 클릭/터치 시 interaction 원샷.
    // 리액션은 LobbyReactionLock 전역 잠금을 따른다 (다른 캐릭터 재생 중이면 무시).
    // 애니메이터는 기본 상태(idle 루프)와 exit time 복귀 전환으로 동작해 파라미터가 없다.
    [RequireComponent(typeof(Image))]
    public class WorldLobbyCharacter : MonoBehaviour, IPointerClickHandler, ILobbyKeyringTarget
    {
        private const string ReactionState = "world_interaction";
        private const string IdleState = "world_idle";

        private Animator _animator;
        private float _reactionRemaining;
        private float _reactionLength;
        private bool _keyringSuspended;

        public bool IsReacting => _reactionRemaining > 0f;

        // 리액션 수명(=클립 길이)을 확보하지 못하면 리액션에 아예 진입하지 않는다.
        // 길이 0 으로 진입하면 락을 잡은 직후 Tick 이 !IsReacting 으로 빠져나가 카운트다운이
        // 한 번도 돌지 않고, 전역 락이 영구 고착돼 로비 전체 캐릭터가 조용히 반응을 멈춘다.
        private bool CanReact => _animator != null && _reactionLength > 0f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            var controller = _animator != null ? _animator.runtimeAnimatorController : null;
            if (controller == null)
            {
                Debug.LogError("WorldLobbyCharacter: Animator/RuntimeAnimatorController 미할당 — 리액션 비활성.", this);
                return;
            }
            foreach (var clip in controller.animationClips)
                if (clip.name == ReactionState)
                    _reactionLength = clip.length;
            if (_reactionLength <= 0f)
                Debug.LogError($"WorldLobbyCharacter: 리액션 클립 '{ReactionState}' 을 컨트롤러에서 못 찾음 — 리액션 비활성. " +
                               "클립 이름을 바꿨다면 상수도 같이 고칠 것.", this);
        }

        // 리액션 중 비활성화되면 Update 가 멈춰 카운트다운이 끝나지 않는다. 파괴와 달리
        // 컴포넌트는 살아 있어 락이 자동 해제되지 않으므로(TryAcquire 의 fake-null 회수 불발)
        // 여기서 명시적으로 놓는다. 실제 경로: 로그아웃 시 lobbyCharactersRoot.SetActive(false).
        // 파괴 시에도 활성 오브젝트면 OnDisable 이 먼저 불리므로 OnDestroy 해제는 불필요.
        private void OnDisable()
        {
            _reactionRemaining = 0f;
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

        // 리액션 시작. 자신 또는 다른 캐릭터가 재생 중이면 무시. 검증 툴 호출용 public.
        // 단발 클릭만 리액션 — 키링 드래그/낙하 중(suspended)에는 진입 불가(스와이프와 구분).
        public void TriggerReaction()
        {
            if (!CanReact || _keyringSuspended || IsReacting || !LobbyReactionLock.TryAcquire(this)) return;
            _animator.Play(ReactionState, 0, 0f);
            _reactionRemaining = _reactionLength;
        }

        // lobby-keyring-drag — 드래그 픽업: 진행 중 리액션 강제 종료 + idle 즉시 전환
        // (스와이프 중에는 IDLE 만 재생 — 2026-07-07 사용자 결정).
        public void SuspendForKeyring()
        {
            if (IsReacting)
            {
                _reactionRemaining = 0f;
                LobbyReactionLock.Release(this);
            }
            if (_animator != null) _animator.Play(IdleState, 0, 0f);
            _keyringSuspended = true;
        }

        // 착지 완료: 새 위치에서 idle 재개.
        public void ResumeFromKeyring()
        {
            _keyringSuspended = false;
        }

        // Update 에서 분리된 이유: 비포커스 에디터에선 프레임이 안 흘러, 에디터 검증 툴이
        // dt 를 직접 주입해 로직을 전진시킬 수 있어야 한다.
        public void Tick(float dt)
        {
            if (_keyringSuspended || !IsReacting) return;
            _reactionRemaining -= dt;
            if (!IsReacting) LobbyReactionLock.Release(this);
        }
    }
}
