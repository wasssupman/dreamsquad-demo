using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class LegacyTraceV0Tests
    {
        [Test]
        public void SerializeRoundTrip_IsByteIdenticalAndKeepsPlainPayload()
        {
            LegacyTraceRecorder recorder = CreateRecorder();
            recorder.RecordEvent(3, "EnemyKilled", "entity=sim:7,killScore=120");
            recorder.RecordTick(new LegacyTraceTickV0
            {
                tick = 0,
                battleClockMicros = 200000,
                attackers = 4,
                defenders = 2,
                bosses = 1,
                killScore = 120,
            });

            string json = recorder.Complete(new LegacyTraceFinalV0
            {
                outcome = "victory",
                scoreTotal = 1234,
                scoreTime = 1000,
                scoreStress = 114,
                scoreKill = 120,
                stateHash = LegacyTraceV0.Sha256("state"),
                executedTicks = 1,
            });
            LegacyTraceV0 decoded = LegacyTraceV0.DeserializeChecked(json);

            Assert.AreEqual(json, decoded.SerializeRoundTripChecked());
            Assert.AreEqual("LegacyTraceV0", decoded.header.version);
            Assert.AreEqual(3, decoded.events[0].tick);
            Assert.AreEqual("entity=sim:7,killScore=120", decoded.events[0].payload);
            Assert.AreEqual(1234, decoded.final.scoreTotal);
        }

        [Test]
        public void Recorder_AssignsContiguousEventSequenceInObservationOrder()
        {
            LegacyTraceRecorder recorder = CreateRecorder();
            recorder.RecordEvent(1, "A", "first");
            recorder.RecordEvent(1, "B", "second");
            string json = recorder.Complete(new LegacyTraceFinalV0
            {
                outcome = "incomplete",
                stateHash = LegacyTraceV0.Sha256(string.Empty),
            });
            LegacyTraceV0 decoded = LegacyTraceV0.DeserializeChecked(json);

            Assert.AreEqual(0, decoded.events[0].sequence);
            Assert.AreEqual(1, decoded.events[1].sequence);
            Assert.AreEqual("A", decoded.events[0].channel);
            Assert.AreEqual("B", decoded.events[1].channel);
        }

        [Test]
        public void MissingConfigHash_IsRejectedBeforePersistence()
        {
            LegacyTraceRecorder recorder = new LegacyTraceRecorder(new LegacyTraceHeaderV0
            {
                configHash = string.Empty,
                tickRate = 20,
            });

            Assert.Throws<InvalidOperationException>(() => recorder.Complete(new LegacyTraceFinalV0()));
        }

        [Test]
        public void SameCanonicalState_ProducesSameLowercaseSha256()
        {
            string first = LegacyTraceV0.Sha256("entity+1\nhealth=10\n");
            string second = LegacyTraceV0.Sha256("entity+1\nhealth=10\n");

            Assert.AreEqual(first, second);
            Assert.AreEqual(64, first.Length);
            Assert.AreEqual(first.ToLowerInvariant(), first);
        }

        [Test]
        public void TrackedGoldenCorpus_HasRequiredScenariosAndValidChannelPolicy()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Tests", "Golden", "LegacyTraceV0");
            string[] files = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
            string[] expected =
            {
                "normal", "boss_wave", "multi_goal", "dreamcatcher_heavy",
                "forced_wave", "simultaneous_death", "restart",
            };
            CollectionAssert.AreEquivalent(expected, files.Select(Path.GetFileNameWithoutExtension).ToArray());

            foreach (string file in files)
            {
                string json = File.ReadAllText(file);
                LegacyTraceV0 trace = LegacyTraceV0.DeserializeChecked(json);
                Assert.AreEqual(json, trace.SerializeRoundTripChecked(), Path.GetFileName(file));
                Assert.AreEqual(18, trace.header.bridgeDrainedChannels.Length, Path.GetFileName(file));
                Assert.AreEqual(9, trace.header.internalPhaseChannels.Length, Path.GetFileName(file));
                Assert.AreEqual(1, trace.header.commandChannels.Length, Path.GetFileName(file));
                Assert.AreEqual(trace.ticks.Count, trace.final.executedTicks, Path.GetFileName(file));
            }
        }

        private static LegacyTraceRecorder CreateRecorder()
        {
            return new LegacyTraceRecorder(new LegacyTraceHeaderV0
            {
                configSchemaVersion = 1,
                configHash = new string('a', 64),
                scenario = "test",
                seed = 42,
                tickRate = 20,
                deckId = "test_deck",
                mapGoalCount = 1,
                channelPolicy = "test",
                bridgeDrainedChannels = new[] { "A", "B" },
                internalPhaseChannels = new[] { "Internal" },
                commandChannels = new[] { "CommandReceipt" },
            });
        }
    }
}
