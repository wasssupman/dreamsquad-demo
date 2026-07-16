using UnityEngine;

namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout unit 1 — 사직서 뷰 (BattleBridge 가 엔티티↔GameObject 조정).
    // prefab 있으면 그걸, 없으면 절차적 플레이스홀더(흰 종이). idle 부양(unscaled — 정지/슬로우모 무관).
    // 좌표는 BattleBridge 가 셀 월드중심(BoardSpace.ToView)으로 세팅. PickupPresenter 동형(단순화).
    public class ResignationPresenter : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 1.8f;

        private Transform _visual;
        private float _baseLocalY;
        private float _phase;

        // BattleBridge 가 뷰 생성 직후 1회 호출. prefab null → 절차적 흰 종이.
        public void Init(GameObject prefab, float baseLocalY)
        {
            if (_visual != null) return;
            _visual = prefab != null ? BuildFromPrefab(prefab) : BuildPlaceholder();
            _baseLocalY = baseLocalY;
            var lp = _visual.localPosition;
            _visual.localPosition = new Vector3(lp.x, _baseLocalY, lp.z);
        }

        private Transform BuildFromPrefab(GameObject prefab)
        {
            var m = Instantiate(prefab, transform);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            foreach (var col in m.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var r in m.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return m.transform;
        }

        // 흰 종이 느낌 — 얇고 넓은 박스를 살짝 눕힘.
        private Transform BuildPlaceholder()
        {
            var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "Resignation_Placeholder";
            var col = paper.GetComponent<Collider>();
            if (col != null) Destroy(col);
            paper.transform.SetParent(transform, false);
            paper.transform.localScale = new Vector3(0.34f, 0.44f, 0.04f);
            paper.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            paper.transform.localPosition = Vector3.zero;

            var renderer = paper.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreatePaperMaterial();
            return paper.transform;
        }

        private static Material CreatePaperMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            var m = new Material(shader);
            var c = new Color(0.96f, 0.96f, 0.92f); // 미색 종이
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        private void Update()
        {
            if (_visual == null) return;
            _phase += Time.unscaledDeltaTime;
            float y = _baseLocalY + Mathf.Sin(_phase * bobSpeed) * bobAmplitude;
            var lp = _visual.localPosition;
            _visual.localPosition = new Vector3(lp.x, y, lp.z);
        }
    }
}
