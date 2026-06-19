using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // Runtime-built skill bar at screen bottom. Shows one slot per SkillData in
    // the loadout, displays cooldown countdown, and handles aim-mode targeting.
    // Follows the DraftView pattern: own Canvas, zero prefab assets.
    public class SkillBar : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private SkillRuntime skillRuntime;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float tileSize = 1f;

        private GameObject _panel;
        private Transform _slotContainer;
        private SlotView[] _slots;
        private SkillData _aimingSkill;
        private TextMeshProUGUI _aimLabel;
        private bool _built;

        // Phase 7 — Portal is a two-tap skill: first tap captures the entry tile
        // and waits; second tap completes the cast. When `_portalEntryTile.x >= 0`
        // the bar is in the "second tap" state for the currently-aimed Portal skill.
        private Vector2Int _portalEntryTile = new(-1, -1);

        private struct SlotView
        {
            public SkillData skill;
            public Image background;
            public TextMeshProUGUI nameLabel;
            public TextMeshProUGUI cooldownLabel;
            public Button button;
        }

        [SerializeField] private DraftController draftController;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed += OnDraftConfirmed;
                draftController.DraftStarted += OnDraftStarted;
            }
            // Squad mode has no draft — the bar reveal comes via
            // GameManager.PlacementRequested instead of DraftConfirmed, else the
            // skill buttons never appear in squad mode.
            if (GameManager.Instance != null)
                GameManager.Instance.PlacementRequested += OnDraftConfirmed;
            // Last-pressed-wins mutual exclusion (Phase 8 §12 follow-up):
            // DefenderSelector fires AimCanceled when the player picks a
            // placement target, so any pending skill aim must drop.
            if (GameManager.Instance != null)
                GameManager.Instance.AimCanceled += ExitAimMode;
        }

        private void OnDisable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed -= OnDraftConfirmed;
                draftController.DraftStarted -= OnDraftStarted;
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlacementRequested -= OnDraftConfirmed;
                GameManager.Instance.AimCanceled -= ExitAimMode;
            }
        }

        // Editor Exit Play destroys MonoBehaviours in an unspecified order, so
        // GameManager may already be gone when SkillBar.OnDisable runs. Mirror
        // the unsubscribe on OnDestroy so dead delegates never accumulate on
        // the GameManager instance across play sessions.
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlacementRequested -= OnDraftConfirmed;
                GameManager.Instance.AimCanceled -= ExitAimMode;
            }
        }

        private void OnDraftConfirmed()
        {
            if (bridge != null && bridge.SkillLoadout != null)
                Show(bridge.SkillLoadout);
        }

        private void OnDraftStarted() => Hide();

        public void Show(SkillData[] loadout)
        {
            if (!_built) BuildCanvas();
            RebuildSlots(loadout);
            _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            ExitAimMode();
        }

        private void Update()
        {
            if (_slots == null) return;

            var gm = GameManager.Instance;
            bool battlePhase = gm != null && gm.CurrentPhase == GamePhase.Battle;
            var cost = gm != null ? gm.CostRuntime : null;

            for (int i = 0; i < _slots.Length; i++)
            {
                ref var slot = ref _slots[i];
                if (slot.skill == null || skillRuntime == null) continue;
                float rem = skillRuntime.GetRemainingSeconds(slot.skill);
                bool ready = skillRuntime.IsReady(slot.skill);
                bool affordable = cost == null || cost.CanAfford(slot.skill.cost);

                slot.cooldownLabel.text = rem > 0f
                    ? rem.ToString("0.0")
                    : (cost != null ? $"-{slot.skill.cost}" : "");
                slot.button.interactable = battlePhase && (ready && affordable || slot.skill == _aimingSkill);

                if (slot.skill == _aimingSkill)
                    slot.background.color = Color.Lerp(slot.skill.uiTint, Color.white, 0.5f);
                else if (!battlePhase || !ready || !affordable)
                    slot.background.color = slot.skill.uiTint * 0.4f;     // gray / dimmed
                else
                    slot.background.color = slot.skill.uiTint;
            }

            if (_aimingSkill != null) HandleAimInput();
        }

        private void HandleAimInput()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitAimMode();
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            var screenPos = pointer.position.ReadValue();
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (mainCamera == null) return;
            var ray = mainCamera.ScreenPointToRay(screenPos);
            // tilemap-view-backend — 입력 평면은 모드별(BoardSpace), 히트 지점을 sim 으로 되돌려
            // 기존 셀 변환(bridge.DebugWorldToCell) 흐름 유지. (PlacementInput 과 동일 패턴.)
            // Tilemap 모드는 보드가 수직 평면이라 옛 Plane(up) 은 카메라 레이와 평행해 교차 실패 →
            // 스킬 타겟이 잡히지 않던 버그를 고친다. Legacy3D 는 동작 동일(plane=up@origin, ToSim=identity).
            var plane = BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return;

            var worldPos = (Vector3)BoardSpace.ToSim(ray.GetPoint(enter));
            Vector2Int tile;
            if (bridge != null)
            {
                var hitCell = bridge.DebugWorldToCell(worldPos);
                tile = new Vector2Int(hitCell.x, hitCell.y);
            }
            else
            {
                tile = new Vector2Int(Mathf.RoundToInt(worldPos.x / tileSize), Mathf.RoundToInt(worldPos.z / tileSize));
            }

            // Phase 6 — verify cost one more time at cast-resolution time in case
            // regen/phase changed while the player was aiming.
            var cost = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (cost != null && !cost.CanAfford(_aimingSkill.cost))
            {
                ExitAimMode();
                return;
            }

            // Phase 7 — Portal two-tap state machine lives inline on the aim loop.
            // First tap just records the entry tile and swaps the label prompt;
            // cost is spent (and cooldown consumed) only on the completing cast.
            if (_aimingSkill.effect == SkillEffectType.Portal)
            {
                if (_portalEntryTile.x < 0)
                {
                    _portalEntryTile = tile;
                    if (_aimLabel != null)
                        _aimLabel.text = $"[{_aimingSkill.displayName}] Tap exit tile";
                    return;
                }
                bool portalCast = bridge.CastPortal(_aimingSkill, _portalEntryTile, tile, out _);
                if (portalCast)
                {
                    if (cost != null) cost.TrySpend(_aimingSkill.cost);
                    ExitAimMode();
                }
                return;
            }

            bool cast;
            int affected;
            if (_aimingSkill.target == SkillTargetType.TilePoint)
                cast = bridge.CastSkillAtTile(_aimingSkill, tile, out affected);
            else
                cast = bridge.CastSkillOnDefender(_aimingSkill, tile, out affected);

            if (cast)
            {
                if (cost != null) cost.TrySpend(_aimingSkill.cost);
                ExitAimMode();
            }
        }

        private void OnSlotClicked(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length) return;
            var skill = _slots[index].skill;
            if (skill == null) return;

            if (_aimingSkill == skill)
            {
                ExitAimMode();
                return;
            }

            if (skillRuntime != null && !skillRuntime.IsReady(skill)) return;

            _aimingSkill = skill;
            _portalEntryTile = new Vector2Int(-1, -1);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.IsAiming = true;
                // Last-pressed-wins: entering skill aim cancels any pending
                // placement so the next tap doesn't stack both actions.
                GameManager.Instance.SelectedDefender = null;
            }
            if (_aimLabel != null)
            {
                _aimLabel.text = skill.effect == SkillEffectType.Portal
                    ? $"[{skill.displayName}] Tap entry tile"
                    : $"[{skill.displayName}] Tap target";
                _aimLabel.gameObject.SetActive(true);
            }
        }

        private void ExitAimMode()
        {
            _aimingSkill = null;
            _portalEntryTile = new Vector2Int(-1, -1);
            if (GameManager.Instance != null) GameManager.Instance.IsAiming = false;
            if (_aimLabel != null) _aimLabel.gameObject.SetActive(false);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("SkillPanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(1f, 0.5f);
            prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-40f, 0f);
            prt.sizeDelta = new Vector2(140f, 400f);

            var vlg = _panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            _slotContainer = _panel.transform;

            var aimGO = new GameObject("AimLabel", typeof(RectTransform));
            aimGO.transform.SetParent(transform, false);
            var art = (RectTransform)aimGO.transform;
            art.anchorMin = new Vector2(1f, 0f);
            art.anchorMax = new Vector2(1f, 0f);
            art.pivot = new Vector2(1f, 0f);
            art.anchoredPosition = new Vector2(-40f, 40f);
            art.sizeDelta = new Vector2(500f, 50f);
            _aimLabel = aimGO.AddComponent<TextMeshProUGUI>();
            _aimLabel.fontSize = 28;
            _aimLabel.color = Color.yellow;
            _aimLabel.alignment = TextAlignmentOptions.Center;
            aimGO.SetActive(false);
        }

        private void RebuildSlots(SkillData[] loadout)
        {
            // Clear old
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);

            if (loadout == null || loadout.Length == 0) { _slots = null; return; }
            _slots = new SlotView[loadout.Length];

            for (int i = 0; i < loadout.Length; i++)
            {
                var skill = loadout[i];
                if (skill == null) continue;

                var go = new GameObject($"Slot_{skill.id}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_slotContainer, false);
                var bg = go.GetComponent<Image>();
                bg.color = skill.uiTint;
                var btn = go.GetComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => OnSlotClicked(idx));

                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(go.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                nrt.anchorMin = new Vector2(0f, 0.5f);
                nrt.anchorMax = new Vector2(1f, 1f);
                nrt.offsetMin = new Vector2(4f, 0f);
                nrt.offsetMax = new Vector2(-4f, -4f);
                var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
                nameTmp.text = skill.displayName;
                nameTmp.fontSize = 20;
                nameTmp.color = Color.white;
                nameTmp.alignment = TextAlignmentOptions.Center;

                var cdGO = new GameObject("Cooldown", typeof(RectTransform));
                cdGO.transform.SetParent(go.transform, false);
                var cdrt = (RectTransform)cdGO.transform;
                cdrt.anchorMin = new Vector2(0f, 0f);
                cdrt.anchorMax = new Vector2(1f, 0.5f);
                cdrt.offsetMin = new Vector2(4f, 4f);
                cdrt.offsetMax = new Vector2(-4f, 0f);
                var cdTmp = cdGO.AddComponent<TextMeshProUGUI>();
                cdTmp.text = "";
                cdTmp.fontSize = 28;
                cdTmp.color = Color.white;
                cdTmp.alignment = TextAlignmentOptions.Center;

                _slots[i] = new SlotView
                {
                    skill = skill,
                    background = bg,
                    nameLabel = nameTmp,
                    cooldownLabel = cdTmp,
                    button = btn,
                };
            }
        }
    }
}
