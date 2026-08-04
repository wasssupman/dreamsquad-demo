using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // battle-sim-extraction unit 3 — one match's complete, immutable data boundary.
    // The snapshot retains only a canonical string and its SHA-256; source SOs and
    // NativeArrays may be changed/disposed immediately after Capture returns.
    public sealed class MatchConfigSnapshot
    {
        public const int SchemaVersion = 1;
        public const string RulesetVersion = "legacy-ecs-m0";

        public int Version { get; }
        public string CanonicalBlob { get; }
        public string ConfigHash { get; }

        private MatchConfigSnapshot(string canonicalBlob)
        {
            Version = SchemaVersion;
            CanonicalBlob = canonicalBlob ?? string.Empty;
            ConfigHash = ComputeSha256(CanonicalBlob);
        }

        public static MatchConfigSnapshot Capture(MatchConfigCapture source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var writer = new CanonicalWriter();
            writer.WriteScalar("schemaVersion", SchemaVersion);
            writer.WriteScalar("rulesetVersion", RulesetVersion);
            writer.WriteScalar("matchSeed", source.matchSeed);
            writer.WriteScalar("fixedMapSeed", source.fixedMapSeed);
            writer.WriteScalar("usesGeneratedWaves", source.usesGeneratedWaves);
            writer.WriteScalar("timerDurationSec", source.timerDurationSec);

            WriteMap(writer, source.generatedMap);
            WriteWavePlan(writer, source.generatedWavePlan, source.usesGeneratedWaves);
            WriteEffectTiles(writer, source.effectTiles);

            writer.WriteValue("activeDeck", source.activeDeck);
            writer.WriteValue("defenderPool", source.defenderPool);
            writer.WriteValue("skillLoadout", source.skillLoadout);
            writer.WriteValue("dreamstones", source.dreamstones);
            writer.WriteValue("dreamcatcherCards", source.dreamcatcherCards);
            writer.WriteValue("awakeningConfig", source.awakeningConfig);
            writer.WriteValue("assignedGimmick", source.assignedGimmick);
            writer.WriteValue("scoreRules", source.scoreRules);
            writer.WriteValue("costConfig", source.costConfig);
            writer.WriteScalar("costRegenRateMultiplier", source.costRegenRateMultiplier);
            writer.WriteValue("stackModifiers", source.stackModifiers);

            writer.WriteScalar("scene.tileSize", source.tileSize);
            writer.WriteScalar("scene.spawnSpreadEnabled", source.spawnSpreadEnabled);
            writer.WriteScalar("scene.spawnSpreadFraction", source.spawnSpreadFraction);
            writer.WriteScalar("scene.spawnSpreadTopScale", source.spawnSpreadTopScale);
            writer.WriteScalar("scene.spawnSubLaneCount", source.spawnSubLaneCount);
            writer.WriteScalar("scene.enableAdjacencySynergy", source.enableAdjacencySynergy);
            writer.WriteScalar("scene.bossLeapTotalSeconds", source.bossLeapTotalSeconds);

            return new MatchConfigSnapshot(writer.ToString());
        }

        private static void WriteMap(CanonicalWriter writer, GeneratedMap map)
        {
            writer.Begin("generatedMap");
            writer.WriteScalar("created", map.IsCreated);
            writer.WriteScalar("seed", map.seed);
            writer.WriteScalar("generatorVersion", map.generatorVersion);
            writer.WriteScalar("gridWidth", map.gridSize.x);
            writer.WriteScalar("gridHeight", map.gridSize.y);
            writer.WriteScalar("goal.x", map.goal.x);
            writer.WriteScalar("goal.y", map.goal.y);

            writer.Begin("tiles");
            if (map.tiles.IsCreated)
                for (int i = 0; i < map.tiles.Length; i++) writer.WriteScalar(i.ToString(CultureInfo.InvariantCulture), (int)map.tiles[i]);
            writer.End("tiles");

            writer.Begin("mergeDegree");
            if (map.mergeDegree.IsCreated)
                for (int i = 0; i < map.mergeDegree.Length; i++) writer.WriteScalar(i.ToString(CultureInfo.InvariantCulture), map.mergeDegree[i]);
            writer.End("mergeDegree");

            writer.Begin("chokepoint");
            if (map.chokepoint.IsCreated)
                for (int i = 0; i < map.chokepoint.Length; i++) writer.WriteScalar(i.ToString(CultureInfo.InvariantCulture), map.chokepoint[i]);
            writer.End("chokepoint");

            writer.Begin("spawns");
            if (map.spawns.IsCreated)
                for (int i = 0; i < map.spawns.Length; i++) WriteInt2(writer, i, map.spawns[i]);
            writer.End("spawns");

            writer.Begin("goals");
            if (map.goals.IsCreated)
                for (int i = 0; i < map.goals.Length; i++) WriteInt2(writer, i, map.goals[i]);
            else
                WriteInt2(writer, 0, map.goal);
            writer.End("goals");
            writer.End("generatedMap");
        }

        private static void WriteInt2(CanonicalWriter writer, int index, int2 value)
        {
            string prefix = index.ToString(CultureInfo.InvariantCulture);
            writer.WriteScalar(prefix + ".x", value.x);
            writer.WriteScalar(prefix + ".y", value.y);
        }

        private static void WriteWavePlan(CanonicalWriter writer, GeneratedWavePlan plan, bool enabled)
        {
            writer.Begin("generatedWavePlan");
            writer.WriteScalar("enabled", enabled);
            if (enabled)
            {
                writer.WriteScalar("seed", plan.seed);
                writer.WriteScalar("generatorVersion", plan.generatorVersion);
                writer.WriteScalar("timerDurationSec", plan.timerDurationSec);
                writer.WriteScalar("waveIntervalSec", plan.waveIntervalSec);
                writer.WriteScalar("intraWaveSpacingSec", plan.intraWaveSpacingSec);
                writer.WriteScalar("spawnLeadInSec", plan.spawnLeadInSec);
                int waveCount = plan.waves != null ? plan.waves.Count : 0;
                writer.WriteScalar("waveCount", waveCount);
                for (int i = 0; i < waveCount; i++)
                {
                    GeneratedWave wave = plan.waves[i];
                    writer.Begin("wave." + i.ToString(CultureInfo.InvariantCulture));
                    writer.WriteScalar("waveIndex", wave.waveIndex);
                    writer.WriteScalar("triggerTimeSec", wave.triggerTimeSec);
                    writer.WriteScalar("totalCount", wave.totalCount);
                    writer.WriteScalar("spawnIntervalSec", wave.spawnIntervalSec);
                    writer.WriteScalar("expandMode", (int)wave.expandMode);
                    int groupCount = wave.groups != null ? wave.groups.Count : 0;
                    writer.WriteScalar("groupCount", groupCount);
                    for (int g = 0; g < groupCount; g++)
                    {
                        WaveSpawnGroup group = wave.groups[g];
                        writer.Begin("group." + g.ToString(CultureInfo.InvariantCulture));
                        writer.WriteScalar("count", group.count);
                        writer.WriteScalar("triggerOffsetSec", group.triggerOffsetSec);
                        writer.WriteValue("unit", group.unit);
                        writer.End("group." + g.ToString(CultureInfo.InvariantCulture));
                    }
                    writer.End("wave." + i.ToString(CultureInfo.InvariantCulture));
                }
            }
            writer.End("generatedWavePlan");
        }

        private static void WriteEffectTiles(CanonicalWriter writer, IReadOnlyList<MatchConfigEffectTile> effectTiles)
        {
            writer.Begin("effectTiles");
            if (effectTiles != null)
            {
                var sorted = new List<MatchConfigEffectTile>(effectTiles.Count);
                for (int i = 0; i < effectTiles.Count; i++) sorted.Add(effectTiles[i]);
                sorted.Sort((a, b) =>
                {
                    int y = a.cell.y.CompareTo(b.cell.y);
                    return y != 0 ? y : a.cell.x.CompareTo(b.cell.x);
                });
                for (int i = 0; i < sorted.Count; i++)
                {
                    writer.Begin(i.ToString(CultureInfo.InvariantCulture));
                    writer.WriteScalar("x", sorted[i].cell.x);
                    writer.WriteScalar("y", sorted[i].cell.y);
                    writer.WriteValue("data", sorted[i].data);
                    writer.End(i.ToString(CultureInfo.InvariantCulture));
                }
            }
            writer.End("effectTiles");
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(value));
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private sealed class CanonicalWriter
        {
            private static readonly HashSet<string> PresentationFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "displayName", "description", "visible", "icon", "art", "portrait", "overlayTile",
                "visualMesh", "visualMaterial", "skeletonDataAsset", "spineSkinName", "idleAnimation",
                "walkAnimation", "attackAnimation", "deathAnimation", "spineVisualScale", "partSkins",
                "slotColors", "dragAnimation", "deployAnimation", "placementVfxPrefab", "attackVfxPrefab",
                "deployCutsceneFrames", "deployCutsceneFps", "deployCutsceneScale", "deployCutsceneOffset",
                "deployCutsceneDepth", "deployCutsceneTiltGain", "castAnchorBone", "castAnchorLocalOffset",
                "weaponTrailPrefab", "weaponTrailEndNormalized", "knockupVisualHeight", "uiTint",
                "visualScale", "visualHeightOffset", "projectilePrefab", "hitPrefab", "facing", "spinSpeed",
                "preserveVfxColors", "tintColor", "emissionMultiplier", "scaleJitter", "hueJitter",
                "rotationJitter", "textureVariants", "selectMode", "hitVfxLifetime", "hitVfxScale",
                "castPrefab", "castVfxLifetime", "dropHeight", "fallPortion"
            };

            private readonly StringBuilder _builder = new StringBuilder(16384);
            private readonly HashSet<object> _activeObjects = new HashSet<object>(ReferenceComparer.Instance);

            public void Begin(string name) => _builder.Append('+').Append(Escape(name)).Append('\n');
            public void End(string name) => _builder.Append('-').Append(Escape(name)).Append('\n');
            public void WriteScalar(string name, object value) => WriteLine(name, FormatScalar(value));
            public override string ToString() => _builder.ToString();

            public void WriteValue(string name, object value)
            {
                if (value == null || (value is UnityEngine.Object unityObject && unityObject == null))
                {
                    WriteLine(name, "null");
                    return;
                }

                Type type = value.GetType();
                if (IsScalar(type))
                {
                    WriteLine(name, FormatScalar(value));
                    return;
                }

                if (value is UnityEngine.Object && !(value is ScriptableObject))
                {
                    WriteLine(name, "presentation-object-excluded");
                    return;
                }

                if (value is IEnumerable sequence && !(value is string))
                {
                    Begin(name);
                    int index = 0;
                    foreach (object item in sequence)
                        WriteValue(index++.ToString(CultureInfo.InvariantCulture), item);
                    End(name);
                    return;
                }

                if (!type.IsValueType && !_activeObjects.Add(value))
                {
                    WriteLine(name, "cycle:" + type.FullName);
                    return;
                }

                Begin(name);
                WriteLine("$type", type.FullName ?? type.Name);
                FieldInfo[] fields = SerializedFields(type);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (PresentationFields.Contains(field.Name)) continue;
                    if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)
                        && !typeof(ScriptableObject).IsAssignableFrom(field.FieldType)) continue;
                    WriteValue(field.Name, field.GetValue(value));
                }
                End(name);

                if (!type.IsValueType) _activeObjects.Remove(value);
            }

            private void WriteLine(string name, string value)
                => _builder.Append(Escape(name)).Append('=').Append(Escape(value)).Append('\n');

            private static FieldInfo[] SerializedFields(Type type)
            {
                var fields = new List<FieldInfo>();
                for (Type cursor = type; cursor != null && cursor != typeof(ScriptableObject)
                    && cursor != typeof(UnityEngine.Object); cursor = cursor.BaseType)
                {
                    FieldInfo[] declared = cursor.GetFields(BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    for (int i = 0; i < declared.Length; i++)
                    {
                        FieldInfo field = declared[i];
                        if (field.IsStatic || field.IsNotSerialized) continue;
                        if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null
                            && field.GetCustomAttribute<SerializeReference>() == null) continue;
                        fields.Add(field);
                    }
                }
                fields.Sort((a, b) => string.CompareOrdinal(
                    (a.DeclaringType?.FullName ?? string.Empty) + "." + a.Name,
                    (b.DeclaringType?.FullName ?? string.Empty) + "." + b.Name));
                return fields.ToArray();
            }

            private static bool IsScalar(Type type)
                => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);

            private static string FormatScalar(object value)
            {
                if (value == null) return "null";
                if (value is bool boolean) return boolean ? "true" : "false";
                if (value is float single) return single.ToString("R", CultureInfo.InvariantCulture);
                if (value is double dbl) return dbl.ToString("R", CultureInfo.InvariantCulture);
                if (value is decimal dec) return dec.ToString(CultureInfo.InvariantCulture);
                if (value is Enum enumeration) return Convert.ToInt64(enumeration, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
                return value.ToString() ?? string.Empty;
            }

            private static string Escape(string value)
            {
                if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
                return value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("=", "\\=");
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    public sealed class MatchConfigCapture
    {
        public int matchSeed;
        public int fixedMapSeed;
        public bool usesGeneratedWaves;
        public float timerDurationSec;
        public GeneratedMap generatedMap;
        public GeneratedWavePlan generatedWavePlan;
        public IReadOnlyList<MatchConfigEffectTile> effectTiles;
        public AttackDeck activeDeck;
        public DefenderUnitData[] defenderPool;
        public SkillData[] skillLoadout;
        public IReadOnlyList<DreamstoneData> dreamstones;
        public IReadOnlyList<DreamcatcherCard> dreamcatcherCards;
        public AwakeningConfig awakeningConfig;
        public GimmickData assignedGimmick;
        public ScoreRulesData scoreRules;
        public CostConfig costConfig;
        public float costRegenRateMultiplier = 1f;
        public StackModifierSO[] stackModifiers;
        public float tileSize;
        public bool spawnSpreadEnabled;
        public float spawnSpreadFraction;
        public float spawnSpreadTopScale;
        public int spawnSubLaneCount;
        public bool enableAdjacencySynergy;
        public float bossLeapTotalSeconds;
    }

    public readonly struct MatchConfigEffectTile
    {
        public readonly Vector2Int cell;
        public readonly EffectTileData data;

        public MatchConfigEffectTile(Vector2Int cell, EffectTileData data)
        {
            this.cell = cell;
            this.data = data;
        }
    }
}
