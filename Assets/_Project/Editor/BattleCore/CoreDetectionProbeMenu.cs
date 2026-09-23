#if UNITY_EDITOR
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;

namespace Wassup.EditorTools.BattleCore
{
    // battle-core-rebuild unit 5a — 감지 계측기. 옛 `DetectionProbeMenu`(도구 처분표 5행)의 후계다.
    //
    // **왜 이 도구가 있어야 하나**: CLAUDE.md 의 버그 수정 절차 첫 줄이 「재현이 먼저다」이고,
    // 감지는 그 재현을 **눈으로 볼 수 없는** 영역이다. 적이 안 쫓아오는 이유가 (a) 반경 밖 (b)
    // 억제 창 (c) 통행 층으로 못 감 (d) 관성 중 — 넷 중 무엇인지 화면에는 아무 차이도 안 난다.
    // 계측기가 없으면 그 구간에서 「재현이 먼저다」가 집행 불가다.
    //
    // 옛 것과 달라진 것: 고정 스텝 하네스를 새로 돌리지 않고 **지금 돌고 있는 판을 읽는다**.
    // 옛 계측기는 감지를 **넣기 전에** 기준선을 재는 물건이었고(그 답은 이미 나왔다),
    // 지금 필요한 것은 「이 판의 이 적이 왜 저러고 있나」다.
    public static class CoreDetectionProbeMenu
    {
        [MenuItem("Wassup/BattleCore/Debug/감지 상태 찍기")]
        private static void Dump()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return;

            var world = driver.Match.World;
            var map = driver.Match.Map;
            var sb = new StringBuilder();
            sb.AppendLine($"[CoreDetectionProbe] 틱 {driver.Match.Clock.Tick} · 유닛 {world.Units.Count}");
            sb.AppendLine("id | 종류 | 셀 | 반경 | 사냥 | 대상 | 관성 | 막힘 | 억제 | 표식쿨 | 최근접 방어유닛");

            var units = world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var d = u.Detection;
                if (d == null) continue;   // 감지 저작이 없는 개체는 이 표의 대상이 아니다

                int2 cell = map.CellOf(u.Position);
                string range = d.Unlimited ? "무제한" : (d.Range <= 0f ? "없음" : d.Range.ToString("F1"));
                sb.AppendLine(
                    $"{u.Id} | {u.Kind} | ({cell.x},{cell.y}) | {range} | {(d.Hunting ? "O" : "-")} | "
                    + $"{d.Target} | {d.Grace:F2} | {d.Stuck:F2} | {d.Suppress:F2} | {d.MarkCooldown:F2} | "
                    + NearestDefender(world, u));
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Wassup/BattleCore/Debug/감지 상태 찍기", true)]
        private static bool Validate() => Application.isPlaying;

        // ⚠ **이것은 「감지가 고를 대상」이 아니다.** 직선 최근접일 뿐이고, 실제 감지는 통행
        // 층으로 갈 수 있는 것만 고른다 — 실측 5.0% 에서 둘이 갈린다. 표에 같이 찍는 이유는
        // 「반경 안인데 왜 안 무나」를 물을 때 **그 5%** 가 답인 경우를 바로 알아보기 위해서다.
        private static string NearestDefender(BattleWorld world, Unit self)
        {
            float best = float.MaxValue;
            SimEntityId bestId = SimEntityId.None;
            var units = world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var o = units[i];
                if (o.Id == self.Id || o.Dead) continue;
                if (((int)o.Faction & Wassup.Battle.Units.Factions.AnyDefender) == 0) continue;
                float dist = math.distance(o.Position, self.Position);
                if (dist >= best) continue;
                best = dist;
                bestId = o.Id;
            }
            return bestId.IsNone ? "없음" : $"{bestId} @{best:F2}칸";
        }
    }
}
#endif
