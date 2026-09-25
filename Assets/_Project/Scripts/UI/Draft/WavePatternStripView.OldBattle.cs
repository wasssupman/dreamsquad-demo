using System;
using UnityEngine;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // battle-core-rebuild unit 8c — **옛 씬 전용 입력**만 이 부분 파일에 떼어 뒀다. 덱 경로는 옛 웨이브 생성기
    // (`WavePatternGenerator`)를 부르고, 부르는 쪽은 옛 `DraftView`·`SquadPrepView` 뿐이다. 새 씬은 판의 `WavePlan` 을
    // `RebuildFromPlan` 으로 준다(`CoreBriefingPlan`). `ledgers/retire-set.md` 에 올라 있어 unit 9 는 이 파일을
    // **지우기만** 하면 되고, 그러면 생성기의 마지막 소비처가 사라진다. 거동 무변(파일 분할만).
    public partial class WavePatternStripView
    {
        [SerializeField] private AttackDeck deck;

        public void RebuildFromDeck()
        {
            if (deck == null) { RebuildFromPlan(default); return; }
            try { RebuildFromPlan(WavePatternGenerator.Generate(deck)); }
            catch (Exception ex)
            {
                if (!_built) Build();
                ClearCards();
                AddMessageCard("웨이브 미리보기 불가", ex.Message);
            }
        }
    }
}
