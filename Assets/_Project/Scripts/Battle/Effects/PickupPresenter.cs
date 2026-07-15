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

        // 모델을 정규화할 목표 월드 크기(최대 변) — FBX 네이티브 스케일 미지수를 auto-fit 으로 흡수.
        private const float TargetWorldSize = 0.8f;

        // BattleBridge 가 뷰 생성 직후 1회 호출. modelPrefab null → 절차적 큐브.
        // modelScale: auto-fit 결과에 곱하는 미세 배율(기본 1). baseLocalY: 지면 위 hover 기준.
        // overrideMaterial: FBX 임베디드 머티리얼(텍스처 미바인딩)을 덮어쓸 머티리얼(null=원본 유지).
        public void Init(GameObject modelPrefab, float modelScale, float baseLocalY, Material overrideMaterial)
        {
            if (_visual != null) return;
            float mul = modelScale > 0f ? modelScale : 1f;

            if (modelPrefab != null)
            {
                var m = Instantiate(modelPrefab, transform);
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                StripPhysicsAndShadows(m);
                if (overrideMaterial != null) ApplyMaterial(m, overrideMaterial);
                // auto-fit: 렌더러 바운드 최대 변을 TargetWorldSize 로 정규화(네이티브 스케일 무관).
                // localScale 을 절대값으로 덮어쓰지 않는다 — import 보정 스케일을 곱으로 유지.
                float fit = ComputeFitScale(m, TargetWorldSize);
                m.transform.localScale = m.transform.localScale * (fit * mul);
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

        // 인스턴스의 결합 렌더러 바운드 최대 변 → target 배율. 바운드 0 이면 1(폴백).
        private static float ComputeFitScale(GameObject instance, float target)
        {
            var rends = instance.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 1f;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            return maxDim > 1e-4f ? target / maxDim : 1f;
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

        // 모든 렌더러 슬롯을 override 머티리얼로 교체 (텍스처 바인딩된 URP 머티리얼).
        private static void ApplyMaterial(GameObject root, Material mat)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
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
