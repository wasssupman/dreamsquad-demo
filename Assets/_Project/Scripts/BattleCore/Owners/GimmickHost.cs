namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — **이번 판의 기믹.**
    //
    // 오늘 이 담당자가 하는 일은 **고르기와 알리기**뿐이다. 바인딩 부착(그 기믹이 판에
    // 무엇을 하는가)은 unit 7 이고, 그래서 여기에 효과가 한 줄도 없다.
    //
    // ⚠ **빈 호스트라도 판 시작에 사건을 낸다.** 리빌 페이즈(배치 앞에서 「이번 판은 이런
    // 판이다」를 보여주는 자리)가 그 사건을 기다리기 때문이고, 「고를 것이 없다」도 답이다
    // (`-1`). 사건을 안 내면 그 화면은 영영 안 넘어간다.
    //
    // 선택은 **판 시드에서 파생**한다 — 같은 시드면 같은 기믹이다(재현 축은 modeId + seed).
    // 모드가 정하는 것은 「기믹을 쓰나」와 「어느 풀인가」이고, 「어느 기믹인가」는 시드가 정한다.
    public sealed class GimmickHost
    {
        private readonly EventBus _bus;
        private readonly MatchDefinition _def;

        private int _index = -1;

        public GimmickHost(EventBus bus, MatchDefinition def)
        {
            _bus = bus;
            _def = def;
        }

        /// <summary>이번 판의 기믹 인덱스. -1 = 없는 판.</summary>
        public int Index => _index;

        public bool HasGimmick => _index >= 0;

        /// <summary>그 기믹의 안정 키. 없으면 빈 문자열.</summary>
        public string Id
            => _index >= 0 && _index < _def.Gimmicks.Length ? _def.Gimmicks[_index].Id : "";

        public void Begin(bool enabled, int matchSeed)
        {
            _index = enabled
                ? GimmickSelection.PickIndex(_def.Gimmicks.Length,
                                             unchecked((uint)Wassup.Core.MatchSeed.DeriveGimmickSeed(matchSeed)))
                : -1;

            // 꺼진 판에서는 사건을 내지 않는다. 「없는 판」과 「기믹 기능이 없는 모드」는
            // 다른 말이고, 뒤쪽은 리빌 페이즈 자체가 없다.
            if (enabled) _bus.Publish(CoreEvent.GimmickAssigned(0, _index));
        }
    }
}
