using System;
using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.UI
{
    // squad-character-page Unit 1 — the left 1/3 detail panel: a live Spine
    // backdrop (SkeletonGraphic) with a unified translucent card overlaid at the
    // bottom (name / rarity+class+cost badges / 5 stats / kit summary / deploy
    // button). Show(unit, inSquad) binds everything; the deploy button raises
    // DeployClicked and the squad logic (unit 4) drives SetDeployState back.
    //
    // Spine binding mirrors SceneTransition's proven runtime path: the
    // SkeletonGraphic is authored in the scene (material + canvas channels set up
    // there, unit 5), and here we only swap skeletonDataAsset + Initialize + apply
    // the combined skin (SpineCombinedSkinCache) + loop idle. Units without a
    // skeleton fall back to their portrait sprite.
    public class SquadUnitDetailView : MonoBehaviour
    {
        [SerializeField] private SkeletonGraphic spineView;   // scene-authored (unit 5)
        [SerializeField] private Image portraitFallback;      // shown when no skeleton
        [SerializeField] private Image rarityFrame;           // panel-edge glow tinted by rarity
        [SerializeField] private RectTransform cardRoot;      // procedural card children go here
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string idleAnimation = "idle";

        public event Action DeployClicked;

        private static readonly Color CardBg = new Color(0.06f, 0.07f, 0.10f, 0.82f);
        private static readonly Color DeployColor = new Color(0.20f, 0.52f, 0.32f, 1f);
        private static readonly Color UnequipColor = new Color(0.52f, 0.24f, 0.24f, 1f);
        private static readonly Color ClassBadgeColor = new Color(0.20f, 0.24f, 0.34f, 1f);
        private static readonly Color CostBadgeColor = new Color(0.16f, 0.34f, 0.52f, 1f);

        private static readonly string[] StatLabels =
            { "데미지", "체력", "사거리", "공격주기", "각성보상" };

        private bool _cardBuilt;
        private TMP_Text _nameText;
        private GameObject _badgeRowGo;
        private Image _rarityBadgeBg;
        private TMP_Text _rarityBadgeText;
        private TMP_Text _classBadgeText;
        private TMP_Text _costBadgeText;
        private readonly TMP_Text[] _statValues = new TMP_Text[5];
        private readonly GameObject[] _statRowGos = new GameObject[5];
        private TMP_Text _summaryText;
        private Image _deployBg;
        private TMP_Text _deployLabel;

        private DefenderUnitData _current;

        public void Show(DefenderUnitData u, bool inSquad)
        {
            _current = u;
            EnsureCardBuilt();
            BindSpine(u);
            FillCard(u);
            SetDeployState(inSquad);
        }

        public void SetDeployState(bool inSquad)
        {
            if (_deployLabel != null) _deployLabel.text = inSquad ? "편성 해제" : "출전";
            if (_deployBg != null) _deployBg.color = inSquad ? UnequipColor : DeployColor;
        }

        // Unit 3 — stone mode reuses this panel: spine off, big stone icon in the
        // portrait slot, grade+effect line, "장착/해제" button. Unit-only rows (badges,
        // 5 stats) hide and are restored by the next ShowUnit. The shared DeployClicked
        // event is interpreted by the orchestrator's current mode (unit vs stone).
        public void ShowStone(DreamstoneData stone, bool equipped)
        {
            EnsureCardBuilt();
            _current = null;
            if (spineView != null) spineView.gameObject.SetActive(false);
            if (portraitFallback != null)
            {
                Sprite icon = stone != null ? stone.icon : null;
                portraitFallback.sprite = icon;
                portraitFallback.enabled = icon != null;
            }
            if (rarityFrame != null)
                rarityFrame.color = stone != null ? DreamstoneStyle.Frame(stone.grade) : Color.clear;

            SetUnitPartsActive(false);
            if (_nameText != null)
                _nameText.text = stone == null ? "" : (string.IsNullOrEmpty(stone.displayName) ? stone.id : stone.displayName);
            if (_summaryText != null)
                _summaryText.text = stone == null ? "" : DreamstoneStyle.GradeLabel(stone.grade) + " · " + DreamstoneStyle.Summary(stone);

            if (_deployLabel != null) _deployLabel.text = equipped ? "해제" : "장착";
            if (_deployBg != null) _deployBg.color = equipped ? UnequipColor : DeployColor;
        }

        private void SetUnitPartsActive(bool active)
        {
            if (_badgeRowGo != null) _badgeRowGo.SetActive(active);
            for (int i = 0; i < _statRowGos.Length; i++)
                if (_statRowGos[i] != null) _statRowGos[i].SetActive(active);
        }

        // -- Spine backdrop -------------------------------------------------

        private void BindSpine(DefenderUnitData u)
        {
            var dataAsset = u != null ? u.SpineSkeletonDataAsset : null;
            bool hasSkeleton = spineView != null && dataAsset != null;

            if (spineView != null) spineView.gameObject.SetActive(hasSkeleton);
            if (hasSkeleton)
            {
                if (spineView.skeletonDataAsset != dataAsset)
                {
                    spineView.skeletonDataAsset = dataAsset;
                    spineView.Initialize(true);
                }
                else if (spineView.Skeleton == null)
                {
                    spineView.Initialize(false);
                }
                if (spineView.Skeleton != null)
                    SpineCombinedSkinCache.Apply(spineView.Skeleton, u);
                PlayIdle();
            }

            if (portraitFallback != null)
            {
                Sprite p = (!hasSkeleton && u != null) ? u.portrait : null;
                portraitFallback.sprite = p;
                portraitFallback.enabled = p != null;
            }

            if (rarityFrame != null)
                rarityFrame.color = u != null ? UnitRarityStyle.Frame(u.rarity) : Color.clear;
        }

        private void PlayIdle()
        {
            if (spineView == null || spineView.AnimationState == null) return;
            var data = spineView.Skeleton != null ? spineView.Skeleton.Data : null;
            if (data == null) return;
            string anim = FirstAnimation(data, idleAnimation, "idle", "Idle", "walk", "Walk");
            if (!string.IsNullOrEmpty(anim))
                spineView.AnimationState.SetAnimation(0, anim, true);
        }

        private static string FirstAnimation(Spine.SkeletonData data, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                string c = candidates[i];
                if (!string.IsNullOrEmpty(c) && data.FindAnimation(c) != null) return c;
            }
            return null;
        }

        // -- Unified card ---------------------------------------------------

        private void FillCard(DefenderUnitData u)
        {
            if (!_cardBuilt) return;

            if (u == null)
            {
                if (_nameText != null) _nameText.text = "";
                if (_summaryText != null) _summaryText.text = "";
                for (int i = 0; i < _statValues.Length; i++)
                    if (_statValues[i] != null) _statValues[i].text = "";
                return;
            }

            // Restore unit-only rows in case we came back from stone mode.
            SetUnitPartsActive(true);

            if (_nameText != null)
                _nameText.text = string.IsNullOrEmpty(u.displayName) ? u.name : u.displayName;

            if (_rarityBadgeBg != null) _rarityBadgeBg.color = UnitRarityStyle.Frame(u.rarity);
            if (_rarityBadgeText != null) _rarityBadgeText.text = UnitLabels.RarityLabel(u.rarity);
            if (_classBadgeText != null) _classBadgeText.text = UnitLabels.ClassLabel(u.role);
            if (_costBadgeText != null) _costBadgeText.text = $"코스트 {u.cost}";

            SetStat(0, DamageText(u));
            SetStat(1, u.health.ToString("0"));
            SetStat(2, u.attackRange.ToString("0.#"));
            SetStat(3, u.attackCooldown.ToString("0.0") + "s");
            SetStat(4, u.awakeningReward.ToString());

            if (_summaryText != null) _summaryText.text = UnitKitSummary.Describe(u);
        }

        private void SetStat(int index, string value)
        {
            if (index >= 0 && index < _statValues.Length && _statValues[index] != null)
                _statValues[index].text = value;
        }

        private static string DamageText(DefenderUnitData u)
        {
            return AttackOutputStats.TryGetUniqueMagnitude(u.outputs, AttackOutputKind.Damage, out float dmg)
                ? dmg.ToString("0.#")
                : "—";
        }

        private void EnsureCardBuilt()
        {
            if (_cardBuilt || cardRoot == null) return;
            _cardBuilt = true;

            var bg = cardRoot.gameObject.GetComponent<Image>();
            if (bg == null) bg = cardRoot.gameObject.AddComponent<Image>();
            bg.color = CardBg;

            var vlg = cardRoot.gameObject.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = cardRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            // ui-polish 2026-07-18 — 하단 패딩 크게(모바일 바닥 여백) + 항목 간격.
            vlg.padding = new RectOffset(22, 22, 18, 40);
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _nameText = MakeText(cardRoot, "", 44, TextAlignmentOptions.Left, 54);

            var badgeRow = MakeRow(cardRoot, 42, 8);
            _badgeRowGo = badgeRow.gameObject;
            _rarityBadgeBg = null;
            _rarityBadgeText = MakeBadge(badgeRow.transform, out _rarityBadgeBg, UnitRarityStyle.Frame(DefenderRarity.Common));
            MakeBadge(badgeRow.transform, out var classBg, ClassBadgeColor);
            _classBadgeText = classBg.GetComponentInChildren<TMP_Text>();
            MakeBadge(badgeRow.transform, out var costBg, CostBadgeColor);
            _costBadgeText = costBg.GetComponentInChildren<TMP_Text>();

            for (int i = 0; i < StatLabels.Length; i++)
            {
                var row = MakeRow(cardRoot, 38, 0);
                _statRowGos[i] = row.gameObject;
                MakeText(row.transform, StatLabels[i], 26, TextAlignmentOptions.Left, 38);
                _statValues[i] = MakeText(row.transform, "", 26, TextAlignmentOptions.Right, 38);
            }

            _summaryText = MakeText(cardRoot, "", 24, TextAlignmentOptions.TopLeft, 76);
            _summaryText.enableWordWrapping = true;

            // ui-polish 2026-07-18 — 편성/출전 버튼 크게(모바일 탭 타겟).
            var btnGo = new GameObject("DeployButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(cardRoot, false);
            _deployBg = btnGo.GetComponent<Image>();
            _deployBg.color = DeployColor;
            var btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.minHeight = 84; btnLe.preferredHeight = 84;
            btnGo.GetComponent<Button>().onClick.AddListener(() => DeployClicked?.Invoke());
            _deployLabel = MakeText(btnGo.transform, "출전", 34, TextAlignmentOptions.Center, 0);
            var lblRt = _deployLabel.rectTransform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
        }

        private RectTransform MakeRow(Transform parent, float height, float spacing)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height;
            return go.GetComponent<RectTransform>();
        }

        private TMP_Text MakeText(Transform parent, string text, int size, TextAlignmentOptions align, float preferredHeight)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.enableWordWrapping = false;
            if (font != null) t.font = font;
            if (preferredHeight > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = preferredHeight; le.preferredHeight = preferredHeight;
            }
            return t;
        }

        private TMP_Text MakeBadge(Transform parent, out Image bg, Color color)
        {
            var go = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            bg = go.GetComponent<Image>();
            bg.color = color;
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 108; le.preferredWidth = 108; le.minHeight = 40; le.preferredHeight = 40;
            var label = MakeText(go.transform, "", 24, TextAlignmentOptions.Center, 0);
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return label;
        }
    }
}
