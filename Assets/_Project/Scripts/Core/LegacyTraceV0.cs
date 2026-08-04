using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Wassup.Core
{
    // battle-sim-extraction unit 4 — M1 A/B parity의 레거시 기준선.
    // 스키마에는 UnityEngine.Object/Entity/NativeContainer를 넣지 않는다. Bridge가 모든
    // 경계 값을 plain scalar/string으로 정규화한 뒤 이 DTO에 넘긴다.
    [Serializable]
    public sealed class LegacyTraceV0
    {
        public const string Version = "LegacyTraceV0";

        public LegacyTraceHeaderV0 header = new LegacyTraceHeaderV0();
        public List<LegacyTraceTickV0> ticks = new List<LegacyTraceTickV0>();
        public List<LegacyTraceEventV0> events = new List<LegacyTraceEventV0>();
        public LegacyTraceFinalV0 final = new LegacyTraceFinalV0();

        public string SerializeRoundTripChecked()
        {
            Validate();
            string first = JsonUtility.ToJson(this, false);
            LegacyTraceV0 decoded = JsonUtility.FromJson<LegacyTraceV0>(first);
            if (decoded == null) throw new InvalidOperationException("LegacyTraceV0 deserialize returned null.");
            decoded.Validate();
            string second = JsonUtility.ToJson(decoded, false);
            if (!string.Equals(first, second, StringComparison.Ordinal))
                throw new InvalidOperationException("LegacyTraceV0 JSON round-trip was not byte-identical. "
                    + DescribeFirstDifference(first, second));
            return first;
        }

        private static string DescribeFirstDifference(string first, string second)
        {
            int common = Math.Min(first.Length, second.Length);
            int index = 0;
            while (index < common && first[index] == second[index]) index++;
            int start = Math.Max(0, index - 48);
            int firstLength = Math.Min(first.Length - start, 96);
            int secondLength = Math.Min(second.Length - start, 96);
            string firstSlice = firstLength > 0 ? first.Substring(start, firstLength) : string.Empty;
            string secondSlice = secondLength > 0 ? second.Substring(start, secondLength) : string.Empty;
            return $"index={index}, firstLength={first.Length}, secondLength={second.Length}, "
                + $"first='{firstSlice}', second='{secondSlice}'.";
        }

        public static LegacyTraceV0 DeserializeChecked(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("Trace JSON is empty.", nameof(json));
            LegacyTraceV0 trace = JsonUtility.FromJson<LegacyTraceV0>(json);
            if (trace == null) throw new InvalidOperationException("LegacyTraceV0 deserialize returned null.");
            trace.Validate();
            return trace;
        }

        public static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(value ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
                return result.ToString();
            }
        }

        private void Validate()
        {
            if (header == null) throw new InvalidOperationException("LegacyTraceV0.header is required.");
            if (!string.Equals(header.version, Version, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported trace version '{header.version}'.");
            if (header.configHash == null || header.configHash.Length != 64)
                throw new InvalidOperationException("LegacyTraceV0.configHash must be a 64-character SHA-256.");
            if (header.tickRate <= 0) throw new InvalidOperationException("LegacyTraceV0.tickRate must be positive.");
            if (ticks == null || events == null || final == null)
                throw new InvalidOperationException("LegacyTraceV0 collections/final are required.");
            if (final.stateHash == null || final.stateHash.Length != 64)
                throw new InvalidOperationException("LegacyTraceV0.stateHash must be a 64-character SHA-256.");
            if (final.executedTicks != ticks.Count)
                throw new InvalidOperationException(
                    $"LegacyTraceV0 executedTicks ({final.executedTicks}) does not match ticks.Count ({ticks.Count}).");
            for (int i = 0; i < ticks.Count; i++)
            {
                if (ticks[i] == null || ticks[i].tick != i)
                    throw new InvalidOperationException($"LegacyTraceV0 tick sequence is not contiguous at {i}.");
            }
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] == null || events[i].sequence != i)
                    throw new InvalidOperationException($"LegacyTraceV0 event sequence is not contiguous at {i}.");
            }
        }
    }

    [Serializable]
    public sealed class LegacyTraceHeaderV0
    {
        public string version = LegacyTraceV0.Version;
        public int configSchemaVersion;
        public string configHash;
        public string scenario;
        public int seed;
        public int tickRate;
        public string deckId;
        public int mapGoalCount;
        public string channelPolicy;
        public string[] bridgeDrainedChannels = Array.Empty<string>();
        public string[] internalPhaseChannels = Array.Empty<string>();
        public string[] commandChannels = Array.Empty<string>();
    }

    [Serializable]
    public sealed class LegacyTraceTickV0
    {
        public int tick;
        public long battleClockMicros;
        public int attackers;
        public int defenders;
        public int projectiles;
        public int bosses;
        public int nextWaveIndex;
        public int pendingSpawns;
        public int goals;
        public int killScore;
        public bool running;
        public int phase;
        public float timerRemaining;
        public int cost;
    }

    [Serializable]
    public sealed class LegacyTraceEventV0
    {
        public int sequence;
        public int tick;
        public string channel;
        public string payload;
    }

    [Serializable]
    public sealed class LegacyTraceFinalV0
    {
        public string outcome;
        public int scoreTotal;
        public int scoreTime;
        public int scoreStress;
        public int scoreKill;
        public string stateHash;
        public int executedTicks;
    }

    public sealed class LegacyTraceRecorder
    {
        private readonly LegacyTraceV0 _trace;

        public LegacyTraceRecorder(LegacyTraceHeaderV0 header)
        {
            _trace = new LegacyTraceV0 { header = header ?? throw new ArgumentNullException(nameof(header)) };
        }

        public void RecordEvent(int producerTick, string channel, string payload)
        {
            _trace.events.Add(new LegacyTraceEventV0
            {
                sequence = _trace.events.Count,
                tick = producerTick,
                channel = channel ?? string.Empty,
                payload = payload ?? string.Empty,
            });
        }

        public void RecordTick(LegacyTraceTickV0 tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            _trace.ticks.Add(tick);
        }

        public string Complete(LegacyTraceFinalV0 final)
        {
            _trace.final = final ?? throw new ArgumentNullException(nameof(final));
            return _trace.SerializeRoundTripChecked();
        }
    }
}
