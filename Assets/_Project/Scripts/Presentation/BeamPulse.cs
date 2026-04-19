using UnityEngine;

namespace Wassup.Presentation
{
    // Phase 8 §13 — Portal link beam alpha pulse.
    //
    // LineRenderer.startColor/endColor 직접 대입은 내부적으로 dirty flag 를
    // partial update 경로로 처리하므로 vertex count 2 수준에서 비용 무시 가능.
    // MaterialPropertyBlock 경로는 LineRenderer vertex color 가 _BaseColor 를
    // 곱하기 구조라 alpha 전달이 불확실 — 직접 대입이 안전.
    //
    // Attached to the LinkBeam GameObject inside the Portal prefab. One
    // instance per portal — no cross-portal coordination.
    [DisallowMultipleComponent]
    public class BeamPulse : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private float frequency = 2.5f;
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.4f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 1.0f;

        private void Awake()
        {
            if (line == null) line = GetComponent<LineRenderer>();
        }

        private void Update()
        {
            if (line == null) return;
            float t = (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(alphaMin, alphaMax, t);
            var sc = line.startColor; sc.a = alpha; line.startColor = sc;
            var ec = line.endColor;   ec.a = alpha; line.endColor = ec;
        }
    }
}
