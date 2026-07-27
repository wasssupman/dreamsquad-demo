using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Wassup.Core.Api;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // tournament-history — 히스토리 행의 라벨 밴드 계약. 이름/랭크는 행 상단 절반,
    // 날짜/점수는 하단 절반을 차지하고 서로 겹치지 않아야 한다.
    //
    // 회귀 방지 대상: 세로로 stretch 된 rect 에 anchoredPosition 을 대입하면
    // 깎아둔 밴드가 중앙으로 리셋된다 (offsetMin/offsetMax 는 anchoredPosition 에서
    // 파생되므로). 랭크/점수가 같은 rect 로 붕괴해 텍스트가 겹쳤던 버그.
    public class TournamentHistoryRowLayoutTests
    {
        private GameObject _host;
        private RectTransform _content;

        [SetUp]
        public void SetUp()
        {
            // TournamentHistoryPanel 은 ExecuteInEditMode 가 아니라서 EditMode 에서는
            // OnEnable 이 돌지 않는다 — BuildCanvas/API 호출 없이 CreateRow 만 검사한다.
            _host = new GameObject("HistoryPanelHost", typeof(RectTransform));
            _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(_host.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        private RectTransform BuildRow()
        {
            var panel = _host.AddComponent<TournamentHistoryPanel>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            typeof(TournamentHistoryPanel).GetField("_listContent", flags)
                .SetValue(panel, _content);

            var entry = new TournamentApi.UserTournamentResultEntry
            {
                tournamentEntryId = "entry-1",
                tournamentName = "테스트 컵",
                score = 123456,
                rank = 7,
                createdTime = "2026-07-27T04:05:06Z",
            };
            typeof(TournamentHistoryPanel).GetMethod("CreateRow", flags)
                .Invoke(panel, new object[] { entry });

            Assert.AreEqual(1, _content.childCount, "CreateRow 가 행 하나를 만든다");
            return (RectTransform)_content.GetChild(0);
        }

        private static RectTransform Label(RectTransform row, string name)
        {
            var child = row.Find(name);
            Assert.IsNotNull(child, $"행에 '{name}' 라벨이 있다");
            return (RectTransform)child;
        }

        // 라벨은 행 높이 전체에 stretch 되고 밴드는 오프셋으로만 깎는다 — 이 전제가
        // 성립해야 offsetMin.y / offsetMax.y 를 밴드 경계로 읽을 수 있다.
        private static void AssertSpansRowHeight(RectTransform rt)
        {
            Assert.AreEqual(0f, rt.anchorMin.y, 1e-4f, $"{rt.name} anchorMin.y");
            Assert.AreEqual(1f, rt.anchorMax.y, 1e-4f, $"{rt.name} anchorMax.y");
        }

        // 두 밴드는 각자 행의 0.42 지점까지 걸쳐서 rect 자체는 의도적으로 조금 겹친다
        // (라벨이 숨쉴 여유). 글리프를 떼어놓는 건 Top*/Bottom* 정렬 짝이다. 따라서
        // 계약은 "밴드가 양 모서리 모두 위/아래로 확실히 엇갈릴 것" + "정렬이 짝지어질 것".
        // 버그였을 때는 두 rect 가 완전히 동일해져 양쪽 비교가 모두 무너진다.
        private static void AssertStackedBands(RectTransform top, RectTransform bottom)
        {
            AssertSpansRowHeight(top);
            AssertSpansRowHeight(bottom);

            Assert.Greater(top.offsetMin.y, bottom.offsetMin.y,
                $"{top.name} 밴드 하단이 {bottom.name} 보다 위여야 한다");
            Assert.Greater(top.offsetMax.y, bottom.offsetMax.y,
                $"{top.name} 밴드 상단이 {bottom.name} 보다 위여야 한다");

            AssertVerticalAlignment(top, "Top");
            AssertVerticalAlignment(bottom, "Bottom");
        }

        // 밴드가 겹치는 만큼, 위 라벨은 밴드 위쪽에 아래 라벨은 밴드 아래쪽에 붙어야
        // 글리프가 안 닿는다. Midline 정렬이면 두 글자가 맞물린다.
        private static void AssertVerticalAlignment(RectTransform rt, string expectedPrefix)
        {
            var tmp = rt.GetComponent<TMP_Text>();
            Assert.IsNotNull(tmp, $"{rt.name} 은 TMP 라벨");
            StringAssert.StartsWith(expectedPrefix, tmp.alignment.ToString(),
                $"{rt.name} 은 {expectedPrefix}* 정렬이어야 밴드 겹침 구간에서 글리프가 안 닿는다");
        }

        [Test]
        public void Row_RankSitsAboveScore_WithPairedAlignment()
        {
            var row = BuildRow();
            AssertStackedBands(Label(row, "Rank"), Label(row, "Score"));
        }

        [Test]
        public void Row_NameSitsAboveDate_WithPairedAlignment()
        {
            var row = BuildRow();
            AssertStackedBands(Label(row, "Name"), Label(row, "Date"));
        }

        [Test]
        public void Row_RightColumn_HugsRightEdge_WithoutEnteringNameColumn()
        {
            var row = BuildRow();
            var name = Label(row, "Name");

            foreach (string labelName in new[] { "Rank", "Score" })
            {
                var rt = Label(row, labelName);
                Assert.AreEqual(1f, rt.anchorMin.x, 1e-4f, $"{labelName} 은 우측 앵커");
                Assert.AreEqual(1f, rt.anchorMax.x, 1e-4f, $"{labelName} 은 우측 앵커");
                Assert.Less(rt.offsetMax.x, 0f, $"{labelName} 우변이 행 우측에서 안쪽으로 들어옴");
                Assert.Less(rt.offsetMin.x, rt.offsetMax.x, $"{labelName} 폭이 양수");

                // 좌측 컬럼(Name)의 우변보다 왼쪽으로 침범하지 않는다. 둘 다 행 우측
                // 기준 오프셋이라 그대로 비교 가능하다.
                Assert.LessOrEqual(name.offsetMax.x, rt.offsetMin.x,
                    $"{labelName} 이 Name 컬럼을 침범한다");
            }
        }
    }
}
