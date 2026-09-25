using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Core;
using Wassup.Core.Api;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 8b — **로비에서 들어간 판이 옛 판과 같은 입력으로 지어지나.**
    //
    // 옛 진입 테스트 6(`OutgameFlowSmokeTest`·`SceneTransitionSmokeTest`·`PresetCarryInTest`·`SquadCarryInSmokeTest`·
    // `DreamcatcherDeckCarryInTest`·`DreamstoneCarryInSmokeTest`)의 **재작성**이다(이동이 아니다 — 옛 것은 `GameManager`·옛 씬을
    // 직접 불렀다). 판정은 정의표로 본다 — 「무엇으로 지어졌나」가 이 테스트의 질문이고, 그 답은 정의표에 있다.
    //
    // ⚠ 로비(`OutgameScene`)가 **디스크 프로필을 읽는다**(`IsLoadedThisSession`). 테스트는 메모리 사본만 바꾸고 저장하지 않는다 —
    // 결과·나가기의 저장 seam 은 전부 no-op 로 갈아 끼우고, TearDown 이 바꾼 칸을 되돌린다.
    public sealed class CoreMatchEntryTests
    {
        private PlayerProfileSO _profSO;
        private PlayerProfile _original;
        private SquadPreset _savedSquad;
        private string _savedSelectedSquad;
        private List<DreamcatcherPreset> _savedDecks;
        private string _savedSelectedDeck;
        private int _savedMatchesPlayed;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            TestModeContext.Clear();
            MatchEntryContext.Clear();
            if (_profSO == null) return;
            if (_original != null && _profSO.profile != _original) _profSO.SetLoadedProfile(_original);
            var p = _profSO.profile;
            if (p == null) return;
            // 순서: 테스트 프리셋을 걷고 확정을 되돌린 **뒤** 그 확정 편성의 칸을 되돌린다.
            p.squads?.RemoveAll(s => s != null && s.id == "squad_carryin_test");
            p.selectedSquadId = _savedSelectedSquad;
            p.NormalizePresets();
            if (_savedSquad != null)
            {
                var squad = p.CommittedSquad();
                if (squad != null)
                {
                    squad.unitIds = new List<string>(_savedSquad.unitIds);
                    squad.stoneIds = new List<string>(_savedSquad.stoneIds);
                }
            }
            if (_savedDecks != null) p.dreamcatcherDecks = _savedDecks;
            p.selectedDeckId = _savedSelectedDeck;
            p.matchesPlayed = _savedMatchesPlayed;
            p.NormalizePresets();
            // 로비가 켠 「이번 세션에 읽은 프로필」 표시를 테스트 전 상태로 되돌린다 — 안 되돌리면 같은 PlayMode 실행의 뒤
            // 테스트들(에디터 직접 진입을 가정하는 판)이 로비 판으로 지어진다(표시는 Play 진입 때만 초기화된다).
            typeof(PlayerProfileSO).GetField("_loadedSessionToken", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(_profSO, 0);
        }

        // ── 고정구 ─────────────────────────────────────────────────────────

        private IEnumerator EnterLobby()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;
            var menu = Object.FindAnyObjectByType<OutgameMenuController>();
            Assert.IsNotNull(menu, "로비 메뉴가 있다");
            _profSO = Field<PlayerProfileSO>(menu, "profileSO");
            Assert.IsNotNull(_profSO, "profileSO 배선");
            Assert.IsTrue(_profSO.IsLoadedThisSession, "로비가 이번 세션에 프로필을 읽었다");
            var p = _profSO.profile;
            _original = p;
            p.NormalizePresets();
            var squad = p.CommittedSquad();
            _savedSquad = squad != null
                ? new SquadPreset { unitIds = new List<string>(squad.unitIds), stoneIds = new List<string>(squad.stoneIds) }
                : null;
            _savedSelectedSquad = p.selectedSquadId;
            _savedDecks = p.dreamcatcherDecks != null ? new List<DreamcatcherPreset>(p.dreamcatcherDecks) : null;
            _savedSelectedDeck = p.selectedDeckId;
            _savedMatchesPlayed = p.matchesPlayed;
        }

        private static DefenderCatalog Catalog()
            => Object.FindAnyObjectByType<OutgameMenuController>() is OutgameMenuController m
                ? Field<DefenderCatalog>(m, "catalog") : null;

        private IEnumerator EnterBattle(System.Action<BattleDriver> found)
        {
            LogAssert.ignoreFailingMessages = true;   // 판이 로드될 때의 저작 경고(모드 검증 등)는 이 테스트의 질문이 아니다
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            BattleDriver driver = null;
            for (int i = 0; i < 30 && (driver == null || !driver.Running); i++)
            {
                driver = Object.FindAnyObjectByType<BattleDriver>();
                yield return null;
            }
            Assert.IsNotNull(driver, "새 전투 씬의 드라이버");
            Assert.IsTrue(driver.Running, "판이 지어졌다");
            var outcome = Object.FindAnyObjectByType<CoreMatchOutcomePresenter>();
            if (outcome != null) outcome.ProfileSaver = _ => { };
            found(driver);
        }

        private static List<string> DefenderIds(BattleDriver d)
            => d.DefenderAssets.Where(u => u != null).Select(u => u.id).ToList();

        // ── 옛 OutgameFlowSmokeTest · SceneTransitionSmokeTest ─────────────────

        [UnityTest]
        public IEnumerator 로비_왕복_로비에서_새_전투_씬으로_갔다가_돌아오면_판이_통째로_사라진다()
        {
            Assert.AreEqual("BattleCoreScene", SceneNames.Battle, "로비 교대 — 전투 목적지는 새 씬이다");
            yield return EnterLobby();
            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            Assert.AreEqual(SceneNames.Battle, SceneManager.GetActiveScene().name);
            Assert.IsTrue(driver.Entry.FromLobby, "로비를 거친 판");

            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual(SceneNames.Outgame, SceneManager.GetActiveScene().name);
            Assert.IsNull(Object.FindAnyObjectByType<BattleDriver>(), "판의 수명 = 씬의 수명(G19)");
        }

        [UnityTest]
        public IEnumerator 씬_전환이_새_전투_씬으로_가고_같은_인스턴스로_영속한다()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(SceneTransition.Instance, "SceneTransition 자가 부팅");
            int id = SceneTransition.Instance.GetInstanceID();
            LogAssert.ignoreFailingMessages = true;
            SceneTransition.Go(SceneNames.Battle);
            float timeout = Time.unscaledTime + 10f;
            while (SceneManager.GetActiveScene().name != SceneNames.Battle && Time.unscaledTime < timeout)
                yield return null;
            Assert.AreEqual(SceneNames.Battle, SceneManager.GetActiveScene().name);
            Assert.AreEqual(id, SceneTransition.Instance.GetInstanceID(), "같은 영속 인스턴스");
        }

        // ── 옛 PresetCarryInTest · SquadCarryInSmokeTest ───────────────────────

        [UnityTest]
        public IEnumerator 확정한_프리셋의_유닛만_반입되고_배치_국면에서_시작한다()
        {
            yield return EnterLobby();
            var ids = new List<string>(Catalog().AllIds());
            Assert.GreaterOrEqual(ids.Count, 3);
            var p = _profSO.profile;
            var first = p.CommittedSquad();
            for (int i = 0; i < first.unitIds.Count; i++) first.unitIds[i] = "";
            first.unitIds[0] = ids[0];
            var second = new SquadPreset { id = "squad_carryin_test", name = "반입 테스트" };
            second.NormalizeSlots();
            second.unitIds[0] = ids[1];
            second.unitIds[1] = ids[2];
            p.squads.Add(second);
            p.selectedSquadId = second.id;
            p.NormalizePresets();

            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            Assert.AreEqual(MatchEntryKind.Squad, driver.Entry.Kind);
            CollectionAssert.AreEqual(new[] { ids[1], ids[2] }, DefenderIds(driver), "확정 프리셋의 유닛(G7 — 랜덤 채움 없음)");
            CollectionAssert.DoesNotContain(DefenderIds(driver), ids[0], "확정하지 않은 프리셋은 반입되지 않는다");
            Assert.AreEqual(MatchPhase.Placement, driver.Match.Clock.Phase, "뽑기 없이 배치 국면(계약 9)");
        }

        [UnityTest]
        public IEnumerator 못_찾는_유닛_id_는_그_슬롯만_빠진다()
        {
            yield return EnterLobby();
            var ids = new List<string>(Catalog().AllIds());
            var squad = _profSO.profile.CommittedSquad();
            for (int i = 0; i < squad.unitIds.Count; i++) squad.unitIds[i] = "";
            squad.unitIds[0] = ids[0];
            squad.unitIds[1] = "unit_that_does_not_exist";
            squad.unitIds[2] = ids[1];

            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            CollectionAssert.AreEqual(new[] { ids[0], ids[1] }, DefenderIds(driver));
            var info = TournamentDeckInfo.Deserialize(driver.DeckInfoJson);
            Assert.IsNotNull(info);
            CollectionAssert.Contains(info.squad.units, "unit_that_does_not_exist", "기록은 원시 id(G22·G23)");
        }

        // ── 옛 DreamcatcherDeckCarryInTest ─────────────────────────────────────

        [UnityTest]
        public IEnumerator 로비_판은_확정_덱으로_짓고_개발용_덮어쓰기는_양보한다()
        {
            yield return EnterLobby();
            var p = _profSO.profile;
            p.dreamcatcherDecks.Clear();
            p.dreamcatcherDecks.Add(new DreamcatcherPreset
                { id = "deck_test", name = "T", cardIds = Enumerable.Repeat("ranger_atk", 10).ToList() });
            p.selectedDeckId = "deck_test";

            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            var cards = driver.Definition.Cards;
            int ranger = cards.Count(c => c.Id == "ranger_atk");
            Assert.AreEqual(10, ranger, "확정 덱 10장 — 씬의 개발용 덱(`_cards`)이 아니다");
            Assert.IsTrue(cards.All(c => c.Id == "ranger_atk" || c.Kind == CardKind.Active),
                "부착 카드는 확정 덱뿐이고 나머지는 판마다 굴린 액티브다");
            var info = TournamentDeckInfo.Deserialize(driver.DeckInfoJson);
            Assert.AreEqual(10, info.dc.cards.Count, "스냅샷 카드 = 고른 덱(액티브 제외)");
        }

        [UnityTest]
        public IEnumerator 확정_덱이_없으면_부착_카드는_0장이다_기본_덱_폴백_없음()
        {
            yield return EnterLobby();
            _profSO.profile.dreamcatcherDecks.Clear();
            _profSO.profile.selectedDeckId = null;
            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            Assert.AreEqual(0, driver.Definition.Cards.Count(c => c.Kind != CardKind.Active), "D3 — 폴백 덱 없음");
        }

        // ── 옛 DreamstoneCarryInSmokeTest ──────────────────────────────────────

        [UnityTest]
        public IEnumerator 스탯_돌은_규칙_줄로_들어가고_코스트는_무변이다()
        {
            yield return EnterLobby();
            var ids = new List<string>(Catalog().AllIds());
            var squad = _profSO.profile.CommittedSquad();
            squad.unitIds[0] = ids[0];
            squad.stoneIds[0] = "stone_001";
            squad.stoneIds[1] = "stone_002";
            squad.stoneIds[2] = "stone_003";
            squad.stoneIds[3] = "stone_004";

            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            Assert.AreEqual(4, driver.Definition.MatchBindings.Length, "스탯 돌 4 = 판 호스트 규칙 4줄(G9)");
            Assert.AreEqual(1f, driver.Definition.CostRateMultiplier, 1e-4f);
            var info = TournamentDeckInfo.Deserialize(driver.DeckInfoJson);
            CollectionAssert.AreEqual(new[] { "stone_001", "stone_002", "stone_003", "stone_004" }, info.squad.stones);
        }

        [UnityTest]
        public IEnumerator 코스트_돌은_재생_배율이_된다_한_번만_곱한다()
        {
            yield return EnterLobby();
            var ids = new List<string>(Catalog().AllIds());
            var squad = _profSO.profile.CommittedSquad();
            squad.unitIds[0] = ids[0];
            squad.stoneIds[0] = "stone_049";
            squad.stoneIds[1] = "";
            squad.stoneIds[2] = "";
            squad.stoneIds[3] = "stone_that_does_not_exist";

            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            Assert.AreEqual(1.075f, driver.Definition.CostRateMultiplier, 0.001f, "stone_049 +7.5% → 1.075(G10)");
            Assert.AreEqual(0, driver.Definition.MatchBindings.Length, "코스트 돌은 규칙 줄이 아니다");
            var info = TournamentDeckInfo.Deserialize(driver.DeckInfoJson);
            CollectionAssert.AreEqual(new[] { "stone_049", "stone_that_does_not_exist" }, info.squad.stones,
                "못 찾는 돌도 id 로 남는다(G23)");
        }

        // ── 테스트 모드(G13) ──────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 테스트_모드는_저작_플랜_판이고_한_번만_소비된다()
        {
            yield return EnterLobby();
            var plan = LoadPlan("WavePlan_BossTest");
            Assert.IsNotNull(plan);
            TestModeContext.Set(plan, null);

            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            Assert.AreEqual(MatchEntryKind.TestMode, driver.Entry.Kind);
            Assert.IsFalse(TestModeContext.Active, "1회 소비");
            Assert.AreEqual(WaveSourceKind.AuthoredPlan, driver.Definition.Mode.WaveSource);
            Assert.AreEqual(plan.waves.Count, driver.Definition.WavePlan.Waves.Length, "그 플랜의 웨이브");
            Assert.AreEqual(ClockKind.CountUp, driver.Definition.Mode.Clock, "플랜 시간 0 = 끝없는 판(옛 `timerDurationSec` 0)");

            yield return EnterBattle(d => driver = d);
            Assert.AreNotEqual(MatchEntryKind.TestMode, driver.Entry.Kind, "다음 판은 일반 판");
        }

        // ── 나가기 · 기록(G15) ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 나가기와_결과가_겹쳐도_한_판은_한_번만_센다()
        {
            yield return EnterLobby();
            int before = _profSO.profile.matchesPlayed;
            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            var outcome = Object.FindAnyObjectByType<CoreMatchOutcomePresenter>();
            Assert.IsNotNull(outcome);
            int saves = 0;
            outcome.ProfileSaver = _ => saves++;

            outcome.AbandonAndLeave(loadLobby: false);
            outcome.AbandonAndLeave(loadLobby: false);
            outcome.RecordMatchPlayed();
            Assert.AreEqual(before + 1, _profSO.profile.matchesPlayed, "래치 1");
            Assert.AreEqual(1, saves);
            Assert.IsTrue(outcome.Abandoned);
        }

        [UnityTest]
        public IEnumerator 메뉴_나가기는_로비로_돌아가고_한_판을_센다()
        {
            yield return EnterLobby();
            var prof = _profSO.profile;   // 로비로 돌아가면 디스크에서 새로 읽는다 — 이 판의 사본을 붙든다
            int before = prof.matchesPlayed;
            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            var menu = Object.FindAnyObjectByType<Wassup.BattleCoreUnity.Hud.CoreMenuPopup>();
            Assert.IsNotNull(menu);
            yield return null;
            menu.Open();
            Assert.IsFalse(menu.ExitIsSubmit, "해금 전 = 나가기");
            menu.PressExit();
            float timeout = Time.unscaledTime + 10f;
            while (SceneManager.GetActiveScene().name != SceneNames.Outgame && Time.unscaledTime < timeout)
                yield return null;
            Assert.AreEqual(SceneNames.Outgame, SceneManager.GetActiveScene().name, "로비로 돌아왔다");
            Assert.AreEqual(before + 1, prof.matchesPlayed, "씬 전환 앞에서 기록했다");
        }

        // ── 새 계정(사용자 결정 ④ 2026-09-25 — 첫 판 안내 제거) ────────────────────────

        [UnityTest]
        public IEnumerator 새_계정의_판은_일반_판이다_생성_웨이브_보너스_억제_없음()
        {
            yield return EnterLobby();
            UseFreshAccount();
            BattleDriver driver = null;
            yield return EnterBattle(d => driver = d);
            Assert.AreEqual(MatchEntryKind.Squad, driver.Entry.Kind, "새 계정도 저장 편성 판으로 들어간다");
            Assert.AreNotEqual(WaveSourceKind.AuthoredPlan, driver.Definition.Mode.WaveSource,
                "새 계정 판이 저작 웨이브로 떨어졌다 — 첫 판 전용 플랜이 남아 있다");
        }

        private void UseFreshAccount()
        {
            var menu = Object.FindAnyObjectByType<OutgameMenuController>();
            var fresh = ProfileStore.CreateDefault(Field<DefenderCatalog>(menu, "catalog"),
                Field<DreamcatcherDeck>(menu, "defaultDeck"), Field<DreamcatcherCardCatalog>(menu, "cardCatalog"),
                Field<DreamstoneData[]>(menu, "defaultStones"));
            _profSO.SetLoadedProfile(fresh);
            Assert.AreEqual(0, fresh.matchesPlayed, "새 계정");
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

        private static T Field<T>(object target, string name) where T : class
            => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
