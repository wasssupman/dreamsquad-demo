using Unity.Entities;

namespace Wassup.Battle.Units
{
    // dreamcatcher-kill-and-threshold unit 2 — 킬 귀속 규칙(contract 4)을 순수 fold 로
    // 고정. DamageApplicationSystem 이 한 프레임의 IncomingDamage 엔트리를 하나씩
    // Consider 로 접어 killer 를 뽑는다. 아키텍처-blind(값만 비교) — EditMode 로 결정성
    // (다중 source·동점) 회귀 고정. sim-critical 이라 인라인 대신 테스트 가능한 함수로 분리.
    public static class KillAttribution
    {
        // 규칙: source 非Null 엔트리 중 amount 최대가 killer. 동점(같은 amount)은
        // 먼저 접힌 엔트리(버퍼 순서상 앞)가 유지된다 — strict `>` 로 결정론.
        // source==Null(DoT/on-place/환경)은 후보에서 제외 → 전부 Null 이면 미귀속.
        public static void Consider(float amount, Entity source, ref Entity bestSource, ref float bestAmount)
        {
            if (source != Entity.Null && amount > bestAmount)
            {
                bestAmount = amount;
                bestSource = source;
            }
        }
    }
}
