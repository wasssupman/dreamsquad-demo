using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core.TimeControl;

namespace Wassup.EditorTools.Battle
{
    // battle-sim-extraction M0 unit 2 — 고정 스텝 하네스의 구동자 겸 결정론 증거 수집기.
    //
    // 스텝 자체는 런타임(`BattleBridge.StepOneTick`)이 소유하고, **무엇을 언제 넣을지**
    // (입력 스케줄)와 **무엇을 재는지**(틱 다이제스트)는 여기 있다. 스케줄러를 런타임에
    // 두지 않은 이유: 지금 소비자가 이 하나뿐이고, 커맨드 어휘의 정본은 unit 4(골든)와
    // M1(세션 파사드)이 정할 것이라 그 앞에 자리를 잡아두면 두 번 만들게 된다.
    //
    // 재는 것은 카운트가 아니라 **상태 지문**이다: 살아 있는 모든 sim 엔티티의
    // (SimEntityId, 위치, 체력)을 ID 순으로 접은 FNV-1a. 카운트만 같고 위치가 갈리는
    // 사고를 통과시키지 않기 위해서다(그 사고가 정확히 골든이 잡아야 할 종류다).
    public static class SimHarnessRunMenu
    {
        private const string ReportPath = "docs/spec/battle-sim-extraction/harness-determinism.md";
        private const int MatchSeed = 20260822;
        private const int TickCount = 900;      // 1/60 s × 900 = 15 초 (웨이브·전투가 실제로 도는 길이)
        private const float StepDt = 1f / 60f;
        // 입력 스케줄: 이 틱들에 방어유닛 1기씩. 코스트가 차오르는 시각이라 실제로 놓인다
        // (틱 30 에 걸었을 땐 매번 InsufficientCost 로 거부돼 스케줄이 공전했다).
        private static readonly int[] PlacementTicks = { 150, 330, 510, 690 };
        private const int ScanExtent = 32;      // 배치 후보 셀 스캔 범위(고정 = 결정론)

        [MenuItem("Wassup/Battle/Sim Harness/Run Determinism Check (2 runs)")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SimHarness] Play Mode 에서 실행하라 — 월드가 있어야 스텝이 의미를 가진다.");
                return;
            }

