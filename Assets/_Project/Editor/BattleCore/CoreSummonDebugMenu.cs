#if UNITY_EDITOR
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;

namespace Wassup.EditorTools.BattleCore
{
    // battle-core-rebuild unit 7d — **순찰병 수동 소환** 도구. 옛 `PatrolDebugMenu`(도구 처분표 10행)의 후계이고,
    // 브리지 메서드 대신 코어 커맨드 `DebugSummonPatrol`(24)을 넣는다 — 하네스·리플레이가 같은 길을 탄다.
    //
    // 코어 쪽은 소환사와 **같은 조립 자리**(`CombatPhase.SpawnPatrol`)를 지난다. 그래서 이 메뉴로 세운 순찰병은
    // 구역·이동·공격이 진짜 소환물과 같고, 다른 것은 소환사가 없다는 것(연쇄 소멸 없음) 하나다.
    //
    // ⚠ **커서 위치를 쓰지 않는다**(옛 메뉴와 같은 이유 — 메뉴를 누르는 순간 커서는 메뉴 위다). 앵커 =
    // **배치된 첫 방어유닛의 칸**(실제 소환의 앵커가 소환사 칸이다), 없으면 보드 중심.
    // ⚠ 순찰 정의 줄은 **이 판의 정의표에 있어야** 한다 — 빌더는 편성의 소환사가 가리키는 순찰 유닛만 싣는다.
    // 소환사가 편성에 없으면 이 메뉴는 그 사실을 말하고 멈춘다(조용히 아무 일도 안 하지 않는다).
    public static class CoreSummonDebugMenu
    {
        private const string Root = "Wassup/BattleCore/Debug/순찰/";

        [MenuItem(Root + "순찰병 소환 (반경 2 · 배치 유닛 칸)")] private static void Summon2() => Summon(2);
        [MenuItem(Root + "순찰병 소환 (반경 4 · 배치 유닛 칸)")] private static void Summon4() => Summon(4);
        [MenuItem(Root + "순찰 구역 찍기 (앵커·반경·주인)")] private static void Dump() => DumpPatrols();

        [MenuItem(Root + "순찰병 소환 (반경 2 · 배치 유닛 칸)", true)]
        [MenuItem(Root + "순찰병 소환 (반경 4 · 배치 유닛 칸)", true)]
        [MenuItem(Root + "순찰 구역 찍기 (앵커·반경·주인)", true)]
        private static bool Validate() => Application.isPlaying;

        /// <summary>이 판 정의표의 순찰 줄 — 소환사가 가리키는 첫 줄. 없으면 -1.</summary>
        public static int PatrolDefIndex(MatchDefinition def)
        {
            var units = def.Units;
            for (int i = 0; i < units.Length; i++)
            {
                int p = units[i].Attack.SummonPatrolDefIndex;
                if (p >= 0 && p < units.Length) return p;
            }
            return -1;
        }

        public static bool Summon(int radius)
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return false;
            var match = driver.Match;
            int row = PatrolDefIndex(match.Definition);
            if (row < 0)
            {
                Debug.LogWarning("[CoreSummonDebug] 이 판 정의표에 순찰 줄이 없다 — 편성에 소환사가 없다(빌더는 소환사가 가리키는 순찰 유닛만 싣는다).");
                return false;
            }
            int2 anchor = AnchorCell(match, out bool fromDefender);
            var r = driver.Apply(Command.DebugSummonPatrol(row, anchor, radius));
            Debug.Log(r.Accepted
                ? $"[CoreSummonDebug] 순찰병 '{match.Definition.Units[row].Id}' 소환 · 앵커 ({anchor.x},{anchor.y})"
                  + $"({(fromDefender ? "배치 유닛" : "보드 중심")}) · 반경 {radius} · 판 위 순찰 {CountPatrols(match)}"
                : $"[CoreSummonDebug] 소환 거절 — {r.Reason} · 앵커 ({anchor.x},{anchor.y})");
            return r.Accepted;
        }

        public static void DumpPatrols()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return;
            var match = driver.Match;
            var sb = new StringBuilder();
            sb.AppendLine($"[CoreSummonDebug] 틱 {match.Clock.Tick} · 순찰 {CountPatrols(match)} · 순찰 줄 {PatrolDefIndex(match.Definition)}");
            sb.AppendLine("id | 앵커 | 집 | 반경 | 주인(소환사) | 지금 칸 | 구역 안");
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Patrol || u.Dead || u.Patrol == null) continue;
                var p = u.Patrol;
                var cell = match.Map.CellOf(u.Position);
                bool inside = Wassup.BattleCore.Move.PatrolAreaMath.IsInArea(cell, p.Anchor, p.Radius);
                sb.AppendLine($"{u.Id} | ({p.Anchor.x},{p.Anchor.y}) | ({p.Home.x},{p.Home.y}) | {p.Radius} | "
                    + $"{(p.SummonedBy.IsNone ? "없음(디버그)" : p.SummonedBy.ToString())} | ({cell.x},{cell.y}) | {(inside ? "O" : "X")}");
            }
            Debug.Log(sb.ToString());
        }

        private static int2 AnchorCell(BattleMatch match, out bool fromDefender)
        {
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead || u.Footprint == null) continue;
                fromDefender = true;
                return match.Map.CellOf(u.Position);
            }
            fromDefender = false;
            return new int2(match.Map.GridSize.x / 2, match.Map.GridSize.y / 2);
        }

        private static int CountPatrols(BattleMatch match)
        {
            int n = 0;
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++) if (units[i].Kind == UnitKind.Patrol && !units[i].Dead) n++;
            return n;
        }
    }
}
#endif
