namespace Wassup.Core.Session
{
    // battle-sim-extraction unit 12 — IMatchSession 계약의 커맨드/receipt 축.
    // 청사진 ①(docs/spec/battle-sim-extraction/m1_blueprint_session_contract.md) §2·§3 이 정본.
    //
    // **엔진 타입 금지**가 이 파일의 계약이다 — UnityEngine·Unity.Mathematics·Unity.Entities 를
    // 참조하지 않는다. 셀은 SimCell, 엔티티는 SimEntityId(int), SO 는 안정 id(string) 로만 오간다.
    // 그래서 이 타입들은 나중에 그대로 와이어에 실릴 수 있고(M3), 지금은 인프로세스로만 쓴다.

    // int2 대용. Unity.Mathematics 를 DTO 에 들이지 않기 위한 최소 좌표 타입.
    public readonly struct SimCell
    {
        public readonly int X;
        public readonly int Y;
        public SimCell(int x, int y) { X = x; Y = y; }
        public override string ToString() => $"({X},{Y})";
    }

    // 플레이어 동사 7종(실측 전수 — 청사진 ① §2). restart 는 세션 재생성, 이탈은 세션 파기이므로
    // 커맨드가 아니다.
    public enum CommandKind
    {
        None = 0,
        DeployDefender,     // cell + unitDefId
        SetDeployFacing,    // defender + facing (활성화 주체는 Deploy 가 예약한 activationTick)
        RelocateDefender,   // from + to
        PlayCard,           // cardHandle + variant 별 대상
        ForceNextWave,      // payload 없음 (비멱등 — clientSeq 총순서 필수)
        FinishPlacement,    // 남은 배치 시간 건너뛰기
        SetPaused,          // pauseOn (유일하게 커맨드 자격이 있는 시간 제어)
    }

    public enum CardVariant
    {
        None = 0,
        Attach,        // host 유닛에 부착
        MarkEnemy,     // 적 표식
        ActiveTile,    // 타일 지정 액티브
        ActivePortal,  // 두 타일(entry/exit)
    }

    // 봉투 + 변종별 페이로드를 한 struct 에 담는다(직렬화 단순성 우선 — 다형성 없음).
    // 쓰이지 않는 필드는 기본값이며, 어느 필드가 유효한지는 Kind/Variant 가 정한다.
    public readonly struct MatchCommand
    {
        public readonly uint ClientSeq;
        public readonly CommandKind Kind;
        public readonly SimCell Cell;        // Deploy 대상 / Relocate to / ActiveTile
        public readonly SimCell Cell2;       // Relocate from / ActivePortal exit
        public readonly string UnitDefId;    // Deploy — DefenderUnitData.id
        public readonly int TargetSimId;     // SetDeployFacing 대상 / Attach host / MarkEnemy 적
        public readonly SimCell Facing;      // SetDeployFacing (cardinal)
        public readonly int CardHandle;      // PlayCard — 손패 엔트리 핸들
        public readonly CardVariant Variant;
        public readonly bool Flag;           // SetPaused 의 on/off

        private MatchCommand(uint seq, CommandKind kind, SimCell cell = default, SimCell cell2 = default,
            string unitDefId = null, int targetSimId = -1, SimCell facing = default,
            int cardHandle = 0, CardVariant variant = CardVariant.None, bool flag = false)
        {
            ClientSeq = seq; Kind = kind; Cell = cell; Cell2 = cell2; UnitDefId = unitDefId;
            TargetSimId = targetSimId; Facing = facing; CardHandle = cardHandle;
            Variant = variant; Flag = flag;
        }

        public static MatchCommand DeployDefender(uint seq, SimCell cell, string unitDefId)
            => new MatchCommand(seq, CommandKind.DeployDefender, cell: cell, unitDefId: unitDefId);

        public static MatchCommand SetDeployFacing(uint seq, int defenderSimId, SimCell facing)
            => new MatchCommand(seq, CommandKind.SetDeployFacing, targetSimId: defenderSimId, facing: facing);

        // from → to. Cell2 = from(출발), Cell = to(도착) — Deploy 와 "대상 셀 = Cell" 규약을 맞춘다.
        public static MatchCommand RelocateDefender(uint seq, SimCell from, SimCell to)
            => new MatchCommand(seq, CommandKind.RelocateDefender, cell: to, cell2: from);

        public static MatchCommand PlayCardAttach(uint seq, int cardHandle, int hostSimId)
            => new MatchCommand(seq, CommandKind.PlayCard, targetSimId: hostSimId,
                cardHandle: cardHandle, variant: CardVariant.Attach);

        public static MatchCommand PlayCardMarkEnemy(uint seq, int cardHandle, int enemySimId)
            => new MatchCommand(seq, CommandKind.PlayCard, targetSimId: enemySimId,
                cardHandle: cardHandle, variant: CardVariant.MarkEnemy);

        public static MatchCommand PlayCardActiveTile(uint seq, int cardHandle, SimCell cell)
            => new MatchCommand(seq, CommandKind.PlayCard, cell: cell,
                cardHandle: cardHandle, variant: CardVariant.ActiveTile);

        public static MatchCommand PlayCardActivePortal(uint seq, int cardHandle, SimCell entry, SimCell exit)
            => new MatchCommand(seq, CommandKind.PlayCard, cell: entry, cell2: exit,
                cardHandle: cardHandle, variant: CardVariant.ActivePortal);

        public static MatchCommand ForceNextWave(uint seq)
            => new MatchCommand(seq, CommandKind.ForceNextWave);

        public static MatchCommand FinishPlacement(uint seq)
            => new MatchCommand(seq, CommandKind.FinishPlacement);

        public static MatchCommand SetPaused(uint seq, bool paused)
            => new MatchCommand(seq, CommandKind.SetPaused, flag: paused);
    }

