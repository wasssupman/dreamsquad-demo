namespace Wassup.Core.Session
{
    // battle-sim-extraction unit 12 — 이벤트 축(청사진 ① §4). 엔진 타입 금지 계약은
    // MatchSessionContract.cs 와 동일하다.
    //
    // 이벤트 3분리 중 이 파일이 담는 것은 ② semantic 과 ③ presentation 이다. ① 내부 phase
    // queue(9채널)는 sim 내부 전달 수단이라 **계약 밖**이다 — 여기 나타나면 안 된다.
    //
    // ⚠ unit 12 범위: 타입과 구독 표면만 정의한다. **드레인 배선은 unit 13** 이 한다 —
    // 소비자가 아직 Bridge 직독이라, 지금 어댑터가 큐를 소비하면 두 소비자가 같은 큐를 다툰다.

    public enum SessionEventKind
    {
        None = 0,

        // ── semantic (상태 재구성에 필요한 게임 사실) ──
        // genesis — 현행 18채널엔 없어서 신설된 축(청사진 ① §4 C1). 이것 없이는 스트림만으로
        // 판을 재구성할 수 없다(tick 0 부터 재생하면 개체가 존재하지 않는다).
        EnemySpawned,
        // unit 13-B — "보스가 등장했다"는 게임 사실이다(경보 UI 는 그 파생). 구 코드의
        // `BakeNightmareMechanics` 가 보스 판별의 단일 진실 지점이라 발행도 거기 하나뿐이다.
        BossSpawned,
        DefenderDeployed,
        ProjectileSpawned,
        WaypointUpdate,        // 권위 웨이포인트 — 코스메틱 보간은 클라 몫

        DamageApplied,         // 신설 — DamageNumber 의 semantic 원본
        HealApplied,           // 대상 SimId 신설(현 DTO 는 pos+amount 뿐)
        EnemyKilled,
        EnemyLeaked,           // = 현 GoalReached
        DefenderDied,
        ShieldBroken,
        HazardSpawned,         // = 현 HazardSpawnRequest(요청 → 사실 시점으로)
        HazardDestroyed,
        HazardApplied,         // = 현 HazardRuntime
        AttackResolved,        // = 현 AttackOutputLog
        ProjectileHit,
        CardTriggered,         // = 현 DcTriggerFired
        GimmickThresholdHit,   // = 현 MeteorBarrageRequest

        // ── presentation projection (semantic 의 파생 연출 신호) ──
        VfxDamageNumber,
        VfxShieldGranted,
        VfxUnitAttack,
        VfxKnockup,
        VfxBossLeap,
        VfxUltimateLeap,

        // ── 매치 종료 ──
        MatchEnded,
    }

    public enum MatchOutcome
    {
        None = 0,
        Victory,
        VictoryTimeout,
        Defeat,
        Aborted,        // 비종결 종료(현 trace 의 incomplete/stopped 대응 — 청사진 ① §1)
    }

    // 봉투. EventSeq 는 **매치 전역 단조**이며 백로그 재개점과 틱 내 총순서의 축이다
    // (청사진 ① §4 — receipt 의 OrderInTick 과 별개 축이다).
    public readonly struct SessionEvent
    {
        public readonly int EventSeq;
        public readonly int Tick;          // 발생 tick(신 sim 통일). 구 trace 의 tick-1 시프트는 비교기가 보정
        public readonly SessionEventKind Kind;
        public readonly int SubjectSimId;  // 주체(피해자·스폰된 개체 등). 없으면 -1
        public readonly int SourceSimId;   // 가해자·소유자. 없으면 -1
        public readonly float Amount;      // 피해/회복/점수 등 스칼라
        public readonly SimCell Cell;       // 셀 축 사건(해저드·배치)
        public readonly float WorldX, WorldY, WorldZ; // 연속값 — parity 는 epsilon 축
        public readonly MatchOutcome Outcome;         // MatchEnded 전용

        public SessionEvent(int eventSeq, int tick, SessionEventKind kind,
            int subjectSimId = -1, int sourceSimId = -1, float amount = 0f,
            SimCell cell = default, float worldX = 0f, float worldY = 0f, float worldZ = 0f,
            MatchOutcome outcome = MatchOutcome.None)
        {
            EventSeq = eventSeq; Tick = tick; Kind = kind;
            SubjectSimId = subjectSimId; SourceSimId = sourceSimId; Amount = amount;
            Cell = cell; WorldX = worldX; WorldY = worldY; WorldZ = worldZ; Outcome = outcome;
        }

        // semantic/projection 분류 — 리플레이 정본은 semantic 이고 projection 은 파생이다.
        public bool IsPresentation => Kind >= SessionEventKind.VfxDamageNumber
                                      && Kind <= SessionEventKind.VfxUltimateLeap;
    }
}
