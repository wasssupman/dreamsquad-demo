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

        /// <summary>
        /// 거점 정의표(본능·적 마음). `MapSnapshot.StructureSpot.DefIndex` 가 가리킨다.
        ///
        /// ⚠ **방어 마음(골 타워)은 이 표에 없다.** 그 체력은 개체가 아니라 `HeartMeter` 가
        /// 들고(X29), 공격도 하지 않는다 — 표에 빈 줄을 만들어 두면 언젠가 누가 거기에
        /// 체력을 적고 그 순간 마음의 체력이 두 벌이 된다.
        /// </summary>
        public StructureDef[] Structures = System.Array.Empty<StructureDef>();

        // unit 3 — 전투가 쓰는 정의표. 유닛 줄이 **인덱스로** 가리킨다(참조를 복제하지 않는다).
        public ProjectileDef[] Projectiles = System.Array.Empty<ProjectileDef>();
        public PatternDef[] Patterns = System.Array.Empty<PatternDef>();

        // ── unit 4 (매치 담당자) ──────────────────────────────────────────────

        /// <summary>
        /// 드림캐쳐 카드. **이 배열의 순서가 곧 덱의 구성 순서**다(저장 부착 10 + 공용 액티브 2) —
        /// 별도의 「덱」 배열을 두지 않는 이유는 둘이 갈릴 수 있기 때문이다.
        /// </summary>
        public CardDef[] Cards = System.Array.Empty<CardDef>();

        /// <summary>
        /// 놓을 수 있는 방어유닛(`Units` 의 인덱스). **비면 정의표 전체**다(고정구).
        /// 실제로 필요한 이유: 전투 빌더가 카탈로그 밖 에셋(순찰 소환물)을 `Units` 에
        /// 편입하므로 「표에 있다 = 놓을 수 있다」가 언제나 참인 것이 아니다.
        /// </summary>
        public int[] Roster = System.Array.Empty<int>();

        /// <summary>이번 판의 기믹 후보. 고르는 것은 `GimmickHost`, 부착은 unit 7.</summary>
        public GimmickDef[] Gimmicks = System.Array.Empty<GimmickDef>();

        /// <summary>시드 생성 덱. `Mode.WaveSource` 가 `GeneratedFromDeck` 일 때 읽힌다.</summary>
        public Wave.WaveDeckDef WaveDeck = Wave.WaveDeckDef.Empty();

        /// <summary>저작 플랜. 웨이브가 있으면 **덱보다 이긴다**(플랜 우선순위).</summary>
        public Wave.WavePlanDef WavePlan;

        /// <summary>보너스 당김의 저작. 기본은 없음.</summary>
        public Wave.BonusWaveDef Bonus = Wave.BonusWaveDef.None();

        /// <summary>
        /// 마음의 체력·회복 배율. 체력을 드는 것은 `HeartMeter` 다(거점 개체가 아니다).
        ///
        /// ⚠ **기본이 「마음 없음」(체력 0)이다.** 라이브 값은 덱이 주고(`goalStabilityMax`)
        /// 빌더가 그 한 곳에서 싣는다 — 정의표에 기본값 1500 을 박아 두면 덱을 안 넘긴 경로가
        /// 「마음이 있는 판」이 되고, 그 판에는 아무도 저작하지 않은 마음 타워가 선다.
        /// 0 = 마음 미저작 = 타워를 안 세운다 = 스트레스 0(`StressMath.FromHealth(0, 0)`).
        /// </summary>
        public HeartDef Heart;

        /// <summary>
        /// 코스트 재생 배율(드림스톤 `CostRate`). **모드 값이 아니다** — 그 판에 들고 들어온
        /// 플레이어의 장비라 모드가 아니라 반입이 정한다(C7 이 「초기화가 절대 건드리지 마라」로
        /// 지키려던 것이 이 구분이다). 1 = 버프 없음.
        /// </summary>
        public float CostRateMultiplier = 1f;

        /// <summary>
        /// 판 시작에 뽑는 효과 타일 수. 0 = 이 판에 효과 타일이 없다.
        /// 뽑기는 `PlacementService` 가 하고 **효과의 적용은 unit 6** 이다.
        /// </summary>
        public int EffectTileCount;

        /// <summary>
        /// 적이 어떻게 서고 어떻게 퍼지나. **기본값이 옛 씬 값**이라 고정구가 정의표를 직접
        /// 만들어도 라이브와 같은 판이 된다(struct 가 0 으로 떨어지면 몸 반지름 0 이 된다).
        /// </summary>
        public MovementTuningDef Movement = MovementTuningDef.Default();

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
            Mode.Canonicalize(sb, inv);
            Put(sb, "costRateMultiplier", CostRateMultiplier, inv);
            Put(sb, "effectTileCount", EffectTileCount, inv);
            Movement.Canonicalize(sb, inv);
            Heart.Canonicalize(sb, inv);
            Bonus.Canonicalize(sb, inv);

            sb.Append("[deck]\n");
            WaveDeck.Canonicalize(sb, inv);

            sb.Append("[plan]\n");
            WavePlan.Canonicalize(sb, inv);

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
                    Cell(st.Cell, inv) + "," + st.Faction.ToString(inv) + ","
                    + st.Footprint.ToString(inv) + "," + st.DefIndex.ToString(inv));
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
            for (int i = 0; i < Structures.Length; i++)
            {
                sb.Append("[structure").Append(i.ToString(inv)).Append("]\n");
                Structures[i].Canonicalize(sb, inv);
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
            for (int i = 0; i < Cards.Length; i++)
            {
                sb.Append("[card").Append(i.ToString(inv)).Append("]\n");
                Cards[i].Canonicalize(sb, inv);
            }
            for (int i = 0; i < Gimmicks.Length; i++)
            {
                sb.Append("[gimmick").Append(i.ToString(inv)).Append("]\n");
                Gimmicks[i].Canonicalize(sb, inv);
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

        // ── unit 4 (배치 판정) ────────────────────────────────────────────────

        /// <summary>배치에 드는 코스트.</summary>
        public int Cost;

        /// <summary>
        /// 「방금 놓았다」가 거는 연사 게이트(초). 0 = 없는 것과 같다.
        /// ⚠ `MaxOnBoard` 가 1 이면 이 값은 죽은 값이다 — 배치 즉시 소진이라 끝나도 못 놓는다.
        /// 두 손잡이는 상보적으로 쓴다: 상한 1 = 「고유 유닛」, 상한 100 + 쿨타임 = 「연사 제어」.
        /// </summary>
        public float PlacementCooldown;

        /// <summary>「판에서 자리가 비었다」가 거는 재배치 대기(초). 사망이 이 값 그대로.</summary>
        public float DeathCooldown;

        /// <summary>
        /// 퇴근 대기 = 사망 대기 × 이 비율. **초를 두 개 저작하지 않는 것이 계약**이다 —
        /// 독립 저작하면 언젠가 뒤집히고(퇴근이 사망보다 길어지고) 그 인버전은 화면에 안 보인다.
        /// 읽는 자리에서 `Clamp01` 하는 것이 진짜 방어선이다(시트 임포터는 리플렉션으로 직접 쓴다).
        /// </summary>
        public float RetireCooldownRatio;

        /// <summary>판 위 동시 존재 상한. **매치당 총 횟수가 아니다.** 0 이하는 1 로 접힌다.</summary>
        public int MaxOnBoard;

        /// <summary>
        /// 배치 모션 길이(초) = 배치 페이즈의 길이. **저작 초가 아니라 모션에서 파생된 값**이고
        /// 그 파생은 소유자(SO)가 한다 — 코어는 결과 숫자만 받는다.
        /// </summary>
        public float DeployMotionSeconds;

        /// <summary>
        /// 이 유닛이 **죽었을 때** 주는 각성. 각성은 처치와 사망의 보상이고 퇴근은 0 이다(D7) —
        /// 그래서 이 값은 「죽음의 값」이지 「보유의 값」이 아니다.
        /// </summary>
        public int AwakeningReward;

        public int EffectiveMaxOnBoard => MaxOnBoard <= 0 ? 1 : MaxOnBoard;

        public float EffectiveDeathCooldown => DeathCooldown > 0f ? DeathCooldown : 0f;

        public float EffectiveRetireCooldown
            => EffectiveDeathCooldown * math.clamp(RetireCooldownRatio, 0f, 1f);

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
            MatchDefinition.Put(sb, "cost", Cost, inv);
            MatchDefinition.Put(sb, "placementCooldown", PlacementCooldown, inv);
            MatchDefinition.Put(sb, "deathCooldown", DeathCooldown, inv);
            MatchDefinition.Put(sb, "retireCooldownRatio", RetireCooldownRatio, inv);
            MatchDefinition.Put(sb, "maxOnBoard", MaxOnBoard, inv);
            MatchDefinition.Put(sb, "deployMotionSeconds", DeployMotionSeconds, inv);
            MatchDefinition.Put(sb, "awakeningReward", AwakeningReward, inv);
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

    // 거점 정의표 한 줄(본능·적 마음). 저작 SO 는 `StructureData` 하나이고 **진영은 여기
    // 없다** — 같은 스탯의 방어 본능과 적 본능이 SO 두 벌이 되지 않게 진영을 배치가 정하기
    // 때문이다(`MapSnapshot.StructureSpot.Faction`).
    //
    // 크기·몸 반경도 여기 없다: 진영이 정하므로(`StructureSize`) SO 가 알 수 없다.
    public struct StructureDef
    {
        public string Id;
        public float Health;

        // ── 공격(본능만) ──
        // 마음은 공격하지 않는다 — 저작이 0 이면 공격 상태 자체가 안 붙는다.
        public float AttackRange;
        public float AttackCooldown;
        public float HitDelaySeconds;
        public int AttackTargetCount;

        /// <summary>때릴 수 있는 진영 비트. **0 = 아무도 안 때린다**(적·유닛의 「0 = 기본값」과 다르다).</summary>
        public int TargetFactions;

        /// <summary>공격 저작. 유닛·적과 **같은 타입**이다 — 통합 루프가 셋을 구분하지 않는다.</summary>
        public AttackDef Attack;

        /// <summary>공격 저작이 실제로 있나. 없으면 `AttackState` 를 안 붙인다(= 아무도 안 때린다).</summary>
        public bool HasAttack
            => TargetFactions != 0 && AttackRange > 0f
               && Attack.Outputs != null && Attack.Outputs.Length > 0;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "health", Health, inv);
            MatchDefinition.Put(sb, "attackRange", AttackRange, inv);
            MatchDefinition.Put(sb, "attackCooldown", AttackCooldown, inv);
            MatchDefinition.Put(sb, "hitDelaySeconds", HitDelaySeconds, inv);
            MatchDefinition.Put(sb, "attackTargetCount", AttackTargetCount, inv);
            MatchDefinition.Put(sb, "targetFactions", TargetFactions, inv);
            Attack.Canonicalize(sb, inv);
        }
    }
}
