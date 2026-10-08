using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 7e ③ — **카드 자가진단은 사용자 판을 건드리지 않는다.**
    //
    // 메뉴(`Somnia/Battle/BattleCore/Debug/카드 자가진단`)는 에디터 어셈블리라 여기서 못 부른다. 그 메뉴가 부르는 것은
    // 코어의 `CardProbe.RunAll(driver.Definition)` 이고, 여기서는 **그 호출** 전후로 살아 있는 판의 틱 · 개체 수 ·
    // 사건 수 · 정의표 해시가 같은지 본다. 프로브가 정의표를 복사하지 않거나 살아 있는 판에 커맨드를 넣으면 빨갛다.
    //
    // 덱은 기본 편성(`DefaultLoadout`)의 메모리 사본으로 건다 — 프로필 저장 덱은 머신 상태를 상속한다(`CoreCardViewTests` 와 같은 이유).
    public sealed class CoreCardSelfCheckTests
    {
        private static readonly string[] CardPaths =
        {
            "Assets/_Project/Data/Dreamcatcher/Card_TremorPlate.asset",
            "Assets/_Project/Data/Dreamcatcher/Card_FattenedOffering.asset",
            "Assets/_Project/Data/Dreamcatcher/Active_Meteor.asset",
        };

        [UnityTest]
        public IEnumerator 카드_자가진단은_살아_있는_판의_틱과_개체_수를_바꾸지_않는다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
#if UNITY_EDITOR
            var cards = new DreamcatcherCard[CardPaths.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(CardPaths[i]);
                Assert.IsNotNull(cards[i], "카드 에셋이 없다: " + CardPaths[i]);
            }
            CoreSceneFixture.OverrideDeck(driver, cards);
#endif
            driver.Begin();
            driver.Apply(Command.FinishPlacement());
            for (int i = 0; i < 10; i++) yield return null;   // 판이 몇 틱 돈 상태에서 잰다

            var match = driver.Match;
            Assert.Greater(driver.Definition.Cards.Length, 0, "덱이 정의표에 안 들어갔다");
            int tick = match.Clock.Tick;
            int units = match.World.Units.Count;
            int events = match.Events.Count;
            string hash = driver.Definition.ComputeConfigHash();

            var results = CardProbe.RunAll(driver.Definition);   // 동기 호출 — 이 사이에 드라이버 틱은 안 돈다

            UnityEngine.Debug.Log("[CoreCardSelfCheckTests]\n" + CardProbe.FormatTable(results));
            Assert.AreSame(match, driver.Match, "판이 바뀌었다");
            Assert.AreEqual(tick, match.Clock.Tick, "살아 있는 판의 틱이 움직였다");
            Assert.AreEqual(units, match.World.Units.Count, "살아 있는 판의 개체 수가 바뀌었다");
            Assert.AreEqual(events, match.Events.Count, "살아 있는 판에 사건이 생겼다");
            Assert.AreEqual(hash, driver.Definition.ComputeConfigHash(), "살아 있는 정의표가 바뀌었다");
            Assert.AreEqual(driver.Definition.Cards.Length, results.Length, "덱의 카드마다 한 줄");
            CoreSceneFixture.EndErrorWatch();
            CollectionAssert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }
    }
}
