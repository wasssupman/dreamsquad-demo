using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.Presentation
{
    // summon-patrol-defender unit 6 — 아군 이동체 식별 표식(발밑 링).
    //
    // 왜 필요한가: 이 게임에서 **화면을 가로질러 움직이는 것은 지금까지 전부 적이었다**.
    // 순찰병은 적과 같은 스켈레톤·같은 실루엣으로 걸어다니므로, 표식이 없으면 플레이어가
    // 적으로 오독하고 이 유닛의 배치 판단 자체가 성립하지 않는다.
    //
    // 구현: 새 에셋도 씬 배선도 만들지 않는다. 링 스프라이트는 런타임 절차 생성 후 캐시하고,
    // 배치는 기존 발밑 데칼 계층(BlobShadow.Attach, live=true)을 그대로 재사용한다 —
    // 유닛 자식으로 붙어 매 프레임 따라가고 유닛 파괴 시 함께 사라진다.
    //
    // 색/두께는 BoardSortOrder 와 같은 급의 **프레젠테이션 상수**다(유닛 스탯이 아니므로 SO 아님).
    // 육안 튜닝 지점이 여기 한 곳이라는 뜻이기도 하다.
    public static class AllyMarkerDecal
    {
        // 배치 하이라이트와 같은 계열의 청록. 그림자(검정 45%) 위에 겹쳐도 읽히도록 알파를 높게.
        private static readonly Color RingColor = new Color(0.35f, 0.95f, 1f, 0.75f);
        // 블롭 그림자보다 조금 크게 — 그림자를 테두리처럼 감싼다.
        private const float SizeMul = 1.45f;
        // 그림자 **아래**. ShadowOrder+1 은 TileGaugeOrder(-4)와 충돌하므로 반대로 내린다.
        // 링 띠는 SizeMul 덕에 그림자 원 바깥에 놓여서 가려지지 않는다.
        private const int SortingOrder = BoardSortOrder.ShadowOrder - 1;

        private const int TexSize = 64;
        // 정규화 반경. **바깥 페더가 텍스처 경계 안에서 끝나야 한다** — OuterRadius+feather 가
        // 1.0 을 넘으면 상하좌우 끝이 잘려 링이 평평하게 깎인 사각형처럼 보인다(실제로 겪었다).
        private const float InnerRadius = 0.64f;
        private const float OuterRadius = 0.90f;

        private static Sprite _sprite;

        // 절차 생성 링(안이 빈 원). 유닛을 가리지 않으면서 "발밑에 뭔가 있다"만 말하게
        // 안쪽을 비운다 — 꽉 찬 원은 그림자와 구분이 안 되고 캐릭터 발을 덮는다.
        private static Sprite GetOrBuildSprite()
        {
            if (_sprite != null) return _sprite;

            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "AllyMarkerRing",
            };
            var pixels = new Color32[TexSize * TexSize];
            float half = TexSize * 0.5f;
            // 안/밖 경계 각각에 1픽셀 폭의 부드러운 전이를 둔다(계단 방지).
            float feather = 1.5f / half;

            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float inner = Mathf.InverseLerp(InnerRadius - feather, InnerRadius + feather, r);
                float outer = 1f - Mathf.InverseLerp(OuterRadius - feather, OuterRadius + feather, r);
                float a = Mathf.Clamp01(Mathf.Min(inner, outer));

                pixels[y * TexSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);

            _sprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize),
                new Vector2(0.5f, 0.5f), pixelsPerUnit: TexSize);
            _sprite.name = "AllyMarkerRing";
            return _sprite;
        }

        // 유닛 뷰 발밑에 표식을 붙인다. 뷰 GameObject 는 스폰마다 새로 만들어지므로
        // (SpineUnitView.Spawn 이 AddComponent 를 한다) 풀 재사용으로 적에게 남을 위험은 없다.
        public static void Attach(Transform unitViewRoot)
        {
            if (unitViewRoot == null) return;
            BlobShadow.Attach(
                unitViewRoot,
                GetOrBuildSprite(),
                BattleBridge.BlobShadowSize * SizeMul,
                RingColor,
                BattleBridge.BlobShadowGroundY,
                SortingOrder,
                live: true);
        }
    }
}
