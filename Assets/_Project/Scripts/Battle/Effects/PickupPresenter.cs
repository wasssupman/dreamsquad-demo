using UnityEngine;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 6 — 레드불 픽업 뷰 (BattleBridge 가 엔티티↔GameObject 조정).
    // BlockingHazardPresenter 동형이되, pickup 은 순수 ECS 스폰이라 BattleBridge 가 매 프레임
    // poll-reconcile 로 생성/파괴한다(이벤트 아님). 좌표는 BattleBridge 가 셀 월드중심으로 세팅.
    // 아트는 플레이스홀더(절차적 발광 큐브 + bob/spin) — 정식 프리팹은 후속.
    public class PickupPresenter : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float spinDegPerSec = 90f;

        private Transform _visual;
        private float _baseLocalY;
        private float _phase;

        private void Awake()
        {
            if (_visual == null)
                BuildPlaceholder();
        }

        private void Update()
        {
            if (_visual == null) return;
            // unscaled: 배치 슬로우모/정지와 무관한 아이들 연출.
            _phase += Time.unscaledDeltaTime;
            float y = _baseLocalY + Mathf.Sin(_phase * bobSpeed) * bobAmplitude;
            var lp = _visual.localPosition;
            _visual.localPosition = new Vector3(lp.x, y, lp.z);
            _visual.Rotate(Vector3.up, spinDegPerSec * Time.unscaledDeltaTime, Space.Self);
        }

        private void BuildPlaceholder()
        {
            // Red Bull 캔 느낌의 플레이스홀더: 살짝 세로로 긴 발광 큐브.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Redbull_Placeholder";
            var col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col); // 물리 불필요
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(0.28f, 0.5f, 0.28f);
            _baseLocalY = 0.25f;
            cube.transform.localPosition = new Vector3(0f, _baseLocalY, 0f);
            _visual = cube.transform;

            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreateEmissiveMaterial(new Color(0.05f, 0.35f, 0.9f)); // Red Bull 블루
        }

        private static Material CreateEmissiveMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            var m = new Material(shader);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.6f);
            }
            return m;
        }
    }
}
