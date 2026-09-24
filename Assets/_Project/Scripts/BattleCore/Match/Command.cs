using Unity.Mathematics;
using Wassup.Battle.Units;

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

        // ── unit 4 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 뷰가 「배치 비행이 끝났다」고 알린다. 비행은 **프레젠테이션 시간**이라 코어가
        /// 길이를 모르고, 착지부터 배치 모션 길이를 다시 잰다.
        /// </summary>
        LandDefender = 8,

        /// <summary>
        /// 배치 창을 닫는다. **종료 경로가 하나**인 것이 계약이다 — 자동 시작(카운트다운
        /// 만료)도 같은 함수로 합류한다. 두 번째 경로가 생기면 코스트 재생·국면 전이 중
        /// 하나를 빠뜨린다.
        /// </summary>
        FinishPlacement = 9,

        /// <summary>다음 웨이브를 당긴다. **규칙층** — 상한이 걸린다(전멸로만 회복).</summary>
        PullWave = 10,

        /// <summary>손패의 카드를 유닛에 붙인다. 효과는 unit 7 — 여기서는 자원만 움직인다.</summary>
        AttachCard = 11,

        /// <summary>액티브 카드를 시전한다. 효과는 unit 7.</summary>
        CastActive = 12,

        /// <summary>
        /// 보너스 웨이브를 당긴다. **본류와 코드 경로를 공유하지 않는다** — 별도 큐·타임라인·
        /// 포탈이고, 그래서 커맨드도 별개다(같은 버튼으로 접으면 둘 중 하나가 조용히 죽는다).
        /// </summary>
        PullBonus = 13,

        /// <summary>
        /// 다음 웨이브를 **기제층**으로 민다(상한 무시). 하네스가 판을 굴리는 동력이라
        /// no-op 으로 만들면 통합 스모크가 타임아웃한다.
        /// </summary>
        DebugForceWave = 14,

        // ── unit 6a2 ──────────────────────────────────────────────────────────

        /// <summary>
        /// **공격 루프를 거치지 않고** 탄을 쏜다. 카드탄·배치 스킬탄·포물선탄의 생산자는
        /// unit 7 인데, 그 생산자들이 관문을 똑같이 지나는지를 지금 물을 수 있어야 한다 —
        /// 「모든 탄」이 이 unit 의 전부이고, 평타로만 검증하면 그 말을 증언하지 못한다.
        /// </summary>
        DebugFireProjectile = 15,

        /// <summary>
        /// 시전자의 탄에 얹히는 부여를 걸거나(`Flag = true`) 회수한다. 생산자(카드·스킬)는
        /// unit 7 이므로 그때까지의 유일한 생산자다.
        /// </summary>
        DebugImbue = 16,

        // ── unit 6b ───────────────────────────────────────────────────────────

        /// <summary>
        /// 존 장판을 **판정 없이** 깐다. 까는 자(카드·스킬)는 unit 7 이라 그때까지의 유일한
        /// 생산자이고, 메뉴(6c)도 담당자를 직접 부르지 않고 이 커맨드를 낸다(tools.md 원칙 —
        /// 하네스·리플레이가 같은 길을 탄다).
        /// </summary>
        DebugSpawnHazard = 17,

        /// <summary>
        /// 길막 설치물을 세운다. 탄 착탄(`PayloadKind.SpawnBlocker`)과 **같은 문**
        /// (`BlockerSpawn.TrySpawn`)을 지나므로 자리 검증도 같다.
        /// </summary>
        DebugSpawnBlocker = 18,
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

        // ── unit 4 ────────────────────────────────────────────────────────────
        /// <summary>그 종류의 재배치 대기가 안 끝났다.</summary>
        OnCooldown,
        /// <summary>당김 상한을 다 썼다. **전멸로만 회복된다**(상한 경과는 회복이 아니다).</summary>
        PullCapReached,
        /// <summary>더 밀 웨이브가 없다.</summary>
        NoMoreWaves,
        /// <summary>각성 게이지가 그 카드 값에 모자란다.</summary>
        InsufficientAwakening,
        /// <summary>그 카드가 손패에 없다.</summary>
        CardNotInHand,
        /// <summary>액티브는 부착 경로로 못 가고, 부착 카드는 시전 경로로 못 간다.</summary>
        WrongCardKind,
        /// <summary>그 액티브의 재사용 대기가 안 끝났다.</summary>
        CardOnCooldown,
        /// <summary>그 유닛의 부착 상한이 찼다.</summary>
        AttachCapReached,
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

        /// <summary>unit 4 — `MatchDefinition.Cards` 의 인덱스(`AttachCard` · `CastActive`). -1 = 해당 없음.</summary>
        public int CardIndex;

        // ── unit 6a2 디버그 ──────────────────────────────────────────────────
        // 아래 넷은 **디버그 커맨드 둘만** 읽는다. 다른 종류의 값은 쓰이지 않으므로 기본값
        // 그대로 흘러가고, 그래서 기존 팩토리를 한 줄도 안 고친다.

        /// <summary>`MatchDefinition.Projectiles` 의 인덱스(`DebugFireProjectile`). -1 = 없음.</summary>
        public int ProjectileDefIndex;

        /// <summary>`DebugImbue` 가 거는 부여의 키.</summary>
        public Effects.ImbueKey Key;

        /// <summary>`DebugFireProjectile` = 피해 · `DebugImbue` = 부여 크기.</summary>
        public float Magnitude;

        /// <summary>`DebugImbue` 가 거는 효과의 지속(초).</summary>
        public float Seconds;

        // ── unit 6b 디버그 ───────────────────────────────────────────────────
        /// <summary>`DebugSpawnHazard` = `MatchDefinition.Hazards` 줄 · `DebugSpawnBlocker` = `BlockingHazards` 줄.</summary>
        public int HazardDefIndex;

        /// <summary>`DebugSpawnHazard` — 깐 쪽의 진영(사건 스냅샷). 대상 진영은 저작이 정한다.</summary>
        public Faction HazardFaction;

        // 스킬 파라미터(대상 자리·방향 등)는 unit 7(트리거 레이어)에서 붙는다.

        public static Command PlaceDefender(int defIndex, int2 cell, float2 facing = default) => new Command
        {
            Kind = CommandKind.PlaceDefender,
            DefIndex = defIndex,
            Cell = cell,
            Facing = facing,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        public static Command Retire(SimEntityId target) => new Command
        {
            Kind = CommandKind.Retire,
            DefIndex = -1,
            Target = target,
            Lane = -1,
            CardIndex = -1,
        };

        public static Command Submit() => new Command
        {
            Kind = CommandKind.Submit,
            DefIndex = -1,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        /// <summary>칸 지정 스폰(맵 없는 픽스처용). 레인은 쓰지 않는다.</summary>
        public static Command DebugSpawnEnemy(int defIndex, int2 cell) => new Command
        {
            Kind = CommandKind.DebugSpawnEnemy,
            DefIndex = defIndex,
            Cell = cell,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        /// <summary>레인 지정 스폰. 입구 칸·기본 경로·측면 분산 레인이 전부 이 번호에서 나온다.</summary>
        public static Command DebugSpawnEnemyInLane(int defIndex, int lane) => new Command
        {
            Kind = CommandKind.DebugSpawnEnemy,
            DefIndex = defIndex,
            Cell = int2.zero,
            Target = SimEntityId.None,
            Lane = lane,
            CardIndex = -1,
        };

        public static Command DebugDestroy(SimEntityId target) => new Command
        {
            Kind = CommandKind.DebugDestroy,
            DefIndex = -1,
            Target = target,
            Lane = -1,
            CardIndex = -1,
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
            CardIndex = -1,
        };

        /// <summary>길목을 막았다 푼다. 흐름장은 **막힌 틱에** 다시 구워진다(장애물 시그니처).</summary>
        public static Command DebugSetObstacle(int2 cell, bool on) => new Command
        {
            Kind = CommandKind.DebugSetObstacle,
            DefIndex = -1,
            Cell = cell,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
            Flag = on,
        };

        // ── unit 4 ────────────────────────────────────────────────────────────

        /// <summary>배치 비행이 끝났다(뷰가 알린다). 여기서부터 배치 모션 길이를 잰다.</summary>
        public static Command LandDefender(SimEntityId target) => new Command
        {
            Kind = CommandKind.LandDefender,
            DefIndex = -1,
            Target = target,
            Lane = -1,
            CardIndex = -1,
        };

        /// <summary>배치 창을 닫는다(플레이어 또는 카운트다운 만료).</summary>
        public static Command FinishPlacement() => new Command
        {
            Kind = CommandKind.FinishPlacement,
            DefIndex = -1,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        /// <summary>다음 웨이브를 당긴다(규칙층 — 상한이 걸린다).</summary>
        public static Command PullWave() => new Command
        {
            Kind = CommandKind.PullWave,
            DefIndex = -1,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        /// <summary>카드를 유닛에 붙인다.</summary>
        public static Command AttachCard(int cardIndex, SimEntityId host) => new Command
        {
            Kind = CommandKind.AttachCard,
            DefIndex = -1,
            Target = host,
            Lane = -1,
            CardIndex = cardIndex,
        };

        /// <summary>액티브 카드를 시전한다. `cell` = 대상 자리(쓰는 카드만).</summary>
        public static Command CastActive(int cardIndex, int2 cell = default) => new Command
        {
            Kind = CommandKind.CastActive,
            DefIndex = -1,
            Cell = cell,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = cardIndex,
        };

        /// <summary>보너스 웨이브를 당긴다(제안이 떠 있을 때만).</summary>
        public static Command PullBonus() => new Command
        {
            Kind = CommandKind.PullBonus,
            DefIndex = -1,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        /// <summary>다음 웨이브를 기제층으로 민다(상한 무시 — 하네스 동력).</summary>
        public static Command DebugForceWave() => new Command
        {
            Kind = CommandKind.DebugForceWave,
            DefIndex = -1,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        // ── unit 6a2 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 공격 루프 밖에서 탄 하나를 쏜다. `caster` 가 **발사자**(관문이 그의 착탄 출력과
        /// 부여를 접는다)이고 조준은 `cell` 이다 — 카드탄·배치 스킬탄·포물선탄이 전부
        /// 칸 바인딩이라 이 한 형태로 셋을 다 세울 수 있다.
        /// </summary>
        // ── unit 6b ───────────────────────────────────────────────────────────

        /// <summary>
        /// 존 장판 하나를 `cell` 에 깐다. `faction` 은 **깐 쪽**이다(사건이 값으로 나른다) —
        /// 누가 맞는지는 저작의 `TargetFactions` 가 정하고 이 값과 무관하다.
        /// </summary>
        public static Command DebugSpawnHazard(int hazardDefIndex, int2 cell,
                                               Faction faction = Faction.DefenderUnit) => new Command
        {
            Kind = CommandKind.DebugSpawnHazard,
            DefIndex = -1,
            HazardDefIndex = hazardDefIndex,
            Cell = cell,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
            HazardFaction = faction,
        };

        /// <summary>길막 설치물 하나를 `cell` 에 세운다.</summary>
        public static Command DebugSpawnBlocker(int blockerDefIndex, int2 cell) => new Command
        {
            Kind = CommandKind.DebugSpawnBlocker,
            DefIndex = -1,
            HazardDefIndex = blockerDefIndex,
            Cell = cell,
            Target = SimEntityId.None,
            Lane = -1,
            CardIndex = -1,
        };

        public static Command DebugFireProjectile(int projectileDefIndex, SimEntityId caster,
                                                  int2 cell, float damage = 0f) => new Command
        {
            Kind = CommandKind.DebugFireProjectile,
            DefIndex = -1,
            ProjectileDefIndex = projectileDefIndex,
            Cell = cell,
            Target = caster,
            Lane = -1,
            CardIndex = -1,
            Magnitude = damage,
        };

        /// <summary>시전자의 탄에 부여를 건다. 출처는 **시전자 자신**이다(디버그의 단순화).</summary>
        public static Command DebugImbue(SimEntityId caster, in Effects.ImbueKey key,
                                         float magnitude, float seconds) => new Command
        {
            Kind = CommandKind.DebugImbue,
            DefIndex = -1,
            ProjectileDefIndex = -1,
            Target = caster,
            Lane = -1,
            CardIndex = -1,
            Key = key,
            Magnitude = magnitude,
            Seconds = seconds,
            Flag = true,
        };

        /// <summary>그 시전자에게 자기가 건 부여를 전부 회수한다.</summary>
        public static Command DebugRevokeImbue(SimEntityId caster) => new Command
        {
            Kind = CommandKind.DebugImbue,
            DefIndex = -1,
            ProjectileDefIndex = -1,
            Target = caster,
            Lane = -1,
            CardIndex = -1,
            Flag = false,
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
