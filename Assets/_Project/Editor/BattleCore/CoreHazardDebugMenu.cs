#if UNITY_EDITOR
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCoreUnity;

namespace Wassup.EditorTools.BattleCore
{
    // battle-core-rebuild unit 6c — **「여기에 물건을 놓는다」** 도구. 옛 `HazardDebugMenu`(존) +
    // `BlockingHazardDebugMenu`(길막)(도구 처분표 6·7행)를 하나로 통합 재작성했다.
    //
    // 달라진 것: 브리지 메서드 대신 **코어 커맨드**를 넣는다(`DebugSpawnHazard` 17 · `DebugSpawnBlocker` 18).
    // 손으로 만든 상황이 하네스·골든과 **같은 길**을 탄다. 옛 메뉴는 SO 를 에셋 경로로 직접 읽었는데,
    // 새 코어는 **정의표 줄**로만 깐다 — 그래서 까는 대상은 `BattleDriver` 의 장판·길막 저작 목록이고
    // (그 순서가 줄 번호), 목록이 비면 이 도구가 그렇게 말한다(조용히 아무 일도 안 하지 않는다).
    //
    // 자리는 마우스 아래 칸(보드 평면 레이캐스트) → 없으면 적 경로 첫 칸 근처. 장판은 **길 칸으로 스냅**
    // (옛 동작 — 적이 밟아야 보인다), 길막은 코어가 받아 줄 때까지 가까운 칸을 **차례로 시도**한다
    // (거절 사유는 코어 `BlockerSpawn` 이 정한다 — 이 도구는 다시 판정하지 않는다).
    //
    // 「왜 안 걸렸나」 프로브(구현 10): 효과가 안 먹는 경로 넷을 **대상마다 전부** 찍는다 — ⑴ 대상 자격
    // ⑵ 진영·통행층 ⑶ 반경 ⑷ 병합 키. 넷이 화면에서 구분되지 않기 때문이다(`CoreDetectionProbeMenu` 와
    // 같은 형태). 판정은 **코어의 그 함수를 부른다** — 도구가 자를 새로 만들면 도구가 거짓말한다(제약 13).
    public static class CoreHazardDebugMenu
    {
        private const string Root = "Wassup/BattleCore/Debug/";

        [MenuItem(Root + "존 장판 깔기 (0번 줄)")] private static void Zone0() => SpawnZone(0);
        [MenuItem(Root + "존 장판 깔기 (1번 줄)")] private static void Zone1() => SpawnZone(1);
        [MenuItem(Root + "존 장판 깔기 (2번 줄)")] private static void Zone2() => SpawnZone(2);
        [MenuItem(Root + "길막 세우기 (0번 줄)")] private static void Blocker0() => SpawnBlocker(0);
        [MenuItem(Root + "길막 세우기 (1번 줄)")] private static void Blocker1() => SpawnBlocker(1);
        [MenuItem(Root + "존 장판 — 왜 안 걸렸나 찍기")] private static void Probe() => DumpZoneProbe();

        [MenuItem(Root + "존 장판 깔기 (0번 줄)", true)]
        [MenuItem(Root + "존 장판 깔기 (1번 줄)", true)]
        [MenuItem(Root + "존 장판 깔기 (2번 줄)", true)]
        [MenuItem(Root + "길막 세우기 (0번 줄)", true)]
        [MenuItem(Root + "길막 세우기 (1번 줄)", true)]
        [MenuItem(Root + "존 장판 — 왜 안 걸렸나 찍기", true)]
        private static bool Validate() => Application.isPlaying;

        // ── 깔기 ─────────────────────────────────────────────────────────────
        public static bool SpawnZone(int row)
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return false;
            var def = driver.Match.Definition;
            if (row < 0 || row >= def.Hazards.Length)
            {
                Debug.LogWarning($"[CoreHazardDebug] 정의표에 장판 {row}번 줄이 없다(줄 {def.Hazards.Length}개) — "
                    + "BattleDriver 의 존 장판 저작을 확인할 것.");
                return false;
            }
            var map = driver.Match.Map;
            int2 requested = MouseCellOrFallback(driver);
            int2 cell = NearestPathCell(map, requested);
            var receipt = driver.Apply(Command.DebugSpawnHazard(row, cell));
            if (!receipt.Accepted)
            {
                Debug.LogWarning($"[CoreHazardDebug] 장판 '{def.Hazards[row].Id}' 거절 ({cell.x},{cell.y}) — {receipt.Reason}");
                return false;
            }
            Debug.Log($"[CoreHazardDebug] 장판 '{def.Hazards[row].Id}' 깔림 ({cell.x},{cell.y})"
                + (cell.Equals(requested) ? "" : $" · 요청 ({requested.x},{requested.y}) 에서 길 칸으로 스냅")
                + $" · 반경 {def.Hazards[row].RadiusTiles}칸 · 수명 {def.Hazards[row].Lifetime:0.#}초 · 판 위 장판 {driver.Match.World.Hazards.Count}");
            return true;
        }

