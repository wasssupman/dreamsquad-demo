#if UNITY_EDITOR
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Effects;
using Somnia.Battle.BattleCoreUnity;

namespace Somnia.Battle.EditorTools.BattleCore
{
    // battle-core-rebuild unit 6c — **기믹 셈판** 도구. 옛 `FatigueDebugMenu`(도구 처분표 9행 — 피로 스택
    // 로그 · 레드불 로그)의 후계이고, 6b2 가 연 디버그 커맨드 셋을 넣는다(`DebugSpawnPickup` 19 ·
    // `DebugDropResignation` 20 · `DebugSetStack` 21).
    //
    // 왜 이 도구가 있어야 하나: 6b2 끝에 픽업·사직서·열기/피로는 **라이브에서 저절로 나타나지 않는다**
    // (놓는 자·계기가 unit 7 이다). 도구가 없으면 이 영역에서 「재현이 먼저다」(CLAUDE.md 버그 수정 절차 1)가
    // 집행 불가다.
    //
    // ⚠ 커맨드는 **이 판에 뽑힌 기믹**만 받는다(코어 `GimmickHost.TryActive` — 거절 사유 `GimmickInactive`).
    // 판마다 기믹은 하나이고 시드가 정한다. 그래서 이 메뉴는 먼저 「이 판의 기믹이 무엇인가」를 말하고,
    // 거절되면 그 이유를 그대로 찍는다(조용히 아무 일도 안 하지 않는다).
    //
    // 「왜 안 걸렸나」(구현 10)의 네 원인을 셈판 쪽에서도 찍는다 — ⑴ 대상 자격 ⑵ 진영(누가 셈판을 갖나)
    // ⑶ 반경(픽업을 밟을 수 있는 자리인가 — 코어 진입점) ⑷ 병합 키(같은 종류의 스택이 **출처마다** 따로 쌓인다).
    public static class CoreGimmickDebugMenu
    {
        private const string Root = "Somnia/Battle/BattleCore/Debug/기믹/";

        [MenuItem(Root + "셈판 찍기 (기믹·피로·열기·픽업·사직서)")] private static void Dump() => DumpGimmick();
        [MenuItem(Root + "피로 +3 (방어유닛 전원)")] private static void Fatigue() => BumpFatigue(3);
        [MenuItem(Root + "열기 +3 (방어유닛 전원)")] private static void Heat() => BumpHeat(3);
        [MenuItem(Root + "레드불 놓기 (마우스 칸 · 없으면 시드 자리)")] private static void Pickup() => SpawnPickup();
        [MenuItem(Root + "사직서 떨어뜨리기 (마우스 칸)")] private static void Resign() => DropResignation();

        [MenuItem(Root + "셈판 찍기 (기믹·피로·열기·픽업·사직서)", true)]
        [MenuItem(Root + "피로 +3 (방어유닛 전원)", true)]
        [MenuItem(Root + "열기 +3 (방어유닛 전원)", true)]
        [MenuItem(Root + "레드불 놓기 (마우스 칸 · 없으면 시드 자리)", true)]
        [MenuItem(Root + "사직서 떨어뜨리기 (마우스 칸)", true)]
        private static bool Validate() => Application.isPlaying;

