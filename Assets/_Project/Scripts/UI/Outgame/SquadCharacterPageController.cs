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
            // deck-info-preset-apply unit 3 — 히스토리 덱보기에서 넘어온 예약이 있으면
            // 소비한다. LoadWorking(확정분) **뒤** — 예약이 없을 때의 기존 동작을 그대로
            // 두고, 있을 때만 새 프리셋으로 갈아탄다.
            if (PresetApply.TryConsume(PresetApply.Target.Squad, out var staged)) ApplyStaged(staged);
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
                presetBar.RevertClicked += OnRevertWorking;
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

        // page-local-presets unit 8 — 씬 CloseButton 은 공통 메뉴 컨트롤러를 향하므로,
        // 메뉴가 활성 페이지를 찾은 뒤 이 가드에 실제 닫기 콜백을 맡긴다. dirty 판정은
        // 드롭다운 전환과 동일하게 저장본 대 작업본 비교를 그대로 쓴다.
        public void RequestClose(Action close)
        {
            if (!IsDirty())
            {
                close?.Invoke();
                return;
            }

            if (confirmPopup == null)
            {
                Debug.LogError("[SquadPreset] confirmPopup 미주입 — 미저장 변경이 있어 페이지 "
                    + "닫기를 차단했다. 페이지 빌더의 주입을 확인할 것.", this);
                return;
            }

            confirmPopup.Show(
                "저장하지 않은 변경이 있습니다.\n닫으면 변경은 사라집니다.",
                close, "닫기");
        }

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

            bool isCommitted = p != null && _viewingPresetId == p.selectedSquadId;
            bool dirty = IsDirty();

            presetBar.SetName(_workingName);
            presetBar.SetButtonEnabled(
                commit: !isCommitted,
                save: dirty,
                // [되돌리기]는 되돌릴 것이 있을 때만 = dirty. [저장]과 같은 조건이라 둘이
                // "미저장 변경을 남길래 버릴래" 한 쌍으로 읽힌다.
                revert: dirty,
                // [삭제]는 **항상 누를 수 있다**(사용자 결정 2026-07-30). 죽은 버튼은 왜
                // 안 눌리는지 말해주지 못한다 — 누르게 하고 OnDeletePreset 이 사유를
                // 안내한다. 뷰는 여전히 dim 조건을 판단하지 않으므로 파라미터는 남긴다.
                delete: true);
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

            var created = CreatePreset("스쿼드 " + (p.squads.Count + 1));
            if (created == null) return;
            SwitchTo(created.id);
        }

        // 생성 공통부 — [+] 와 프리셋 적용 픽업(ApplyStaged)이 공유한다. 상한/CanPersist
        // 판정은 호출처가 먼저 한다 — 차단 사유 안내가 호출처마다 다르다.
        private SquadPreset CreatePreset(string name)
        {
            var p = Profile;
            if (p == null || p.squads == null) return null;

            var ids = new List<string>(p.squads.Count);
            for (int i = 0; i < p.squads.Count; i++) if (p.squads[i] != null) ids.Add(p.squads[i].id);

            var created = new SquadPreset
            {
                id = PresetIds.NextId(ids, IdPrefix),
                name = name,
            };
            created.NormalizeSlots();
            p.squads.Add(created);
            p.NormalizePresets();
            Save();                       // 구조 변경 = 즉시 디스크
            return created;
        }

        // deck-info-preset-apply unit 3 — 예약 소비. **적용 = 생성 + 작업본 세팅, 저장이
        // 아니다.** 새 프리셋은 빈 내용으로 즉시 디스크에 생기고(구조 변경), 랭커의 편성은
        // 작업본에만 들어간다 → dirty 배지 on. [저장]이 유일한 기록 경로라는 규율에 예외를
        // 만들지 않는다 — 마음에 들면 [저장], 아니면 [되돌리기]로 빈 프리셋이 된다.
        //
        // 차단 사유는 이 페이지에서 안내한다(NoticePopup 3000 이 덱 팝업 3200 아래 깔리는
        // z-order 문제를 피하고, "가득 참"은 삭제할 수 있는 화면에서 말해야 이어진다).
        private void ApplyStaged(PresetApply.Request req)
        {
            var p = Profile;
            if (!CanPersist() || p == null || p.squads == null)
            {
                // 미로드를 조용한 무동작으로 위장하지 않는다 — confirmPopup fail-closed 와
                // 같은 정책.
                Debug.LogError("[SquadPreset] 프리셋 적용 차단 — 프로필이 로드되지 않았다.", this);
                NoticePopup.ShowAlert("적용할 수 없음", "프로필이 로드되지 않아 프리셋을 만들 수 없습니다.");
                return;
            }
            if (p.squads.Count >= PlayerProfile.MaxPresets)
            {
                NoticePopup.ShowAlert("적용할 수 없음",
                    $"프리셋이 {PlayerProfile.MaxPresets}개로 가득 차 새로 만들 수 없습니다.\n"
                    + "하나를 삭제한 뒤 다시 시도하세요.");
                return;
            }

            // 내 빌드에서 쓸 수 없는 항목은 제외한다 — 덱보기의 "미해석 id 를 남긴다"와
            // 의도적으로 반대(반입할 편성에 유령 id 가 남으면 안 된다). 근거는 PresetApply.
            var units = PresetApply.FilterUnits(req.unitIds, catalog, out int droppedUnits);
            var stones = PresetApply.FilterStones(req.stoneIds, stoneCatalog, out int droppedStones);

            var names = new List<string>(p.squads.Count);
            for (int i = 0; i < p.squads.Count; i++) if (p.squads[i] != null) names.Add(p.squads[i].name);
            var created = CreatePreset(PresetApply.UniqueName(names, req.presetName));
            if (created == null) return;

            // 빈 저장본을 작업본으로 복제한 뒤 그 위에 얹는다 — 목록 셀 썸네일은 저장본
            // (빈)을 그리는 게 맞고([저장] 후 채워진다), 작업본 리스트 길이는 CopySlots 가
            // 보장한 슬롯 수 불변식을 유지한다.
            LoadWorking(created.id);
            for (int i = 0; i < units.Count && i < _workingUnits.Count; i++) _workingUnits[i] = units[i];
            for (int i = 0; i < stones.Count && i < _workingStones.Count; i++) _workingStones[i] = stones[i];

            int dropped = droppedUnits + droppedStones;
            if (dropped > 0)
                NoticePopup.ShowAlert("일부 항목 제외",
                    $"{dropped}개 항목은 현재 버전에서 사용할 수 없어 제외했습니다.");
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

        // [되돌리기] — 작업본을 **저장본 기준으로** 복원한다(구 "리셋"=완전 비움에서 변경,
        // 사용자 결정 2026-07-30). 완전 비움은 두 가지가 어색했다: 초기화했는데 dirty 가
        // 켜졌고, "백지에서 시작"은 이미 [+] 빈 프리셋 생성이 담당한다. 저장본 기준이면
        // 되돌린 뒤 dirty 가 꺼져 [저장]/[되돌리기]가 "미저장 변경을 남길래 버릴래" 한 쌍이
        // 된다. 신규 프리셋은 저장본이 비어 있으므로 자연히 완전 비움이 된다.
        //
        // 디스크에 쓰지 않는다 — 저장본을 읽어 작업본에 덮는 것뿐이다.
        private void OnRevertWorking()
        {
            LoadWorking(_viewingPresetId);
            EnterUnitMode(initial: true);
        }

        private void OnDeletePreset()
        {
            if (!CanPersist()) return;   // review MEDIUM-4
            var p = Profile;
            if (p == null || p.squads == null) return;
            // 차단 사유를 **말해준다**. 예전엔 버튼을 dim 해 두고 조용히 return 했는데,
            // 죽은 버튼은 "고장인가 내가 뭘 잘못했나"로 읽힌다. NoticePopup 은 자기
            // 부트스트랩(RuntimeInitializeOnLoadMethod + DontDestroyOnLoad)이라 씬 배선이
            // 필요 없고 sortingOrder 3000 으로 프리셋 팝업 위에 뜬다.
            if (_viewingPresetId == p.selectedSquadId)
            {
                NoticePopup.ShowAlert("삭제할 수 없음",
                    "지금 보고 있는 프리셋이 <b>확정</b> 상태입니다.\n"
                    + "다른 프리셋을 [선택]으로 확정한 뒤 삭제하세요.");
                return;
            }
            if (p.squads.Count <= 1)
            {
                NoticePopup.ShowAlert("삭제할 수 없음",
                    "프리셋이 하나뿐입니다.\n반입할 편성이 없어지므로 마지막 프리셋은 지울 수 없습니다.");
                return;
            }

            // 삭제는 **되돌릴 수 없다** — [되돌리기]는 작업본 대상이라 지워진 프리셋을
            // 살리지 못한다. 유닛 한 칸 바꾸는 것도 [저장]을 요구하는 이 페이지에서
            // 프리셋 통째 삭제만 무경고 즉시 실행이면 앞뒤가 맞지 않으므로 확인을 받는다.
            // 미주입이면 fail-closed — 파괴적 동작을 확인 없이 진행하지 않는다.
            if (confirmPopup == null)
            {
                Debug.LogError("[SquadPreset] confirmPopup 미주입 — 확인을 받을 수 없어 삭제를 "
                    + "차단했다. 페이지 빌더의 주입을 확인할 것.", this);
                return;
            }

            var target = StoredPreset(_viewingPresetId);
            string label = (target != null && !string.IsNullOrEmpty(target.name)) ? target.name : "이 프리셋";
            string captured = _viewingPresetId;   // 콜백 시점의 _viewingPresetId 에 의존하지 않는다
            confirmPopup.Show(
                $"'{label}' 프리셋을 삭제합니다.\n저장된 편성이 사라지며 되돌릴 수 없습니다.",
                () => DeletePresetConfirmed(captured), "삭제");
        }

        // 확인 후 실제 삭제. **가드를 다시 확인한다** — 팝업 콜백은 나중에 오므로 그 사이
        // 상태가 바뀔 수 있다(예: 그 프리셋이 확정됐거나 다른 경로로 이미 지워졌거나).
        private void DeletePresetConfirmed(string id)
        {
            if (!CanPersist()) return;
            var p = Profile;
            if (p == null || p.squads == null || string.IsNullOrEmpty(id)) return;
            if (id == p.selectedSquadId) return;   // 그 사이 확정됐다
            if (p.squads.Count <= 1) return;

            p.squads.RemoveAll(s => s != null && s.id == id);
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
