using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode.Profile
{
    // deck-info-preset-apply units 3·4 — 페이지 진입 픽업. EditMode 에서는 일반
    // MonoBehaviour 생명주기가 자동으로 돌지 않으므로 Enter 가 실제 OnEnable 메서드를
    // 호출해 예약을 소비시킨다(뷰 refs 는 전부 null-guard 라 안전).
    // 핵심 계약: 적용 = 생성(즉시 디스크) + 작업본 세팅(미저장 → dirty). [저장]이 유일한
    // 기록 경로라는 규율에 예외가 생기면 여기가 잡는다.
    public class PresetApplyPickupTests
    {
        private PlayerProfile _p;
        private PlayerProfileSO _profSO;
        private GameObject _host;
        private int _saves;

        [SetUp]
        public void SetUp()
        {
            PresetApply.Clear();
            _p = new PlayerProfile
            {
                squads = new List<SquadPreset> { new SquadPreset { id = "squad_1", name = "스쿼드 1" } },
                dreamcatcherDecks = new List<DreamcatcherPreset>
                {
                    new DreamcatcherPreset { id = "deck_1", name = "덱 1" },
                },
                selectedSquadId = "squad_1",
                selectedDeckId = "deck_1",
            };
            _p.NormalizePresets();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_profSO != null) Object.DestroyImmediate(_profSO);
            PresetApply.Clear();
        }

        // ---- 헬퍼 ------------------------------------------------------------

        private static DefenderCatalog UnitCatalog(params string[] ids)
        {
            var arr = new DefenderUnitData[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                arr[i] = ScriptableObject.CreateInstance<DefenderUnitData>();
                arr[i].id = ids[i];
            }
            var c = ScriptableObject.CreateInstance<DefenderCatalog>();
            c.units = arr;
            return c;
        }

        private static DreamstoneCatalog StoneCatalog(params string[] ids)
        {
            var arr = new DreamstoneData[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                arr[i] = ScriptableObject.CreateInstance<DreamstoneData>();
                arr[i].id = ids[i];
            }
            var c = ScriptableObject.CreateInstance<DreamstoneCatalog>();
            c.stones = arr;
            return c;
        }

        private static DreamcatcherCardCatalog CardCatalog(params string[] ids)
        {
            var arr = new DreamcatcherCard[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                arr[i] = ScriptableObject.CreateInstance<DreamcatcherCard>();
                arr[i].id = ids[i];
                arr[i].type = CardType.Unit;   // 기본 상한 무제한 — 상한 테스트는 장수로 건다
            }
            var c = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            c.cards = arr;
            return c;
        }

        private static void Set<T>(T target, string field, object value) where T : Component
            => typeof(T).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private static object Get<T>(T target, string field) where T : Component
            => typeof(T).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        private static void Call<T>(T target, string method, params object[] args) where T : Component
            => typeof(T).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, args);

        private static void Enter<T>(T target) where T : Component
        {
            target.gameObject.SetActive(true);
            Call(target, "OnEnable");
        }

        private SquadCharacterPageController MakeSquadController(bool loaded = true)
        {
            _profSO = ScriptableObject.CreateInstance<PlayerProfileSO>();
            if (loaded) _profSO.SetLoadedProfile(_p);
            else _profSO.profile = _p;   // IsLoadedThisSession = false 경로

            _host = new GameObject("PickupHost");
            _host.SetActive(false);
            var ctrl = _host.AddComponent<SquadCharacterPageController>();
            Set(ctrl, "profileSO", _profSO);
            Set(ctrl, "catalog", UnitCatalog("u_a", "u_b", "u_c"));
            Set(ctrl, "stoneCatalog", StoneCatalog("s_a", "s_b"));
            _saves = 0;
            ctrl.ProfileSaver = _ => _saves++;
            return ctrl;
        }

        private DreamcatcherDeckPageController MakeDeckController(params string[] catalogCards)
        {
            _profSO = ScriptableObject.CreateInstance<PlayerProfileSO>();
            _profSO.SetLoadedProfile(_p);

            _host = new GameObject("PickupHost");
            _host.SetActive(false);
            var ctrl = _host.AddComponent<DreamcatcherDeckPageController>();
            Set(ctrl, "profileSO", _profSO);
            Set(ctrl, "catalog", CardCatalog(catalogCards));
            _saves = 0;
            ctrl.ProfileSaver = _ => _saves++;
            return ctrl;
        }

        private static void StageSquad(string owner, string[] units, string[] stones = null)
            => PresetApply.Stage(new PresetApply.Request
            {
                target = PresetApply.Target.Squad,
                presetName = PresetApply.DeckName(owner),
                unitIds = units != null ? new List<string>(units) : null,
                stoneIds = stones != null ? new List<string>(stones) : null,
            });

        private static void StageDeck(string owner, params string[] cards)
            => PresetApply.Stage(new PresetApply.Request
            {
                target = PresetApply.Target.Dreamcatcher,
                presetName = PresetApply.DeckName(owner),
                cardIds = new List<string>(cards),
            });

        // ---- 스쿼드 픽업 (unit 3) ---------------------------------------------

        [Test]
        public void SquadPickup_CreatesPreset_FillsWorkingCopy_StoredStaysEmpty()
        {
            var ctrl = MakeSquadController();
            StageSquad("wassup", new[] { "u_a", "u_b" }, new[] { "s_a" });

            Enter(ctrl);   // OnEnable → TryConsume → ApplyStaged

            Assert.AreEqual(2, _p.squads.Count, "새 프리셋이 생긴다");
            var created = _p.squads[1];
            Assert.AreEqual("wassup의 덱", created.name);
            Assert.AreEqual(created.id, (string)Get(ctrl, "_viewingPresetId"), "새 프리셋을 보고 있다");
            Assert.IsTrue(created.IsEmpty(), "저장본은 비어 있다 — 적용은 저장이 아니다");

            var workingUnits = (List<string>)Get(ctrl, "_workingUnits");
            var workingStones = (List<string>)Get(ctrl, "_workingStones");
            Assert.AreEqual("u_a", workingUnits[0]);
            Assert.AreEqual("u_b", workingUnits[1]);
            Assert.AreEqual("", workingUnits[2]);
            Assert.AreEqual("s_a", workingStones[0]);

            Assert.IsTrue((bool)typeof(SquadCharacterPageController)
                .GetMethod("IsDirty", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ctrl, null), "작업본 ≠ 저장본 → dirty");
            Assert.AreEqual(1, _saves, "생성(구조 변경)만 즉시 디스크 — 내용은 안 쓴다");
            Assert.IsFalse(PresetApply.HasPending);
        }

        [Test]
        public void SquadPickup_SavePersists_RevertEmpties()
        {
            var ctrl = MakeSquadController();
            StageSquad("wassup", new[] { "u_a" });
            Enter(ctrl);
            var created = _p.squads[1];

            Call(ctrl, "OnSavePreset");
            Assert.AreEqual("u_a", created.unitIds[0], "[저장]으로 저장본에 반영된다");

            Call(ctrl, "OnRevertWorking");
            Assert.AreEqual("u_a", ((List<string>)Get(ctrl, "_workingUnits"))[0],
                "저장 후 되돌리기는 저장본(방금 저장한 것) 기준이다");

            // 다시: 저장 전 되돌리기 = 빈 프리셋
            StageSquad("rival", new[] { "u_b" });
            _host.SetActive(false);
            Enter(ctrl);
            Call(ctrl, "OnRevertWorking");
            Assert.AreEqual("", ((List<string>)Get(ctrl, "_workingUnits"))[0],
                "미저장 상태의 되돌리기는 빈 저장본으로 — 완전 비움이 된다");
        }

        [Test]
        public void SquadPickup_FiltersUnusable_KeepsRest()
        {
            var ctrl = MakeSquadController();   // 카탈로그: u_a, u_b, u_c
            StageSquad("wassup", new[] { "u_a", "u_GHOST", "u_b" });

            Enter(ctrl);

            var working = (List<string>)Get(ctrl, "_workingUnits");
            Assert.AreEqual("u_a", working[0]);
            Assert.AreEqual("u_b", working[1], "유령 id 는 슬롯을 차지하지 않는다 — 압축");
            Assert.AreEqual("", working[2]);
        }

        [Test]
        public void SquadPickup_DuplicateName_GetsSuffix()
        {
            _p.squads.Add(new SquadPreset { id = "squad_2", name = "wassup의 덱" });
            _p.NormalizePresets();
            var ctrl = MakeSquadController();
            StageSquad("wassup", new[] { "u_a" });

            Enter(ctrl);

            Assert.AreEqual("wassup의 덱 2", _p.squads[2].name);
        }

        [Test]
        public void SquadPickup_AtMaxPresets_DoesNotCreate_ConsumesReservation()
        {
            while (_p.squads.Count < PlayerProfile.MaxPresets)
                _p.squads.Add(new SquadPreset { id = "squad_" + (_p.squads.Count + 1), name = "s" });
            _p.NormalizePresets();
            var ctrl = MakeSquadController();
            StageSquad("wassup", new[] { "u_a" });

            Enter(ctrl);   // NoticePopup 안내 — EditMode 에서도 안전

            Assert.AreEqual(PlayerProfile.MaxPresets, _p.squads.Count, "상한에서 생성 없음");
            Assert.AreEqual(0, _saves);
            Assert.IsFalse(PresetApply.HasPending, "예약은 소멸한다 — 재진입에 되살아나지 않는다");
        }

        [Test]
        public void SquadPickup_ProfileNotLoaded_BlockedWithError()
        {
            var ctrl = MakeSquadController(loaded: false);
            StageSquad("wassup", new[] { "u_a" });

            // 미로드를 조용한 무동작으로 위장하지 않는다 — 가드 발화를 기대값으로 못박는다.
            LogAssert.Expect(LogType.Error, new Regex("프리셋 적용 차단"));

            Enter(ctrl);

            Assert.AreEqual(1, _p.squads.Count, "프리셋이 생기지 않는다");
            Assert.AreEqual(0, _saves);
        }

        [Test]
        public void SquadPickup_WrongTargetReservation_NoChange_ReservationDies()
        {
            var ctrl = MakeSquadController();
            StageDeck("wassup", "c_a");   // 드림캐쳐 예약을 들고 스쿼드 페이지 진입

            Enter(ctrl);

            Assert.AreEqual(1, _p.squads.Count);
            Assert.AreEqual("squad_1", (string)Get(ctrl, "_viewingPresetId"), "기존 동작 그대로");
            Assert.IsFalse(PresetApply.HasPending, "대상이 달라도 예약은 죽는다");
        }

        [Test]
        public void SquadPage_NoReservation_BehavesAsBefore()
        {
            var ctrl = MakeSquadController();

            Enter(ctrl);

            Assert.AreEqual(1, _p.squads.Count);
            Assert.AreEqual("squad_1", (string)Get(ctrl, "_viewingPresetId"), "확정 프리셋 표시");
            Assert.AreEqual(0, _saves);
        }

        // ---- 드림캐쳐 픽업 (unit 4) --------------------------------------------

        [Test]
        public void DeckPickup_CreatesPreset_FillsWorkingCopy_StoredStaysEmpty()
        {
            var ctrl = MakeDeckController("c_a", "c_b");
            StageDeck("wassup", "c_a", "c_b");

            Enter(ctrl);

            Assert.AreEqual(2, _p.dreamcatcherDecks.Count);
            var created = _p.dreamcatcherDecks[1];
            Assert.AreEqual("wassup의 덱", created.name);
            Assert.AreEqual(created.id, (string)Get(ctrl, "_viewingPresetId"));
            Assert.AreEqual(0, created.Count(), "저장본은 빈 리스트 — 적용은 저장이 아니다");

            CollectionAssert.AreEqual(new[] { "c_a", "c_b" }, (List<string>)Get(ctrl, "_working"));
            Assert.IsTrue((bool)typeof(DreamcatcherDeckPageController)
                .GetMethod("IsDirty", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ctrl, null));
            Assert.AreEqual(1, _saves);
            Assert.IsFalse(PresetApply.HasPending);
        }

        [Test]
        public void DeckPickup_SavePersists_RevertEmpties()
        {
            var ctrl = MakeDeckController("c_a");
            StageDeck("wassup", "c_a");
            Enter(ctrl);

            Call(ctrl, "OnSavePreset");
            CollectionAssert.AreEqual(new[] { "c_a" }, _p.dreamcatcherDecks[1].cardIds);

            StageDeck("rival", "c_a");
            _host.SetActive(false);
            Enter(ctrl);
            Call(ctrl, "OnRevertWorking");
            Assert.AreEqual(0, ((List<string>)Get(ctrl, "_working")).Count,
                "미저장 상태의 되돌리기 = 빈 덱");
        }

        [Test]
        public void DeckPickup_CapsAtDeckSize_AndDropsUnresolved()
        {
            var all = new string[12];
            for (int i = 0; i < 12; i++) all[i] = "c_" + i;
            var ctrl = MakeDeckController(all);

            var staged = new List<string>(all) { "c_GHOST" };
            PresetApply.Stage(new PresetApply.Request
            {
                target = PresetApply.Target.Dreamcatcher,
                presetName = "wassup의 덱",
                cardIds = staged,
            });

            Enter(ctrl);

            Assert.AreEqual(DeckRules.DefaultDeckSize, ((List<string>)Get(ctrl, "_working")).Count,
                "원본 12장이어도 덱 상한만큼만 — 페이지에서 손으로 만들 수 있는 덱과 같다");
        }

        [Test]
        public void DeckPickup_AtMaxPresets_DoesNotCreate()
        {
            while (_p.dreamcatcherDecks.Count < PlayerProfile.MaxPresets)
                _p.dreamcatcherDecks.Add(new DreamcatcherPreset
                {
                    id = "deck_" + (_p.dreamcatcherDecks.Count + 1),
                    name = "d",
                });
            _p.NormalizePresets();
            var ctrl = MakeDeckController("c_a");
            StageDeck("wassup", "c_a");

            Enter(ctrl);

            Assert.AreEqual(PlayerProfile.MaxPresets, _p.dreamcatcherDecks.Count);
            Assert.AreEqual(0, _saves);
            Assert.IsFalse(PresetApply.HasPending);
        }

        [Test]
        public void DeckPage_NoReservation_BehavesAsBefore()
        {
            var ctrl = MakeDeckController("c_a");

            Enter(ctrl);

            Assert.AreEqual(1, _p.dreamcatcherDecks.Count);
            Assert.AreEqual("deck_1", (string)Get(ctrl, "_viewingPresetId"));
            Assert.AreEqual(0, _saves);
        }
    }
}
