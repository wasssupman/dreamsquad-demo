using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Data.BattleView;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 8a — **"퇴근 중"** 연출. 옛 `UI/DefenderRetireFlight.cs`(305줄, defender-clock-out
    // unit 3 rev 5)의 복사·적응본이다. 장부 bridge-fields 33 `retireFlight`(「5c」 배정 — 5c 가 잇지 않았다)의 새 주인.
    // 8a 대조표 재측정에서 드러난 행이다: 옛 씬에서 살아 있는데 새 씬에서는 퇴근한 유닛이 그냥 사라졌다.
    //
    // 3막 (~1.6초): ① 연결(줄이 내려와 걸린다) → ② 저항(발이 박힌 채 움찔) → ③ 뽑힘(카메라로 왔다가
    // 뱅글뱅글 돌며 화면 밖). 튜닝 값은 옛 씬 저작 그대로다(`BattleScene.unity` DefenderRetireFlight 직렬화 = 아래 기본값).
    //
    // 바뀐 것:
    //   · 구동 — 옛 것은 브리지가 퇴근 확정 시 `Fly()` 를 불렀다(`BattleBridge.cs:4459~4461`). 여기는 자기 구독
    //     (`Retired` 사건 — 코어가 소멸보다 **먼저** 낸다)으로 받아 유닛 풀에서 뷰를 떼어 온다(`TryDetach`).
    //   · 키링 하드웨어 — 옛 드래그 컨트롤러의 팩토리를 부르던 것을, 같은 저작(`DragSwaySettings`·`KeyringStyle`)을
    //     읽는 복사본으로 둔다. ⚠ 절차적 폴백의 머티리얼은 `Shader.Find` 대신 `RuntimeMaterialFactory` 를 지난다.
    //   · 스케일 — 브리지 static(`CharacterVisualScale`) 대신 `CharacterViewConfig`.
    //
    // ⚠ 회전은 `Billboard`(Tilted) 컴포넌트가 매 LateUpdate 로 소유한다. 뱅글뱅글을 위해 떼어낸 뷰에서
    // 그것을 **끄고 회전을 인수**한다(안 끄면 다음 프레임에 덮인다).
    [DisallowMultipleComponent]
    public sealed class CoreRetireFlightPresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;
        [Tooltip("뽑히는 순간 떠난 칸에 칠 링. 배치 때 나는 그 링과 같은 것.")]
        [SerializeField] private CoreVfxSpawner _vfx;
        [Tooltip("키링(고리+줄) 저작. 배치 D&D 와 같은 SO.")]
        [SerializeField] private DragSwaySettings _keyringConfig;
        [SerializeField] private CharacterViewConfig _characterView;

        [Header("① 연결")]
        [Tooltip("줄이 내려와 걸리기까지(초, Battle 도메인)")]
        [SerializeField] private float hookSeconds = 0.25f;
        [Tooltip("줄이 얼마나 위에서 내려오나(view 단위)")]
        [SerializeField] private float hookDropDistance = 6f;

        [Header("② 저항")]
        [Tooltip("버티는 시간(초). rev 5 — 1.75 는 너무 길었다(사용자)")]
        [SerializeField] private float resistSeconds = 0.85f;
        [Tooltip("저항 중 실제로 뽑혀 나오는 거리(view 단위). 작을수록 '박혀 있다'")]
        [SerializeField] private float resistRise = 0.5f;
        [Tooltip("움찔 진동 주기(Hz)")]
        [SerializeField] private float wiggleHz = 13f;
        [Tooltip("움찔 좌우 진폭(view 단위, 구간 끝 기준). 앞쪽은 작게 시작한다")]
        [SerializeField] private float wiggleAmplitude = 0.16f;
        [Tooltip("움찔 회전 진폭(도, 구간 끝 기준)")]
        [SerializeField] private float wiggleTiltDegrees = 9f;
        [Tooltip("장력으로 몸이 늘어나는 정도(구간 끝 세로 배율)")]
        [SerializeField] private float tensionStretchY = 1.18f;

        [Header("③ 뽑힘 — 카메라로 왔다가 멀어지며 이탈")]
        [SerializeField] private float popSeconds = 0.5f;
        [Tooltip("올라가는 거리(view 단위)")]
        [SerializeField] private float popRise = 7f;
        [Tooltip("옆으로 튕기는 거리(view 단위)")]
        [SerializeField] private float popLateral = 1.8f;
        [Tooltip("뱅글뱅글 총 회전량(도). 1440 = 4바퀴")]
        [SerializeField] private float popSpinDegrees = 1440f;
        [Tooltip("카메라 쪽으로 얼마나 다가오나(view 단위). 퍼스펙티브라 실제로 커진다")]
        [SerializeField] private float popApproachDistance = 7f;
        [Tooltip("그 뒤 멀어지는 거리(view 단위) — 화면 밖까지")]
        [SerializeField] private float popRecedeDistance = 26f;
        [Tooltip("다가오는 데 쓰는 구간 비율(0~1). 나머지는 멀어진다")]
        [SerializeField, Range(0.05f, 0.9f)] private float popApproachFraction = 0.3f;
        [Tooltip("발사 순간 세로로 늘어나는 배율. 곧 1 로 돌아온다 — 회전 중엔 균일해야 안 깨진다")]
        [SerializeField] private float popLaunchStretch = 1.35f;

        private const int RingSegments = 14;

        // 동시 퇴근이 가능하므로 **단일 슬롯이 아니라 목록**이다. teardown 에서 뷰와 키링 루트를 **쌍으로** 정리한다.
        private readonly List<(CoreUnitView view, GameObject keyringRoot)> _inFlight =
            new List<(CoreUnitView, GameObject)>();
        private Material _cordMaterial;

        /// <summary>진행 중인 퇴근 연출 수. 테스트의 증언 창.</summary>
        public int InFlightCount => _inFlight.Count;

        /// <summary>시작한 퇴근 연출 수(판 동안 누적).</summary>
        public int FlownCount { get; private set; }

        private void OnEnable()
        {
            FlownCount = 0;
            if (_driver != null) _driver.Subscribe(ViewOrder.Unit, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            CancelAll();
        }

        private void OnDestroy()
        {
            if (_cordMaterial != null) Destroy(_cordMaterial);
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.Retired:
                    // `Arg` = 정의표 줄 번호. 뷰는 이 순간부터 여기 것이다 — 뒤따르는 `UnitDestroyed` 에서 풀은
                    // 그 뷰를 모른다(옛 `SpineUnitPool.Detach` 의 계약).
                    if (_units == null || !_units.TryDetach(e.A, out var view)) return;
                    var defenders = _driver.DefenderAssets;
                    var data = e.Arg >= 0 && e.Arg < defenders.Count ? defenders[e.Arg] : null;
                    Fly(view, (Vector3)BoardSpace.ToView(e.SiteFired.Pos), data);
                    return;

                case CoreEventKind.MatchStarted:
                    CancelAll();
                    return;
            }
        }

        // 옛 `Fly` 그대로. ringView = 링을 칠 **view** 좌표(떠난 자리 = 발밑).
        public void Fly(CoreUnitView view, Vector3 ringView, DefenderUnitData unitData)
        {
            if (view == null) return;
            // 그림자는 지면에 남는다 — 유닛이 뽑혀 올라가는 동안 원래 칸에 원본 크기로 눌러앉아
            // "아직 저기 있다"로 읽힌다. 떠나는 연출이므로 함께 걷어낸다.
            var blob = view.GetComponentInChildren<BlobShadow>(true);
            if (blob != null) Destroy(blob.gameObject);

            // 회전 인수 — Billboard 가 살아 있으면 매 LateUpdate 에 우리 회전이 덮인다.
            var billboard = view.GetComponent<Billboard>();
            if (billboard != null) billboard.enabled = false;

            // 스케일 인수 — 뷰는 자기 코루틴(펀치·스쿼시)으로 localScale 을 덮는다. 여기서 멈춰야
            // "소유자가 하나뿐" 이 실제로 참이 된다(옛 코드리뷰 2026-08-15).
            view.StopAllCoroutines();

            // ⚠ **등록을 코루틴 안이 아니라 여기서** 한다 — 떼어낸 뷰가 풀에도 목록에도 없는 유령이 되지 않게.
            _inFlight.Add((view, null));
            FlownCount++;
            StartCoroutine(Run(view, ringView, unitData));
        }

        private void AttachKeyringRoot(CoreUnitView view, GameObject root)
        {
            for (int i = 0; i < _inFlight.Count; i++)
                if (_inFlight[i].view == view) { _inFlight[i] = (view, root); return; }
        }

        private IEnumerator Run(CoreUnitView view, Vector3 ringView, DefenderUnitData unitData)
        {
            Vector3 basePos = view.transform.position;
            Vector3 baseScale = view.transform.localScale;
            Quaternion baseRot = view.transform.rotation; // Billboard 가 마지막에 세운 틸트가 기준
            var cam = Camera.main;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            Vector3 right = cam != null
                ? Vector3.ProjectOnPlane(cam.transform.right, BoardSpace.RaycastPlane().normal).normalized
                : Vector3.right;
            Vector3 spinAxis = cam != null ? cam.transform.forward : Vector3.forward; // 화면 평면 회전

            // 뽑는 주체. 하드웨어는 옛 `CreateKeyringHardware → BuildRingAndCord` 의 복사(아래 `CreateKeyring`)이고
            // 값은 같은 `DragSwaySettings`·`KeyringStyle` 에서 온다 — 룩이 저절로 일치한다. 미배선이면
            // 키링 없이 모션만 — 연출은 게임 규칙을 하나도 소유하지 않는다.
            var keyring = CreateKeyring(unitData);
            AttachKeyringRoot(view, keyring.root);

            // ⚠ localScale/rotation 직접 대입 — UnitView 는 "스케일 쓰기의 단일 지점"을,
            // Billboard 는 회전을 요구하지만 둘 다 **경합 소유자가 있을 때**의 규칙이다.
            // Fly 가 매 프레임 피드(끊김)·Billboard·뷰 자체 코루틴을 전부 정리했으므로
            // 여기서는 소유자가 하나뿐이다.

            // ── ① 연결 ───────────────────────────────────────────────────────
            float t = 0f, dur = Mathf.Max(0.01f, hookSeconds);
            while (t < 1f)
            {
                if (view == null) { Finish(view, keyring); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / dur;
                float k = Mathf.Clamp01(t);
                float e = 1f - (1f - k) * (1f - k); // OutQuad — 줄이 탁 내려온다
                // 걸리는 순간 한 번 움찔: 구간 끝 20% 에서만 짧게 흔든다.
                float hit = k > 0.8f ? Mathf.Sin((k - 0.8f) / 0.2f * Mathf.PI) : 0f;
                view.transform.position = basePos + right * (hit * wiggleAmplitude * 0.5f);
                DrawKeyring(keyring, view, up, hookDropDistance * (1f - e));
                yield return null;
            }

            // ── ② 저항 — 박혀서 움찔거린다 ────────────────────────────────────
            // 고리는 계속 올라가는데 유닛은 resistRise 만큼만 따라온다. 그 **차이가 장력**이고,
            // 장력이 몸을 늘리고 진동을 키운다. 세 값(고리 높이·몸 늘어남·진동 진폭)이 같은
            // 진행도 하나에서 나오므로 서로 어긋나지 않는다.
            float elapsed = 0f;
            float total = Mathf.Max(0.01f, resistSeconds);
            float phase = 0f;
            while (elapsed < total)
            {
                if (view == null) { Finish(view, keyring); yield break; }
                float dt = TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                elapsed += dt;
                phase += dt * wiggleHz;
                float p = Mathf.Clamp01(elapsed / total);          // 장력 진행도
                // rev 5 — 구간이 0.85초로 짧아졌다. p² 램프는 앞 절반이 거의 정지라 짧은 창을
                // 낭비한다. 바닥값을 깔아 **처음부터 떨고 갈수록 심해지게** 바꾼다.
                float ramp = Mathf.Lerp(0.35f, 1f, p);
                float w = Mathf.Sin(phase * Mathf.PI * 2f);

                view.transform.position = basePos
                    + up * (resistRise * p)
                    + right * (w * wiggleAmplitude * ramp);
                view.transform.localScale = Vector3.Scale(baseScale,
                    new Vector3(1f, Mathf.Lerp(1f, tensionStretchY, p), 1f));
                view.transform.rotation =
                    baseRot * Quaternion.AngleAxis(w * wiggleTiltDegrees * ramp, spinAxis);
                DrawKeyring(keyring, view, up, 0f);
                yield return null;
            }

            // ── ③ 뽑힘 ───────────────────────────────────────────────────────
            // 링은 **뽑히는 순간**에 친다. 연결/저항 때 치면 배치처럼 읽히고, 끝나고 치면 늦다.
            if (_vfx != null) _vfx.SpawnPlacementRing(ringView);

            Vector3 popFrom = basePos + up * resistRise;
            Vector3 tensionScale = Vector3.Scale(baseScale, new Vector3(1f, tensionStretchY, 1f));
            Vector3 launchScale = Vector3.Scale(baseScale, new Vector3(1f, popLaunchStretch, 1f));
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
            float approachFrac = Mathf.Clamp(popApproachFraction, 0.05f, 0.9f);

            t = 0f; dur = Mathf.Max(0.01f, popSeconds);
            while (t < 1f)
            {
                if (view == null) { Finish(view, keyring); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / dur;
                float k = Mathf.Clamp01(t);
                float e = k >= 1f ? 1f : 1f - Mathf.Pow(2f, -9f * k); // OutExpo — 팡

                // rev 5 — **카메라로 왔다가 멀어진다.** 퍼스펙티브(fov 36)라 -forward 로
                // 다가가면 실제로 커지므로 별도 스케일 조작이 필요 없다(중복 적용 금지 —
                // 원근이 이미 하는 일을 스케일로 또 하면 두 배로 부푼다).
                // ⚠ 소팅은 sortingOrder 가 소유하므로 앞으로 나와도 다른 것에 가려지지 않는다.
                float depth = k < approachFrac
                    // ㉠ 다가옴: 0 → -approach. OutQuad 로 훅 다가온다.
                    ? -popApproachDistance * (1f - Mathf.Pow(1f - k / approachFrac, 2f))
                    // ㉡ 멀어짐: -approach → +recede. InQuad 로 가속하며 빠진다.
                    : Mathf.Lerp(-popApproachDistance, popRecedeDistance,
                                 Mathf.Pow((k - approachFrac) / (1f - approachFrac), 2f));

                view.transform.position = popFrom
                    + up * (popRise * e)
                    + right * (popLateral * e)
                    + camFwd * depth;

                // 발사 순간만 늘어나고 곧 **균일 배율로 복귀**한다 — 회전 중에 비균일 스케일이
                // 남아 있으면 매 프레임 실루엣이 찌그러져 보인다.
                float settle = Mathf.Clamp01(k / 0.35f);
                view.transform.localScale = Vector3.Lerp(
                    Vector3.Lerp(tensionScale, launchScale, Mathf.Clamp01(k / 0.12f)),
                    baseScale, settle);

                // 뱅글뱅글 — 회전은 **선형**으로 돌린다(감속시키면 도는 게 멈춰 보인다).
                // 축은 camera.forward = 화면 평면 회전 = 보이는 그대로의 z축 스핀.
                view.transform.rotation = baseRot * Quaternion.AngleAxis(popSpinDegrees * k, spinAxis);
                DrawKeyring(keyring, view, up, 0f); // 줄이 딸려 올라가며 같이 사라진다
                yield return null;
            }

            Finish(view, keyring);
        }

        // ── 키링 하드웨어 ─────────────────────────────────────────────────────
        // 옛 `DefenderDragPlacementController.KeyringHardware`·`CreateKeyringHardware`·`BuildRingAndCord`
        // (`:1811~1890`)의 복사. 위치는 매 프레임 `DrawKeyring` 이 정한다. 머티리얼은 공유 — 루트만 파괴한다.
        private readonly struct KeyringHardware
        {
            public readonly GameObject root;
            public readonly Transform ring;
            public readonly LineRenderer cord;
            public readonly float ropeWorld;
            public readonly bool valid;
            public KeyringHardware(GameObject root, Transform ring, LineRenderer cord, float ropeWorld)
            { this.root = root; this.ring = ring; this.cord = cord; this.ropeWorld = ropeWorld; valid = root != null; }
        }

        private KeyringHardware CreateKeyring(DefenderUnitData unitData)
        {
            var cfg = _keyringConfig;
            if (unitData == null || cfg == null) return default;
            float charScale = _characterView != null ? _characterView.CharacterScale : 1f;
            float scale = Mathf.Max(0.01f, unitData.spineVisualScale * charScale);
            float tilt = _characterView != null ? _characterView.BillboardTilt : 45f;
            var root = new GameObject("RetireKeyring");
            root.transform.SetParent(transform, false);
            var st = cfg.style;

            var ringGo = new GameObject("RetireKeyring_Ring");
            ringGo.transform.SetParent(root.transform, false);
            if (st != null && st.ringSprite != null)
            {
                var ringSr = ringGo.AddComponent<SpriteRenderer>();
                ringSr.sprite = st.ringSprite;
                if (st.worldRingMaterial != null) ringSr.sharedMaterial = st.worldRingMaterial;
                ringSr.color = Color.white;
                ringSr.sortingOrder = BoardSortOrder.DragPreviewOrder;
                float spriteWidth = st.ringSprite.bounds.size.x;
                if (spriteWidth > 1e-4f)
                    ringGo.transform.localScale = Vector3.one * (cfg.ringRadius * 2f * scale / spriteWidth);
            }
            else
            {
                var ringLr = ringGo.AddComponent<LineRenderer>();
                ringLr.useWorldSpace = false;
                ringLr.loop = true;
                ringLr.numCapVertices = 2;
                ringLr.positionCount = RingSegments;
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                    ringLr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (cfg.ringRadius * scale));
                }
                ringLr.sharedMaterial = CordMaterial(cfg.cordColor);
                ringLr.widthMultiplier = cfg.cordWidth * scale;
                ringLr.startColor = ringLr.endColor = cfg.cordColor;
                ringLr.sortingOrder = BoardSortOrder.DragPreviewOrder;
            }
            ringGo.AddComponent<Billboard>().Setup(BillboardMode.Tilted, tilt);

            var cordGo = new GameObject("RetireKeyring_Cord");
            cordGo.transform.SetParent(root.transform, false);
            var cord = cordGo.AddComponent<LineRenderer>();
            cord.useWorldSpace = true;
            cord.numCapVertices = 2;
            cord.positionCount = 2;
            bool styledCord = st != null && st.worldCordMaterial != null;
            cord.sharedMaterial = styledCord ? st.worldCordMaterial : CordMaterial(cfg.cordColor);
            cord.widthMultiplier = cfg.cordWidth * scale;
            cord.startColor = cord.endColor = styledCord ? Color.white : cfg.cordColor;
            cord.sortingOrder = BoardSortOrder.DragPreviewOrder - 1;

            return new KeyringHardware(root, ringGo.transform, cord, cfg.ropeLength * scale);
        }

        // 스타일이 없을 때만 쓰는 폴백(라이브 SO 는 스타일이 있다). `Shader.Find` 금지 → 팩토리.
        private Material CordMaterial(Color color)
        {
            if (_cordMaterial == null) _cordMaterial = Wassup.Rendering.RuntimeMaterialFactory.CreateTransparent(color);
            return _cordMaterial;
        }

        private static void DrawKeyring(KeyringHardware kr,
            CoreUnitView view, Vector3 up, float extraLift)
        {
            if (!kr.valid || view == null) return;
            Vector3 head = view.transform.position + up * view.ApproxWorldHeight;
            Vector3 ringPos = head + up * (kr.ropeWorld + extraLift);
            kr.ring.position = ringPos;
            kr.cord.SetPosition(0, ringPos);
            kr.cord.SetPosition(1, head);
        }

        private void Finish(CoreUnitView view, KeyringHardware keyring)
        {
            // 머티리얼은 드래그 컨트롤러 공유 — 루트만 파괴한다(재배치 HideKeyring 과 동일 규약).
            if (keyring.root != null) Destroy(keyring.root);
            for (int i = _inFlight.Count - 1; i >= 0; i--)
                if (_inFlight[i].view == view) _inFlight.RemoveAt(i);
            if (view != null) view.Dispose();
        }

        // 진행 중 연출을 무효화하고 뷰·키링을 전부 치운다. 풀이 더 이상 모르는 뷰라 여기서 안 치우면
        // **고아 GameObject 로 남는다**(Detach 의 계약).
        public void CancelAll()
        {
            StopAllCoroutines();
            for (int i = 0; i < _inFlight.Count; i++)
            {
                if (_inFlight[i].keyringRoot != null) Destroy(_inFlight[i].keyringRoot);
                if (_inFlight[i].view != null) _inFlight[i].view.Dispose();
            }
            _inFlight.Clear();
        }
    }
}