        // ── 강제 ─────────────────────────────────────────────────────────────
        public static int BumpFatigue(int delta)
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return 0;
            int ok = 0;
            string lastReject = "";
            var units = driver.Match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead) continue;
                int have = OwnCount(u, StackKind.Fatigue);
                var r = driver.Apply(Command.DebugSetStack(u.Id, StackKind.Fatigue, have + delta));
                if (r.Accepted) ok++; else lastReject = r.Reason.ToString();
            }
            Debug.Log($"[CoreGimmickDebug] 피로 +{delta} — 받아들여짐 {ok}"
                + (lastReject.Length > 0 ? $" · 거절 사유(마지막) {lastReject}" : "") + $" · 기믹 {ActiveGimmick(driver)}");
            return ok;
        }

        public static int BumpHeat(int delta)
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return 0;
            int ok = 0;
            string lastReject = "";
            var units = driver.Match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead) continue;
                var r = driver.Apply(Command.DebugSetHeat(u.Id, u.Stacks.Heat + delta));
                if (r.Accepted) ok++; else lastReject = r.Reason.ToString();
            }
            Debug.Log($"[CoreGimmickDebug] 열기 +{delta} — 받아들여짐 {ok}"
                + (lastReject.Length > 0 ? $" · 거절 사유(마지막) {lastReject}" : "") + $" · 기믹 {ActiveGimmick(driver)}");
            return ok;
        }

        public static bool SpawnPickup()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return false;
            var map = driver.Match.Map;
            var cell = CoreHazardDebugMenu.NearestPathCell(map, CoreHazardDebugMenu.MouseCellOrFallback(driver));
            var r = driver.Apply(Command.DebugSpawnPickup(cell));
            if (!r.Accepted && r.Reason != RejectReason.GimmickInactive)
                r = driver.Apply(Command.DebugSpawnPickupSeeded());   // 그 칸이 막혔으면 코어가 고른 자리로
            Debug.Log(r.Accepted
                ? $"[CoreGimmickDebug] 레드불 놓임 · 판 위 픽업 {driver.Match.World.Pickups.Count}"
                : $"[CoreGimmickDebug] 레드불 거절 — {r.Reason} · 기믹 {ActiveGimmick(driver)}");
            return r.Accepted;
        }

        public static bool DropResignation()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return false;
            var map = driver.Match.Map;
            var cell = CoreHazardDebugMenu.NearestPathCell(map, CoreHazardDebugMenu.MouseCellOrFallback(driver));
            var r = driver.Apply(Command.DebugDropResignation(cell));
            Debug.Log(r.Accepted
                ? $"[CoreGimmickDebug] 사직서 떨어짐 ({cell.x},{cell.y}) · 판 위 {driver.Match.World.Resignations.Count}장"
                : $"[CoreGimmickDebug] 사직서 거절 — {r.Reason} · 기믹 {ActiveGimmick(driver)}");
            return r.Accepted;
        }

        // ── 찍기 ─────────────────────────────────────────────────────────────
        public static void DumpGimmick()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return;
            var world = driver.Match.World;
            var map = driver.Match.Map;
            var sb = new StringBuilder();
            sb.AppendLine($"[CoreGimmickDebug] 틱 {driver.Match.Clock.Tick} · 기믹 {ActiveGimmick(driver)} · "
                + $"픽업 {world.Pickups.Count} · 사직서 {world.Resignations.Count}장");
            sb.AppendLine("id | 종류 | ⑴자격 | ⑵진영 | 피로(출처별 ⑷) | 열기 | 라스트런 | ⑶밟을 수 있는 픽업");

            float inv = map.TileSize > 1e-6f ? 1f / map.TileSize : 1f;
            var units = world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Kind == UnitKind.Structure) continue;
                bool accepts = EffectEligibility.AcceptsModifier(u);
                // ⑷ 병합 키 — 스택은 (출처, 종류) 칸이다. 같은 종류가 두 출처에서 오면 **따로** 쌓인다.
                var fatigue = new StringBuilder();
                var slots = u.Stacks.Slots;
                for (int s = 0; s < slots.Count; s++)
                    if (slots[s].Kind == StackKind.Fatigue)
                        fatigue.Append($"[출처{slots[s].Source} {slots[s].Count}/{slots[s].MaxStack} {slots[s].Remaining:0.0}s]");
                if (fatigue.Length == 0) fatigue.Append("-");

                string lastRun = u.Progressive != null && u.Progressive.LastRunActive
                    ? $"창 {u.Progressive.LastRunRemaining:0.0}s" : "-";

                // ⑶ 반경 — 픽업의 판정은 제약 13 **진입점**(자리형: 칸 반폭 + 먹는 자의 몸, 범위 0 = 그 칸)이다.
                var reach = new StringBuilder();
                for (int p = 0; p < world.Pickups.Count; p++)
                {
                    var pk = world.Pickups[p];
                    float dx = (u.Position.x - pk.Center.x) * inv, dz = (u.Position.z - pk.Center.z) * inv;
                    if (Somnia.Battle.Skills.SkillMath.ReachFromCell(dx, dz, 0f, u.HitRadius)) reach.Append($"[{pk.Id}]");
                }
                if (reach.Length == 0) reach.Append("-");

                sb.AppendLine($"{u.Id} | {u.Kind} | {(accepts ? "O" : "X(면역)")} | {u.Faction} | {fatigue} | "
                    + $"{u.Stacks.Heat} | {lastRun} | {reach}");
            }
            for (int p = 0; p < world.Pickups.Count; p++)
            {
                var pk = world.Pickups[p];
                sb.AppendLine($"픽업 {pk.Id} {pk.Kind} ({pk.Cell.x},{pk.Cell.y}) 남은 {pk.Remaining:0.0}s");
            }
            for (int r = 0; r < world.Resignations.Count; r++)
            {
                var rs = world.Resignations[r];
                sb.AppendLine($"사직서 {rs.Id} ({rs.Cell.x},{rs.Cell.y}) 떨어뜨린 자 {rs.Source}");
            }
            Debug.Log(sb.ToString());
        }

        private static int OwnCount(Unit u, StackKind kind)
        {
            int idx = u.Stacks.IndexOf(u.Id, kind);
            return idx >= 0 ? u.Stacks.Slots[idx].Count : 0;
        }

        // 「이 판의 기믹」 — 코어에게 **종류마다 묻는다**(`TryActive`). 인덱스를 읽어 해석하지 않는다.
        private static string ActiveGimmick(BattleDriver driver)
        {
            var host = driver.Match.Gimmick;
            if (host == null) return "없음";
            foreach (GimmickKind k in System.Enum.GetValues(typeof(GimmickKind)))
                if (host.TryActive(k, out _)) return k.ToString();
            return "없음";
        }
    }
}
#endif
