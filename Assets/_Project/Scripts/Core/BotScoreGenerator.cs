using System;

namespace Wassup.Core
{
    public static class BotScoreGenerator
    {
        public static int[] GenerateBotScores(int count, int playerScore, int seed)
        {
            if (count <= 0) return Array.Empty<int>();

            var random = new Random(seed);
            var scores = new int[count];
            int minScore = Math.Max(0, (int)Math.Floor(playerScore * 0.5f));
            int maxScore = Math.Max(minScore, (int)Math.Ceiling(playerScore * 1.5f));

            for (int i = 0; i < count; i++)
            {
                double roll = (random.NextDouble() + random.NextDouble()) * 0.5d;
                scores[i] = minScore + (int)Math.Round((maxScore - minScore) * roll);
            }

            Array.Sort(scores, (a, b) => b.CompareTo(a));
            return scores;
        }
    }
}
