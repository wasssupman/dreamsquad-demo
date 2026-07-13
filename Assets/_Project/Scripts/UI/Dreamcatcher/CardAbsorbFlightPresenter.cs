using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // card-fly-to-target-absorb unit 0 — 손패 카드(UGUI)가 커밋 성공 시 타겟으로
    // 가속 비행 → 찰싹 splat → 즉시 dissolve. 순수 프레젠테이션(ECS 변경 0).
    // 이동 타겟(유닛 행진) 추적이라 baked tween 대신 자체 Update-follow.
    // 묵직 임팩트 반응(유닛 펀치/흰플래시/링/카메라킥/SFX)은 unit 1(별도 훅).
    //
    // 셰이더 dissolve 대신 scale+alpha 페이드 — UGUI 커스텀 셰이더 채널 함정 회피.
    // 머무름 없음(닿고 ~0.08s 소멸)이라 평평-스티커 문제도 없음.
    public class CardAbsorbFlightPresenter : MonoBehaviour
    {
        [Header("Flight")]
        [Tooltip("초기 속도(px/s, 스크린)")]
        [SerializeField] private float baseSpeed = 700f;
        [Tooltip("가속(px/s^2) — InBack 느낌의 가속 접근")]
        [SerializeField] private float accel = 7000f;
        [Tooltip("도착 판정 거리(px)")]
        [SerializeField] private float arriveDist = 36f;
        [Tooltip("비행 안전 상한(s) — 타겟 소실 시 무한루프 방지")]
        [SerializeField] private float maxFlightTime = 1.2f;
        [Tooltip("임팩트 직전 살짝 커짐(anticipation)")]
        [SerializeField] private float anticipationScale = 1.15f;
        [Tooltip("anticipation 시작 거리(px)")]
        [SerializeField] private float anticipationDist = 130f;

        [Header("Splat / Dissolve")]
        [Tooltip("찰싹 스쿼시 (가로↑ 세로↓)")]
        [SerializeField] private Vector2 splatScale = new Vector2(1.5f, 0.55f);
        [SerializeField] private float splatTime = 0.05f;
        [Tooltip("흡수 소멸 시간(s) — 머물지 않음")]
        [SerializeField] private float dissolveTime = 0.08f;

        private Canvas _canvas;
        private RectTransform _parentRect;
        private Camera _uiCam; // canvas 렌더 모드에 따른 UI 좌표 변환 카메라(overlay=null)

        public void Init(Canvas canvas)
        {
            _canvas = canvas;
            _parentRect = canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
            _uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;
        }

        // startUiWorld: 발사 슬롯 rect.position(캔버스 월드). worldCam: 게임 카메라(월드→스크린).
        // worldProvider: 매프레임 타겟 월드 위치(유닛 행진 추적; null 반환 시 마지막값 유지).
        public void Fly(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, Camera worldCam,
            Func<Vector3?> worldProvider)
        {
            if (worldProvider == null || worldCam == null || _parentRect == null) return;
            Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(_uiCam, startUiWorld);
            var ghost = CreateGhost(face, ghostSize);
            StartCoroutine(FlyRoutine(ghost, startScreen, worldCam, worldProvider));
        }

        private RectTransform CreateGhost(Sprite face, Vector2 size)
        {
            var go = new GameObject("CardAbsorbGhost", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_parentRect, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size.sqrMagnitude > 1f ? size : new Vector2(140f, 190f);
            rt.SetAsLastSibling(); // 카드 위에 렌더
            var img = go.GetComponent<Image>();
            img.sprite = face;
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (face == null) img.color = new Color(1f, 1f, 1f, 0.9f);
            return rt;
        }

        private IEnumerator FlyRoutine(RectTransform ghost, Vector2 startScreen, Camera worldCam,
            Func<Vector3?> worldProvider)
        {
            SetGhostScreen(ghost, startScreen);
            Vector2 cur = startScreen;
            Vector2 lastTarget = startScreen;
            float speed = baseSpeed;
            float t = 0f;

            while (t < maxFlightTime)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;

                Vector3? w = worldProvider();
                Vector2 target = w.HasValue ? (Vector2)worldCam.WorldToScreenPoint(w.Value) : lastTarget;
                lastTarget = target;

                Vector2 dir = target - cur;
                float dist = dir.magnitude;

                if (dist < anticipationDist)
                {
                    float k = 1f - Mathf.Clamp01(dist / anticipationDist);
                    ghost.localScale = Vector3.one * Mathf.Lerp(1f, anticipationScale, k);
                }

                speed += accel * dt;
                float step = speed * dt;
                if (dist <= arriveDist || step >= dist)
                {
                    cur = target;
                    SetGhostScreen(ghost, cur);
                    break;
                }
                cur += dir / dist * step;
                SetGhostScreen(ghost, cur);
                yield return null;
            }

            // 찰싹 splat — 가로↑ 세로↓
            yield return LerpScale(ghost, ghost.localScale, new Vector3(splatScale.x, splatScale.y, 1f), splatTime);
            // 흡수 dissolve — scale→0 + alpha→0
            var img = ghost.GetComponent<Image>();
            yield return DissolveOut(ghost, img, dissolveTime);

            if (ghost != null) Destroy(ghost.gameObject);
        }

        private void SetGhostScreen(RectTransform ghost, Vector2 screen)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, screen, _uiCam, out var local))
                ghost.anchoredPosition = local;
        }

        private IEnumerator LerpScale(RectTransform rt, Vector3 from, Vector3 to, float dur)
        {
            float e = 0f;
            while (e < dur && rt != null)
            {
                e += Time.unscaledDeltaTime;
                rt.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(e / dur));
                yield return null;
            }
            if (rt != null) rt.localScale = to;
        }

        private IEnumerator DissolveOut(RectTransform rt, Image img, float dur)
        {
            Vector3 from = rt != null ? rt.localScale : Vector3.one;
            Color c0 = img != null ? img.color : Color.white;
            float e = 0f;
            while (e < dur && rt != null)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / dur);
                rt.localScale = Vector3.Lerp(from, Vector3.zero, k);
                if (img != null) { var c = c0; c.a = c0.a * (1f - k); img.color = c; }
                yield return null;
            }
        }
    }
}
