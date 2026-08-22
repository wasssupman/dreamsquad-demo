using UnityEngine;

namespace Wassup.Battle.Effects
{
    // bomb-barrel-on-place unit 6 — 설치물의 「퓨즈가 얼마나 탔나」.
    //
    // plain 값 입력 → plain 값 출력. ECS 도 MonoBehaviour 도 모른다(제약 10).
    // **0..1 스칼라만 돌려주고 색은 모른다** — 색·프로퍼티 이름·갱신 임계는 전부 뷰 정책이고,
    // 순수하게 결정되는 값은 「얼마나 탔나」 하나다. Color 를 반환하면 순수층이 프레젠테이션
    // 어휘를 알게 되어 「값이 아키텍처를 모른 채 흐른다」는 shape 이 깨진다.
    public static class BlockerFuse
    {
        // remainingLife: 남은 수명(초). lifetime: 총 수명(초, 0 이하 = 무한).
        // exponent: 1 = 선형, 클수록 **막판에 몰아서** 진행한다.
        //
        // ⚠ 지수를 역수(1/exponent)로 쓰면 곡선이 **정반대**가 된다 — 초반에 확 타고
        // 막판엔 변화가 없다. 주석으로만 막던 실수라 이 함수와 그 테스트가 유일한 방어다.
        public static float Progress(float remainingLife, float lifetime, float exponent)
        {
            if (lifetime <= 0f) return 0f; // 무한 수명 = 영원히 «막 놓인» 상태
            float t = Mathf.Clamp01(1f - remainingLife / lifetime);
            return Mathf.Pow(t, Mathf.Max(0.1f, exponent));
        }
    }
}
