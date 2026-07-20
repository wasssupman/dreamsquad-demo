using System;
using UnityEngine;

namespace Wassup.Data
{
    // unit-overhead-ui — 1920x1080 reference pixel 기반 공통 레이아웃과 진영별 skin.
    [CreateAssetMenu(fileName = "UnitOverheadUiStyle", menuName = "Wassup/Unit Overhead UI Style", order = 21)]
    public class UnitOverheadUiStyle : ScriptableObject
    {
        [Serializable]
        public struct BarSkin
        {
            [Range(0.1f, 1f)] public float tileWidthFraction;
            public float minWidth;
            public float maxWidth;
            public float height;
            public float inset;
            public float border;
            public float radius;
            public float shadowOffset;
            public Color frame;
            public Color track;
            public Color shadow;
            public Color fillLow;
            public Color fillHigh;
            public Color highlight;
            public float highlightHeight;
            public Color damageTrail;
            [Range(0f, 1f)] public float fullHealthAlpha;
            public bool clippedEnds;
        }

        [Header("Reference pixels")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private float headGap = 5f;
        [SerializeField] private float cardGap = 5f;
        [SerializeField] private float cardHeight = 28.8f;
        [SerializeField] private float cardSpacing = 4f;
        [SerializeField, Range(0.5f, 1f)] private float cardRowTileWidthFraction = 0.92f;
        [SerializeField] private Color cardPlate = new Color(0.035f, 0.055f, 0.09f, 0.96f);
        [SerializeField] private Color unitCardBorder = new Color(0.08f, 0.82f, 0.9f, 1f);
        [SerializeField] private Color squadCardBorder = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private float damageTrailCatchup = 2.8f;

        [Header("Defender — rounded cyan")]
        [SerializeField] private BarSkin defender = new BarSkin
        {
            tileWidthFraction = 0.88f, minWidth = 96f, maxWidth = 148f, height = 14f,
            inset = 2.5f, border = 2.5f, radius = 7f, shadowOffset = 2f,
            frame = new Color(0.018f, 0.03f, 0.045f, 1f),
            track = new Color(0.07f, 0.12f, 0.15f, 0.94f),
            shadow = new Color(0.005f, 0.012f, 0.02f, 0.72f),
            fillLow = new Color(0.05f, 0.58f, 0.66f, 1f), fillHigh = new Color(0.12f, 0.92f, 0.94f, 1f),
            highlight = new Color(0.78f, 1f, 1f, 0.86f), highlightHeight = 1.5f,
            damageTrail = new Color(1f, 0.78f, 0.18f, 0.95f), fullHealthAlpha = 0.94f, clippedEnds = false
        };

        [Header("Enemy — clipped coral")]
        [SerializeField] private BarSkin enemy = new BarSkin
        {
            tileWidthFraction = 0.74f, minWidth = 82f, maxWidth = 124f, height = 12f,
            inset = 2f, border = 2f, radius = 2f, shadowOffset = 2f,
            frame = new Color(0.055f, 0.022f, 0.032f, 1f),
            track = new Color(0.19f, 0.055f, 0.065f, 0.96f),
            shadow = new Color(0.012f, 0.004f, 0.008f, 0.72f),
            fillLow = new Color(0.75f, 0.12f, 0.08f, 1f), fillHigh = new Color(1f, 0.34f, 0.17f, 1f),
            highlight = new Color(1f, 0.78f, 0.58f, 0.8f), highlightHeight = 1.25f,
            damageTrail = new Color(1f, 0.68f, 0.15f, 0.9f), fullHealthAlpha = 0.92f, clippedEnds = true
        };

        public Vector2 ReferenceResolution => referenceResolution;
        public float HeadGap => Mathf.Max(0f, headGap);
        public float CardGap => Mathf.Max(0f, cardGap);
        public float CardHeight => Mathf.Max(1f, cardHeight);
        public float CardSpacing => Mathf.Max(0f, cardSpacing);
        public float CardRowTileWidthFraction => Mathf.Clamp01(cardRowTileWidthFraction);
        public Color CardPlate => cardPlate;
        public Color UnitCardBorder => unitCardBorder;
        public Color SquadCardBorder => squadCardBorder;
        public float DamageTrailCatchup => Mathf.Max(0.01f, damageTrailCatchup);
        public BarSkin Defender => defender;
        public BarSkin Enemy => enemy;

        public static Color EvaluateFill(in BarSkin skin, float ratio)
            => Color.Lerp(skin.fillLow, skin.fillHigh, Mathf.Clamp01(ratio));
    }
}
