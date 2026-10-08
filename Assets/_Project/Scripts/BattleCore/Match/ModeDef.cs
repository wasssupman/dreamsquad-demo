using System.Globalization;
using System.Text;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 4 — **모드가 정하는 판의 틀.**
    //
    // 원칙(별첨 `match-mode-design.md` 6): 모드는 **규칙**이고 담당자는 **상태**다.
    // 담당자 안에 `if (mode == …)` 가 생기면 그것은 계약 12 가 담당자 안으로 샌 것이다.
    // 그래서 모드는 담당자에게 **값으로만** 말한다 — 이 구조체가 그 값의 전부다.
    //
    // ⚠ **모드는 값을 덮어쓰지 않고 «어느 저작 자산을 쓸지» 고른다**(원칙 3). 웨이브 램프·
    // 당김 상한·보스 케이던스는 덱(`WaveDeckDef`)이 소유한 채로 온다. 모드가 소유하는 것은
    // **지금 단일 소유자가 없는 것**뿐이다: 목표 · 종료 정책 · 점수 정책 · 시계 정책 · 수량 축.
    //
    // ⚠ **필드는 append-only** 다(사용자 판정 4). SO(`MatchModeData`)가 이 모양을 직렬화하고,
    // 제출 payload·리더보드가 `ModeId` 를 저장한다.

    /// <summary>목표 종류. **닫힌 집합**이고 append-only 다(제약 5).</summary>
    public enum GoalKind : byte
    {
        /// <summary>정해진 시간 안에 몇 마리 잡았나. 현행 라이브.</summary>
        KillScoreTimed = 0,
        /// <summary>정해진 웨이브 N 을 끝까지 막았나.</summary>
        WaveClear = 1,
        /// <summary>정해진 웨이브를 얼마나 빨리 끝냈나.</summary>
        TimeAttack = 2,
    }

    /// <summary>시계 정책. `Endless` 가 enum 값이 아닌 이유 = 이 축 + `SubmitsReport` 로 표현된다.</summary>
    public enum ClockKind : byte
    {
        /// <summary>제한 시간이 있다. 만료 = 종료 통로 `complete`.</summary>
        FixedLimit = 0,
        /// <summary>세는 시계. 만료가 없다 — 끝내는 것은 목표다.</summary>
        CountUp = 1,
    }

    /// <summary>웨이브의 원천. 저작 플랜이면 케이던스가 **타임라인**이 된다.</summary>
    public enum WaveSourceKind : byte
    {
        GeneratedFromDeck = 0,
        AuthoredPlan = 1,
    }

    /// <summary>
    /// 배치 자원의 저작. **재생 배율은 여기 없다** — 그것은 모드가 아니라 그 판에 들고 들어온
    /// 플레이어의 드림스톤이라 `MatchDefinition.CostRateMultiplier` 가 든다(C7: 「초기화가
    /// 절대 건드리지 않는다」는 계약이 «모드 값이 아니다» 라는 뜻이다).
    /// </summary>
    public struct CostDef
    {
        public float Start;
        public float Max;
        public float RegenPerSec;

        public static CostDef Default() => new CostDef
        {
            Start = 10f,
            Max = 15f,
            RegenPerSec = 1f,
        };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "costStart", Start, inv);
            MatchDefinition.Put(sb, "costMax", Max, inv);
            MatchDefinition.Put(sb, "costRegenPerSec", RegenPerSec, inv);
        }
    }

    /// <summary>
    /// 각성 게이지의 저작. 「시간으로는 차지 않는다」가 이 표에 필드가 없는 방식으로 표현된다
    /// (D7 — 각성은 **처치와 사망의 보상**이다).
    /// </summary>
    public struct AwakeningDef
    {
        public float Start;
        public float Max;

        // ⚠ 처치·사망 보상은 여기 **없다.** 그 값은 「이 적을 잡으면 얼마」 · 「이 유닛이
        // 죽으면 얼마」라 유닛 줄(`EnemyDef.AwakeningReward` · `UnitDef.AwakeningReward`)이
        // 소유한다 — 여기로 올리면 유닛마다 다른 서열이 한 숫자로 뭉개진다.
        // 퇴근이 0 인 것도 필드의 **부재**로 표현된다(D7: 주면 배치→퇴근 반복이 게이지 파밍이 된다).

        public static AwakeningDef Default() => new AwakeningDef
        {
            Start = 20f,
            Max = 100f,
        };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "awakeningStart", Start, inv);
            MatchDefinition.Put(sb, "awakeningMax", Max, inv);
        }
    }

    /// <summary>
    /// 마음의 저작. 덱(`AttackDeck`)이 소유한 두 값이 **담당자 이름으로** 온다 —
    /// `WaveDeckDef` 안에 두면 「웨이브 저작」을 읽으러 온 사람이 거기서 마음 체력을 발견한다.
    /// </summary>
    public struct HeartDef
    {
        /// <summary>마음 체력 = 스트레스의 분모(`StressMath.FromHealth`). 라이브 1500.</summary>
        public float MaxHealth;

        /// <summary>처치 회복 = 그 적의 `AwakeningReward` × 이 값. 0 = 회복 없음.</summary>
        public float KillHealPerAwakening;

        public static HeartDef Default() => new HeartDef
        {
            MaxHealth = 1500f,
            KillHealPerAwakening = 10f,
        };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "heartMaxHealth", MaxHealth, inv);
            MatchDefinition.Put(sb, "heartKillHealPerAwakening", KillHealPerAwakening, inv);
        }
    }

    // 모드가 정하는 판의 틀. SO(`MatchModeData`) → `MatchDefinitionBuilder` → **여기**.
    public struct ModeDef
    {
        // ── 정체성 ── 제출·리플레이·리더보드가 저장한다. **리네임 금지.**
        public string ModeId;

        // ── 목표 ──
        public GoalKind Goal;

        /// <summary>`WaveClear`·`TimeAttack` 의 목표 웨이브 수. `KillScoreTimed` 는 안 읽는다.</summary>
        public int TargetWaves;

        // ── 시계 ──
        public ClockKind Clock;
        public float MatchSeconds;
        public float SubmitUnlockSeconds;

        /// <summary>제출 어휘를 여는 모드인가. false 면 `Submit` 커맨드가 항상 거절된다.</summary>
        public bool AllowSubmit;

        // ── 웨이브 원천 ──
        public WaveSourceKind WaveSource;

        // ── 기믹 ──
        public bool GimmickEnabled;

        // ── 배치(「배치 수량」 축) ──
        /// <summary>
        /// 배치 페이즈에 **입력을 받나**. false 여도 페이즈 자체는 돈다(census 계약 3) —
        /// 페이즈를 건너뛰면 트레이를 만드는 신호가 안 나 전투 내내 트레이가 빈다.
        /// </summary>
        public bool PlacementInputEnabled;

        /// <summary>
        /// 배치 창의 길이(초). **0 = 플레이어가 닫는다**(`FinishPlacement` 커맨드).
        /// 입력이 꺼진 모드는 여기에 자동 시작 카운트다운(라이브 3초)이 온다.
        /// </summary>
        public float PlacementSeconds;

        /// <summary>트레이 슬롯 수(현행 7). 0 이면 로스터 전체.</summary>
        public int SquadSlots;

        /// <summary>판 위 방어유닛 총 상한. 0 = 모드 미지정 → 유닛 저작(`UnitDef.MaxOnBoard`)만 본다.</summary>
        public int BoardCap;

        public bool RetireEnabled;

        public CostDef Cost;

        // ── 드림캐쳐(「드림캐쳐 수량」 축) ──
        /// <summary>저장 덱에서 반입하는 부착 카드 수(현행 10).</summary>
        public int DeckSize;

        /// <summary>판마다 굴리는 공용 액티브 수(현행 2).</summary>
        public int PublicActiveCount;

        /// <summary>손패 창의 크기.</summary>
        public int HandSize;

        /// <summary>유닛 하나에 붙는 카드 수 상한(현행 3).</summary>
        public int AttachCap;

        public AwakeningDef Awakening;

        // ── 토너먼트 ──
        public bool SubmitsReport;

        /// <summary>리더보드 키. 비면 `ModeId`.</summary>
        public string LeaderboardId;

        /// <summary>
        /// 배치 페이즈가 **있는** 모드인가. 창의 길이가 0 이어도 신호는 난다(census 계약 3) —
        /// 그래서 술어는 길이가 아니라 「입력을 받거나, 기다릴 시간이 있거나」다.
        /// 둘 다 아니면 배치 페이즈가 **아예 없는** 모드이고, 판은 전투로 시작한다(고정구용).
        /// </summary>
        public bool HasPlacementPhase => PlacementInputEnabled || PlacementSeconds > 0f;

        public string EffectiveLeaderboardId
            => string.IsNullOrEmpty(LeaderboardId) ? ModeId : LeaderboardId;

        /// <summary>
        /// 코드 기본값 = **고정구의 모드**다. 라이브 모드는 SO(`MatchMode_KillScore3Min`)가 정본이고
        /// 이 값이 아니다 — 여기 배치 페이즈가 없는 것이 그 차이다(고정구 골든이 배치 신호를
        /// 세지 않게 한다).
        /// </summary>
        public static ModeDef Default() => new ModeDef
        {
            ModeId = "kill_score_timed",
            Goal = GoalKind.KillScoreTimed,
            TargetWaves = 0,
            Clock = ClockKind.FixedLimit,
            MatchSeconds = 180f,
            SubmitUnlockSeconds = 60f,
            AllowSubmit = true,
            WaveSource = WaveSourceKind.GeneratedFromDeck,
            GimmickEnabled = false,
            PlacementInputEnabled = false,
            PlacementSeconds = 0f,
            SquadSlots = 7,
            BoardCap = 0,
            RetireEnabled = true,
            Cost = CostDef.Default(),
            DeckSize = 10,
            PublicActiveCount = 2,
            HandSize = 5,
            AttachCap = 3,
            Awakening = AwakeningDef.Default(),
            SubmitsReport = true,
            LeaderboardId = "",
        };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "modeId", ModeId);
            MatchDefinition.Put(sb, "goalKind", (int)Goal, inv);
            MatchDefinition.Put(sb, "targetWaves", TargetWaves, inv);
            MatchDefinition.Put(sb, "clockKind", (int)Clock, inv);
            MatchDefinition.Put(sb, "matchSeconds", MatchSeconds, inv);
            MatchDefinition.Put(sb, "submitUnlockSeconds", SubmitUnlockSeconds, inv);
            MatchDefinition.Put(sb, "allowSubmit", AllowSubmit ? 1 : 0, inv);
            MatchDefinition.Put(sb, "waveSource", (int)WaveSource, inv);
            MatchDefinition.Put(sb, "gimmickEnabled", GimmickEnabled ? 1 : 0, inv);
            MatchDefinition.Put(sb, "placementInput", PlacementInputEnabled ? 1 : 0, inv);
            MatchDefinition.Put(sb, "placementSeconds", PlacementSeconds, inv);
            MatchDefinition.Put(sb, "squadSlots", SquadSlots, inv);
            MatchDefinition.Put(sb, "boardCap", BoardCap, inv);
            MatchDefinition.Put(sb, "retireEnabled", RetireEnabled ? 1 : 0, inv);
            Cost.Canonicalize(sb, inv);
            MatchDefinition.Put(sb, "deckSize", DeckSize, inv);
            MatchDefinition.Put(sb, "publicActiveCount", PublicActiveCount, inv);
            MatchDefinition.Put(sb, "handSize", HandSize, inv);
            MatchDefinition.Put(sb, "attachCap", AttachCap, inv);
            Awakening.Canonicalize(sb, inv);
            MatchDefinition.Put(sb, "submitsReport", SubmitsReport ? 1 : 0, inv);
            MatchDefinition.Put(sb, "leaderboardId", EffectiveLeaderboardId);
        }
    }
}
