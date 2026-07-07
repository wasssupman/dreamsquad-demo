using UnityEngine;

namespace Wassup.UI
{
    // 로비 캐릭터 터치 리액션 전역 잠금. 한 캐릭터의 리액션이 재생 중이면
    // 모든 캐릭터의 새 리액션 진입을 막는다. 소유자 기반이라 자기 것만 해제 가능.
    // 잠금 획득 성공 = "터치 인터랙션이 실제로 시작됨" 이므로, 배경 전환 같은
    // 공용 연출은 개별 캐릭터가 아니라 여기의 ReactionStarted 를 구독한다.
    // (MonoBehaviour/씬 오브젝트가 아닌 순수 정적 상태 — Manager 싱글톤 금지 제약과 무관)
    public static class LobbyReactionLock
    {
        private static Component _owner;

        // 잠금 획득에 성공한 순간 발화. 인자는 트리거한 캐릭터 컴포넌트.
        public static event System.Action<Component> ReactionStarted;

        public static bool IsLocked => _owner != null;

        public static bool TryAcquire(Component owner)
        {
            if (_owner != null) return false;
            _owner = owner;
            ReactionStarted?.Invoke(owner);
            return true;
        }

        public static void Release(Component owner)
        {
            if (ReferenceEquals(_owner, owner)) _owner = null;
        }

        // 도메인 리로드 꺼짐 환경에서도 Play 시작 시 항상 깨끗한 상태 보장
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _owner = null;
            ReactionStarted = null;
        }
    }
}
