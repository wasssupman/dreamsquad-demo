// salvaged from Assets/_Project/Scripts/Data/EffectTilePlacer.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `GeneratedMap` → `MapSnapshot`, `List<int2>` 반환 → 호출자 버퍼 + 개수 반환.
// 소금 XOR · `|1u` 0-시드 가드 · row-major 수집 · partial Fisher-Yates 는 그대로다 — M16.
using Unity.Mathematics;

namespace Wassup.BattleCore.Map
{
    // 「효과 타일을 어느 칸에 놓나」의 순수 규칙.
    //
    // 셋이 함께 있어야 성립한다(M16) — 하나만 빠져도 조용히 틀린다:
    //   · **소금 XOR** — 프랍 배치와 같은 맵 시드를 쓰므로, 섞지 않으면 효과 타일이 프랍과
    //     같은 칸에 몰린다(둘 다 같은 난수열의 앞부분을 뽑는다).
    //   · **`| 1u` 0-시드 가드** — 시드 0 은 생성기가 별도 폴백을 타서 같은 시드가 다른 판을 만든다.
    //   · **row-major 수집** — 후보 순서가 곧 셔플 입력이다. 순서가 흔들리면 같은 시드가 다른 칸을 준다.
    //
    // ⚠ 효과 타일은 **배치지면(Ground) 층 고정**이다 — 경로 위로 번지지 않는다.
    // 배치 결합 시스템이라 통행이 아니라 **배치 정본**(`PlaceMask`)을 따른다.
    public static class EffectTileSelect
    {
        // 프랍 배치와 decorrelate 하는 XOR 상수.
        private const int SeedSalt = 0x51F15EED;

        // unit 6b — 종류 배정 난수열의 소금. 옛 `BattleBridge` 의 `seed ^ 0x7EFFEC7` 그대로다 —
        // 칸 뽑기와 **다른 난수열**이어야 한다(같으면 「몇 번째 칸」이 곧 「어느 종류」가 된다).
        private const int KindSalt = 0x7EFFEC7;

        /// <summary>
        /// 뽑힌 칸마다 종류를 정한다(`kindCount` 개 중 하나). 옛 unit 4 규칙 — **칸마다 난수**다.
        /// round-robin 은 종류 수 &gt; 칸 수면 뒤 종류가 영영 안 나온다. 종류가 없으면 전부 -1.
        /// </summary>
        public static void AssignKinds(int seed, int kindCount, int[] outKinds, int count)
        {
            if (kindCount <= 0)
            {
                for (int i = 0; i < count; i++) outKinds[i] = -1;
                return;
            }
            var rng = Random.CreateFromIndex((uint)(seed ^ KindSalt) | 1u);
            for (int i = 0; i < count; i++) outKinds[i] = rng.NextInt(0, kindCount);
        }

        /// <summary>
        /// 배치 가능 칸 중 `count` 개를 뽑아 `outCells` 앞부분에 쓴다. 반환 = 실제로 뽑은 수.
        /// `scratch` 는 후보 수집 버퍼(길이 ≥ 칸 수) — 호출자가 들고 다닌다.
        /// </summary>
        public static int SelectCells(MapSnapshot map, int seed, int count,
                                      int2[] scratch, int2[] outCells)
        {
            if (map == null || count <= 0) return 0;

            int candidates = 0;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new int2(x, y);
                if (map.PlaceableAt(cell, LayerBits.Ground)) scratch[candidates++] = cell;
            }
            if (candidates == 0) return 0;

            var rng = Random.CreateFromIndex((uint)(seed ^ SeedSalt) | 1u);
            int take = math.min(math.min(count, candidates), outCells.Length);
            for (int i = 0; i < take; i++)
            {
                int j = rng.NextInt(i, candidates);
                (scratch[i], scratch[j]) = (scratch[j], scratch[i]);
                outCells[i] = scratch[i];
            }
            return take;
        }
    }
}
