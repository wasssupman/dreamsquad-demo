using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-health-display unit 3 — 방어유닛 점유 타일 테두리 게이지.
    // 바닥에 눕힌(Euler 90,0,0 = BlobShadow 규약) 사각 테두리를 4 edge 스프라이트로
    // 그리고, HP 비율만큼 시계방향(상단 시작)으로 채운다. 색은 gaugeColorGradient(녹→적).
    // 값은 전부 HealthDisplayStyle. TileHealthGaugeLayer 가 풀링/셀 관리.
    public class TileHealthGaugeView : MonoBehaviour
    {
        private static Sprite _sprite;
        private SpriteRenderer[] _edges; // 0=top 1=right 2=bottom 3=left
        private bool _built;

        // tileCenterView: 타일 중심(view 좌표, 바닥). tileWorldSize: tileSize.
        public void Set(Vector3 tileCenterView, float tileWorldSize, float ratio, HealthDisplayStyle style)
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            transform.position = tileCenterView + Vector3.up * style.GaugeYOffset;

            float r = HealthDisplayStyle.SafeRatio01(ratio);
            Color col = style.EvaluateGaugeColor(r);
            float s = tileWorldSize * style.GaugeTileFill;
            float t = style.GaugeThickness;
            float half = s * 0.5f;

            // 각 변 = 둘레의 1/4. 시계방향 top→right→bottom→left 순으로 채워진다.
            float ft = EdgeFill(r, 0);
            float fr = EdgeFill(r, 1);
            float fb = EdgeFill(r, 2);
            float fl = EdgeFill(r, 3);

            // 로컬 XY(눕힌 뒤 월드 XZ 에 대응). 각 변은 채워진 구간의 중심에 놓고 그만큼 스케일.
            SetEdge(0, new Vector3(-half + s * ft * 0.5f, half, 0f), new Vector3(s * ft, t, 1f), col); // top → 오른쪽
            SetEdge(1, new Vector3(half, half - s * fr * 0.5f, 0f), new Vector3(t, s * fr, 1f), col);  // right ↓
            SetEdge(2, new Vector3(half - s * fb * 0.5f, -half, 0f), new Vector3(s * fb, t, 1f), col);  // bottom ← 왼쪽
            SetEdge(3, new Vector3(-half, -half + s * fl * 0.5f, 0f), new Vector3(t, s * fl, 1f), col); // left ↑
        }

        // 둘레 시계방향 fill: 각 변(edge 0=top,1=right,2=bottom,3=left)이 [k/4,(k+1)/4] 구간을 담당.
        // ratio 는 SafeRatio01 후 값 가정. 순수 함수 — EditMode 테스트 대상.
        public static float EdgeFill(float ratio01, int edgeIndex)
            => Mathf.Clamp01((ratio01 - edgeIndex * 0.25f) / 0.25f);

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void SetEdge(int i, Vector3 localPos, Vector3 localScale, Color col)
        {
            var e = _edges[i];
            e.transform.localPosition = localPos;
            e.transform.localScale = localScale;
            e.color = col;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 바닥에 눕힘
            _edges = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("edge" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = WhiteSprite();
                sr.sortingOrder = BoardSortOrder.TileGaugeOrder;
                _edges[i] = sr;
            }
        }

        // 1x1 월드 유닛 흰 스프라이트(shared). center pivot. localScale 로 실제 크기.
        private static Sprite WhiteSprite()
            => _sprite != null ? _sprite
             : (_sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4),
                    new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect));
    }
}
