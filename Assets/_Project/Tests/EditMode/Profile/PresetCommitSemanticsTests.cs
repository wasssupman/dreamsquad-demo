using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.EditMode.Profile
{
    // page-local-presets unit 6 — 저장/확정 분리 의미론의 회귀 테스트.
    //
    // 이게 이 feature 에서 가장 중요한 테스트다: 사용자가 명시적으로 고른 규칙이 "[선택]은
    // **디스크에 저장된** 내용을 확정한다" 이고, 이게 깨지면 플레이어가 편집한 줄 알았던
    // 편성과 다른 편성으로 게임이 시작된다(조용한 오적재).
    //
    // 컨트롤러(MonoBehaviour) 없이 검증할 수 있도록, 컨트롤러가 하는 일을 같은 순서로
    // 재현한다 — 작업본 복제 → 편집 → (저장하거나 말거나) → 확정 → 반입값 확인.
    public class PresetCommitSemanticsTests
    {
        private PlayerProfile _p;

        [SetUp]
        public void SetUp()
        {
            _p = new PlayerProfile();
            _p.squads = new List<SquadPreset>
            {
                Squad("squad_1", "스쿼드 1", "u_a"),
                Squad("squad_2", "스쿼드 2", "u_b"),
            };
            _p.dreamcatcherDecks = new List<DreamcatcherPreset>
            {
                new DreamcatcherPreset { id = "deck_1", name = "덱 1", cardIds = new List<string> { "c_a" } },
            };
            _p.selectedSquadId = "squad_1";
            _p.selectedDeckId = "deck_1";
            _p.NormalizePresets();
        }

        private static SquadPreset Squad(string id, string name, params string[] units)
        {
            var s = new SquadPreset { id = id, name = name, unitIds = new List<string>(units) };
            s.NormalizeSlots();
            return s;
        }

        // 컨트롤러의 LoadWorking 과 같은 복제.
        private static List<string> Working(SquadPreset src, int count)
        {
            var l = new List<string>();
            for (int i = 0; i < count; i++)
                l.Add(src != null && i < src.unitIds.Count ? src.unitIds[i] : "");
            return l;
        }

        // 컨트롤러의 OnSavePreset.
        private static void SaveInto(SquadPreset stored, string name, List<string> units, List<string> stones)
        {
            stored.name = name;
            stored.unitIds = new List<string>(units);
            if (stones != null) stored.stoneIds = new List<string>(stones);
            stored.NormalizeSlots();
        }

        // ---- 핵심: 저장하지 않은 편집은 반입되지 않는다 ----------------------

        [Test]
        public void CommitWithoutSave_CarriesInStoredContent_NotWorkingCopy()
        {
            var stored = _p.CommittedSquad();
            var working = Working(stored, SquadPreset.SlotCount);

            working[0] = "u_EDITED";       // 편집만 하고
            working[1] = "u_EXTRA";
            // [저장]을 누르지 않았다.

            _p.selectedSquadId = "squad_1";   // [선택]

            var carried = _p.CommittedSquad();
            Assert.AreEqual("u_a", carried.unitIds[0],
                "저장하지 않은 편집은 반입되지 않는다 — 반입은 저장본이다");
            Assert.AreEqual("", carried.unitIds[1]);
        }

        [Test]
        public void SaveThenCommit_CarriesInEditedContent()
        {
            var stored = _p.CommittedSquad();
            var working = Working(stored, SquadPreset.SlotCount);
            working[0] = "u_EDITED";

            SaveInto(stored, "스쿼드 1", working, null);   // [저장]
            _p.selectedSquadId = "squad_1";                // [선택]

            Assert.AreEqual("u_EDITED", _p.CommittedSquad().unitIds[0]);
        }

        // ---- 확정과 내용은 서로를 건드리지 않는다 ----------------------------

        [Test]
        public void Commit_DoesNotMutatePresetContent()
        {
            var before = new List<string>(StoredById("squad_2").unitIds);

            _p.selectedSquadId = "squad_2";   // [선택]만

            CollectionAssert.AreEqual(before, StoredById("squad_2").unitIds,
                "확정 포인터 변경이 프리셋 내용을 바꾸면 안 된다");
        }

        [Test]
        public void Save_DoesNotChangeCommittedPointer()
        {
            var target = StoredById("squad_2");           // 확정분이 아닌 프리셋
            var working = Working(target, SquadPreset.SlotCount);
            working[0] = "u_NEW";

            SaveInto(target, "스쿼드 2", working, null);   // [저장]

            Assert.AreEqual("squad_1", _p.selectedSquadId,
                "[저장]은 확정을 옮기지 않는다 — 저장과 확정은 완전 분리다");
            Assert.AreEqual("u_a", _p.CommittedSquad().unitIds[0], "반입 대상은 그대로 squad_1");
        }

        // ---- 전환은 작업본을 버린다 -----------------------------------------

        [Test]
        public void SwitchAwayAndBack_ShowsStoredContent_WorkingCopyIsLost()
        {
            var a = StoredById("squad_1");
            var working = Working(a, SquadPreset.SlotCount);
            working[0] = "u_UNSAVED";                     // 저장 안 함

            // squad_2 로 이동 → 다시 squad_1 (컨트롤러의 LoadWorking 재실행)
            var reloaded = Working(StoredById("squad_1"), SquadPreset.SlotCount);

            Assert.AreEqual("u_a", reloaded[0], "복귀 시 보이는 것은 저장본이다(작업본 유실이 정상)");
        }

        // ---- 삭제 가드 · 상한 (실제 컨트롤러 구동) ---------------------------
        //
        // review HIGH-2 — 이전 버전은 가드 조건을 테스트 안에서 다시 계산해
        // `viewing == _p.selectedSquadId` 같은 항진명제를 단정했다. 그건 컨트롤러의 가드를
        // 지워도 그린이라 회귀를 못 잡는다. 이제 `SquadCharacterPageController` 를 실제로
        // 구동한다(DreamcatcherDeckSaveTests 와 같은 reflection 방식).

        private GameObject _host;
        private SquadCharacterPageController _ctrl;
        private PlayerProfileSO _profSO;
        private int _saves;

        private void MakeController()
        {
            _profSO = ScriptableObject.CreateInstance<PlayerProfileSO>();
            _profSO.SetLoadedProfile(_p);
            _host = new GameObject("PresetCommitSemanticsHost");
            _host.SetActive(false);   // OnEnable 이 돌지 않게 — 상태를 직접 세팅한다
            _ctrl = _host.AddComponent<SquadCharacterPageController>();
            SetField("profileSO", _profSO);
            _saves = 0;
            _ctrl.ProfileSaver = _ => _saves++;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_profSO != null) Object.DestroyImmediate(_profSO);
        }

        [Test]
        public void Delete_CommittedPresetIsProtected()
        {
            MakeController();
            SetField("_viewingPresetId", "squad_1");   // = 확정분
            Assert.AreEqual("squad_1", _p.selectedSquadId, "precondition");

            Invoke("OnDeletePreset");

            Assert.AreEqual(2, _p.squads.Count, "확정 프리셋은 삭제되지 않는다");
            Assert.AreEqual(0, _saves, "차단됐으므로 디스크 쓰기도 없다");
        }

        [Test]
        public void Delete_LastRemainingPresetIsProtected()
        {
            _p.squads.RemoveAll(s => s.id == "squad_2");
            _p.selectedSquadId = "";            // 확정분 가드를 비켜 마지막-1개 가드만 남긴다
            MakeController();
            SetField("_viewingPresetId", "squad_1");
            Assert.AreEqual(1, _p.squads.Count, "precondition");

            Invoke("OnDeletePreset");

            Assert.AreEqual(1, _p.squads.Count, "마지막 1개는 삭제되지 않는다");
            Assert.AreEqual(0, _saves);
        }

        [Test]
        public void Delete_NonCommittedPreset_RemovesItAndReturnsToCommitted()
        {
            MakeController();
            SetField("_viewingPresetId", "squad_2");   // 확정분이 아니다

            Invoke("OnDeletePreset");

            Assert.AreEqual(1, _p.squads.Count, "확정분이 아니면 삭제된다");
            Assert.AreEqual("squad_1", _p.selectedSquadId);
            Assert.IsNotNull(_p.CommittedSquad());
            Assert.AreEqual(1, _saves, "구조 변경이므로 즉시 저장된다");
        }

        [Test]
        public void Create_IsBlockedAtMaxPresets()
        {
            while (_p.squads.Count < PlayerProfile.MaxPresets)
                _p.squads.Add(Squad("squad_" + (_p.squads.Count + 1), "s"));
            _p.NormalizePresets();
            MakeController();

            Invoke("OnCreatePreset");

            Assert.AreEqual(PlayerProfile.MaxPresets, _p.squads.Count, "상한에서 생성은 막힌다");
            Assert.AreEqual(0, _saves);
        }

        [Test]
        public void Create_BelowMax_AddsPresetWithUniqueId_AndPersists()
        {
            MakeController();

            Invoke("OnCreatePreset");

            Assert.AreEqual(3, _p.squads.Count);
            Assert.AreEqual("squad_3", _p.squads[2].id, "접미 max+1 로 발급된다");
            Assert.AreEqual(1, _saves, "구조 변경 = 즉시 저장");
        }

        // ---- 컨트롤러 경유 저장/확정 분리 -----------------------------------

        [Test]
        public void Controller_SaveDoesNotMoveCommittedPointer()
        {
            MakeController();
            SetField("_viewingPresetId", "squad_2");
            Invoke("LoadWorking", "squad_2");
            Working("_workingUnits")[0] = "u_EDITED";

            Invoke("OnSavePreset");

            Assert.AreEqual("u_EDITED", StoredById("squad_2").unitIds[0], "저장본에 반영된다");
            Assert.AreEqual("squad_1", _p.selectedSquadId, "[저장]은 확정을 옮기지 않는다");
        }

        [Test]
        public void Controller_CommitDoesNotWriteWorkingCopy()
        {
            MakeController();
            SetField("_viewingPresetId", "squad_2");
            Invoke("LoadWorking", "squad_2");
            Working("_workingUnits")[0] = "u_UNSAVED";   // 저장하지 않는다

            Invoke("OnCommitPreset");

            Assert.AreEqual("squad_2", _p.selectedSquadId, "확정은 옮겨진다");
            Assert.AreEqual("u_b", StoredById("squad_2").unitIds[0],
                "확정은 내용을 기록하지 않는다 — 저장본이 그대로다");
            Assert.AreEqual("u_b", _p.CommittedSquad().unitIds[0], "따라서 반입도 저장본이다");
        }

        [Test]
        public void Controller_EditsDoNotPersist()
        {
            MakeController();
            SetField("_viewingPresetId", "squad_1");
            Invoke("LoadWorking", "squad_1");

            Invoke("ToggleUnit", "u_new_unit");

            Assert.AreEqual(0, _saves, "내용 편집은 디스크에 닿지 않는다");
            Assert.AreEqual("u_a", StoredById("squad_1").unitIds[0], "저장본 무변경");
        }

        [Test]
        public void Controller_StructuralChangeBlockedBeforeProfileLoaded()
        {
            // review MEDIUM-4 — 로드본이 아니면 **변이 자체가** 일어나지 않아야 한다.
            // (예전에는 변이 후 Save() 에서만 막혀 메모리/디스크가 갈렸다.)
            _profSO = ScriptableObject.CreateInstance<PlayerProfileSO>();
            _profSO.profile = _p;                 // SetLoadedProfile 을 거치지 않음
            _host = new GameObject("PresetCommitSemanticsHost");
            _host.SetActive(false);
            _ctrl = _host.AddComponent<SquadCharacterPageController>();
            SetField("profileSO", _profSO);
            _saves = 0;
            _ctrl.ProfileSaver = _ => _saves++;

            Invoke("OnCreatePreset");

            Assert.AreEqual(2, _p.squads.Count, "프리셋이 메모리에도 추가되지 않는다");
            Assert.AreEqual(0, _saves);
        }

        private System.Collections.Generic.List<string> Working(string field) =>
            (System.Collections.Generic.List<string>)typeof(SquadCharacterPageController)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_ctrl);

        private void SetField(string name, object value) =>
            typeof(SquadCharacterPageController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_ctrl, value);

        private void Invoke(string name, params object[] args) =>
            typeof(SquadCharacterPageController)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_ctrl, args);

        // ---- 드림캐쳐 쪽도 같은 규칙 ----------------------------------------

        [Test]
        public void Deck_CommitWithoutSave_CarriesInStoredCards()
        {
            var stored = _p.CommittedDeck();
            var working = new List<string>(stored.cardIds);
            working.Add("c_EDITED");                      // 저장 안 함

            _p.selectedDeckId = "deck_1";                 // [선택]

            CollectionAssert.AreEqual(new[] { "c_a" }, _p.CommittedDeck().cardIds,
                "덱도 동일 — 반입은 저장본이다");
        }

        [Test]
        public void Deck_SaveInvalidIntermediate_IsAllowed()
        {
            // 유효하지 않은 중간 덱(규칙 미달)도 저장된다 — START 는 LoadoutGate 가 막는다.
            var stored = _p.CommittedDeck();
            stored.cardIds = new List<string> { "c_a", "c_b" };   // 규칙상 부족한 장수

            Assert.AreEqual(2, _p.CommittedDeck().Count());
        }

        private SquadPreset StoredById(string id)
        {
            for (int i = 0; i < _p.squads.Count; i++)
                if (_p.squads[i].id == id) return _p.squads[i];
            return null;
        }
    }
}
