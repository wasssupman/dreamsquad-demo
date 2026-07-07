using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wassup.UI
{
    // 로비 world 캐릭터: 제자리 idle 루프 + 클릭/터치 시 interaction 원샷.
    // 리액션은 LobbyReactionLock 전역 잠금을 따른다 (다른 캐릭터 재생 중이면 무시).
    // 애니메이터는 기본 상태(idle 루프)와 exit time 복귀 전환으로 동작해 파라미터가 없다.
    [RequireComponent(typeof(Image))]
    public class WorldLobbyCharacter : MonoBehaviour, IPointerClickHandler
    {
        private const string ReactionState = "world_interaction";

        private Animator _animator;
        private float _reactionRemaining;
        private float _reactionLength;

        public bool IsReacting => _reactionRemaining > 0f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                if (clip.name == ReactionState)
                    _reactionLength = clip.length;
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

        // 리액션 시작. 자신 또는 다른 캐릭터가 재생 중이면 무시. 검증 툴 호출용 public.
        public void TriggerReaction()
        {
            if (IsReacting || !LobbyReactionLock.TryAcquire(this)) return;
            _animator.Play(ReactionState, 0, 0f);
            _reactionRemaining = _reactionLength;
        }

        // Update 에서 분리된 이유: 비포커스 에디터에선 프레임이 안 흘러, 에디터 검증 툴이
        // dt 를 직접 주입해 로직을 전진시킬 수 있어야 한다.
        public void Tick(float dt)
        {
            if (!IsReacting) return;
            _reactionRemaining -= dt;
            if (!IsReacting) LobbyReactionLock.Release(this);
        }
    }
}
