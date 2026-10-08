using System.Collections.Generic;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity.Cards;

namespace Somnia.Battle.BattleCoreUnity.Input
{
    // battle-core-rebuild unit 7c — **손패 → 커맨드.** 옛 `DreamcatcherHandController.CommitAttach`·`CommitMarkEnemy`·
    // `CommitActiveTile`·`CommitActivePortal` 의 호출부 몫이다(그 효과·자원 몫은 7b 가 `HandDeck` 으로 옮겼다).
    //
    // 5b `DragPlacementInput` 의 규율 그대로 — **판정 0**:
    //   · 커밋은 커맨드 **하나**(`AttachCard` · `CastActive`/`CastActivePair`)이고 답은 receipt 다.
    //   · 「쓸 수 있나」·「이 몸에 붙나」는 코어 preflight(`HandDeck.UsableReason` · `HandDeck.WouldAttach`)를 **부르기만** 한다 —
    //     커밋과 같은 함수를 부르므로 「밝은 카드인데 거절」·「초록 링인데 무효」가 구조적으로 불가능하다.
    //   · 「무엇을 겨누나」는 코어의 두 칸(`CardDef.Kind` · `TargetsEnemies`)이다 — 메커닉을 뒤져 추측하지 않는다(구현 9).
    //
    // 이 클래스는 MonoBehaviour 가 아니다. 제스처(누름·끌기·뗌)는 손패 슬롯의 것이고, 여기는 그 끝의 **한 줄**만 갖는다 —
    // 테스트가 포인터 장치 없이 같은 커밋 경로를 타는 창구이기도 하다.
    public sealed class CardInput
    {
        private readonly BattleDriver _driver;
        private readonly List<HandDeck.Entry> _scratch = new List<HandDeck.Entry>(8);

        public CardInput(BattleDriver driver) { _driver = driver; }

        public bool Ready => _driver != null && _driver.Running;
        private HandDeck Hand => _driver.Match.Hand;

        /// <summary>그 손패 항목이 어느 카드 줄인가(-1 = 손패에 없다).</summary>
        public int CardIndexOf(int entryId)
        {
            if (!Ready) return -1;
            Hand.Hand(_scratch);
            for (int i = 0; i < _scratch.Count; i++) if (_scratch[i].EntryId == entryId) return _scratch[i].CardIndex;
            return -1;
        }

        /// <summary>코어의 두 칸으로 조준을 가른다.</summary>
        public CoreCardAim AimOf(int cardIndex)
        {
            if (!Ready || cardIndex < 0 || cardIndex >= _driver.Definition.Cards.Length) return CoreCardAim.None;
            ref var c = ref _driver.Definition.Cards[cardIndex];
            if (c.Kind == CardKind.Active) return c.ActiveBinding >= 0 ? CoreCardAim.TileAim : CoreCardAim.None;
            return Hand.TargetsEnemies(cardIndex) ? CoreCardAim.EnemyMark : CoreCardAim.Defender;
        }

        public bool NeedsTwoCells(int cardIndex)
            => Ready && cardIndex >= 0 && cardIndex < _driver.Definition.Cards.Length
               && _driver.Definition.Cards[cardIndex].NeedsTwoCells;

        /// <summary>손패 · 대기 · 각성 — 부착·시전 공통 앞단(코어 preflight 그대로).</summary>
        public RejectReason UsableReason(int entryId)
            => Ready ? Hand.UsableReason(entryId) : RejectReason.NotRunningOrPlacementClosed;

        /// <summary>이 카드가 이 몸에 붙나(상한 포함 · 각성 제외 — 옛 `WouldDreamcatcherCardApply` + `CanAttachMore`).</summary>
        public RejectReason WouldAttach(int cardIndex, SimEntityId host)
            => Ready ? Hand.WouldAttach(cardIndex, host) : RejectReason.NotRunningOrPlacementClosed;

        public Receipt Attach(int entryId, SimEntityId host)
            => Ready ? _driver.Apply(Command.AttachCard(entryId, host)) : Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);

        public Receipt Cast(int entryId, int2 cell)
            => Ready ? _driver.Apply(Command.CastActive(entryId, cell)) : Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);

        public Receipt CastPair(int entryId, int2 entry, int2 exit)
            => Ready ? _driver.Apply(Command.CastActivePair(entryId, entry, exit)) : Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);
    }
}
