using System;
using TMPro;
using UnityEngine;

namespace Wassup.Presentation
{
    // World-space floating damage number. Self-animates (punch scale-in, upward
    // drift, fade-out), billboards to the battle camera, then reports completion so
    // the pool can recycle it. Magnitude drives font size + face color + punch via
    // the spawner-provided DamageNumberStyle.
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageNumberView : MonoBehaviour
    {
        private TextMeshPro _tmp;
        private MeshRenderer _renderer;
        private Camera _camera;
        private DamageNumberStyle _style;
        private Action<DamageNumberView> _onComplete;

        private Vector3 _startPos;
        private float _elapsed;
        private float _lifetime;
        private float _punchT;
        private Color _faceColor;
        private bool _playing;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _renderer = GetComponent<MeshRenderer>();
            // Render above unit sprites/meshes.
            if (_renderer != null) _renderer.sortingOrder = 32000;
        }

        public void Play(int amount, Vector3 worldPos, Camera cam, DamageNumberStyle style, Action<DamageNumberView> onComplete)
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshPro>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            _camera = cam;
            _style = style;
            _onComplete = onComplete;
            _startPos = worldPos;
            _lifetime = Mathf.Max(0.05f, style.lifetime);

            float t = style.Normalize(amount);
            _punchT = t;
            _tmp.fontSize = Mathf.Lerp(style.minFontSize, style.maxFontSize, t);
            _tmp.text = amount.ToString();
            _faceColor = style.EvaluateColor(t);

            transform.position = worldPos;
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);
            _elapsed = 0f;
            _playing = true;
            ApplyFrame(0f);
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.deltaTime;
            float n = _elapsed / _lifetime;
            if (n >= 1f) { Finish(); return; }
            ApplyFrame(n);
        }

        private void ApplyFrame(float n)
        {
            transform.position = _startPos + Vector3.up * (_style.driftUp * n);

            float curve = _style.scaleCurve != null && _style.scaleCurve.length > 0
                ? _style.scaleCurve.Evaluate(n)
                : 1f;
            // Amplify the overshoot/deviation-from-1 for big hits.
            float punchMul = Mathf.Lerp(1f, Mathf.Max(1f, _style.bigHitPunchMul), _punchT);
            float scale = 1f + (curve - 1f) * punchMul;
            transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);

            float a = _style.alphaCurve != null && _style.alphaCurve.length > 0
                ? _style.alphaCurve.Evaluate(n)
                : 1f - n;
            var c = _faceColor; c.a = a;
            _tmp.color = c;
        }

        private void LateUpdate()
        {
            if (!_playing || _camera == null) return;
            // Full billboard: copy camera rotation so the number stays readable.
            transform.rotation = _camera.transform.rotation;
        }

        private void Finish()
        {
            _playing = false;
            gameObject.SetActive(false);
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke(this);
        }
    }
}
