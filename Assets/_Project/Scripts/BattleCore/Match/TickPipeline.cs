namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 틱 한 번의 순서. **순서가 계약**이다(UML §4).
    //
    // 옛 전투의 `TickBattleFrame`(한 함수가 드레인 20개를 차례로 부르던 것)을 재현하지
    // 않는다. 여기 있는 것은 **목록뿐**이고, 각 단계는 그 규칙의 담당자가 소유한다.
    // 그래서 「이 규칙은 누구 것인가」의 답이 언제나 한 명이다(계약 12).
    public interface ITickPhase
    {
        /// <summary>진단·트레이스용 이름. 리터럴이라 할당이 없다.</summary>
        string Name { get; }

        void Run(TickContext ctx);
    }

    // 틱 하나가 보는 것. **매 틱 새로 만들지 않는다** — 판 하나당 한 개를 재사용한다.
    public sealed class TickContext
    {
        public BattleWorld World;
        public MatchDefinition Def;
        public EventBus Bus;
        public RngStreams Rng;

        /// <summary>unit 2 — 맵의 런타임 상태(흐름장·벽·장애물·사냥판·점유표). 판당 한 벌.</summary>
        public Wassup.BattleCore.Map.MapRuntime Map;

        /// <summary>항상 `BattleMatch.Dt` = 1/60. 코어는 프레임을 모른다.</summary>
        public float Dt;

        /// <summary>이 틱의 번호(0 부터). `MatchClock` 이 단계 끝에서 올린다.</summary>
        public int Tick;
    }

    public sealed class TickPipeline
    {
        private readonly ITickPhase[] _phases;

        public TickPipeline(ITickPhase[] phases) => _phases = phases;

        public int PhaseCount => _phases.Length;
        public ITickPhase PhaseAt(int index) => _phases[index];

        public void Run(TickContext ctx)
        {
            for (int i = 0; i < _phases.Length; i++) _phases[i].Run(ctx);
        }
    }
}