            var bridge = Object.FindFirstObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[SimHarness] BattleBridge 를 못 찾았다. 전투 씬에서 실행하라.");
                return;
            }

            var runA = RunOnce(bridge);
            var runB = RunOnce(bridge);

            int firstDiff = -1;
            for (int i = 0; i < TickCount; i++)
            {
                if (runA.digests[i].Equals(runB.digests[i])) continue;
                firstDiff = i;
                break;
            }

            System.IO.File.WriteAllText(ReportPath, BuildReport(runA, runB, firstDiff));
            AssetDatabase.Refresh();
            Debug.Log(firstDiff < 0
                ? $"[SimHarness] 2회 {TickCount}틱 완전 일치 (configHash {runA.configHash}). 보고서: {ReportPath}"
                : $"[SimHarness] 틱 {firstDiff} 에서 갈렸다 (configHash A={runA.configHash} B={runB.configHash}). 보고서: {ReportPath}");
        }

        private struct RunResult
        {
            public string configHash;   // unit 3 — 조건 지문. 두 판이 다르면 코드가 아니라 값이 바뀐 것
            public Digest[] digests;
        }

        // 한 판을 처음부터 세워 N 틱 굴린다. seed 를 고정하므로 두 판의 맵·웨이브가 같다.
        private static RunResult RunOnce(BattleBridge bridge)
        {
            bridge.StopBattle();
            bridge.SetMatchSeed(MatchSeed);
            bridge.PrepareDraftMap();
            bridge.BeginPlacement();
            bridge.StartBattle();

            // ⚠ 코스트 재생의 스위치는 **UI 가 갖고 있다**(`PlacementPhaseView` 가 배치 진입에
            // ResetToStart, 전투 시작에 BeginRegen). 스크립트 진입은 그 뷰를 지나지 않아
            // 코스트가 0 에 멎고, 그러면 배치 입력이 전부 InsufficientCost 로 거부된다
            // — 실제로 처음엔 그렇게 스케줄이 공전했다. 여기서 하네스가 그 UI 역할을 대신한다.
            // (라이브 진입 경로 자체의 재현은 unit 3 «MatchConfig 물질화» 의 몫이다.)
            var cost = Wassup.Core.GameManager.Instance != null
                ? Wassup.Core.GameManager.Instance.CostRuntime : null;
            if (cost != null) { cost.ResetToStart(); cost.BeginRegen(); }

            var digests = new Digest[TickCount];
            SimHarnessClock.Begin(StepDt);
            try
            {
                for (int t = 0; t < TickCount; t++)
                {
                    ApplyScheduledInput(bridge, t);
                    bridge.StepOneTick();
                    digests[t] = Capture(bridge);
                }
            }
            finally
            {
                SimHarnessClock.End();
            }
            return new RunResult { configHash = bridge.MatchConfigHash, digests = digests };
        }

        // 입력 스케줄. 벽시계가 아니라 **틱 번호**로 반입한다 — 그래야 두 판의 입력이
        // 같은 sim 시각에 들어가고, 「입력 타이밍이 달라서 갈렸다」가 원인 후보에서 빠진다.
        private static void ApplyScheduledInput(BattleBridge bridge, int tick)
        {
            int slot = System.Array.IndexOf(PlacementTicks, tick);
            if (slot < 0) return;
            var pool = bridge.DefenderPool;
            if (pool == null || pool.Length == 0) return;
            // 스케줄 슬롯마다 **다른 유닛**을 쓴다. 같은 유닛을 4번 걸었더니 첫 배치 뒤
            // `LimitReached`(타입별 판 상한)로 나머지 3번이 조용히 공전했다 — 스케줄이
            // 반쯤 비어 있으면 「입력을 넣어도 같다」는 증거가 그만큼 약해진다.
            var unit = pool[slot % pool.Length];
            // 고정 칸을 박지 않는 이유: 맵이 seed 로 정해져 어느 칸이 배치 가능한지 미리 못
            // 박는다. **스캔 순서가 고정**이면 선택은 그대로 결정론이고, 맵이 바뀌어도
            // 스케줄이 공전하지 않는다.
            for (int y = 0; y < ScanExtent; y++)
            for (int x = 0; x < ScanExtent; x++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, unit, out _)) continue;
                bridge.PlaceDefenderAs(x, y, unit);
                return;
            }
        }

        private struct Digest
        {
            public float clock;
            public int entities;
            public ulong fingerprint;

            public bool Equals(Digest o) => clock == o.clock && entities == o.entities && fingerprint == o.fingerprint;
            public override string ToString() => $"{clock:F4} / {entities} / {fingerprint:X16}";
        }

        private static Digest Capture(BattleBridge bridge)
        {
            var d = new Digest { clock = bridge.LogElapsedTime };
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return d;
            var em = world.EntityManager;

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<SimEntityId>(),
                ComponentType.ReadOnly<LocalTransform>());
            var ents = q.ToEntityArray(Allocator.Temp);
            var rows = new List<(int id, Vector3 pos, float hp)>(ents.Length);
            for (int i = 0; i < ents.Length; i++)
            {
                var p = em.GetComponentData<LocalTransform>(ents[i]).Position;
                float hp = em.HasComponent<Health>(ents[i]) ? em.GetComponentData<Health>(ents[i]).value : 0f;
                rows.Add((em.GetComponentData<SimEntityId>(ents[i]).value, new Vector3(p.x, p.y, p.z), hp));
            }
            ents.Dispose();
            // ID 오름차순으로 접는다 — 청크 순서가 흔들려도 지문이 흔들리지 않아야
            // 「sim 이 갈렸다」와 「배열 순서가 갈렸다」를 혼동하지 않는다.
            rows.Sort((a, b) => a.id.CompareTo(b.id));

            ulong h = 1469598103934665603UL; // FNV-1a offset basis
            foreach (var r in rows)
            {
                Mix(ref h, (ulong)r.id);
                Mix(ref h, (ulong)Mathf.RoundToInt(r.pos.x * 1000f));
                Mix(ref h, (ulong)Mathf.RoundToInt(r.pos.y * 1000f));
                Mix(ref h, (ulong)Mathf.RoundToInt(r.pos.z * 1000f));
                Mix(ref h, (ulong)Mathf.RoundToInt(r.hp * 100f));
            }
            d.entities = rows.Count;
            d.fingerprint = h;
            return d;
        }

        private static void Mix(ref ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (i * 8)) & 0xFF;
                h *= 1099511628211UL; // FNV prime
            }
        }

        private static string BuildReport(RunResult ra, RunResult rb, int firstDiff)
        {
            var a = ra.digests;
            var b = rb.digests;
            bool configSame = ra.configHash == rb.configHash;
            var sb = new StringBuilder();
            sb.AppendLine("# harness-determinism — 고정 스텝 2회 실행 대조");
            sb.AppendLine();
            sb.AppendLine("> **자동 생성물.** `Wassup/Battle/Sim Harness/Run Determinism Check (2 runs)` 로 Play 중 갱신한다.");
            sb.AppendLine("> 손으로 고치지 말 것 — 이 파일은 «고정 스텝에서 같은 seed·같은 입력이 같은 궤적을 낸다» 는");
            sb.AppendLine("> 사실의 기록이고, 골든(unit 4)은 이 전제 위에서만 성립한다.");
            sb.AppendLine();
            sb.AppendLine($"- seed: `{MatchSeed}` · 스텝: `{StepDt:F6}s` × **{TickCount}** 틱 · 입력: 틱 [{string.Join(", ", PlacementTicks)}] 에 방어유닛 1기씩");
            sb.AppendLine($"- 판정: **{(firstDiff < 0 ? "완전 일치" : $"틱 {firstDiff} 에서 분기")}**");
            sb.AppendLine($"- `configHash`: run A `{ra.configHash}` · run B `{rb.configHash}` — "
                          + (configSame
                              ? "동일(조건 드리프트 없음)"
                              : "**갈렸다 — 코드 회귀가 아니라 조건 드리프트다.** 시트 임포트가 SO 를 덮었는지 먼저 본다"));
            sb.AppendLine();
            sb.AppendLine("다이제스트 = `_battleClock` / 살아있는 sim 엔티티 수 / 상태 지문(FNV-1a over ID·위치·체력, ID 정렬).");
            sb.AppendLine();
            sb.AppendLine("| 틱 | run A | run B | 일치 |");
            sb.AppendLine("|---:|---|---|:-:|");
            for (int i = 0; i < a.Length; i++)
            {
                bool same = a[i].Equals(b[i]);
                // 전량은 길다 — 앞뒤와 분기 지점만 남긴다(사실의 밀도가 거기 있다).
                bool keep = i < 3 || i >= a.Length - 3 || i % 60 == 0 || !same
                            || System.Array.IndexOf(PlacementTicks, i) >= 0
                            || (firstDiff >= 0 && System.Math.Abs(i - firstDiff) <= 2);
                if (!keep) continue;
                sb.AppendLine($"| {i} | {a[i]} | {b[i]} | {(same ? "✓" : "✗")} |");
            }
            return sb.ToString();
        }
    }
}
