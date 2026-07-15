using UnityEngine;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 6 — 레드불 픽업 뷰 (BattleBridge 가 엔티티↔GameObject 조정).
    // BattleBridge 가 AddComponent 직후 Init 호출: modelPrefab(FBX 등) 있으면 그걸, 없으면
    // 절차적 발광 큐브 플레이스홀더. 둘 다 동일하게 bob/spin idle 연출.
    // 좌표는 BattleBridge 가 셀 월드중심(BoardSpace.ToView)으로 세팅.
    public class PickupPresenter : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float spinDegPerSec = 90f;

        private Transform _visual;
        private float _baseLocalY;
        private float _phase;

        // BattleBridge 가 뷰 생성 직후 1회 호출. modelPrefab null → 절차적 큐브.
        // modelScale: 모델 로컬 스케일(FBX 크기 미지수 → 인스펙터 튜닝). baseLocalY: 지면 위 hover 기준.
        public void Init(GameObject modelPrefab, float modelScale, float baseLocalY)
        {
            if (_visual != null) return;

            if (modelPrefab != null)
            {
                var m = Instantiate(modelPrefab, transform);
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                m.transform.localScale = Vector3.one * (modelScale > 0f ? modelScale : 1f);
                StripPhysicsAndShadows(m);
                _visual = m.transform;
            }
            else
            {
                _visual = BuildPlaceholderCube();
            }

            _baseLocalY = baseLocalY;
            var lp = _visual.localPosition;
            _visual.localPosition = new Vector3(lp.x, _baseLocalY, lp.z);
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

        private static void StripPhysicsAndShadows(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private Transform BuildPlaceholderCube()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Redbull_Placeholder";
            var col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col);
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(0.28f, 0.5f, 0.28f);
            cube.transform.localPosition = Vector3.zero;

            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreateEmissiveMaterial(new Color(0.05f, 0.35f, 0.9f)); // Red Bull 블루
            return cube.transform;
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
