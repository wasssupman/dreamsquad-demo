#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Bridge
{
    public partial class BattleBridge
    {
        private static readonly string[] LegacyBridgeChannels =
        {
            "DefenderDeath", "DcTriggerFired", "KnockupVisual", "ShieldBreak",
            "UnitAttackVisual", "AttackOutputLog", "ProjectileHit", "HealApplied",
            "ShieldGranted", "DamageNumber", "EnemyKilled", "MeteorBarrageRequest",
            "GoalReached", "HazardRuntime", "HazardSpawnRequest", "HazardDestroyed",
            "BossLeapVisual", "UltimateLeapVisual",
        };

        private static readonly string[] LegacyInternalPhaseChannels =
        {
            "AggroHit", "Cast", "ThreatHit", "BlinkRequest", "EnemyCc", "DotApply",
            "CcClear", "StatModifierApply", "StackModifierApply",
        };

        private readonly Dictionary<Entity, int> _legacyTraceEntityIds = new Dictionary<Entity, int>();
        private LegacyTraceRecorder _legacyTraceRecorder;
        private int _legacyTraceEventTick = -1;
        private bool _legacyTraceHasFinalScore;
        private string _legacyTraceOutcome;
        private ScoreMath.BattleScore _legacyTraceFinalScore;

        public bool BeginLegacyTrace(string scenario, int tickRate)
        {
            if (!TestModeContext.HarnessActive || CurrentMatchConfig == null || tickRate <= 0) return false;
            _legacyTraceEventTick = -1;
            _legacyTraceHasFinalScore = false;
            _legacyTraceOutcome = null;
            _legacyTraceRecorder = new LegacyTraceRecorder(new LegacyTraceHeaderV0
            {
                configSchemaVersion = CurrentMatchConfig.Version,
                configHash = CurrentMatchConfig.ConfigHash,
                scenario = scenario ?? string.Empty,
                seed = _matchSeed,
                tickRate = tickRate,
                deckId = ActiveDeck != null ? ActiveDeck.deckId : string.Empty,
                mapGoalCount = _generatedMap.IsCreated && _generatedMap.goals.IsCreated
                    ? _generatedMap.goals.Length
                    : 0,
                channelPolicy = "27 catalogued: 18 Bridge-drained serialized; 9 internal phase queues excluded",
                bridgeDrainedChannels = (string[])LegacyBridgeChannels.Clone(),
                internalPhaseChannels = (string[])LegacyInternalPhaseChannels.Clone(),
                commandChannels = new[] { "CommandReceipt" },
            });
            return true;
        }

        public string CompleteLegacyTrace()
        {
            if (_legacyTraceRecorder == null) return null;

            bool defeated = !_outcome.IsEndless && _outcome.GoalReachedCount >= _outcome.EffectiveLeakLimit;
            ScoreMath.BattleScore score = _legacyTraceHasFinalScore
                ? _legacyTraceFinalScore
                : _outcome.CalculateScore(defeated, (float)_battleClock);
            // battle-sim-extraction unit 18-N — 그림자가 실제로 채워졌는지 한 줄로 남긴다
            // (골든 초록은 "라이브를 안 깼다" 는 증거일 뿐이다 — 정의는 BattleBridge.Shadow.cs).
            ShadowLogSummary();
            string canonicalState = BuildLegacyFinalStateCanonical();
            string json = _legacyTraceRecorder.Complete(new LegacyTraceFinalV0
            {
                outcome = _legacyTraceOutcome ?? (_running ? "incomplete" : (defeated ? "defeat" : "stopped")),
                scoreTotal = score.Total,
                scoreTime = score.Time,
                scoreStress = score.Stress,
                scoreKill = score.Kill,
                stateHash = LegacyTraceV0.Sha256(canonicalState),
                executedTicks = _harnessTick,
            });
            _legacyTraceRecorder = null;
            return json;
        }

        // Unit 4 restart corpus seam. It mirrors the dormant restart core without UI,
        // tournament reporting, or a rendered-frame gap, then re-arms the fixed-step gate.
        public bool RestartHarnessMatch(float fixedDt, HarnessInputSchedule schedule)
        {
            if (!TestModeContext.HarnessActive || fixedDt <= 0f) return false;
            string previousConfigHash = ConfigHash;
            TeardownCurrentBattle();
            _running = false;
            _outcome.ClearResultLatch();
            BeginPlacement();
            StartBattle();
            if (!_running || string.IsNullOrEmpty(previousConfigHash)
                || !string.Equals(previousConfigHash, ConfigHash, StringComparison.Ordinal))
                return false;
            return BeginHarness(fixedDt, schedule);
        }

        private void CaptureLegacyTraceResult(string outcome, ScoreMath.BattleScore score)
        {
            if (_legacyTraceRecorder == null) return;
            _legacyTraceOutcome = outcome;
            _legacyTraceFinalScore = score;
            _legacyTraceHasFinalScore = true;
        }

        private void SetLegacyTraceEventTick(int tick) => _legacyTraceEventTick = tick;

        private void RecordLegacyTraceTick(int tick)
        {
            if (_legacyTraceRecorder == null) return;
            GetHarnessDigestCounts(
                out int attackers, out int defenders, out int projectiles,
                out int nextWave, out int pending, out int goals, out int killScore);
            int bosses = 0;
            if (HasLiveEntityManager())
            {
                using EntityQuery bossQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<BossTag>());
                bosses = bossQuery.CalculateEntityCount();
            }
            _legacyTraceRecorder.RecordTick(new LegacyTraceTickV0
            {
                tick = tick,
                battleClockMicros = (long)Math.Round(_battleClock * 1_000_000.0, MidpointRounding.AwayFromZero),
                attackers = attackers,
                defenders = defenders,
                projectiles = projectiles,
                bosses = bosses,
                nextWaveIndex = nextWave,
                pendingSpawns = pending,
                goals = goals,
                killScore = killScore,
                running = _running,
                phase = (int)(GameManager.Instance != null ? GameManager.Instance.CurrentPhase : GamePhase.None),
                timerRemaining = _outcome.RemainingBattleSeconds((float)_battleClock),
                cost = GameManager.Instance != null && GameManager.Instance.CostRuntime != null
                    ? GameManager.Instance.CostRuntime.CurrentInt
                    : 0,
            });
        }

        private void TraceLegacyEvent<T>(string channel, T payload) where T : struct
        {
            if (_legacyTraceRecorder == null) return;
            _legacyTraceRecorder.RecordEvent(_legacyTraceEventTick, channel, FormatLegacyValue(payload));
        }

        private void TraceLegacyCommand(string command, bool accepted)
        {
            if (_legacyTraceRecorder == null) return;
            _legacyTraceRecorder.RecordEvent(_harnessTick, "CommandReceipt",
                "command=" + EscapeLegacy(command) + ",accepted=" + (accepted ? "true" : "false"));
        }

        public int ApplyHarnessDreamcatcherCard(DreamcatcherCard card)
        {
            if (!TestModeContext.HarnessActive) return -1;
            int handle = ApplyDreamcatcherCardHosted(card);
            TraceLegacyCommand("ApplyDreamcatcherCard:" + (card != null ? card.id : "null"), handle > 0);
            return handle;
        }

        public int EnqueueHarnessSimultaneousDeaths()
        {
            if (!TestModeContext.HarnessActive || !HasLiveEntityManager()) return 0;
            using EntityQuery attackerQuery = _em.CreateEntityQuery(
                    ComponentType.ReadOnly<AttackUnitTag>(),
                    ComponentType.ReadOnly<SimEntityId>(),
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadWrite<IncomingDamage>());
            using NativeArray<Entity> entities = attackerQuery.ToEntityArray(Allocator.Temp);
            var sorted = new List<(int id, Entity entity)>(entities.Length);
            for (int i = 0; i < entities.Length; i++)
                sorted.Add((_em.GetComponentData<SimEntityId>(entities[i]).value, entities[i]));
            sorted.Sort((a, b) => a.id.CompareTo(b.id));
            for (int i = 0; i < sorted.Count; i++)
                _em.GetBuffer<IncomingDamage>(sorted[i].entity).Add(new IncomingDamage
                {
                    amount = float.MaxValue,
                    source = Entity.Null,
                });
            TraceLegacyCommand("SimultaneousDeaths:count=" + sorted.Count, sorted.Count > 1);
            return sorted.Count;
        }

        private void TraceLegacyEvent<T>(string channel, T payload, Entity relatedEntity) where T : struct
        {
            if (_legacyTraceRecorder == null) return;
            string entity = relatedEntity == Entity.Null
                ? "-1"
                : ResolveLegacyTraceEntity(relatedEntity).ToString(CultureInfo.InvariantCulture);
            _legacyTraceRecorder.RecordEvent(_legacyTraceEventTick, channel,
                "entity=sim:" + entity + ",payload=" + FormatLegacyValue(payload));
        }

        private void ResetLegacyTraceEntityRegistry()
        {
            _legacyTraceEntityIds.Clear();
            _legacyTraceRecorder = null;
            _legacyTraceHasFinalScore = false;
            _legacyTraceOutcome = null;
        }

        private void RegisterLegacyTraceEntity(Entity entity, int simId)
        {
            if (entity != Entity.Null) _legacyTraceEntityIds[entity] = simId;
        }

        private int ResolveLegacyTraceEntity(Entity entity)
        {
            if (entity == Entity.Null) return -1;
            if (_legacyTraceEntityIds.TryGetValue(entity, out int simId)) return simId;
            throw new InvalidOperationException(
                $"LegacyTrace encountered an Entity without SimEntityId registry entry ({entity}).");
        }

        private string BuildLegacyFinalStateCanonical()
        {
            var sb = new StringBuilder(32768);
            AppendStateLine(sb, "battleClock", _battleClock);
            AppendStateLine(sb, "nextWaveIndex", _waveSchedule.NextWaveIndex);
            AppendStateLine(sb, "pendingSpawns", _waveSchedule.PendingCount);
            AppendStateLine(sb, "goals", _outcome.GoalReachedCount);
            AppendStateLine(sb, "leakPenalty", _outcome.LeakAllowancePenalty);
            AppendStateLine(sb, "killScore", _outcome.KillScoreTotal);
            AppendStateLine(sb, "running", _running);
            AppendStateLine(sb, "phase", GameManager.Instance != null
                ? (int)GameManager.Instance.CurrentPhase
                : (int)GamePhase.None);
            AppendStateLine(sb, "timerRemaining", _outcome.RemainingBattleSeconds((float)_battleClock));
            AppendStateLine(sb, "cost", GameManager.Instance != null && GameManager.Instance.CostRuntime != null
                ? GameManager.Instance.CostRuntime.Current
                : 0f);
            AppendStateLine(sb, "simEntityIdCounter", _simEntityIdCounter);
            AppendStateLine(sb, "meteorRng", _meteorRng.state);

            if (!HasLiveEntityManager()) return sb.ToString();
            using EntityQuery simIdQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<SimEntityId>());
            using NativeArray<Entity> entities = simIdQuery.ToEntityArray(Allocator.Temp);
            var sorted = new List<(int id, Entity entity)>(entities.Length);
            for (int i = 0; i < entities.Length; i++)
                sorted.Add((_em.GetComponentData<SimEntityId>(entities[i]).value, entities[i]));
            sorted.Sort((a, b) => a.id.CompareTo(b.id));

            for (int i = 0; i < sorted.Count; i++)
            {
                Entity entity = sorted[i].entity;
                sb.Append("entity+").Append(sorted[i].id).Append('\n');
                if (_em.HasComponent<AttackUnitTag>(entity)) sb.Append("tag=attacker\n");
                if (_em.HasComponent<DefenderUnitTag>(entity)) sb.Append("tag=defender\n");
                if (_em.HasComponent<BossTag>(entity)) sb.Append("tag=boss\n");
                if (_em.HasComponent<PendingDeployment>(entity)) sb.Append("tag=pendingDeployment\n");
                AppendComponent<LocalTransform>(sb, entity);
                AppendComponent<Health>(sb, entity);
                AppendComponent<FactionTag>(sb, entity);
                AppendComponent<KillScore>(sb, entity);
                AppendComponent<DefenderTile>(sb, entity);
                AppendComponent<PathFollowState>(sb, entity);
                AppendComponent<AttackState>(sb, entity);
                AppendComponent<ModifierStats>(sb, entity);
                AppendComponent<ProjectileState>(sb, entity);
                AppendComponent<BombLauncherState>(sb, entity);
                AppendBuffer<PatternSlot>(sb, entity);
                AppendBuffer<CcEffect>(sb, entity);
                AppendBuffer<DotEffect>(sb, entity);
                AppendBuffer<StatModifierSlot>(sb, entity);
                AppendBuffer<StackModifierSlot>(sb, entity);
                AppendBuffer<ThreatEntry>(sb, entity);
                AppendBuffer<ShieldSlot>(sb, entity);
                AppendBuffer<IncomingDamage>(sb, entity);
                AppendBuffer<IncomingHeal>(sb, entity);
                AppendBuffer<IncomingShield>(sb, entity);
                sb.Append("entity-").Append(sorted[i].id).Append('\n');
            }

            AppendUnkeyedComponents<PickupSpawnState>(sb);
            return sb.ToString();
        }

        private void AppendComponent<T>(StringBuilder sb, Entity entity) where T : unmanaged, IComponentData
        {
            if (_em.HasComponent<T>(entity)) AppendStateLine(sb, typeof(T).FullName, _em.GetComponentData<T>(entity));
        }

        private void AppendBuffer<T>(StringBuilder sb, Entity entity) where T : unmanaged, IBufferElementData
        {
            if (!_em.HasBuffer<T>(entity)) return;
            DynamicBuffer<T> buffer = _em.GetBuffer<T>(entity, true);
            sb.Append(typeof(T).FullName).Append("[").Append(buffer.Length).Append("]=");
            for (int i = 0; i < buffer.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(FormatLegacyValue(buffer[i]));
            }
            sb.Append('\n');
        }

        private void AppendUnkeyedComponents<T>(StringBuilder sb) where T : unmanaged, IComponentData
        {
            using EntityQuery componentQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            using NativeArray<Entity> entities = componentQuery.ToEntityArray(Allocator.Temp);
            var values = new List<string>();
            for (int i = 0; i < entities.Length; i++)
            {
                if (_em.HasComponent<SimEntityId>(entities[i])) continue;
                values.Add(FormatLegacyValue(_em.GetComponentData<T>(entities[i])));
            }
            values.Sort(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
                AppendStateLine(sb, "unkeyed." + typeof(T).FullName + "." + i, values[i]);
        }

        private void AppendStateLine(StringBuilder sb, string name, object value)
            => sb.Append(name).Append('=').Append(FormatLegacyValue(value)).Append('\n');

        private string FormatLegacyValue(object value)
        {
            if (value == null) return "null";
            if (value is Entity entity) return "sim:" + ResolveLegacyTraceEntity(entity).ToString(CultureInfo.InvariantCulture);
            if (value is string text) return EscapeLegacy(text);
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is float single) return single.ToString("R", CultureInfo.InvariantCulture);
            if (value is double dbl) return dbl.ToString("R", CultureInfo.InvariantCulture);
            Type type = value.GetType();
            if (type.IsEnum) return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (type.IsPrimitive || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            if (fields.Length == 0) return EscapeLegacy(value.ToString() ?? string.Empty);
            var sb = new StringBuilder();
            sb.Append(type.FullName ?? type.Name).Append('{');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(fields[i].Name).Append('=').Append(FormatLegacyValue(fields[i].GetValue(value)));
            }
            return sb.Append('}').ToString();
        }

        private static string EscapeLegacy(string value)
            => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\r", "\\r")
                .Replace("\n", "\\n").Replace("=", "\\=").Replace(",", "\\,")
                .Replace(";", "\\;").Replace("{", "\\{").Replace("}", "\\}");
    }
}
#endif
