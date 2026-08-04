using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class HarnessInputScheduleTests
    {
        [Test]
        public void RunDue_ExecutesOnlyMatchingTickInRegistrationOrder()
        {
            var calls = new List<string>();
            var schedule = new HarnessInputSchedule();
            schedule.Add(2, () => calls.Add("first"));
            schedule.Add(1, () => calls.Add("earlier"));
            schedule.Add(2, () => calls.Add("second"));
            schedule.Add(2, null);

            schedule.RunDue(0);
            schedule.RunDue(1);
            schedule.RunDue(2);

            CollectionAssert.AreEqual(new[] { "earlier", "first", "second" }, calls);
        }
    }
}
