using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;

namespace Wassup.Presentation
{
    // distance-based-range — **감지범위 디버그 sphere.**
    //
    // 사거리 판정이 몸 기준 거리로 바뀌고 보스에게 몸이 생긴 뒤, 화면에서 확인할 축이 둘이다:
    //   · **도달**(초록) = 이 유닛이 「점 대상」을 때릴 수 있는 경계 = `(사거리 + 0.5) × tileSize`
    //   · **몸**(빨강)   = 이 대상이 얼마나 큰가 = `bodyRadius × tileSize`
    // 실제 판정은 **둘의 합**이다 — A 의 도달구와 B 의 몸구가 닿으면 A 가 B 를 때린다.
    // 그래서 두 구를 따로 그린다. 하나로 합쳐 그리면 「누구의 무엇인지」가 사라진다.
    //
    // ⚠ **산식을 여기서 쓰지 않는다.** 반지름은 브리지가 술어 상수(`SkillMath.SelfBodyRadiusTiles`)로
    // 계산해 넘긴다 — 디버그 표시가 판정과 갈리면 디버그의 존재 이유가 사라진다.
    //
    // 기즈모라 **빌드에 안 들어가고**(에디터 전용 호출) Scene 뷰·Game 뷰의 Gizmos 토글로 켠다.
    // 씬 배선은 필요 없다 — 아무 GameObject 에 붙이면 브리지를 스스로 찾는다.
    [DisallowMultipleComponent]
    public sealed class ReachDebugGizmos : MonoBehaviour
    {
        [Tooltip("도달 구(초록/파랑)를 그린다. 「이 유닛이 어디까지 때리나」.")]
        public bool showReach = true;
        [Tooltip("몸 구(빨강)를 그린다. 「이 대상이 얼마나 큰가」 — 보스 bodyRadius 확인용.")]
        public bool showBody = true;
        [Tooltip("방어유닛의 도달도 그린다. 끄면 적만 본다(밀집 판에서 화면이 덜 지저분하다).")]
        public bool includeDefenders = true;

        [Tooltip("몸이 0 인 대상도 표시(아주 작은 점 구). 「저작이 안 됐다」와 「대상이 없다」를 가른다.")]
        public bool markZeroBody;

        private BattleBridge _bridge;
        private readonly List<BattleBridge.DebugReachSphere> _scratch = new();

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;   // sim 이 돌 때만 의미가 있다
            if (!showReach && !showBody) return;
            if (_bridge == null) _bridge = FindFirstObjectByType<BattleBridge>();
            if (_bridge == null) return;

            _bridge.DebugCollectReachSpheres(_scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                var s = _scratch[i];
                if (s.isDefender && !includeDefenders) continue;

                if (showReach && s.reachWorld > 0f)
                {
                    // 방어유닛 파랑 / 적 초록 — 누구의 사거리인지 색으로 가른다.
                    Gizmos.color = s.isDefender
                        ? new Color(0.35f, 0.7f, 1f, 0.9f)
                        : new Color(0.4f, 1f, 0.35f, 0.9f);
                    Gizmos.DrawWireSphere(s.viewPos, s.reachWorld);
                }
                if (showBody && (s.bodyWorld > 0f || markZeroBody))
                {
                    Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.95f);
                    Gizmos.DrawWireSphere(s.viewPos, s.bodyWorld > 0f ? s.bodyWorld : 0.06f);
                }
            }
        }
    }
}