        public static bool SpawnBlocker(int row)
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return false;
            var def = driver.Match.Definition;
            if (row < 0 || row >= def.BlockingHazards.Length)
            {
                Debug.LogWarning($"[CoreHazardDebug] 정의표에 길막 {row}번 줄이 없다(줄 {def.BlockingHazards.Length}개) — "
                    + "BattleDriver 의 길막 저작(또는 길막을 세우는 탄)을 확인할 것.");
                return false;
            }
            var map = driver.Match.Map;
            int2 requested = MouseCellOrFallback(driver);
            // 가까운 칸부터 코어가 받아 줄 때까지. **판정은 코어의 것**이고 여기는 자리만 바꿔 본다.
            Receipt last = Receipt.Ok;
            for (int ring = 0; ring <= 6; ring++)
                for (int dy = -ring; dy <= ring; dy++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != ring) continue;
                        var c = requested + new int2(dx, dy);
                        if (!map.InBounds(c)) continue;
                        last = driver.Apply(Command.DebugSpawnBlocker(row, c));
                        if (!last.Accepted) continue;
                        Debug.Log($"[CoreHazardDebug] 길막 '{def.BlockingHazards[row].Id}' 섬 ({c.x},{c.y})"
                            + (ring == 0 ? "" : $" · 요청 ({requested.x},{requested.y}) 근처") +
                            $" · 체력 {def.BlockingHazards[row].MaxHealth:0.#}");
                        return true;
                    }
            Debug.LogWarning($"[CoreHazardDebug] 길막 '{def.BlockingHazards[row].Id}' — ({requested.x},{requested.y}) 근처에 선 칸이 없다. 마지막 거절: {last.Reason}");
            return false;
        }

        // ── 왜 안 걸렸나 ──────────────────────────────────────────────────────
        public static void DumpZoneProbe()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return;
            var world = driver.Match.World;
            var def = driver.Match.Definition;
            var map = driver.Match.Map;
            var sb = new StringBuilder();
            sb.AppendLine($"[CoreHazardDebug] 틱 {driver.Match.Clock.Tick} · 장판 {world.Hazards.Count} · 유닛 {world.Units.Count}");
            if (world.Hazards.Count == 0) { sb.AppendLine("판 위에 장판이 없다."); Debug.Log(sb.ToString()); return; }

            float inv = map.TileSize > 1e-6f ? 1f / map.TileSize : 1f;
            for (int z = 0; z < world.Hazards.Count; z++)
            {
                var h = world.Hazards[z];
                string id = h.DefIndex >= 0 && h.DefIndex < def.Hazards.Length ? def.Hazards[h.DefIndex].Id : "?";
                sb.AppendLine($"── 장판 {h.Id} '{id}' 중심 ({h.OriginCell.x},{h.OriginCell.y}) 반경 {h.RadiusTiles}칸 "
                    + $"남은 {h.Remaining:0.0}초 통행층필터 {h.TargetLayers}");
                sb.AppendLine("   id | 종류 | ⑴자격 | ⑵진영·통행층 | ⑶반경(거리/닿음) | ⑷병합 키(같은 칸의 슬롯)");
                for (int i = 0; i < world.Units.Count; i++)
                {
                    var u = world.Units[i];
                    if (u.Dead || u.Kind == UnitKind.Structure) continue;
                    float dx = (u.Position.x - h.Center.x) * inv, dz = (u.Position.z - h.Center.z) * inv;
                    float dist = math.sqrt(dx * dx + dz * dz);
                    // 반경 안팎을 멀리서까지 다 찍으면 표가 판 전체가 된다 — 닿음 경계 + 2칸까지만.
                    if (h.RadiusTiles >= 0 && dist > h.RadiusTiles + 2.5f) continue;
                    sb.AppendLine("   " + ProbeRow(u, h, def, dx, dz, dist));
                }
            }
            Debug.Log(sb.ToString());
        }

        private static string ProbeRow(Unit u, Hazard h, MatchDefinition def, float dx, float dz, float dist)
        {
            // ⑴ 대상 자격 — 거점 전면 면역(F3) · 보스는 잠금·넉백만 면역(M6). **코어 술어를 부른다.**
            bool accepts = EffectEligibility.AcceptsModifier(u);
            bool stunOk = EffectEligibility.AcceptsCc(u, CcRequestKind.Stun);
            string elig = accepts ? (stunOk ? "O" : "O(잠금 면역)") : "X(면역)";

            // ⑵ 진영·통행층 — 저작 진영 비트(F34) · 장판의 통행층 필터(F15, 0 = 필터 없음).
            byte theirs = u.Move != null ? u.Move.TraversalLayers : (byte)0;
            bool layerOk = LayerBits.CanTarget(h.TargetLayers, theirs);
            int factionHits = 0, effects = 0;
            if (h.DefIndex >= 0 && h.DefIndex < def.Hazards.Length)
            {
                var hd = def.Hazards[h.DefIndex];
                effects = hd.EffectCount;
                for (int e = 0; e < effects; e++)
                    if (((int)u.Faction & hd.Effects[e].TargetFactions) != 0) factionHits++;
            }
            string side = $"{(factionHits > 0 ? "O" : "X")}({factionHits}/{effects}) 층{(layerOk ? "O" : "X")}";

            // ⑶ 반경 — 제약 13 의 **그 진입점**(자리형: 칸 반폭 + 대상 몸).
            bool reach = h.RadiusTiles >= 0
                && Wassup.Skills.SkillMath.ReachFromCell(dx, dz, h.RadiusTiles, u.HitRadius);
            string radius = h.RadiusTiles < 0 ? "존 없음(F18)" : $"{dist:0.00}/{(reach ? "O" : "X")}";

            // ⑷ 병합 키 — 존이 건 것이 들어간 **슬롯**을 보여 준다. 감속은 존 칸(`SlotKind.Zone`) 하나를
            // 여러 장판이 나눠 쓰고(겹치면 가장 센 값), 지속 피해는 (장판 출처, 원소) 칸이다. 같은 칸을 다른
            // 효과가 덮었으면 여기서 보인다.
            var merge = new StringBuilder();
            var slots = u.Modifiers.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Key.Tag.Kind == Wassup.BattleCore.Effects.SlotKind.Zone)
                    merge.Append($"[존칸 {slots[i].Key.Stat}×{slots[i].Magnitude:0.##} 출처{slots[i].Key.Source} {slots[i].Remaining:0.0}s]");
            var dots = u.Dot.Slots;
            for (int i = 0; i < dots.Count; i++)
                if (dots[i].Origin == Wassup.BattleCore.Effects.DotOrigin.Zone)
                    merge.Append($"[지속피해 {dots[i].Element} {dots[i].Scalar:0.##}]");
            if (merge.Length == 0) merge.Append("-");

            return $"{u.Id} | {u.Kind} | {elig} | {side} | {radius} | {merge}";
        }

        // ── 자리 ─────────────────────────────────────────────────────────────
        // 마우스 아래 칸. 보드 평면은 **그 값을 이미 소유한 곳**(`BoardSpace.RaycastPlane`)에서 받는다.
        internal static int2 MouseCellOrFallback(BattleDriver driver)
        {
            var map = driver.Match.Map;
            var cam = Camera.main;
            if (cam != null && Mouse.current != null && Wassup.Core.BoardSpace.IsConfigured)
            {
                var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                var plane = Wassup.Core.BoardSpace.RaycastPlane();
                if (plane.Raycast(ray, out float d))
                {
                    var sim = Wassup.Core.BoardSpace.ToSim(ray.GetPoint(d));
                    var c = map.CellOf(sim);
                    if (map.InBounds(c)) return c;
                }
            }
            // 마우스가 판 밖이면(에디터가 포커스가 없을 때 흔하다) 적 첫 입구 근처로.
            var spawns = map.Snapshot.Spawns;
            return spawns.Length > 0 ? spawns[0] : new int2(map.GridSize.x / 2, map.GridSize.y / 2);
        }

        // 가장 가까운 **길 칸**(통행층에 경로 비트). 골 칸은 피한다. 옛 `TryGetNearestWalkCell` 의 자리.
        internal static int2 NearestPathCell(MapRuntime map, int2 from)
        {
            var snap = map.Snapshot;
            for (int ring = 0; ring < math.max(snap.Width, snap.Height); ring++)
                for (int dy = -ring; dy <= ring; dy++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != ring) continue;
                        var c = from + new int2(dx, dy);
                        if (!snap.InBounds(c) || snap.IsGoalCell(c)) continue;
                        if ((snap.TravelLayersAt(c) & LayerBits.Path) != 0) return c;
                    }
            return from;
        }
    }
}
#endif
