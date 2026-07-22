using NUnit.Framework;
using Wassup.Core.Api;

namespace Wassup.Tests.EditMode.Api
{
    // abandoned-match-reconciliation unit 0 — grace-window boundary decisions.
    public class PendingMatchPolicyTests
    {
        [Test]
        public void Decide_WithinWindow_Complete0()
            => Assert.AreEqual(PendingMatchAction.Complete0, PendingMatchPolicy.Decide(300, 600));

        [Test]
        public void Decide_AtBoundary_Complete0()
            => Assert.AreEqual(PendingMatchAction.Complete0, PendingMatchPolicy.Decide(600, 600));

        [Test]
        public void Decide_OverWindow_DiscardOnly()
            => Assert.AreEqual(PendingMatchAction.DiscardOnly, PendingMatchPolicy.Decide(601, 600));

        [Test]
        public void Decide_NegativeElapsed_Complete0()
            => Assert.AreEqual(PendingMatchAction.Complete0, PendingMatchPolicy.Decide(-5, 600));
    }
}