    // 통합 거절 사유(청사진 ① §3). 기존 3개 enum 의 합집합 + 세션 그룹.
    // 접두로 그룹을 표현한다 — 값 손실 없이 한 축으로 모으는 것이 목적이다.
    public enum CommandReject
    {
        None = 0,

        // 배치 — PlacementRejectReason 계승
        Place_NotRunningOrPlacementClosed,
        Place_MissingMap,
        Place_OutOfBounds,
        Place_NotBuildable,
        Place_Occupied,
        Place_InvalidUnit,
        Place_NotInPickedPool,
        Place_InsufficientCost,
        Place_OnCooldown,          // unit 15-A 신설. (그 전에는 UI 게이트뿐이라 커맨드 우회 시
                                   //  무시됐다 — 이제 배치 규칙 자체가 본다.)

        // 재배치 — RelocationCheck 계승
        Relocate_NoDefenderAtSource,
        Relocate_SourceBusy,
        Relocate_SameCell,

        // 드림캐쳐 — DcRejectReason 계승 + 손패/코스트 게이트
        Card_NotInHand,
        Card_WrongType,
        Card_InsufficientGauge,
        Card_AttachCapReached,
        Card_LeakAllowanceTooLow,
        Card_SkillOnCooldown,
        Card_PortalSameCell,
        Card_NoEventPoint,
        Card_NeedsEnemyTargeting,
        Card_NeedsDamageOutput,
        Card_NeedsHomingRoute,
        Card_NeedsTargetContext,
        Card_DuplicateState,
        Card_NeedsFallbackRange,

        // 웨이브
        Wave_NoWaveLeft,
        Wave_NotRunning,

        // 세션
        Session_SeqGap,
        Session_UnknownVerb,
        Session_PhaseClosed,
        Session_TooLate,
        Session_UnknownEntity,
        // DcRejectReason.Unclassified 는 정상 거절이 아니라 배선 버그 센티넬이므로 여기로 분리한다
        // (클라가 버그를 UI 문구로 번역하지 않게).
        Session_InternalError,
    }

    // 수락 여부 기록(청사진 ① §3). 같은 ClientSeq 재전송은 재실행 없이 같은 receipt 를 돌려준다.
    public readonly struct CommandReceipt
    {
        public readonly uint ClientSeq;
        public readonly bool Accepted;
        public readonly CommandReject Reject;
        public readonly int AcceptedTick;   // 수락 시 실행 tick, 거절이면 -1
        public readonly int OrderInTick;    // 같은 tick 내 실행 순서(0부터)

        // unit 13-C2 — 이 커맨드가 **만들거나 움직인 개체**의 SimEntityId. 없으면 -1.
        //
        // 왜 필요한가: 배치는 후속 사건을 낳는다. 방향 지정 유닛은 배치 직후 `SetDeployFacing`
        // 으로 활성화되어야 하고, 드롭 하마 연출은 그 개체의 뷰를 날린다. 구 코드는
        // `TryBeginDefenderDeployment(out Entity)` 로 **엔진 타입**을 뷰에 넘겨 그것이
        // 가능했지만, 계약은 엔진 타입을 넘기지 않으므로(`SimCell` 이 `int2` 를 대신하는 것과
        // 같은 이유) id 를 돌려주고 뷰가 필요할 때 뷰 서비스로 해석한다.
        //
        // 재배치처럼 **생성이 아닌** 커맨드도 대상 id 를 여기 싣는다 — 이름이 Created 가 아니라
        // Subject 인 이유다.
        public readonly int SubjectSimId;

        public CommandReceipt(uint clientSeq, bool accepted, CommandReject reject,
            int acceptedTick, int orderInTick, int subjectSimId = -1)
        {
            ClientSeq = clientSeq; Accepted = accepted; Reject = reject;
            AcceptedTick = acceptedTick; OrderInTick = orderInTick;
            SubjectSimId = subjectSimId;
        }

        public static CommandReceipt Ok(uint seq, int tick, int order, int subjectSimId = -1)
            => new CommandReceipt(seq, true, CommandReject.None, tick, order, subjectSimId);

        public static CommandReceipt Rejected(uint seq, CommandReject reason)
            => new CommandReceipt(seq, false, reason, -1, 0);
    }
}
