using Unity.Mathematics;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 플레이어가 판에 거는 것. 계약 7 의 커맨드 쪽이다.
    //
    // **동기 + receipt.** `BattleMatch.Apply` 가 그 자리에서 판정하고 결과를 돌려준다.
    // 비동기로 만들면 입력 층이 「받아들여졌나」를 다음 프레임에 되물어야 하고, 그러면
    // 손패 복귀·코스트 환급 같은 UI 결정이 한 프레임 늦거나 두 번 일어난다.
    public enum CommandKind : byte
    {
        None = 0,
        PlaceDefender = 1,
        Retire = 2,
        Submit = 3,
        // 디버그 커맨드 — 하네스·골든 전용. 판정을 갖지 않는다(시나리오가 곧 의도다).
        DebugSpawnEnemy = 4,
        DebugDestroy = 5,
        /// <summary>unit 2 — 길목을 막았다 풀었다 하는 디버그 손잡이(우회 시나리오의 입력).</summary>
        DebugSetObstacle = 6,
        /// <summary>
        /// unit 3 — 방어유닛을 **판정 없이** 세운다. 코스트·쿨다운·보드 상한·손패는
        /// `PlacementService`(unit 4)의 것이고, 골든이 그것을 기다리면 조각 A 의 검증 질문
        /// (「헤드리스로 3분 판 완주」)에 답할 수 없다.
        /// </summary>
        DebugSpawnDefender = 7,
    }

    // 거절 사유. 옛 `PlacementRejectReason` · `DcRejectReason` 의 값을 **이름으로** 옮겼다
    // (번호는 무관 — 옛 직렬화와 호환할 것이 없다). 뒤쪽은 코어에서 새로 생긴 사유다.
    public enum RejectReason : byte
    {
        None = 0,

        // ── 옛 PlacementRejectReason ──
        NotRunningOrPlacementClosed,
        MissingMap,
        OutOfBounds,
        NotBuildable,
        Occupied,
        InvalidUnit,
        NotInPickedPool,
        InsufficientCost,
        NoDefenderAtSource,
        SourceBusy,
        SameCell,
        LimitReached,

        // ── 옛 DcRejectReason ──
        NoEventPoint,
        NeedsEnemyTargeting,
        NeedsDamageOutput,
        NeedsHomingRoute,
        NeedsTargetContext,
        DuplicateState,
        NeedsFallbackRange,
        Unclassified,

        // ── 코어 신설 ──
        /// <summary>판이 이미 끝났다. 종료 후 커맨드는 전부 여기로 떨어진다(계약 5).</summary>
        MatchEnded,
        /// <summary>제출 해금 시각 전이다(`ModeDef.SubmitUnlockSeconds`).</summary>
        SubmitLocked,
        /// <summary>그 id 의 개체가 없다.</summary>
        NoSuchEntity,
        /// <summary>배선되지 않은 커맨드가 판정에 도달했다 = 통합 버그.</summary>
        UnknownCommand,
    }

    public struct Command
    {
        public CommandKind Kind;

        /// <summary>`MatchDefinition.Units` / `.Enemies` 의 인덱스. -1 = 해당 없음.</summary>
        public int DefIndex;

        public int2 Cell;

        /// <summary>방향 지정 배치의 바라보는 쪽. 미지정이면 zero.</summary>
        public float2 Facing;

        /// <summary>대상 개체(`Retire` · `DebugDestroy`).</summary>
        public SimEntityId Target;

        /// <summary>
        /// unit 2 — 스폰 레인. `DebugSpawnEnemy` 에서 0 이상이면 그 레인의 입구 칸에서 나오고
        /// `Cell` 은 무시된다. -1 이면 `Cell` 을 그대로 쓴다(맵 없는 픽스처).
        /// 레인 순번은 **웨이브 결정론 키**라 스폰 흩뿌림의 레인 배정도 이 값을 쓴다.
        /// </summary>
        public int Lane;

        /// <summary>`DebugSetObstacle` 의 켬/끔.</summary>
        public bool Flag;

        // 카드·스킬 필드(`cardId` · `host` · `skill`)는 unit 7(트리거 레이어)에서 붙는다.

        public static Command PlaceDefender(int defIndex, int2 cell, float2 facing = default) => new Command
        {
            Kind = CommandKind.PlaceDefender,
            DefIndex = defIndex,
            Cell = cell,
            Facing = facing,
            Target = SimEntityId.None,
            Lane = -1,
        };

        public static Command Retire(SimEntityId target) => new Command
        {
            Kind = CommandKind.Retire,
            DefIndex = -1,
            Target = target,
            Lane = -1,
        };

        public static Command Submit() => new Command
        {
            Kind = CommandKind.Submit,
            DefIndex = -1,
            Target = SimEntityId.None,
            Lane = -1,
        };

        /// <summary>칸 지정 스폰(맵 없는 픽스처용). 레인은 쓰지 않는다.</summary>
        public static Command DebugSpawnEnemy(int defIndex, int2 cell) => new Command
        {
            Kind = CommandKind.DebugSpawnEnemy,
            DefIndex = defIndex,
            Cell = cell,
            Target = SimEntityId.None,
            Lane = -1,
        };

        /// <summary>레인 지정 스폰. 입구 칸·기본 경로·측면 분산 레인이 전부 이 번호에서 나온다.</summary>
        public static Command DebugSpawnEnemyInLane(int defIndex, int lane) => new Command
        {
            Kind = CommandKind.DebugSpawnEnemy,
            DefIndex = defIndex,
            Cell = int2.zero,
            Target = SimEntityId.None,
            Lane = lane,
        };

        public static Command DebugDestroy(SimEntityId target) => new Command
        {
            Kind = CommandKind.DebugDestroy,
            DefIndex = -1,
            Target = target,
            Lane = -1,
        };

        /// <summary>판정 없이 방어유닛을 세운다(하네스·골든 전용).</summary>
        public static Command DebugSpawnDefender(int defIndex, int2 cell, float2 facing = default) => new Command
        {
            Kind = CommandKind.DebugSpawnDefender,
            DefIndex = defIndex,
            Cell = cell,
            Facing = facing,
            Target = SimEntityId.None,
            Lane = -1,
        };

        /// <summary>길목을 막았다 푼다. 흐름장은 **막힌 틱에** 다시 구워진다(장애물 시그니처).</summary>
        public static Command DebugSetObstacle(int2 cell, bool on) => new Command
        {
            Kind = CommandKind.DebugSetObstacle,
            DefIndex = -1,
            Cell = cell,
            Target = SimEntityId.None,
            Lane = -1,
            Flag = on,
        };
    }

    public readonly struct Receipt
    {
        public readonly bool Accepted;
        public readonly RejectReason Reason;

        private Receipt(bool accepted, RejectReason reason)
        {
            Accepted = accepted;
            Reason = reason;
        }

        public static readonly Receipt Ok = new Receipt(true, RejectReason.None);
        public static Receipt Reject(RejectReason reason) => new Receipt(false, reason);
    }
}
