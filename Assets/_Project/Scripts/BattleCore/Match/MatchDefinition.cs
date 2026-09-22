using System.Globalization;
using System.Text;
using Unity.Mathematics;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 한 판의 «조건». 계약 6 의 도착지다.
    //
    // 값의 정본은 판 밖이다: 시트 → SO → `MatchDefinitionBuilder`(Unity 층) → **여기**.
    // 이 타입부터는 plain 값이라 코어가 SO 도 아트도 모른다.
    //
    // `configHash` 의 소유자가 여기인 것이 옛 `MatchConfigSnapshot` 과의 차이다.
    // 옛것은 SO 를 **리플렉션으로 접었다** — 「SO 가 무엇을 갖는지 모른다」는 전제의
    // 산물이고, 그래서 아트 타입 제외 목록을 손으로 유지해야 했다. 정의표는 명시 필드라
    // **직렬화가 곧 해시 입력**이고, 아트는 여기 들어올 수 없다(타입이 없다).
    public sealed class MatchDefinition
    {
        public int Seed;

        /// <summary>SHA-256 hex 16자. `ComputeConfigHash()` 가 채운다.</summary>
        public string ConfigHash = "";

        public ModeDef Mode = ModeDef.Default();

        public UnitDef[] Units = System.Array.Empty<UnitDef>();
        public EnemyDef[] Enemies = System.Array.Empty<EnemyDef>();

        public MapSnapshot Map = MapSnapshot.Empty();

        /// <summary>
        /// 정의표를 canonical 텍스트로 접고 SHA-256 앞 8바이트를 hex 16자로 돌려준다.
        /// `ConfigHash` 에 넣는 것은 호출자(빌더)의 몫이다 — 「굽는 것」과 「박는 것」을
        /// 나눠 두면 테스트가 굽기만 하고 비교할 수 있다.
        ///
        /// 포맷 규칙 셋(옛 `MatchConfigWriter` 계승):
        ///   · 줄 단위 `key=value`, 섹션은 `[name]`
        ///   · `InvariantCulture`, 부동소수는 "R"(왕복 손실 없는 최단 표기)
        ///   · null 은 `~` (빈 문자열과 구분)
        /// ⚠ `Seed` 는 **넣지 않는다.** 시드는 조건이 아니라 «그 조건으로 돌린 한 판»이다.
        /// 재현 축은 `modeId + seed` 둘이고(제약 5) 해시는 앞의 것만 답한다 — 그래야
        /// 「같은 값인데 판이 갈렸다」와 「값이 바뀌었다」를 구분할 수 있다.
        /// </summary>
        public string ComputeConfigHash()
        {
            var sb = new StringBuilder(4096);
            Canonicalize(sb);
            return Sha256Hex16(sb.ToString());
        }

        /// <summary>해시 입력이 된 텍스트 자체. 해시가 갈렸을 때 diff 로 «무엇이» 갈렸는지 본다.</summary>
        public string CanonicalText()
        {
            var sb = new StringBuilder(4096);
            Canonicalize(sb);
            return sb.ToString();
        }

        private void Canonicalize(StringBuilder sb)
        {
            var inv = CultureInfo.InvariantCulture;

            sb.Append("[mode]\n");
            Put(sb, "modeId", Mode.ModeId);
            Put(sb, "matchSeconds", Mode.MatchSeconds, inv);
            Put(sb, "submitUnlockSeconds", Mode.SubmitUnlockSeconds, inv);
            Put(sb, "submitsReport", Mode.SubmitsReport ? 1 : 0, inv);

            sb.Append("[map]\n");
            Put(sb, "width", Map.Width, inv);
            Put(sb, "height", Map.Height, inv);
            for (int i = 0; i < Map.Spawns.Length; i++)
                Put(sb, "spawn" + i.ToString(inv), Cell(Map.Spawns[i], inv));
            for (int i = 0; i < Map.Goals.Length; i++)
                Put(sb, "goal" + i.ToString(inv), Cell(Map.Goals[i], inv));

            for (int i = 0; i < Units.Length; i++)
            {
                sb.Append("[unit").Append(i.ToString(inv)).Append("]\n");
                Units[i].Canonicalize(sb, inv);
            }
            for (int i = 0; i < Enemies.Length; i++)
            {
                sb.Append("[enemy").Append(i.ToString(inv)).Append("]\n");
                Enemies[i].Canonicalize(sb, inv);
            }
        }

        private static string Cell(int2 c, CultureInfo inv)
            => c.x.ToString(inv) + "," + c.y.ToString(inv);

        internal static void Put(StringBuilder sb, string key, string value)
            => sb.Append(key).Append('=').Append(value ?? "~").Append('\n');

        internal static void Put(StringBuilder sb, string key, int value, CultureInfo inv)
            => Put(sb, key, value.ToString(inv));

        internal static void Put(StringBuilder sb, string key, float value, CultureInfo inv)
            => Put(sb, key, value.ToString("R", inv));

        internal static string Sha256Hex16(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var hex = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                    hex.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }
    }

    // 모드가 정하는 판의 틀. SO(`MatchModeData`) → 여기는 unit 4 에서 이어진다 —
    // 이 unit 은 코드 기본값(180초 · 제출 해금 60초)으로 채운다.
    public struct ModeDef
    {
        public string ModeId;
        public float MatchSeconds;
        public float SubmitUnlockSeconds;

        /// <summary>서버에 리포트를 제출하는 모드인가. v1 은 `KillScoreTimed` 만 true(unit 0 항목 8).</summary>
        public bool SubmitsReport;

        public static ModeDef Default() => new ModeDef
        {
            ModeId = "kill_score_timed",
            MatchSeconds = 180f,
            SubmitUnlockSeconds = 60f,
            SubmitsReport = true,
        };
    }

    // 방어 유닛 정의표 한 줄. SO 의 **plain 수치·열거형만** 온다(아트 참조 없음).
    public struct UnitDef
    {
        public string Id;
        public float Health;
        public float AttackRange;
        public float AttackCooldown;
        public float HitDelaySeconds;
        public int AttackTargetCount;
        public float BodyRadiusTiles;
        public int FootprintWidth;
        public int FootprintHeight;
        public int PlacementLayers;
        public int TraversalLayers;
        public int Role;
        public int AttackShape;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "health", Health, inv);
            MatchDefinition.Put(sb, "attackRange", AttackRange, inv);
            MatchDefinition.Put(sb, "attackCooldown", AttackCooldown, inv);
            MatchDefinition.Put(sb, "hitDelaySeconds", HitDelaySeconds, inv);
            MatchDefinition.Put(sb, "attackTargetCount", AttackTargetCount, inv);
            MatchDefinition.Put(sb, "bodyRadiusTiles", BodyRadiusTiles, inv);
            MatchDefinition.Put(sb, "footprintWidth", FootprintWidth, inv);
            MatchDefinition.Put(sb, "footprintHeight", FootprintHeight, inv);
            MatchDefinition.Put(sb, "placementLayers", PlacementLayers, inv);
            MatchDefinition.Put(sb, "traversalLayers", TraversalLayers, inv);
            MatchDefinition.Put(sb, "role", Role, inv);
            MatchDefinition.Put(sb, "attackShape", AttackShape, inv);
        }
    }

    // 적 정의표 한 줄.
    public struct EnemyDef
    {
        public string Id;
        public float Health;
        public float MoveSpeed;
        public float AttackRange;
        public float AttackCooldown;
        public float HitDelaySeconds;
        public int AttackTargetCount;
        public float BodyRadius;
        public int TraversalLayers;
        public int EnemyClass;
        public int Tier;
        public int MinWaveNumber;
        public int MaxPerWave;
        public int StabilityDamage;
        public float DetectionRange;
        public int AwakeningReward;
        public int AttackShape;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "health", Health, inv);
            MatchDefinition.Put(sb, "moveSpeed", MoveSpeed, inv);
            MatchDefinition.Put(sb, "attackRange", AttackRange, inv);
            MatchDefinition.Put(sb, "attackCooldown", AttackCooldown, inv);
            MatchDefinition.Put(sb, "hitDelaySeconds", HitDelaySeconds, inv);
            MatchDefinition.Put(sb, "attackTargetCount", AttackTargetCount, inv);
            MatchDefinition.Put(sb, "bodyRadius", BodyRadius, inv);
            MatchDefinition.Put(sb, "traversalLayers", TraversalLayers, inv);
            MatchDefinition.Put(sb, "enemyClass", EnemyClass, inv);
            MatchDefinition.Put(sb, "tier", Tier, inv);
            MatchDefinition.Put(sb, "minWaveNumber", MinWaveNumber, inv);
            MatchDefinition.Put(sb, "maxPerWave", MaxPerWave, inv);
            MatchDefinition.Put(sb, "stabilityDamage", StabilityDamage, inv);
            MatchDefinition.Put(sb, "detectionRange", DetectionRange, inv);
            MatchDefinition.Put(sb, "awakeningReward", AwakeningReward, inv);
            MatchDefinition.Put(sb, "attackShape", AttackShape, inv);
        }
    }

    // 맵의 plain 스냅샷. 이 unit 은 **stub** 이다 — 크기·스폰·골까지.
    // 타일·배치 마스크·웨이포인트·경로는 unit 2(`MapStageScanner`)에서 채워진다.
    public struct MapSnapshot
    {
        public int Width;
        public int Height;
        public int2[] Spawns;
        public int2[] Goals;

        public static MapSnapshot Empty() => new MapSnapshot
        {
            Width = 0,
            Height = 0,
            Spawns = System.Array.Empty<int2>(),
            Goals = System.Array.Empty<int2>(),
        };
    }
}
