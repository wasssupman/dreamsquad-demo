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
        }

        private void OnDisable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed -= OnDraftConfirmed;
                draftController.DraftStarted -= OnDraftStarted;
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
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return;

            var worldPos = ray.GetPoint(enter);
            int tx = Mathf.RoundToInt(worldPos.x / tileSize);
            int ty = Mathf.RoundToInt(worldPos.z / tileSize);
            var tile = new Vector2Int(tx, ty);

            // Phase 6 — verify cost one more time at cast-resolution time in case
            // regen/phase changed while the player was aiming.
            var cost = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (cost != null && !cost.CanAfford(_aimingSkill.cost))
            {
                ExitAimMode();
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
            if (GameManager.Instance != null) GameManager.Instance.IsAiming = true;
            if (_aimLabel != null)
            {
                _aimLabel.text = $"[{skill.displayName}] Tap target";
                _aimLabel.gameObject.SetActive(true);
            }
        }

        private void ExitAimMode()
        {
            _aimingSkill = null;
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
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 20f);
            prt.sizeDelta = new Vector2(600f, 120f);

            var hlg = _panel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            _slotContainer = _panel.transform;

            var aimGO = new GameObject("AimLabel", typeof(RectTransform));
            aimGO.transform.SetParent(transform, false);
            var art = (RectTransform)aimGO.transform;
            art.anchorMin = new Vector2(0.5f, 0f);
            art.anchorMax = new Vector2(0.5f, 0f);
            art.pivot = new Vector2(0.5f, 0f);
            art.anchoredPosition = new Vector2(0f, 150f);
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
