using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
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
        // 하스스톤식 3축 아치: 하늘로 솟구쳤다가(rise) 위에서 아래로 내리찍는다(slam).
        // 스크린 Bézier(솟구침) + depth 스케일 펌프(솟을 때 크게=카메라에 가깝게, 내리찍을 때
        // 작게=보드로 꽂힘) + 하강 가속. 내리찍는 순간 = 임팩트(펀치/링/킥)와 일치.
        [Header("Arc — rise")]
        [Tooltip("솟구침 시간(s) — 하늘로 오르며 apex 에서 살짝 hang")]
        [SerializeField] private float riseDur = 0.26f;
        [Tooltip("apex 높이(px, 화면 위쪽). 짧은 비행엔 비례 축소")]
        [SerializeField] private float apexRise = 760f;
        [Tooltip("apex 최소 높이(px) — 근접 타겟도 최소 아치")]
        [SerializeField] private float minApexRise = 280f;
        [Tooltip("apex 가 최대 높이에 도달하는 기준 수평거리(px)")]
        [SerializeField] private float arcRefDist = 560f;
        [Tooltip("apex 수평 위치(start→end 보간, 0.5=중앙 · 타겟 쪽으로 살짝)")]
        [SerializeField] private float apexBias = 0.62f;
        [Tooltip("apex(하늘 정점)에서의 스케일 — depth 펌프(카메라에 가까움)")]
        [SerializeField] private float apexScale = 1.4f;

        [Header("Arc — slam")]
        [Tooltip("내리찍기 시간(s) — 짧고 빠르게(가속 하강)")]
        [SerializeField] private float slamDur = 0.16f;
        [Tooltip("타겟에 꽂히는 순간 스케일 — 작게(보드로 파고듦)")]
        [SerializeField] private float impactScale = 0.72f;
        [Tooltip("비행 중 카드 기울기(도) — apex 에서 최대, 슬램에 0 으로 정렬")]
        [SerializeField] private float tiltDeg = 12f;

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
        // onImpact: 도착(splat) 순간 1회 — 마지막 타겟 월드(view) 좌표 전달(unit 1 묵직 반응 구동).
        public void Fly(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, Camera worldCam,
            Func<Vector3?> worldProvider, Action<Vector3> onImpact = null)
        {
            if (worldProvider == null || worldCam == null || _parentRect == null) return;
            Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(_uiCam, startUiWorld);
            var ghost = CreateGhost(face, ghostSize);
            StartCoroutine(FlyRoutine(ghost, startScreen, worldCam, worldProvider, onImpact));
        }

        // defender-footprint unit 5 — 지연 커밋 비행. onArrive = 도착(slam) 프레임의 커밋 실행,
        // 비행 중 고스트 탭 = onCancel(카드 복귀). «유예 중 효과 미시작»을 되감기가 아니라
        // 커밋 부재로 보장한다(defender-footprint README 계약 9). 동시 여러 발 지원 —
        // 상태가 전부 코루틴 지역이라 발마다 독립이다.
        // ⚠ 커밋이 도착 프레임에 실패해도(호스트 사망 등) 고스트 splat 연출은 완주한다 —
        // 실패 표현은 호출부(카드 복귀 + 복귀음)가 담당한다(드문 경로의 시각 불일치 수용).
        public bool FlyDeferred(Vector3 startUiWorld, Vector2 ghostSize, Sprite face, Camera worldCam,
            Func<Vector3?> worldProvider, Action<Vector3> onArrive, Action onCancel,
            float cancelTapPadPx = 24f)
        {
            if (worldProvider == null || worldCam == null || _parentRect == null) return false;
            Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(_uiCam, startUiWorld);
            var ghost = CreateGhost(face, ghostSize);
            var img = ghost.GetComponent<Image>();
            img.raycastTarget = true; // 취소 탭 대상(루트 캔버스 GraphicRaycaster 재사용)
            img.raycastPadding = new Vector4(-cancelTapPadPx, -cancelTapPadPx, -cancelTapPadPx, -cancelTapPadPx);
            bool cancelled = false;
            ghost.gameObject.AddComponent<GhostCancelTap>().tapped = () => cancelled = true;
            StartCoroutine(FlyRoutine(ghost, startScreen, worldCam, worldProvider, onArrive,
                () => cancelled, onCancel));
            return true;
        }

        // 취소 탭 수신기 — 고스트 전용 최소 컴포넌트(EventTrigger 대신: 할당 1, 의존 0).
        private sealed class GhostCancelTap : MonoBehaviour, IPointerClickHandler
        {
            public Action tapped;
            public void OnPointerClick(PointerEventData eventData) => tapped?.Invoke();
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
            Func<Vector3?> worldProvider, Action<Vector3> onImpact,
            Func<bool> cancelRequested = null, Action onCancel = null)
        {
            SetGhostScreen(ghost, startScreen);
            Vector3 lastWorld = worldCam.transform.position; // fallback
            Vector2 endScreen = startScreen;

            // 아치 높이: 초기 수평거리에 비례(짧은 비행엔 작게, 하한 보장).
            {
                Vector3? w0 = worldProvider();
                if (w0.HasValue) { lastWorld = w0.Value; endScreen = worldCam.WorldToScreenPoint(w0.Value); }
            }
            float initDist = Vector2.Distance(startScreen, endScreen);
            float arcHeight = Mathf.Lerp(minApexRise, apexRise, Mathf.Clamp01(initDist / Mathf.Max(1f, arcRefDist)));

            float total = Mathf.Max(0.02f, riseDur) + Mathf.Max(0.02f, slamDur);
            float e = 0f;
            while (e < total)
            {
                // defender-footprint unit 5 — 지연 커밋 취소(고스트 탭). 도착 프레임 전까지만 유효.
                if (cancelRequested != null && cancelRequested())
                {
                    if (ghost != null) Destroy(ghost.gameObject);
                    onCancel?.Invoke();
                    yield break;
                }
                e += Time.unscaledDeltaTime;

                // 타겟 추적(유닛 행진) — end 를 매프레임 재투영.
                Vector3? w = worldProvider();
                if (w.HasValue) { lastWorld = w.Value; endScreen = worldCam.WorldToScreenPoint(w.Value); }

                // apex = start/end 위쪽(하늘). 수평은 타겟 쪽으로 살짝.
                Vector2 apex = new Vector2(
                    Mathf.Lerp(startScreen.x, endScreen.x, apexBias),
                    Mathf.Max(startScreen.y, endScreen.y) + arcHeight);

                float bez, depth, tilt;
                if (e < riseDur)
                {
                    float u = Mathf.Clamp01(e / riseDur);
                    float ue = 1f - (1f - u) * (1f - u); // EaseOut — apex 에서 감속(hang)
                    bez = 0.5f * ue;                      // Bézier 전반부 → apex 부근
                    depth = Mathf.Lerp(1f, apexScale, ue);
                    tilt = Mathf.Lerp(0f, tiltDeg, ue);
                }
                else
                {
                    float u = Mathf.Clamp01((e - riseDur) / slamDur);
                    float ue = u * u;                     // EaseIn — 가속 내리찍기(slam)
                    bez = 0.5f + 0.5f * ue;               // Bézier 후반부 → 타겟
                    depth = Mathf.Lerp(apexScale, impactScale, ue);
                    tilt = Mathf.Lerp(tiltDeg, 0f, ue);   // 슬램에 정렬
                }

                Vector2 pos = QuadBezier(startScreen, apex, endScreen, bez);
                SetGhostScreen(ghost, pos);
                ghost.localScale = Vector3.one * depth;
                ghost.localRotation = Quaternion.Euler(0f, 0f, tilt);
                yield return null;
            }

            // 내리찍는 순간 — 묵직 반응(유닛 펀치/플래시/링/킥/SFX). slam 착지와 일치.
            SetGhostScreen(ghost, endScreen);
            ghost.localRotation = Quaternion.identity;
            onImpact?.Invoke(lastWorld);

            // 찰싹 splat — 위에서 꽂힌 하강 스쿼시(가로↑ 세로↓)
            yield return LerpScale(ghost, ghost.localScale,
                new Vector3(impactScale * splatScale.x, impactScale * splatScale.y, 1f), splatTime);
            // 흡수 dissolve — scale→0 + alpha→0
            var img = ghost.GetComponent<Image>();
            yield return DissolveOut(ghost, img, dissolveTime);

            if (ghost != null) Destroy(ghost.gameObject);
        }

        private static Vector2 QuadBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float it = 1f - t;
            return it * it * a + 2f * it * t * b + t * t * c;
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
