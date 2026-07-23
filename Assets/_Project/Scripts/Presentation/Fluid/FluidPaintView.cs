using UnityEngine;
using UnityEngine.UI;

namespace Wassup.Presentation
{
    // fluid-paint-mixing unit 3 — FluidPaintSim 의 dye 출력을 표시 대상(RawImage/Renderer)에 물리는 얇은 어댑터.
    // sim 은 dye RenderTexture 만 만들고 "어디에 그리나"는 이 컴포넌트가 안다(솔버↔표면 분리). unit 4 재사용.
    public sealed class FluidPaintView : MonoBehaviour
    {
        [SerializeField] private FluidPaintSim sim;
        [Tooltip("UI 표시 대상(선택)")]
        [SerializeField] private RawImage rawImage;
        [Tooltip("월드/머티리얼 표시 대상(선택)")]
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("targetRenderer 머티리얼의 텍스처 프로퍼티 이름")]
        [SerializeField] private string texturePropertyName = "_MainTex";

        private int _texId;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _texId = Shader.PropertyToID(texturePropertyName);
            _mpb = new MaterialPropertyBlock();
        }

        // dye 는 sim 이 매 프레임 갱신하는 안정 핸들(DyeTexture=Display RT). 렌더 후에 물리도록 LateUpdate.
        private void LateUpdate()
        {
            if (sim == null || !sim.IsReady) return;
            var tex = sim.DyeTexture;
            if (tex == null) return;

            if (rawImage != null && rawImage.texture != tex) rawImage.texture = tex;

            if (targetRenderer != null)
            {
                targetRenderer.GetPropertyBlock(_mpb);
                _mpb.SetTexture(_texId, tex);
                targetRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
