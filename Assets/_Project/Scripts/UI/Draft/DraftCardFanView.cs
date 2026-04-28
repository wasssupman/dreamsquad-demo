using System;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // Bottom-center Slay-the-Spire-style card fan. Builds 10 runtime card
    // GameObjects (no prefab assets) at hidden y=-300 then runs PlayEnterSequence
    // to flutter them into fan positions. PlayDiscardCard runs a toss tween;
    // LayoutRemaining re-fits the surviving cards onto the new fan curve.
    public class DraftCardFanView : MonoBehaviour
    {
        public const int MaxCards = 10;

        // Fan curve constants (1080 ref height).
        private const float FanRadius = 1400f;
        private const float FanArcDegrees = 26f;
        private const float CenterLift = 60f;
        private const float OffscreenY = -300f;
        private const float CardWidth = 240f;
        private const float CardHeight = 340f;

        public event Action<DraftCardView> CardDiscardRequested;

        private RectTransform _fanRoot;
        private readonly List<DraftCardView> _cards = new();
        private bool _built;

        public IReadOnlyList<DraftCardView> Cards => _cards;

        private void Awake()
        {
            if (!_built) BuildRoot();
        }

        private void OnDisable()
        {
            Tween.StopAll(this);
            foreach (var c in _cards) if (c != null) Tween.StopAll(c);
        }

        private void BuildRoot()
        {
            if (_built) return;
            _built = true;
            // Self stretch so FanRoot's (0.5, 0) anchor resolves to canvas bottom-center.
            var selfRt = (RectTransform)transform;
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero;
            selfRt.offsetMax = Vector2.zero;
            var rootGo = new GameObject("FanRoot", typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);
            _fanRoot = (RectTransform)rootGo.transform;
            _fanRoot.anchorMin = new Vector2(0.5f, 0f);
            _fanRoot.anchorMax = new Vector2(0.5f, 0f);
            _fanRoot.pivot = new Vector2(0.5f, 0f);
            _fanRoot.anchoredPosition = new Vector2(0f, 80f);
            _fanRoot.sizeDelta = Vector2.zero;
        }

        public void Build(IReadOnlyList<DefenderUnitData> pool)
        {
            if (!_built) BuildRoot();
            foreach (var c in _cards) if (c != null) Destroy(c.gameObject);
            _cards.Clear();

            int n = Mathf.Min(pool != null ? pool.Count : 0, MaxCards);
            for (int i = 0; i < n; i++)
            {
                var unit = pool[i];
                if (unit == null) continue;
                var card = CreateCard(unit, i);
                _cards.Add(card);
            }
            LayoutHomes();
            ResetCardsToOffscreen();
        }

        public Sequence PlayEnterSequence()
        {
            var seq = Sequence.Create();
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == null) continue;
                float startDelay = i * 0.04f;
                seq.Group(Tween.UIAnchoredPosition(card.Rect, card.HomePosition, 0.45f, Ease.OutQuad, startDelay: startDelay));
                seq.Group(Tween.LocalRotation(card.Rect, card.HomeRotation, 0.45f, Ease.OutQuad, startDelay: startDelay));
            }
            return seq;
        }

        // Speed threshold (canvas px/s) above which the card exits in the exact
        // throw direction at constant speed instead of the eased arc toss.
        private const float FastThreshold = 200f;
        private const float FastExitDist  = 1200f;

        public Sequence PlayDiscardCard(DraftCardView card)
        {
            if (card == null) return Sequence.Create();
            card.SetInteractable(false);
            card.Rect.SetAsLastSibling();

            Vector2 vel   = card.LastVelocity;
            bool isFast   = vel.magnitude > FastThreshold;

            Vector2 endPos;
            Quaternion endRot;
            float dur;
            Ease posEase;

            if (isFast)
            {
                // Velocity-aligned exit: constant speed, rotates to face travel direction.
                Vector2 dir = vel.normalized;
                endPos  = card.Rect.anchoredPosition + dir * FastExitDist;
                float angleDeg = -Mathf.Atan2(vel.x, vel.y) * Mathf.Rad2Deg;
                endRot  = Quaternion.Euler(0f, 0f, angleDeg);
                dur     = Mathf.Lerp(0.20f, 0.35f, Mathf.InverseLerp(1500f, FastThreshold, vel.magnitude));
                posEase = Ease.Linear;
            }
            else
            {
                // Click / slow-swipe: arc toss with random jitter.
                float xJitter = UnityEngine.Random.Range(-80f, 80f);
                float zRot    = UnityEngine.Random.Range(-30f, 30f);
                endPos  = card.Rect.anchoredPosition + new Vector2(xJitter, 700f);
                endRot  = card.HomeRotation * Quaternion.Euler(0f, 0f, zRot);
                dur     = 0.45f;
                posEase = Ease.OutCubic;
            }

            // Remove immediately so LayoutRemaining (called right after this) does not
            // re-home the tossed card and overwrite the exit tween.
            _cards.Remove(card);

            var seq = Sequence.Create();
            seq.Group(Tween.UIAnchoredPosition(card.Rect, endPos, dur, posEase));
            seq.Group(Tween.LocalRotation(card.Rect, endRot, dur, Ease.OutQuad));
            seq.Group(Tween.Alpha(card.CanvasGroup, 0f, dur, Ease.InQuad));
            seq.ChainCallback(() => { if (card != null) Destroy(card.gameObject); });
            return seq;
        }

        public void LayoutRemaining()
        {
            LayoutHomes();
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == null) continue;
                Tween.UIAnchoredPosition(card.Rect, card.HomePosition, 0.20f, Ease.OutQuad);
                Tween.LocalRotation(card.Rect, card.HomeRotation, 0.20f, Ease.OutQuad);
            }
        }

        public void CancelInProgress()
        {
            Tween.StopAll(this);
            foreach (var c in _cards) if (c != null) Tween.StopAll(c);
        }

        private void LayoutHomes()
        {
            int n = _cards.Count;
            for (int i = 0; i < n; i++)
            {
                var card = _cards[i];
                if (card == null) continue;
                float t = n == 1 ? 0.5f : (float)i / (n - 1);
                float thetaDeg = Mathf.Lerp(-FanArcDegrees, FanArcDegrees, t);
                float thetaRad = thetaDeg * Mathf.Deg2Rad;
                float x = FanRadius * Mathf.Sin(thetaRad);
                float y = -FanRadius * (1f - Mathf.Cos(thetaRad)) + CenterLift;
                card.HomePosition = new Vector2(x, y);
                card.HomeRotation = Quaternion.Euler(0f, 0f, -thetaDeg);
                card.transform.SetSiblingIndex(i);
            }
        }

        private void ResetCardsToOffscreen()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == null) continue;
                card.Rect.anchoredPosition = new Vector2(card.HomePosition.x, OffscreenY);
                card.Rect.localRotation = Quaternion.identity;
                if (card.CanvasGroup != null) card.CanvasGroup.alpha = 1f;
                card.SetInteractable(true);
            }
        }

        private DraftCardView CreateCard(DefenderUnitData unit, int index)
        {
            var go = new GameObject($"Card_{unit.displayName}",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(_fanRoot, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 1f);
            var group = go.GetComponent<CanvasGroup>();

            // Swatch (top strip — defender base color).
            var swatchGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            swatchGo.transform.SetParent(go.transform, false);
            var swatchRt = (RectTransform)swatchGo.transform;
            swatchRt.anchorMin = new Vector2(0f, 1f);
            swatchRt.anchorMax = new Vector2(1f, 1f);
            swatchRt.pivot = new Vector2(0.5f, 1f);
            swatchRt.anchoredPosition = new Vector2(0f, -10f);
            swatchRt.sizeDelta = new Vector2(-20f, 50f);
            var swatch = swatchGo.GetComponent<Image>();
            swatch.color = unit.visualMaterial != null ? unit.visualMaterial.GetColor("_BaseColor") : Color.white;
            swatch.raycastTarget = false;

            // Stats label.
            var stats = $"{unit.displayName}\n\nHP  {unit.health:0}\nRNG {unit.attackRange:0.##}\nDMG {unit.attackDamage:0}\nCD  {unit.attackCooldown:0.##}s";
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(12f, 12f);
            labelRt.offsetMax = new Vector2(-12f, -68f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = stats;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;

            var view = go.AddComponent<DraftCardView>();
            view.Bind(rt, group, unit);
            view.Discarded += card => CardDiscardRequested?.Invoke(card);
            return view;
        }
    }
}
