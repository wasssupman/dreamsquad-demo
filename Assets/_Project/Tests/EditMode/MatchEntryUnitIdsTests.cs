using System.Collections.Generic;
using NUnit.Framework;
using Somnia.Battle.BattleCoreUnity;

namespace Somnia.Battle.Tests.EditMode
{
    // demo-diet unit 0 — 옛 `SquadDrawTests` 의 후계. 편성 id 정리는 `MatchEntry.ResolveUnitIds` 로 옮겨 왔다:
    // 빈 칸 제거 · 중복 제거 · 순서 유지 · 7칸 상한 · 난수 없음(저장 편성은 판마다 같아야 한다 — 사용자 결정).
    public class MatchEntryUnitIdsTests
    {
        [Test]
        public void 빈_칸은_빠지고_순서는_유지된다()
        {
            var ids = MatchEntry.ResolveUnitIds(new List<string> { "a", "", null, "b", "c" });
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, ids);
        }

        [Test]
        public void 같은_입력은_같은_결과다_난수_없음()
        {
            var squad = new List<string> { "d", "a", "c", "b" };
            CollectionAssert.AreEqual(MatchEntry.ResolveUnitIds(squad), MatchEntry.ResolveUnitIds(squad));
        }

        [Test]
        public void 중복_id_는_첫_것만_남는다()
        {
            var ids = MatchEntry.ResolveUnitIds(new List<string> { "a", "b", "a", "c", "b" });
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, ids);
        }

        [Test]
        public void 칸_상한에서_자른다_중복_제거_뒤에()
        {
            var raw = new List<string> { "x", "x", "1", "2", "3", "4", "5", "6", "7", "8" };
            var ids = MatchEntry.ResolveUnitIds(raw);
            Assert.AreEqual(MatchEntry.FieldCount, ids.Count, "상한은 중복을 뺀 뒤에 센다");
            CollectionAssert.AreEqual(new[] { "x", "1", "2", "3", "4", "5", "6" }, ids);
        }

        [Test]
        public void 상한보다_적으면_채우지_않는다()
        {
            var ids = MatchEntry.ResolveUnitIds(new List<string> { "a", "b" });
            Assert.AreEqual(2, ids.Count, "랜덤 채움 없음(G7)");
        }

        [Test]
        public void null_입력은_빈_목록이다()
        {
            Assert.IsEmpty(MatchEntry.ResolveUnitIds(null));
        }
    }
}
