using System;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-character-page Unit 4 — the orchestrator. Owns the detail view, roster
    // browser and header strip. Two modes share one browser + one detail panel (no
    // modal): Unit mode browses defenders and edits the 7 unit slots; Stone mode
    // (entered by tapping a header stone slot) browses the 64 dreamstones and edits
    // the active stone slot.
    //
    // page-local-presets unit 3 — **편집 대상이 프로필에서 작업본으로 바뀌었다.** 예전에는
    // 확정 스쿼드를 in-place 로 고치고 매 탭마다 디스크에 썼다. 이제는:
    //   저장본(profile.squads[i]) --복제--> 작업본(_working*) --[저장]--> 저장본 --> 디스크
    // 그래서 [저장] 없이 페이지를 떠나면 편집이 사라지고, [선택](확정)은 **저장본**을
    // 가리킬 뿐 작업본을 기록하지 않는다. "확정 ≠ 화면"이 가능하므로 dirty 배지가 필수다.
    public class SquadCharacterPageController : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private DreamstoneCatalog stoneCatalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private SquadUnitDetailView detailView;
        [SerializeField] private SquadRosterBrowser browser;
        [SerializeField] private SquadHeaderStrip header;
        [SerializeField] private PresetBarView presetBar;
        [SerializeField] private ConfirmPopup confirmPopup;

        // 테스트 주입 훅 — DreamcatcherDeckPageController.ProfileSaver 와 동형.
        [NonSerialized] internal Action<PlayerProfile> ProfileSaver = ProfileStore.Save;

        private const string IdPrefix = "squad_";

        private enum Mode { Unit, Stone }
        private Mode _mode = Mode.Unit;
        private int _activeStoneSlot = -1;
        private string _selectedUnitId;
        private string _selectedStoneId;
        private bool _wired;

        private readonly List<DefenderUnitData> _units = new List<DefenderUnitData>();
        private readonly List<DreamstoneData> _stones = new List<DreamstoneData>();

        // ---- 작업본 ---------------------------------------------------------
        private string _viewingPresetId;
        private string _workingName = "";
        private readonly List<string> _workingUnits = new List<string>();
        private readonly List<string> _workingStones = new List<string>();
        private readonly List<PresetBarView.Entry> _entries = new List<PresetBarView.Entry>();

        private PlayerProfile Profile => profileSO != null ? profileSO.profile : null;

        private SquadPreset StoredPreset(string id)
        {
            var p = Profile;
            if (p == null || p.squads == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < p.squads.Count; i++)
                if (p.squads[i] != null && p.squads[i].id == id) return p.squads[i];
            return null;
        }

        private void OnEnable()
        {
            WireOnce();
            BuildLists();

            var p = Profile;
            if (p != null)
            {
                p.NormalizePresets();
                // 페이지 진입은 **확정 프리셋**을 디폴트로 보여준다.
                _viewingPresetId = p.selectedSquadId;
            }
            LoadWorking(_viewingPresetId);
            EnterUnitMode(initial: true);
            RefreshBarEntries();   // 페이지 진입 시 목록 1회 구성
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;
            if (browser != null) browser.EntrySelected += OnEntrySelected;
            if (detailView != null) detailView.DeployClicked += OnDeployClicked;
            if (header != null)
            {
                header.UnitSlotTapped += OnUnitSlotTapped;
                header.StoneSlotTapped += OnStoneSlotTapped;
            }
            if (presetBar != null)
            {
                presetBar.PresetPicked += OnPresetPicked;
                presetBar.CreateClicked += OnCreatePreset;
                presetBar.CommitClicked += OnCommitPreset;
                presetBar.SaveClicked += OnSavePreset;
                presetBar.ResetClicked += OnResetWorking;
                presetBar.DeleteClicked += OnDeletePreset;
                presetBar.NameCommitted += OnNameCommitted;
            }
        }

        private void BuildLists()
        {
            _units.Clear();
            if (catalog != null)
                foreach (var id in catalog.AllIds()) { var u = catalog.ById(id); if (u != null) _units.Add(u); }
            _stones.Clear();
            if (stoneCatalog != null)
                foreach (var id in stoneCatalog.AllIds()) { var s = stoneCatalog.ById(id); if (s != null) _stones.Add(s); }
        }

        // ---- 작업본 로드/저장 ----------------------------------------------

        private void LoadWorking(string presetId)
        {
            _viewingPresetId = presetId;
            var stored = StoredPreset(presetId);

            _workingName = stored != null ? (stored.name ?? "") : "";
            CopySlots(stored != null ? stored.unitIds : null, _workingUnits, SquadPreset.SlotCount);
            CopySlots(stored != null ? stored.stoneIds : null, _workingStones, SquadPreset.StoneSlotCount);
        }

        // 저장본을 작업본으로 **복제**한다(참조 공유 금지 — 공유하면 편집이 곧 저장이 된다).
        private static void CopySlots(List<string> src, List<string> dst, int count)
        {
            dst.Clear();
            for (int i = 0; i < count; i++)
                dst.Add(src != null && i < src.Count && src[i] != null ? src[i] : "");
        }

        private bool IsDirty() =>
            PresetDiff.IsSquadDirty(_workingName, _workingUnits, _workingStones, StoredPreset(_viewingPresetId));

        // review MEDIUM-4 — 구조 변경(생성·삭제·확정)은 **변이 전에** 물어야 한다. Save() 안에서만
        // 걸면 메모리에는 프리셋이 생겼는데 디스크에는 없는 상태로 조용히 갈린다.
        private bool CanPersist() =>
            Profile != null && profileSO != null && profileSO.IsLoadedThisSession;

        private void Save()
        {
            if (!CanPersist()) return;
            (ProfileSaver ?? ProfileStore.Save)(Profile);
        }

        // ---- 프리셋 바 --------------------------------------------------------

        // 가벼운 갱신 — 이름/dirty/버튼 활성만. **내용 편집 경로가 쓰는 것.**
        private void RefreshBarState()
        {
            if (presetBar == null) return;
            var p = Profile;

            int count = p != null && p.squads != null ? p.squads.Count : 0;
            bool isCommitted = p != null && _viewingPresetId == p.selectedSquadId;
            bool dirty = IsDirty();

            presetBar.SetName(_workingName);
            presetBar.SetButtonEnabled(
                commit: !isCommitted,
                save: dirty,
                reset: !AllEmpty(_workingUnits) || !AllEmpty(_workingStones),
                delete: !isCommitted && count > 1);
            // SetDirty 는 SetButtonEnabled 뒤 — [저장] 엑센트 색이 interactable 을 읽는다.
            presetBar.SetDirty(dirty);
        }

        // 목록 셀 전체 재구성(30셀 × 초상 7). **구조 변경에서만** 부른다 — 생성·삭제·확정·
        // 저장·전환. 유닛 토글마다 부르면 매 탭 30셀을 다시 만들고, 아직 저장하지 않은
        // 내용이 목록에 새어 나간다(목록은 "저장된 프리셋들"이다).
        private void RefreshBarEntries()
        {
            if (presetBar == null) return;
            var p = Profile;

            _entries.Clear();
            if (p != null && p.squads != null)
            {
                for (int i = 0; i < p.squads.Count; i++)
                {
                    var s = p.squads[i];
                    if (s == null) continue;
                    _entries.Add(new PresetBarView.Entry
                    {
                        id = s.id,
                        name = s.name,
                        thumbs = Thumbs(s),
                        committed = s.id == p.selectedSquadId,
                    });
                }
            }

            int count = p != null && p.squads != null ? p.squads.Count : 0;
            presetBar.SetEntries(_entries, _viewingPresetId, count < PlayerProfile.MaxPresets);
            RefreshBarState();
        }

        // 목록 셀 썸네일은 **저장본**을 그린다(목록은 "저장된 프리셋들"이다).
        private Sprite[] Thumbs(SquadPreset s)
        {
            var arr = new Sprite[SquadPreset.SlotCount];
            if (s == null || s.unitIds == null || catalog == null) return arr;
            for (int i = 0; i < SquadPreset.SlotCount && i < s.unitIds.Count; i++)
            {
                var u = !string.IsNullOrEmpty(s.unitIds[i]) ? catalog.ById(s.unitIds[i]) : null;
                arr[i] = u != null ? u.portrait : null;
            }
            return arr;
        }

        private static bool AllEmpty(List<string> list)
        {
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i])) return false;
            return true;
        }

        // ---- 프리셋 조작 (구조 변경은 즉시 저장, 내용은 [저장]만) ------------

        private void OnPresetPicked(string id)
        {
            if (string.IsNullOrEmpty(id) || id == _viewingPresetId) return;
            if (IsDirty())
            {
                // review MEDIUM-5 — fail-closed. popup 미주입 시 경고 없이 미저장 변경을
                // 버리면 배선 누락이 데이터 유실로 조용히 번진다. OnStartGame 이 명문화한
                // 정책과 같다 — 미주입 ref 는 플레이어가 고칠 수 있는 상황으로 위장하지 않는다.
                if (confirmPopup == null)
                {
                    Debug.LogError("[SquadPreset] confirmPopup 미주입 — 미저장 변경이 있어 프리셋 "
                        + "전환을 차단했다. 페이지 빌더의 주입을 확인할 것.", this);
                    return;
                }
                string captured = id;
                confirmPopup.Show(
                    "저장하지 않은 변경이 있습니다.\n이동하면 변경은 사라집니다.",
                    () => SwitchTo(captured), "이동");
                return;
            }
            SwitchTo(id);
        }

        private void SwitchTo(string id)
        {
            LoadWorking(id);
            EnterUnitMode(initial: true);
            RefreshBarEntries();   // 선택 하이라이트 이동 = 구조 변경
        }

        private void OnCreatePreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4 — 변이 전에 묻는다
            var p = Profile;
            if (p == null || p.squads == null) return;
            if (p.squads.Count >= PlayerProfile.MaxPresets) return;

            var ids = new List<string>(p.squads.Count);
            for (int i = 0; i < p.squads.Count; i++) if (p.squads[i] != null) ids.Add(p.squads[i].id);

            var created = new SquadPreset
            {
                id = PresetIds.NextId(ids, IdPrefix),
                name = "스쿼드 " + (p.squads.Count + 1),
            };
            created.NormalizeSlots();
            p.squads.Add(created);
            p.NormalizePresets();
            Save();                       // 구조 변경 = 즉시 디스크
            SwitchTo(created.id);
        }

        private void OnCommitPreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4
            var p = Profile;
            if (p == null || string.IsNullOrEmpty(_viewingPresetId)) return;
            if (StoredPreset(_viewingPresetId) == null) return;

            // **내용은 건드리지 않는다** — 확정은 "이 프리셋의 저장본을 반입한다"는 뜻이다.
            p.selectedSquadId = _viewingPresetId;
            Save();
            RefreshBarEntries();   // 확정 뱃지 이동
        }

        private void OnSavePreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4
            var stored = StoredPreset(_viewingPresetId);
            if (stored == null) return;

            stored.name = _workingName;
            stored.unitIds = new List<string>(_workingUnits);
            stored.stoneIds = new List<string>(_workingStones);
            stored.NormalizeSlots();
            Save();
            RefreshBarEntries();          // dirty 꺼짐 + 썸네일/이름 갱신
        }

        // 리셋은 **작업본만** 비운다. 저장 안 하고 나가면 원복된다.
        private void OnResetWorking()
        {
            for (int i = 0; i < _workingUnits.Count; i++) _workingUnits[i] = "";
            for (int i = 0; i < _workingStones.Count; i++) _workingStones[i] = "";
            EnterUnitMode(initial: true);
        }

        private void OnDeletePreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4
            var p = Profile;
            if (p == null || p.squads == null) return;
            if (_viewingPresetId == p.selectedSquadId) return;   // 확정분 보호
            if (p.squads.Count <= 1) return;                     // 최소 1개 유지

            p.squads.RemoveAll(s => s != null && s.id == _viewingPresetId);
            p.NormalizePresets();
            Save();                       // 구조 변경 = 즉시 디스크
            SwitchTo(p.selectedSquadId);  // 확정분으로 복귀
        }

        private void OnNameCommitted(string value)
        {
            _workingName = value ?? "";
            // review MEDIUM-3 — 목록 셀은 **저장본**의 이름을 그리므로 작업본 이름이 바뀐
            // 것만으로 목록을 재구성할 이유가 없다. [저장] 시점에 RefreshBarEntries 가 돈다.
            RefreshBarState();
        }

        // ---- Unit mode ----------------------------------------------------

        private void EnterUnitMode(bool initial = false)
        {
            _mode = Mode.Unit;
            _activeStoneSlot = -1;
            if (header != null) header.SetActiveStoneSlot(-1);
            if (browser != null) browser.ShowUnits(SortedUnits());
            if (initial || string.IsNullOrEmpty(_selectedUnitId) ||
                (catalog != null && catalog.ById(_selectedUnitId) == null))
                _selectedUnitId = FirstWorkingUnitOrDefault();
            RefreshUnitMode();
        }

        // Unit 10 — 편성된 유닛 먼저(슬롯 순서, 헤더 스트립과 동일), 나머지는 카탈로그
        // 순서. 이제 기준은 저장본이 아니라 **작업본**이다.
        private List<DefenderUnitData> SortedUnits()
        {
            var sorted = new List<DefenderUnitData>(_units.Count);
            var seen = new HashSet<string>();
            for (int i = 0; i < _workingUnits.Count; i++)
            {
                var id = _workingUnits[i];
                if (string.IsNullOrEmpty(id) || catalog == null || !seen.Add(id)) continue;
                var u = catalog.ById(id);
                if (u != null) sorted.Add(u);
            }
            for (int i = 0; i < _units.Count; i++)
                if (!seen.Contains(_units[i].id)) sorted.Add(_units[i]);
            return sorted;
        }

        private string FirstWorkingUnitOrDefault()
        {
            for (int i = 0; i < _workingUnits.Count; i++)
                if (!string.IsNullOrEmpty(_workingUnits[i])) return _workingUnits[i];
            return _units.Count > 0 ? _units[0].id : null;
        }

        private void RefreshUnitMode()
        {
            if (header != null)
            {
                header.Refresh(_workingUnits, _workingStones);
                header.SetSelectedUnit(_selectedUnitId);
            }
            if (browser != null)
            {
                browser.SetBadged(IdSet(_workingUnits));
                browser.SetSelected(_selectedUnitId);
            }
            if (detailView != null)
            {
                var unit = catalog != null ? catalog.ById(_selectedUnitId) : null;
                detailView.Show(unit, Contains(_workingUnits, _selectedUnitId));
            }
            RefreshBarState();
        }

        private void ToggleUnit(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            int idx = _workingUnits.IndexOf(id);
            if (idx >= 0)
            {
                _workingUnits[idx] = "";
            }
            else
            {
                int empty = _workingUnits.FindIndex(string.IsNullOrEmpty);
                if (empty < 0) return; // 만석 — 무시
                _workingUnits[empty] = id;
            }
            // 저장하지 않는다 — [저장]이 유일한 기록 경로다.
            if (browser != null) browser.ShowUnits(SortedUnits());
            RefreshUnitMode();
        }

        // ---- Stone mode ---------------------------------------------------

        private void EnterStoneMode(int slotIndex)
        {
            _mode = Mode.Stone;
            _activeStoneSlot = Mathf.Clamp(slotIndex, 0, SquadPreset.StoneSlotCount - 1);
            if (browser != null) browser.ShowStones(SortedStones());
            string cur = _activeStoneSlot < _workingStones.Count ? _workingStones[_activeStoneSlot] : "";
            _selectedStoneId = !string.IsNullOrEmpty(cur) ? cur : (_stones.Count > 0 ? _stones[0].id : null);
            RefreshStoneMode();
        }

        // Unit 13 — 장착 스톤 먼저(슬롯 순서), 나머지는 카탈로그 순서. _stones 를 in-place
        // 정렬하면 안 된다 — EnterStoneMode 의 _stones[0] 폴백이 카탈로그 순서에 의존한다.
        private List<DreamstoneData> SortedStones()
        {
            var sorted = new List<DreamstoneData>(_stones.Count);
            var seen = new HashSet<string>();
            for (int i = 0; i < _workingStones.Count; i++)
            {
                var id = _workingStones[i];
                if (string.IsNullOrEmpty(id) || stoneCatalog == null || !seen.Add(id)) continue;
                var s = stoneCatalog.ById(id);
                if (s != null) sorted.Add(s);
            }
            for (int i = 0; i < _stones.Count; i++)
                if (!seen.Contains(_stones[i].id)) sorted.Add(_stones[i]);
            return sorted;
        }

        private void RefreshStoneMode()
        {
            if (header != null)
            {
                header.Refresh(_workingUnits, _workingStones);
                header.SetActiveStoneSlot(_activeStoneSlot);
                header.SetSelectedUnit(null); // unit outline is unit-mode only (unit 10)
            }
            if (browser != null)
            {
                browser.SetBadged(IdSet(_workingStones));
                browser.SetSelected(_selectedStoneId);
            }
            if (detailView != null)
            {
                var stone = stoneCatalog != null ? stoneCatalog.ById(_selectedStoneId) : null;
                bool equipped = stone != null &&
                    _activeStoneSlot >= 0 && _activeStoneSlot < _workingStones.Count &&
                    _workingStones[_activeStoneSlot] == _selectedStoneId;
                detailView.ShowStone(stone, equipped);
            }
            RefreshBarState();
        }

        private void ToggleStone(string id)
        {
            if (_activeStoneSlot < 0 || string.IsNullOrEmpty(id)) return;
            if (_workingStones[_activeStoneSlot] == id)
            {
                _workingStones[_activeStoneSlot] = "";
            }
            else
            {
                // "one item, one slot" — 다른 슬롯의 같은 id 를 먼저 비운다.
                for (int i = 0; i < _workingStones.Count; i++)
                    if (i != _activeStoneSlot && _workingStones[i] == id) _workingStones[i] = "";
                _workingStones[_activeStoneSlot] = id;
            }
            if (browser != null) browser.ShowStones(SortedStones());
            RefreshStoneMode();
        }

        // ---- Events -------------------------------------------------------

        private void OnEntrySelected(string id)
        {
            if (_mode == Mode.Unit) { _selectedUnitId = id; RefreshUnitMode(); }
            else { _selectedStoneId = id; RefreshStoneMode(); }
        }

        private void OnDeployClicked()
        {
            if (_mode == Mode.Unit) ToggleUnit(_selectedUnitId);
            else ToggleStone(_selectedStoneId);
        }

        // Unit 9 — 찬 슬롯 탭은 그 유닛을 상세 대상으로 선택한다(제거는 [편성 해제]).
        private void OnUnitSlotTapped(int i)
        {
            string id = (i >= 0 && i < _workingUnits.Count) ? _workingUnits[i] : "";
            if (!string.IsNullOrEmpty(id)) _selectedUnitId = id;
            if (_mode == Mode.Stone) { EnterUnitMode(); return; }
            RefreshUnitMode();
        }

        private void OnStoneSlotTapped(int i) => EnterStoneMode(i);

        // ---- Helpers ------------------------------------------------------

        private static HashSet<string> IdSet(List<string> ids)
        {
            var set = new HashSet<string>();
            if (ids != null)
                for (int i = 0; i < ids.Count; i++)
                    if (!string.IsNullOrEmpty(ids[i])) set.Add(ids[i]);
            return set;
        }

        private static bool Contains(List<string> ids, string id)
            => !string.IsNullOrEmpty(id) && ids != null && ids.Contains(id);
    }
}
