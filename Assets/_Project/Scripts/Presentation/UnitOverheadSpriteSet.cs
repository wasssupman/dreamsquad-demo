using System;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Presentation
{
    // unit-overhead-ui review — 동일 style texture를 유닛마다 굽지 않고 Layer 수명 동안 공유한다.
    public sealed class UnitOverheadSpriteSet : IDisposable
    {
        public readonly Sprite defenderBar;
        public readonly Sprite defenderFill;
        public readonly Sprite enemyBar;
        public readonly Sprite enemyFill;

        private readonly UnitOverheadUiStyle _style;
        private Sprite _unitCardFrame;
        private Sprite _squadCardFrame;

        public UnitOverheadSpriteSet(UnitOverheadUiStyle style)
        {
            _style = style;
            var defender = style.Defender;
            var enemy = style.Enemy;
            defenderBar = UiRoundedSprite.Make(defender.radius, defender.border, defender.track, defender.frame);
            defenderFill = UiRoundedSprite.Make(Mathf.Max(1f, defender.radius - defender.inset),
                0f, Color.white, Color.white);
            enemyBar = MakeClippedSprite(enemy.track, enemy.frame, enemy.border);
            enemyFill = MakeClippedSprite(Color.white, Color.white, 0f);
        }

        public Sprite CardFrame(bool squad)
        {
            if (_unitCardFrame == null)
            {
                _unitCardFrame = UiRoundedSprite.Make(2f, 1f, _style.CardPlate, _style.UnitCardBorder);
                _squadCardFrame = UiRoundedSprite.Make(2f, 1f, _style.CardPlate, _style.SquadCardBorder);
            }
            return squad ? _squadCardFrame : _unitCardFrame;
        }

        // 적 전용: defender capsule과 silhouette만으로도 구분되는 clipped-end bar.
        private static Sprite MakeClippedSprite(Color fill, Color border, float borderWidth)
        {
            const int w = 32, h = 12, cut = 3;
            int b = Mathf.Max(0, Mathf.RoundToInt(borderWidth));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int edgeCut = Mathf.Max(0, cut - Mathf.Min(y, h - 1 - y));
                bool inside = x >= edgeCut && x < w - edgeCut;
                bool inner = x >= edgeCut + b && x < w - edgeCut - b && y >= b && y < h - b;
                px[y * w + x] = inside ? (Color32)(inner ? fill : border) : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(5f, 3f, 5f, 3f));
        }

        public void Dispose()
        {
            Release(defenderBar);
            Release(defenderFill);
            Release(enemyBar);
            Release(enemyFill);
            Release(_unitCardFrame);
            Release(_squadCardFrame);
        }

        private static void Release(Sprite sprite)
        {
            if (sprite == null) return;
            if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }
    }
}
