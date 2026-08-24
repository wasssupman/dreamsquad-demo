using System;
using System.Reflection;
using NUnit.Framework;
using Wassup.UI;
using Wassup.UI.Tutorial;

namespace Wassup.Tests.EditMode
{
    public class TutorialGuidanceCopyTests
    {
        [TestCase(typeof(FirstRunTutorialController))]
        [TestCase(typeof(LobbyTutorialStep))]
        public void GuidanceConstants_UseAtMostTwoLines(Type sourceType)
        {
            var fields = sourceType.GetFields(BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (!field.IsLiteral || field.FieldType != typeof(string)) continue;

                string text = (string)field.GetRawConstantValue();
                Assert.LessOrEqual(text.Split('\n').Length, 2,
                    $"{sourceType.Name}.{field.Name} 안내 문구는 최대 두 줄이어야 한다: {text}");
            }
        }

        [Test]
        public void GoalCopy_ExplainsTimeLimitAndKillObjective()
        {
            var field = typeof(FirstRunTutorialController).GetField(
                "GoalText", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field);
            Assert.AreEqual("게임목표: 제한시간 동안\n최대한 많은 악몽 처치!",
                field.GetRawConstantValue());
        }
    }
}
