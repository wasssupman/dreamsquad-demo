using System.Globalization;
using System.Text;
using Unity.Mathematics;
using Wassup.BattleCore.Map;   // unit 2 — `MapSnapshot` 은 이제 맵 폴더가 소유한다

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

        // unit 3 — 전투가 쓰는 정의표. 유닛 줄이 **인덱스로** 가리킨다(참조를 복제하지 않는다).
        public ProjectileDef[] Projectiles = System.Array.Empty<ProjectileDef>();
        public PatternDef[] Patterns = System.Array.Empty<PatternDef>();

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
            Put(sb, "tileSize", Map.TileSize, inv);
            for (int i = 0; i < Map.Spawns.Length; i++)
                Put(sb, "spawn" + i.ToString(inv), Cell(Map.Spawns[i], inv));
            for (int i = 0; i < Map.SpawnRoutes.Length; i++)
                Put(sb, "route" + i.ToString(inv), Map.SpawnRoutes[i], inv);
            for (int i = 0; i < Map.Goals.Length; i++)
                Put(sb, "goal" + i.ToString(inv), Cell(Map.Goals[i], inv));
            for (int i = 0; i < Map.WaypointRanges.Length; i++)
                Put(sb, "path" + i.ToString(inv), Cell(Map.WaypointRanges[i], inv));
            for (int i = 0; i < Map.WaypointCells.Length; i++)
                Put(sb, "wp" + i.ToString(inv), Cell(Map.WaypointCells[i], inv));
            for (int i = 0; i < Map.Structures.Length; i++)
            {
                var st = Map.Structures[i];
                Put(sb, "struct" + i.ToString(inv),
                    Cell(st.Cell, inv) + "," + st.Faction.ToString(inv) + "," + st.Footprint.ToString(inv));
            }
            for (int i = 0; i < Map.BonusSpawns.Length; i++)
                Put(sb, "bonus" + i.ToString(inv), Cell(Map.BonusSpawns[i], inv));
            // 칸 격자는 줄마다 적으면 해시 입력이 수천 줄이 된다. 대신 **행 단위 다이제스트**를
            // 남긴다 — 한 칸만 바뀌어도 그 행의 값이 바뀌므로 「맵을 바꿨는데 해시가 그대로」가
            // 생기지 않고, diff 가 「몇 번째 행이 바뀌었나」를 바로 가리킨다.
            for (int y = 0; y < Map.Height; y++)
            {
                uint tiles = 2166136261u, place = 2166136261u;
                for (int x = 0; x < Map.Width; x++)
                {
                    int idx = y * Map.Width + x;
                    tiles = (tiles ^ (idx < Map.Tiles.Length ? (byte)Map.Tiles[idx] : (byte)0)) * 16777619u;
                    place = (place ^ (idx < Map.PlaceMask.Length ? Map.PlaceMask[idx] : (byte)0)) * 16777619u;
                }
                Put(sb, "row" + y.ToString(inv),
                    tiles.ToString("x8", inv) + "," + place.ToString("x8", inv));
            }

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
            for (int i = 0; i < Projectiles.Length; i++)
            {
                sb.Append("[projectile").Append(i.ToString(inv)).Append("]\n");
                Projectiles[i].Canonicalize(sb, inv);
            }
            for (int i = 0; i < Patterns.Length; i++)
            {
                sb.Append("[pattern").Append(i.ToString(inv)).Append("]\n");
                Patterns[i].Canonicalize(sb, inv);
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

        // ── unit 2 ──
        /// <summary>동시에 붙들 수 있는 적 수. 0 = 가디언이 아니다.</summary>
        public int AggroCapacity;

        /// <summary>때릴 수 있는 진영 비트. 0 = 미저작 → 기본값(`TargetDefaults`).</summary>
        public int TargetFactions;

        // ── unit 3 ──
        /// <summary>
        /// 이동 속도. **순찰 소환물만 읽는다** — 배치 유닛은 칸에 고정이라 이동 상태 자체가
        /// 안 붙는다. 저작은 방어유닛 줄이 들고 있고(소환물도 방어유닛 SO 다), 그래서
        /// 순찰병용 정의표 타입을 따로 만들지 않는다.
        /// </summary>
        public float MoveSpeed;

        /// <summary>공격 저작. 통합 루프는 방어유닛·적을 구분하지 않으므로 **같은 타입**이다.</summary>
        public AttackDef Attack;

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
            MatchDefinition.Put(sb, "aggroCapacity", AggroCapacity, inv);
            MatchDefinition.Put(sb, "targetFactions", TargetFactions, inv);
            MatchDefinition.Put(sb, "moveSpeed", MoveSpeed, inv);
            Attack.Canonicalize(sb, inv);
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

        // ── unit 2 ──
        /// <summary>교전 중 이동 정책(`EngageMovement`). 저작 기본은 Halt(0).</summary>
        public int EngageMovement;

        /// <summary>때릴 수 있는 진영 비트. 0 = 미저작 → 기본값(상대 진영 전부).</summary>
        public int TargetFactions;

        /// <summary>저작 경로 번호. -1 = 미지정 → 컨셉/레인 기본으로 내려간다(`WaypointRouting`).</summary>
        public int WaypointPathIndex;

        // ── unit 3 ──
        /// <summary>공격 저작. 방어유닛 줄과 **같은 타입**이다(통합 루프가 둘을 구분하지 않는다).</summary>
        public AttackDef Attack;

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
            MatchDefinition.Put(sb, "engageMovement", EngageMovement, inv);
            MatchDefinition.Put(sb, "targetFactions", TargetFactions, inv);
            MatchDefinition.Put(sb, "waypointPathIndex", WaypointPathIndex, inv);
            Attack.Canonicalize(sb, inv);
        }
    }
}
