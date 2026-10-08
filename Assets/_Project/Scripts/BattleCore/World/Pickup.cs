using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.BattleCore
{
    /// <summary>픽업 종류. append-only(옛 `PickupKind` 와 같은 번호).</summary>
    public enum PickupKind : byte
    {
        /// <summary>레드불 — 먹으면 라스트런(공속 버프 → 지연 crash).</summary>
        RedBull = 0,
    }

    // battle-core-rebuild unit 6b2 — **판 위에 놓인 먹을 것 하나.**
    //
    // 유닛이 아니다(체력도 공격도 없다) — 존 장판(`Hazard`)과 같이 `BattleWorld.Units` 밖에 산다.
    // 존과 다른 점은 **한 번 먹히면 끝**이라는 것이고(소비형), 그래서 효과 배열 대신 종류 하나를 든다.
    //
    // ⚠ **소비는 칸 일치가 아니라 도달 판정이다**(제약 13 — 「자를 새로 만들지 않는다」).
    // 픽업은 「자리에 떨어지는 것」이라 원점 항은 칸 반폭이고, 소비자의 몸이 붙는다.
    // 옛 전투는 같은 칸 폴링이었다 — 판정이 넓어진 것은 제약 13 의 결과다(README 고지 1건).
    public sealed class Pickup
    {
        public SimEntityId Id;
        public PickupKind Kind;

        /// <summary>놓인 칸. 판정 원점은 이 칸의 중심이고 **몸이 없다**.</summary>
        public int2 Cell;

        /// <summary>칸 중심의 월드 좌표. 스폰 때 한 번 굽는다(픽업은 안 움직인다).</summary>
        public float3 Center;

        /// <summary>남은 수명(초). 0 이하가 되는 틱에 사라지고 **그 틱에는 못 먹는다**.</summary>
        public float Remaining;

        /// <summary>
        /// 놓인 틱. 수명은 **다음 틱부터** 깎인다 — 옛 스폰 시스템이 만료 패스 **뒤**에 새 픽업을
        /// 세웠기 때문이다(놓인 프레임엔 안 깎이고 먹을 수는 있었다).
        /// </summary>
        public int SpawnTick = -1;

        public void Reset()
        {
            Id = SimEntityId.None;
            Kind = PickupKind.RedBull;
            Cell = int2.zero;
            Center = float3.zero;
            Remaining = 0f;
            SpawnTick = -1;
        }
    }

    // 픽업을 놓는 **단 하나의 조립 자리.** 생산자(디버그 커맨드 · unit 7 의 레드불 주기 바인딩)가
    // 전부 여기를 지난다 — 「한 칸에 하나」와 동시 상한을 두 곳에서 세면 한쪽이 언젠가 빠진다.
    public static class PickupSpawn
    {
        /// <summary>
        /// 빈 칸을 찾는 재시도 상한. **밸런스 값이 아니라 포화 가드**다 — 넘으면 이번 스폰을
        /// 건너뛴다(옛 `PickupSpawnSystem.MaxPickAttempts` 와 같은 값 · 같은 뜻). 값을 바꾸면
        /// 같은 시드에서 난수 소비 횟수가 달라져 **자리가 통째로 밀린다**.
        /// </summary>
        public const int MaxPickAttempts = 8;

        /// <summary>
        /// `cell` 에 놓는다. 이미 픽업이 있는 칸이면 null — 「한 칸에 하나」가 불변식이다
        /// (옛 소비가 칸 → 픽업 사전이라 같은 칸 두 번째 것은 영영 안 먹혔다).
        /// </summary>
        public static Pickup At(BattleWorld world, MapRuntime map, PickupKind kind, int2 cell,
                                float lifetime, int tick)
        {
            if (world == null || lifetime <= 0f) return null;
            if (map != null && !map.Snapshot.InBounds(cell)) return null;
            if (Occupied(world, cell)) return null;
            var center = map != null ? map.CenterOf(cell) : new float3(cell.x, 0f, cell.y);
            return world.SpawnPickup(kind, cell, center, lifetime, tick);
        }

        /// <summary>
        /// **시드로 자리를 고른다.** 후보 = 이동 ∪ 배치 칸(옛 `candidateCells` 와 같다) · 난수 계열 =
        /// `RngStreams.Pickup`(운석과 나눈 이유: 한쪽의 호출 횟수가 바뀌면 다른 쪽이 통째로 밀린다).
        /// 동시 상한에 닿았거나 빈 칸을 못 찾으면 null — 「이번 주기는 건너뛴다」가 옛 규칙이다.
        ///
        /// ⚠ **주기(언제 부르나)는 여기 없다** — unit 7 의 Match 호스트 바인딩이다. 상한에 막혔을 때
        /// 밀린 주기를 어떻게 접는가(옛 elapsed 클램프)도 주기 쪽 규칙이라 같이 간다.
        /// </summary>
        public static Pickup TrySpawnRandom(TickContext ctx, PickupKind kind, in RedBullSpec spec, int tick)
        {
            var world = ctx.World;
            var map = ctx.Map;
            if (map == null || spec.MaxActive <= 0 || spec.Lifetime <= 0f) return null;
            if (world.Pickups.Count >= spec.MaxActive) return null;

            var snap = map.Snapshot;
            int candidates = CountCandidates(snap);
            if (candidates == 0) return null;

            ref var rng = ref ctx.Rng.Pickup;
            for (int attempt = 0; attempt < MaxPickAttempts; attempt++)
            {
                // 옛 배열 `cells[rng.NextInt(0, n)]` 과 같은 답 — 후보를 행 우선으로 세어 k 번째를 찾는다
                // (배열을 만들지 않는다 — 틱 중 할당 0).
                int2 cell = NthCandidate(snap, rng.NextInt(0, candidates));
                if (Occupied(world, cell)) continue;
                return world.SpawnPickup(kind, cell, map.CenterOf(cell), spec.Lifetime, tick);
            }
            return null;
        }

        private static bool Occupied(BattleWorld world, int2 cell)
        {
            var list = world.Pickups;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Cell.Equals(cell)) return true;
            return false;
        }

        private static bool IsCandidate(MapTile t) => t == MapTile.Walk || t == MapTile.Place;

        private static int CountCandidates(MapSnapshot snap)
        {
            int n = 0;
            for (int i = 0; i < snap.Tiles.Length; i++) if (IsCandidate(snap.Tiles[i])) n++;
            return n;
        }

        private static int2 NthCandidate(MapSnapshot snap, int k)
        {
            for (int i = 0; i < snap.Tiles.Length; i++)
            {
                if (!IsCandidate(snap.Tiles[i])) continue;
                if (k-- == 0) return new int2(i % snap.Width, i / snap.Width);
            }
            return int2.zero;
        }
    }
}
