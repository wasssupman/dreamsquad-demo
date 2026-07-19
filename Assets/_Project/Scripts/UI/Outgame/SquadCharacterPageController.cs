using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-character-page Unit 4 — the orchestrator. Owns the detail view, roster
    // browser and header strip and drives the selected squad (profile). Replaces the
    // old SquadBuilderView slot+modal flow (retired in scene wiring, unit 5).
    //
    // Two modes share one browser + one detail panel (no modal): Unit mode browses
    // defenders and edits the 7 unit slots; Stone mode (entered by tapping a header
    // stone slot) browses the 64 dreamstones and edits the active stone slot. Every
    // edit mutates the selected SquadSave in place and auto-saves.
    public class SquadCharacterPageController : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private DreamstoneCatalog stoneCatalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private SquadUnitDetailView detailView;
        [SerializeField] private SquadRosterBrowser browser;
        [SerializeField] private SquadHeaderStrip header;

        private enum Mode { Unit, Stone }
        private Mode _mode = Mode.Unit;
        private int _activeStoneSlot = -1;
        private string _selectedUnitId;
        private string _selectedStoneId;
        private bool _wired;

        private readonly List<DefenderUnitData> _units = new List<DefenderUnitData>();
        private readonly List<DreamstoneData> _stones = new List<DreamstoneData>();

        private SquadSave Squad =>
            (profileSO != null && profileSO.profile != null) ? profileSO.profile.SelectedSquad() : null;

        private void OnEnable()
        {
            WireOnce();
            BuildLists();
            var squad = Squad;
            if (squad != null) squad.NormalizeSlots();
            EnterUnitMode(initial: true);
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

        // ---- Unit mode ----------------------------------------------------

        private void EnterUnitMode(bool initial = false)
        {
            _mode = Mode.Unit;
            _activeStoneSlot = -1;
            if (header != null) header.SetActiveStoneSlot(-1);
            if (browser != null) browser.ShowUnits(SortedUnits());
            if (initial || string.IsNullOrEmpty(_selectedUnitId) ||
                (catalog != null && catalog.ById(_selectedUnitId) == null))
                _selectedUnitId = FirstSquadUnitOrDefault();
            RefreshUnitMode();
        }

        // Unit 10 — squad members first (slot order, matching the header strip),
        // the rest in catalog order. Re-shown on every membership change so the
        // invariant holds live.
        private List<DefenderUnitData> SortedUnits()
        {
            var squad = Squad;
            if (squad == null || squad.unitIds == null) return _units;
            var sorted = new List<DefenderUnitData>(_units.Count);
            for (int i = 0; i < squad.unitIds.Count; i++)
            {
                var u = (!string.IsNullOrEmpty(squad.unitIds[i]) && catalog != null) ? catalog.ById(squad.unitIds[i]) : null;
                if (u != null) sorted.Add(u);
            }
            for (int i = 0; i < _units.Count; i++)
                if (!Contains(squad.unitIds, _units[i].id)) sorted.Add(_units[i]);
            return sorted;
        }

        private string FirstSquadUnitOrDefault()
        {
            var squad = Squad;
            if (squad != null)
                for (int i = 0; i < squad.unitIds.Count; i++)
                    if (!string.IsNullOrEmpty(squad.unitIds[i])) return squad.unitIds[i];
            return _units.Count > 0 ? _units[0].id : null;
        }

        private void RefreshUnitMode()
        {
            var squad = Squad;
            if (header != null) { header.Refresh(squad); header.SetSelectedUnit(_selectedUnitId); }
            if (browser != null)
            {
                browser.SetBadged(IdSet(squad != null ? squad.unitIds : null));
                browser.SetSelected(_selectedUnitId);
            }
            if (detailView != null)
            {
                var unit = catalog != null ? catalog.ById(_selectedUnitId) : null;
                detailView.Show(unit, Contains(squad != null ? squad.unitIds : null, _selectedUnitId));
            }
        }

        private void ToggleUnit(string id)
        {
            var squad = Squad;
            if (squad == null || string.IsNullOrEmpty(id)) return;
            squad.NormalizeSlots();
            int idx = squad.unitIds.IndexOf(id);
            if (idx >= 0)
            {
                squad.unitIds[idx] = "";
            }
            else
            {
                int empty = squad.unitIds.FindIndex(s => string.IsNullOrEmpty(s));
                if (empty < 0) return; // squad full — ignore
                squad.unitIds[empty] = id;
            }
            Save();
            if (browser != null) browser.ShowUnits(SortedUnits());
            RefreshUnitMode();
        }

        // ---- Stone mode ---------------------------------------------------

        private void EnterStoneMode(int slotIndex)
        {
            _mode = Mode.Stone;
            _activeStoneSlot = Mathf.Clamp(slotIndex, 0, SquadSave.StoneSlotCount - 1);
            if (browser != null) browser.ShowStones(_stones);
            var squad = Squad;
            string cur = (squad != null && _activeStoneSlot < squad.stoneIds.Count) ? squad.stoneIds[_activeStoneSlot] : "";
            _selectedStoneId = !string.IsNullOrEmpty(cur) ? cur : (_stones.Count > 0 ? _stones[0].id : null);
            RefreshStoneMode();
        }

        private void RefreshStoneMode()
        {
            var squad = Squad;
            if (header != null)
            {
                header.Refresh(squad);
                header.SetActiveStoneSlot(_activeStoneSlot);
                header.SetSelectedUnit(null); // unit outline is unit-mode only (unit 10)
            }
            if (browser != null)
            {
                browser.SetBadged(IdSet(squad != null ? squad.stoneIds : null));
                browser.SetSelected(_selectedStoneId);
            }
            if (detailView != null)
            {
                var stone = stoneCatalog != null ? stoneCatalog.ById(_selectedStoneId) : null;
                bool equipped = stone != null && squad != null &&
                    _activeStoneSlot >= 0 && _activeStoneSlot < squad.stoneIds.Count &&
                    squad.stoneIds[_activeStoneSlot] == _selectedStoneId;
                detailView.ShowStone(stone, equipped);
            }
        }

        private void ToggleStone(string id)
        {
            var squad = Squad;
            if (squad == null || _activeStoneSlot < 0 || string.IsNullOrEmpty(id)) return;
            squad.NormalizeSlots();
            if (squad.stoneIds[_activeStoneSlot] == id)
            {
                squad.SetStoneSlot(_activeStoneSlot, "");
            }
            else
            {
                // "one item, one slot" — move: clear any other slot holding this id.
                for (int i = 0; i < squad.stoneIds.Count; i++)
                    if (i != _activeStoneSlot && squad.stoneIds[i] == id) squad.stoneIds[i] = "";
                squad.SetStoneSlot(_activeStoneSlot, id);
            }
            Save();
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

        // Unit 9 — a filled-slot tap selects the unit for the detail panel (removal
        // moved to the [편성 해제] button); an empty-slot tap is a no-op.
        private void OnUnitSlotTapped(int i)
        {
            var squad = Squad;
            if (squad == null) { if (_mode == Mode.Stone) EnterUnitMode(); return; }
            squad.NormalizeSlots();
            string id = (i >= 0 && i < squad.unitIds.Count) ? squad.unitIds[i] : "";
            if (!string.IsNullOrEmpty(id)) _selectedUnitId = id;
            if (_mode == Mode.Stone) { EnterUnitMode(); return; }
            RefreshUnitMode();
        }

        private void OnStoneSlotTapped(int i)
        {
            EnterStoneMode(i);
        }

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
        {
            return !string.IsNullOrEmpty(id) && ids != null && ids.Contains(id);
        }

        private void Save()
        {
            if (profileSO != null && profileSO.profile != null)
                ProfileStore.Save(profileSO.profile);
        }
    }
}
