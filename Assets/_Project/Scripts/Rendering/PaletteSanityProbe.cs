using UnityEngine;

namespace Wassup.Rendering
{
    // Sanity diagnostic for RuntimeMaterialFactory + Tile_Unlit / URP Unlit
    // shader paths. Drop this on an empty GameObject in a fresh scene, assign
    // the two textures, and Play. Six quads render in a row showing tint and
    // transparency outcomes in isolation from MapView caches and theme rules.
    //
    // Expected results:
    //   [0] white opaque    : texture, no colour shift
    //   [1] red opaque      : texture multiplied by red
    //   [2] blue opaque     : texture multiplied by blue
    //   [3] white transparent (alpha 0.4) : faded texture, sees background
    //   [4] red transparent  (alpha 0.4)  : red-faded texture, sees background
    //   [5] composite       : opaque [0] under, transparent red overlay
    //
    // If [1]/[2] are not red/blue: opaque tint path is broken.
    // If [3]/[4] do not blend with background: transparent surface mode broken.
    // If [5] does not show red tint over opaque: overlay path broken.
    public class PaletteSanityProbe : MonoBehaviour
    {
        [SerializeField] private Texture2D _opaqueTexture;
        [SerializeField] private Texture2D _transparentTexture;
        [SerializeField] private float _quadSize = 1.5f;
        [SerializeField] private float _spacing = 0.3f;
        [SerializeField] private Color _backdropColour = new Color(0.15f, 0.18f, 0.22f, 1f);

        private void Start()
        {
            BuildBackdrop();
            float step = _quadSize + _spacing;

            BuildQuad("0_OpaqueWhite", -2.5f * step,
                RuntimeMaterialFactory.CreateOpaqueTexture(_opaqueTexture, Color.white));
            BuildQuad("1_OpaqueRed", -1.5f * step,
                RuntimeMaterialFactory.CreateOpaqueTexture(_opaqueTexture, Color.red));
            BuildQuad("2_OpaqueBlue", -0.5f * step,
                RuntimeMaterialFactory.CreateOpaqueTexture(_opaqueTexture, Color.blue));
            BuildQuad("3_TransparentWhite", 0.5f * step,
                RuntimeMaterialFactory.CreateTransparentTexture(_transparentTexture, new Color(1f, 1f, 1f, 0.4f)));
            BuildQuad("4_TransparentRed", 1.5f * step,
                RuntimeMaterialFactory.CreateTransparentTexture(_transparentTexture, new Color(1f, 0f, 0f, 0.4f)));

            BuildQuad("5a_Composite_Under", 2.5f * step,
                RuntimeMaterialFactory.CreateOpaqueTexture(_opaqueTexture, Color.white));
            BuildQuad("5b_Composite_Over", 2.5f * step,
                RuntimeMaterialFactory.CreateTransparentTexture(_transparentTexture, new Color(1f, 0f, 0f, 0.55f)),
                yOffset: 0.01f);
        }

        private void BuildBackdrop()
        {
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            backdrop.transform.SetParent(transform, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            backdrop.transform.localScale = new Vector3(20f, 4f, 1f);
            DestroyImmediate(backdrop.GetComponent<Collider>());
            var r = backdrop.GetComponent<Renderer>();
            r.sharedMaterial = RuntimeMaterialFactory.CreateOpaque(_backdropColour);
        }

        private void BuildQuad(string label, float x, Material material, float yOffset = 0f)
        {
            if (material == null)
            {
                Debug.LogWarning($"[PaletteSanityProbe] {label}: material was null. Texture missing?");
                return;
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = label;
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(x, yOffset, 0f);
            quad.transform.localScale = new Vector3(_quadSize, _quadSize, 1f);
            DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
