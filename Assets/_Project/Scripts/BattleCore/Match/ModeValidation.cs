using System.Collections.Generic;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — **모드 × 저작의 유효성.**
    //
    // 모드가 「어느 저작 자산을 쓸지」만 고르므로(원칙 3) 모드와 자산이 **서로 말이 안 되는**
    // 조합이 만들어질 수 있다. 그 조합은 컴파일도 되고 판도 시작하는데, 증상이 판 중간에
    // 나온다 — 「12웨이브를 막으라는데 덱이 10웨이브뿐」이면 그 판은 영영 안 끝난다.
    //
    // 그래서 **판 밖에서 한 번 묻는다.** 이 함수가 코어에 있는 이유는 헤드리스 lane 과
    // 에셋 lane 이 **같은 자**를 써야 하기 때문이다 — 두 벌이면 한쪽만 통과하는 조합이 생긴다.
    //
    // 반환은 문제의 목록이다(예외가 아니다). 저작을 고치는 사람이 **전부 한 번에** 봐야 하고,
    // 첫 문제에서 던지면 두 번째를 고치려고 다시 돌려야 한다.
    public static class ModeValidation
    {
        public static bool Validate(MatchDefinition def, List<string> problems)
        {
            problems?.Clear();
            if (def == null)
            {
                problems?.Add("정의표가 없다.");
                return false;
            }

            ref var mode = ref def.Mode;
            bool ok = true;

            if (string.IsNullOrEmpty(mode.ModeId))
            {
                Add(problems, "modeId 가 비었다 — 제출·리플레이·리더보드가 이 키를 저장한다.");
                ok = false;
            }

            if (mode.Clock == ClockKind.FixedLimit && mode.MatchSeconds <= 0f)
            {
                Add(problems, $"제한 시간 모드인데 길이가 {mode.MatchSeconds} 다.");
                ok = false;
            }

            // 목표 웨이브 ↔ 웨이브 원천. 「막을 웨이브」가 저작된 것보다 많으면 그 판은 안 끝난다.
            if (mode.TargetWaves > 0)
            {
                if (mode.WaveSource == WaveSourceKind.AuthoredPlan)
                {
                    int authored = def.WavePlan.HasWaves ? def.WavePlan.Waves.Length : 0;
                    if (authored < mode.TargetWaves)
                    {
                        Add(problems, $"목표 웨이브 {mode.TargetWaves} 인데 저작 플랜이 {authored} 웨이브뿐이다.");
                        ok = false;
                    }
                }
                else
                {
                    int max = def.WaveDeck.MaxWaveCount;
                    if (max > 0 && mode.TargetWaves > max)
                    {
                        Add(problems, $"목표 웨이브 {mode.TargetWaves} 인데 덱의 최대 웨이브 수가 {max} 다.");
                        ok = false;
                    }
                }
            }

            if (mode.WaveSource == WaveSourceKind.AuthoredPlan && !def.WavePlan.HasWaves)
            {
                Add(problems, "저작 플랜 모드인데 플랜에 웨이브가 없다.");
                ok = false;
            }

            // 타임어택 × 제한 시간 — 제한 시간이 **먼저** 끝내 버려 「빨리」가 의미를 잃는다.
            if (mode.Goal == GoalKind.TimeAttack && mode.Clock == ClockKind.FixedLimit)
            {
                Add(problems, "타임어택은 세는 시계를 전제한다 — 제한 시간이 먼저 끝내면 기록이 안 남는다.");
                ok = false;
            }

            // 저작 플랜 × 제한 시간은 **문제가 아니다**(플랜이 자기 길이를 갖고, 라이브 튜토리얼
            // 판이 그 조합이다). 여기 적어 두는 이유는 다음 사람이 「둘 다 막아야 하나」를
            // 묻지 않게 하기 위해서다.

            if (mode.HandSize > mode.DeckSize + mode.PublicActiveCount)
            {
                Add(problems, $"손패 {mode.HandSize} 가 덱 장수 {mode.DeckSize + mode.PublicActiveCount} 보다 크다.");
                ok = false;
            }

            return ok;
        }

        private static void Add(List<string> problems, string message) => problems?.Add(message);
    }
}
