namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 틱의 마지막 단계. 쌓인 사건을 배달한다.
    //
    // 배달을 틱 끝으로 모으는 이유는 `EventBus` 주석에 있다(순회 중 구조 변경 회피).
    // 단계를 따로 두는 이유는 **순서가 눈에 보여야** 하기 때문이다 — 「담당자가 다 돈
    // 뒤에 뷰가 본다」가 파이프라인 목록에 한 줄로 적혀 있어야 다음 사람이 그 계약을
    // 코드를 읽어 «추론» 하지 않는다.
    public sealed class FlushPhase : ITickPhase
    {
        public string Name => "Flush";

        public void Run(TickContext ctx)
        {
            ctx.Bus.Flush();
            // unit 7 — 트리거 세대(generation) 초기화가 여기 들어온다.
        }
    }
}
