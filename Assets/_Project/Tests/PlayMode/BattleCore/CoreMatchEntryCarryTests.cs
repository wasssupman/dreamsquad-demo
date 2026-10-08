using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.PlayMode.Core
{
    // demo-diet unit 0 — **바깥이 넘긴 입력 값으로 판이 지어지나.** 옛 `CoreMatchEntryTests`(로비 씬을 띄워 프로필을
    // 조작하던 8b 의 재작성)의 후계다. 로비는 이 리포에 없고, 전투의 입구는 `MatchEntryInput` 하나다 — 그래서 로비 대신
    // 입력을 직접 채워 같은 질문(「무엇으로 지어졌나」)을 정의표로 본다.
    public sealed class CoreMatchEntryCarryTests
    {
        private const string DefenderCatalogPath = "Assets/_Project/Runtime/Battle/Data/DefenderCatalog.asset";

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            MatchEntryContext.Clear();
        }

        private static DefenderCatalog Catalog()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<DefenderCatalog>(DefenderCatalogPath);
#else
            return null;
#endif
        }

        private static IEnumerator Boot(MatchEntryInput input, System.Action<BattleDriver> found)
        {
            LogAssert.ignoreFailingMessages = true;   // 판이 로드될 때의 저작 경고(모드 검증 등)는 이 테스트의 질문이 아니다
            // ⚠ **씬 로드 «전»에** 놓는다 — 드라이버가 `Start` 에서 소비한다.
            MatchEntryContext.Set(ModeSelection.None, input);
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            Assert.IsTrue(driver.Running, "판이 지어졌다");
            found(driver);
        }

        private static List<string> DefenderIds(BattleDriver d)
            => d.DefenderAssets.Where(u => u != null).Select(u => u.id).ToList();

        // ── 편성(G5·G7) ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator 입력의_유닛_id_만_반입되고_배치_국면에서_시작한다()
        {
            var ids = new List<string>(Catalog().AllIds());
            Assert.GreaterOrEqual(ids.Count, 3);
            var input = new MatchEntryInput { Kind = MatchEntryKind.Squad, UnitIds = new[] { ids[1], ids[2] } };

            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            Assert.AreEqual(MatchEntryKind.Squad, driver.Entry.Kind);
            Assert.IsTrue(driver.Entry.FromOutside, "바깥에서 편성을 받은 판");
            CollectionAssert.AreEqual(new[] { ids[1], ids[2] }, DefenderIds(driver), "입력의 유닛(G7 — 랜덤 채움 없음)");
            Assert.AreEqual(MatchPhase.Placement, driver.Match.Clock.Phase, "뽑기 없이 배치 국면(계약 9)");
            Assert.IsFalse(MatchEntryContext.HasPending, "입력은 1회 소비다");
        }

        [UnityTest]
        public IEnumerator 못_찾는_유닛_id_는_그_슬롯만_빠지고_기록에는_남는다()
        {
            var ids = new List<string>(Catalog().AllIds());
            var input = new MatchEntryInput
            {
                Kind = MatchEntryKind.Squad,
                UnitIds = new[] { ids[0], "unit_that_does_not_exist", ids[1] },
            };

            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            CollectionAssert.AreEqual(new[] { ids[0], ids[1] }, DefenderIds(driver));
            CollectionAssert.Contains(driver.Entry.UnitIds, "unit_that_does_not_exist", "기록은 원시 id(G22·G23)");
        }

        [UnityTest]
        public IEnumerator 에셋을_직접_넘기면_id_없이도_그_편성으로_짓는다()
        {
            var catalog = Catalog();
            var ids = new List<string>(catalog.AllIds());
            var roster = new[] { catalog.ById(ids[0]), catalog.ById(ids[2]) };
            var input = new MatchEntryInput { Kind = MatchEntryKind.TestMode, Defenders = roster };

            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            CollectionAssert.AreEqual(new[] { ids[0], ids[2] }, DefenderIds(driver));
            CollectionAssert.AreEqual(new[] { ids[0], ids[2] }, driver.Entry.UnitIds, "직접 넘긴 에셋도 id 로 기록된다");
        }

        // ── 덱(D3) ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 입력의_덱으로_짓고_개발용_덮어쓰기는_양보한다()
        {
            var input = new MatchEntryInput
            {
                Kind = MatchEntryKind.Squad,
                DeckCardIds = Enumerable.Repeat("ranger_atk", 10).ToList(),
            };

            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            var cards = driver.Definition.Cards;
            Assert.AreEqual(10, cards.Count(c => c.Id == "ranger_atk"), "입력 덱 10장 — 기본 편성(`DefaultLoadout`)의 덱이 아니다");
            Assert.IsTrue(cards.All(c => c.Id == "ranger_atk" || c.Kind == CardKind.Active),
                "부착 카드는 입력 덱뿐이고 나머지는 판마다 굴린 액티브다");
            CollectionAssert.AreEqual(Enumerable.Repeat("ranger_atk", 10).ToList(), driver.LockedDeckCardIds,
                "확정 덱 id = 고른 덱(액티브 제외)");
        }

        [UnityTest]
        public IEnumerator 덱이_없으면_부착_카드는_0장이다_기본_덱_폴백_없음()
        {
            var input = new MatchEntryInput { Kind = MatchEntryKind.Squad, DeckCardIds = null };
            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            Assert.AreEqual(0, driver.Definition.Cards.Count(c => c.Kind != CardKind.Active), "D3 — 폴백 덱 없음");
            Assert.IsEmpty(driver.LockedDeckCardIds);
        }

        // ── 드림스톤(G9·G10) ────────────────────────────────────

        [UnityTest]
        public IEnumerator 스탯_돌은_규칙_줄로_들어가고_코스트는_무변이다()
        {
            var ids = new List<string>(Catalog().AllIds());
            var input = new MatchEntryInput
            {
                Kind = MatchEntryKind.Squad,
                UnitIds = new[] { ids[0] },
                StoneIds = new[] { "stone_001", "stone_002", "stone_003", "stone_004" },
            };

            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            Assert.AreEqual(4, driver.Definition.MatchBindings.Length, "스탯 돌 4 = 판 호스트 규칙 4줄(G9)");
            Assert.AreEqual(1f, driver.Definition.CostRateMultiplier, 1e-4f);
            CollectionAssert.AreEqual(new[] { "stone_001", "stone_002", "stone_003", "stone_004" }, driver.Entry.StoneIds);
        }

        [UnityTest]
        public IEnumerator 코스트_돌은_재생_배율이_된다_한_번만_곱한다()
        {
            var ids = new List<string>(Catalog().AllIds());
            var input = new MatchEntryInput
            {
                Kind = MatchEntryKind.Squad,
                UnitIds = new[] { ids[0] },
                StoneIds = new[] { "stone_049", "", "", "stone_that_does_not_exist" },
            };

            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            Assert.AreEqual(1.075f, driver.Definition.CostRateMultiplier, 0.001f, "stone_049 +7.5% → 1.075(G10)");
            Assert.AreEqual(0, driver.Definition.MatchBindings.Length, "코스트 돌은 규칙 줄이 아니다");
            CollectionAssert.AreEqual(new[] { "stone_049", "stone_that_does_not_exist" }, driver.Entry.StoneIds,
                "못 찾는 돌도 id 로 남는다(G23)");
        }

        // ── 테스트 모드(G13) ──────────────────────────────────────

        [UnityTest]
        public IEnumerator 플랜_강제는_저작_플랜_판이고_입력은_한_번만_소비된다()
        {
            var plan = LoadPlan("WavePlan_BossTest");
            Assert.IsNotNull(plan);
            var input = new MatchEntryInput { Kind = MatchEntryKind.TestMode, PlanOverride = plan };

            BattleDriver driver = null;
            yield return Boot(input, d => driver = d);
            Assert.AreEqual(MatchEntryKind.TestMode, driver.Entry.Kind);
            Assert.IsFalse(MatchEntryContext.HasPending, "1회 소비");
            Assert.AreEqual(WaveSourceKind.AuthoredPlan, driver.Definition.Mode.WaveSource);
            Assert.AreEqual(plan.waves.Count, driver.Definition.WavePlan.Waves.Length, "그 플랜의 웨이브");
            Assert.AreEqual(ClockKind.CountUp, driver.Definition.Mode.Clock, "플랜 시간 0 = 끝없는 판(옛 `timerDurationSec` 0)");

            // 다음 판은 입력 없음 — 드라이버 저작 그대로.
            LogAssert.ignoreFailingMessages = true;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.AreEqual(MatchEntryKind.EditorDirect, driver.Entry.Kind, "다음 판은 일반 판");
        }

        // ── 나가기 ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 메뉴_나가기는_판당_한_번만_알린다()
        {
            BattleDriver driver = null;
            yield return Boot(null, d => driver = d);
            int abandoned = 0;
            driver.MatchAbandoned += d => abandoned++;
            var menu = Object.FindAnyObjectByType<Somnia.Battle.BattleCoreUnity.Hud.CoreMenuPopup>();
            Assert.IsNotNull(menu);
            yield return null;
            menu.Open();
            Assert.IsFalse(menu.ExitIsSubmit, "해금 전 = 나가기");
            menu.PressExit();
            driver.Abandon();   // 두 번째 호출은 래치에 막힌다
            Assert.AreEqual(1, abandoned, "나가기 사건은 판당 하나다");
            Assert.IsTrue(driver.Abandoned);
        }

        private static WavePlanAsset LoadPlan(string name)
        {
#if UNITY_EDITOR
            foreach (var g in UnityEditor.AssetDatabase.FindAssets(name + " t:WavePlanAsset"))
            {
                var a = UnityEditor.AssetDatabase.LoadAssetAtPath<WavePlanAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (a != null && a.name == name) return a;
            }
#endif
            return null;
        }
    }
}
