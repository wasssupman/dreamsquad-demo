using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.EditorTools
{
    // Runs every corpus case in a fresh Play session twice. A golden is replaced only
    // after the two round-tripped JSON documents compare byte-for-byte equal.
    public static class LegacyTraceGoldenRunner
    {
        private const string ScenePath = "Assets/_Project/Scenes/BattleScene.unity";
        private const string StatePrefix = "LegacyTraceGolden.";
        private const string ActiveKey = StatePrefix + "active";
        private const string ScenarioKey = StatePrefix + "scenario";
        private const string RepeatKey = StatePrefix + "repeat";
        private const string FinishKey = StatePrefix + "finish";
        private const string FailureKey = StatePrefix + "failure";
        private const string OriginalMapKey = StatePrefix + "originalMap";
        private const string OriginalEndlessKey = StatePrefix + "originalEndless";
        private const string SeedKey = "SimHarness.seed";
        private const float FixedDt = 0.05f;
        private const int TickRate = 20;
        private const int StartupFrameCap = 1800;

        private sealed class Scenario
        {
            public readonly string id;
            public readonly int seed;
            public readonly int mapIndex;
            public readonly int ticks;
            public readonly int[] forceWaveTicks;
            public readonly bool dreamcatcherHeavy;
            public readonly bool simultaneousDeaths;
            public readonly bool restart;
            public readonly bool placeDefenders;

            public Scenario(
                string id, int seed, int mapIndex, int ticks, int[] forceWaveTicks = null,
                bool dreamcatcherHeavy = false, bool simultaneousDeaths = false,
                bool restart = false, bool placeDefenders = true)
            {
                this.id = id;
                this.seed = seed;
                this.mapIndex = mapIndex;
                this.ticks = ticks;
                this.forceWaveTicks = forceWaveTicks ?? Array.Empty<int>();
                this.dreamcatcherHeavy = dreamcatcherHeavy;
                this.simultaneousDeaths = simultaneousDeaths;
                this.restart = restart;
                this.placeDefenders = placeDefenders;
            }
        }

        private static readonly Scenario[] Scenarios =
        {
            new Scenario("normal", 202608041, 0, 600),
            new Scenario("boss_wave", 202608042, 5, 600, new[] { 0, 0, 0, 0, 0 }),
            new Scenario("multi_goal", 202608043, 2, 600, new[] { 80 }),
            new Scenario("dreamcatcher_heavy", 202608044, 1, 600, new[] { 100 }, dreamcatcherHeavy: true),
            new Scenario("forced_wave", 202608045, 3, 600, new[] { 0, 20, 40 }),
            new Scenario("simultaneous_death", 202608046, 4, 240, new[] { 0, 0, 0 },
                simultaneousDeaths: true, placeDefenders: false),
            new Scenario("restart", 202608047, 0, 600, new[] { 100 }, restart: true),
        };

        private static int _pollFrames;
        private static bool _prepared;

        [MenuItem("Wassup/Battle/Sim Harness/Regenerate LegacyTraceV0 Goldens", false, 311)]
        public static void RegenerateGoldens()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[LegacyTrace] Exit Play Mode before regenerating goldens.");
                return;
            }

            SessionState.SetInt(OriginalMapKey, DevMapOverride.Index);
            SessionState.SetBool(OriginalEndlessKey, DevMapOverride.Endless);
            SessionState.SetInt(ScenarioKey, 0);
            SessionState.SetInt(RepeatKey, 1);
            SessionState.SetInt(ActiveKey, 1);
            SessionState.EraseInt(FinishKey);
            SessionState.EraseString(FailureKey);
            Directory.CreateDirectory(WorkDirectory);
            Directory.CreateDirectory(GoldenDirectory);
            for (int i = 0; i < Scenarios.Length; i++)
            {
                TryDelete(RunPath(Scenarios[i], 1));
                TryDelete(RunPath(Scenarios[i], 2));
            }
            StartNextRun();
        }

        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(ActiveKey, 0) != 0)
            {
                _pollFrames = 0;
                _prepared = false;
                EditorApplication.update += Drive;
                return;
            }

            if (change != PlayModeStateChange.EnteredEditMode) return;
            if (SessionState.GetInt(FinishKey, 0) != 0)
            {
                int exitCode = string.IsNullOrEmpty(SessionState.GetString(FailureKey, string.Empty)) ? 0 : 1;
                FinishInEditMode(exitCode);
            }
            else if (SessionState.GetInt(ActiveKey, 0) != 0)
            {
                StartNextRun();
            }
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        private static string WorkDirectory => Path.Combine(ProjectRoot, "Library", "LegacyTraceV0");
        private static string GoldenDirectory => Path.Combine(Application.dataPath, "_Project", "Tests", "Golden", "LegacyTraceV0");
        private static string RunPath(Scenario scenario, int repeat) =>
            Path.Combine(WorkDirectory, scenario.id + ".run" + repeat + ".json");
        private static string GoldenPath(Scenario scenario) => Path.Combine(GoldenDirectory, scenario.id + ".json");

        private static void StartNextRun()
        {
            int index = SessionState.GetInt(ScenarioKey, 0);
            if (index < 0 || index >= Scenarios.Length)
            {
                SessionState.SetInt(FinishKey, 1);
                FinishInEditMode(0);
                return;
            }

            Scenario scenario = Scenarios[index];
            DevMapOverride.Endless = false;
            DevMapOverride.Index = scenario.mapIndex;
            SessionState.SetInt(SeedKey, scenario.seed);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void Drive()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Drive;
                return;
            }
            if (++_pollFrames > StartupFrameCap)
            {
                Fail("battle startup timeout");
                return;
            }

            BattleBridge bridge = UnityEngine.Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null) return;
            Scenario scenario = Scenarios[SessionState.GetInt(ScenarioKey, 0)];
            if (!bridge.BattleRunning)
            {
                if (!_prepared)
                {
                    PreparePlacement(bridge, scenario);
                    _prepared = true;
                }
                bridge.StartBattle();
                if (!bridge.BattleRunning) return;
            }

            EditorApplication.update -= Drive;
            RunScenario(bridge, scenario);
        }

        private static void PreparePlacement(BattleBridge bridge, Scenario scenario)
        {
            bridge.SetDreamstones(null);
            DefenderCatalog catalog = FindCatalog();
            if (catalog == null) throw new InvalidOperationException("DefenderCatalog not loaded.");
            DefenderUnitData[] units =
            {
                catalog.ById("ranger"), catalog.ById("guardian"),
                catalog.ById("fire_caster"), catalog.ById("scout"),
            };
            units = units.Where(unit => unit != null).ToArray();
            bridge.SetDefenderPool(units);
            bridge.BeginPlacement();
            GameManager gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm == null || gm.CostRuntime == null) throw new InvalidOperationException("GameManager/CostRuntime missing.");
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            if (!scenario.placeDefenders) return;
            for (int i = 0; i < units.Length; i++)
            {
                if (!PlaceFirstValid(bridge, units[i]))
                    throw new InvalidOperationException("Could not place defender '" + units[i].id + "'.");
            }
        }

        private static void RunScenario(BattleBridge bridge, Scenario scenario)
        {
            try
            {
                HarnessInputSchedule schedule = BuildSchedule(bridge, scenario);
                if (!bridge.BeginHarness(FixedDt, schedule))
                    throw new InvalidOperationException("BeginHarness failed.");
                string expectedConfigHash = bridge.ConfigHash;

                if (scenario.restart)
                {
                    if (!bridge.BeginLegacyTrace("restart_prelude", TickRate))
                        throw new InvalidOperationException("BeginLegacyTrace prelude failed.");
                    for (int i = 0; i < 20 && bridge.BattleRunning; i++) bridge.StepOneTick(FixedDt);
                    if (string.IsNullOrEmpty(bridge.CompleteLegacyTrace()))
                        throw new InvalidOperationException("Restart prelude trace was empty.");
                    if (!bridge.RestartHarnessMatch(FixedDt, schedule))
                        throw new InvalidOperationException("RestartHarnessMatch failed.");
                    if (!string.Equals(expectedConfigHash, bridge.ConfigHash, StringComparison.Ordinal))
                        throw new InvalidOperationException("Restart changed configHash.");
                }

                if (!bridge.BeginLegacyTrace(scenario.id, TickRate))
                    throw new InvalidOperationException("BeginLegacyTrace failed.");
                for (int i = 0; i < scenario.ticks && bridge.BattleRunning; i++) bridge.StepOneTick(FixedDt);
                string json = bridge.CompleteLegacyTrace();
                LegacyTraceV0 trace = LegacyTraceV0.DeserializeChecked(json);
                ValidateCoverage(scenario, trace);
                PersistRunAndContinue(scenario, json);
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (bridge != null) bridge.StopBattle();
            }
        }

        private static HarnessInputSchedule BuildSchedule(BattleBridge bridge, Scenario scenario)
        {
            var schedule = new HarnessInputSchedule();
            for (int i = 0; i < scenario.forceWaveTicks.Length; i++)
                schedule.Add(scenario.forceWaveTicks[i], bridge.ForceNextWave);
            if (scenario.dreamcatcherHeavy)
            {
                AddCard(schedule, 10, bridge, "trace_ranger_damage", CardTargetAxis.ClassRanger, CardBuffKind.AttackDamage, 20f);
                AddCard(schedule, 20, bridge, "trace_ranger_speed", CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 15f);
                AddCard(schedule, 30, bridge, "trace_guardian_health", CardTargetAxis.ClassGuardian, CardBuffKind.EffectiveHealth, 20f);
            }
            if (scenario.simultaneousDeaths)
                schedule.Add(100, () => bridge.EnqueueHarnessSimultaneousDeaths());
            return schedule;
        }

        private static void AddCard(
            HarnessInputSchedule schedule, int tick, BattleBridge bridge, string id,
            CardTargetAxis axis, CardBuffKind kind, float percent)
        {
            schedule.Add(tick, () =>
            {
                DreamcatcherCard card = ScriptableObject.CreateInstance<DreamcatcherCard>();
                card.id = id;
                card.axis = axis;
                card.effects = new[] { new CardEffect { kind = kind, percent = percent } };
                bridge.ApplyHarnessDreamcatcherCard(card);
            });
        }

        private static void ValidateCoverage(Scenario scenario, LegacyTraceV0 trace)
        {
            if (!string.Equals(trace.header.scenario, scenario.id, StringComparison.Ordinal))
                throw new InvalidOperationException("Scenario id was not preserved.");
            if (trace.ticks.Count == 0 || trace.final.executedTicks <= 0)
                throw new InvalidOperationException("Trace contains no executed ticks.");
            if (trace.header.mapGoalCount <= 0)
                throw new InvalidOperationException("Trace map has no goals.");
            if (scenario.id == "multi_goal" && trace.header.mapGoalCount < 2)
                throw new InvalidOperationException("multi_goal did not select a multi-goal map.");
            if (scenario.id == "boss_wave" && !trace.ticks.Any(tick => tick.bosses > 0))
                throw new InvalidOperationException("boss_wave did not observe a boss entity.");

            int acceptedCards = trace.events.Count(evt => evt.channel == "CommandReceipt"
                && evt.payload.Contains("ApplyDreamcatcherCard:")
                && evt.payload.Contains("accepted=true"));
            if (scenario.dreamcatcherHeavy && acceptedCards != 3)
                throw new InvalidOperationException("dreamcatcher_heavy did not accept all three cards.");

            if (scenario.simultaneousDeaths)
            {
                bool commandAccepted = trace.events.Any(evt => evt.channel == "CommandReceipt"
                    && evt.payload.Contains("SimultaneousDeaths:")
                    && evt.payload.Contains("accepted=true"));
                bool sameTickDeaths = trace.events.Where(evt => evt.channel == "EnemyKilled")
                    .GroupBy(evt => evt.tick).Any(group => group.Count() > 1);
                if (!commandAccepted || !sameTickDeaths)
                    throw new InvalidOperationException("simultaneous_death did not record multiple kills on one tick.");
            }
        }

        private static void PersistRunAndContinue(Scenario scenario, string json)
        {
            int repeat = SessionState.GetInt(RepeatKey, 1);
            File.WriteAllText(RunPath(scenario, repeat), json, new System.Text.UTF8Encoding(false));
            if (repeat == 1)
            {
                SessionState.SetInt(RepeatKey, 2);
                EditorApplication.ExitPlaymode();
                return;
            }

            string first = File.ReadAllText(RunPath(scenario, 1));
            if (!string.Equals(first, json, StringComparison.Ordinal))
                throw new InvalidOperationException(scenario.id + " two-run trace diff was non-zero.");
            Debug.Log("[LegacyTrace] STAGED " + scenario.id + " — two-run diff 0, " + json.Length + " bytes.");

            int next = SessionState.GetInt(ScenarioKey, 0) + 1;
            SessionState.SetInt(ScenarioKey, next);
            SessionState.SetInt(RepeatKey, 1);
            if (next >= Scenarios.Length) SessionState.SetInt(FinishKey, 1);
            EditorApplication.ExitPlaymode();
        }

        private static DefenderCatalog FindCatalog()
        {
            DefenderCatalog[] all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData unit)
        {
            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
                if (bridge.CanPlaceDefenderAt(x, y, unit, out _))
                    return bridge.PlaceDefenderAs(x, y, unit);
            return false;
        }

        private static void Fail(string reason)
        {
            EditorApplication.update -= Drive;
            Debug.LogError("[LegacyTrace] FAIL — " + reason);
            SessionState.SetString(FailureKey, reason);
            SessionState.SetInt(FinishKey, 1);
            SessionState.SetInt(ActiveKey, 0);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            else FinishInEditMode(1);
        }

        private static void FinishInEditMode(int exitCode)
        {
            if (exitCode == 0)
            {
                try
                {
                    PublishValidatedCorpus();
                }
                catch (Exception ex)
                {
                    exitCode = 1;
                    SessionState.SetString(FailureKey, ex.GetType().Name + ": " + ex.Message);
                }
            }
            DevMapOverride.Index = SessionState.GetInt(OriginalMapKey, -1);
            DevMapOverride.Endless = SessionState.GetBool(OriginalEndlessKey, false);
            SessionState.SetInt(ActiveKey, 0);
            SessionState.EraseInt(FinishKey);
            AssetDatabase.Refresh();
            string failure = SessionState.GetString(FailureKey, string.Empty);
            if (exitCode == 0) Debug.Log("[LegacyTrace] PASS — all 7 corpus scenarios produced stable goldens.");
            else Debug.LogError("[LegacyTrace] corpus failed: " + failure);
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
        }

        private static void PublishValidatedCorpus()
        {
            var documents = new string[Scenarios.Length];
            for (int i = 0; i < Scenarios.Length; i++)
            {
                string firstPath = RunPath(Scenarios[i], 1);
                string secondPath = RunPath(Scenarios[i], 2);
                if (!File.Exists(firstPath) || !File.Exists(secondPath))
                    throw new InvalidOperationException("Staged corpus is incomplete at '" + Scenarios[i].id + "'.");
                string first = File.ReadAllText(firstPath);
                string second = File.ReadAllText(secondPath);
                if (!string.Equals(first, second, StringComparison.Ordinal))
                    throw new InvalidOperationException("Staged corpus diff changed at '" + Scenarios[i].id + "'.");
                LegacyTraceV0 trace = LegacyTraceV0.DeserializeChecked(second);
                ValidateCoverage(Scenarios[i], trace);
                documents[i] = second;
            }

            Directory.CreateDirectory(GoldenDirectory);
            for (int i = 0; i < Scenarios.Length; i++)
                File.WriteAllText(GoldenPath(Scenarios[i]), documents[i], new System.Text.UTF8Encoding(false));
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Debug.LogWarning("[LegacyTrace] Could not delete '" + path + "': " + ex.Message); }
        }
    }
}
