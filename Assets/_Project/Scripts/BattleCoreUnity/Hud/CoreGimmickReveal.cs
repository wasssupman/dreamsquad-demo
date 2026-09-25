using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI;
using Wassup.UI.Layout;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 8a — 배치 직전 **기믹 리빌**. 옛 `UI/GimmickPhaseView.cs`(517줄,
    // gimmick-recognition-upgrade unit 1)의 복사·적응본이다. 장부 bridge-fields 31 `_gimmickPhaseView` 의 새 주인.
    //
    // 3비트 ~2초: ① 도장(딤+틴트+아이콘) → ② 명명(룰 라벨+정서 카피) → ③ 한 줄 후 퇴장.
    // 끝나면 흔적 없이 사라진다 — 배치 화면에 기믹 UI 를 남기지 않는 게 계약이다. 연출·파티클·효과음은
    // 옛 것 그대로이고 값은 `GimmickRevealConfig`(옛 씬과 같은 에셋)가 소유한다.
    //
    // 바뀐 것은 **진입과 퇴장**뿐이다:
    //   · 진입 — 옛 것은 `GameManager.PlacementRequested` 를 구독했다. 여기는 코어의 `GimmickAssigned` 사건.
    //   · 퇴장 — 옛 것은 끝나면 `PlacementPhaseView.BeginPlacementPhase` 를 불렀다. 새 코어는 배치 창이
    //     판 시작에 이미 열려 있으므로 리빌 동안 **판의 시간을 리스로 세운다**(구현 6 — 메뉴 정지와 같은 기제).
    // ⚠ 옛 `GamePhase.Gimmick` 은 카메라에서 배치와 같은 그림이라(`CameraDirector.ResolveState`) 따로 밀지 않는다.
    //
    // ⚠ 오늘 라이브 경로는 없다 — 옛 `BattleConfig.asset:15`·새 `MatchMode_KillScore3Min.asset:27` 둘 다
    // `gimmickEnabled: 0` 이다. 기믹 판은 7d 에서 살아났고 기믹을 켠 모드에서만 보인다.
    [DisallowMultipleComponent]
    public sealed class CoreGimmickReveal : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private GimmickRevealConfig config;
        [Tooltip("배치 HUD 위(옛 씬 저작 20).")]
        [SerializeField] private int sortingOrder = 20;
        [Tooltip("월드 VFX 를 카메라 앞 몇 미터에 띄울지(옛 씬 저작 6).")]
        [SerializeField] private float vfxCameraDistance = 6f;

        private bool _holding;
        private TimeLease _lease;
        private bool _built;
        private GameObject _panel;
        private CanvasGroup _rootGroup;
        private Image _dim;
        private Image _tint;
        private RectTransform _particleRoot;
        private Image _icon;
        private CanvasGroup _iconGroup;
        private RectTransform _iconRect;
        private TextMeshProUGUI _ruleLabel;
        private TextMeshProUGUI _subtitle;
        private CanvasGroup _nameGroup;
        private RectTransform _nameRect;
        private TextMeshProUGUI _summary;
        private CanvasGroup _summaryGroup;
        private TextMeshProUGUI _tapHint;
        private CanvasGroup _tapHintGroup;
        private Sequence _seq;
        private float _startedAt;
        private GameObject _vfxInstance;
        private Sprite _particleSprite;
        private readonly System.Collections.Generic.List<Image> _particles = new();
        private Material _labelOutlineMat;

        private const float ParticleStartAlpha = 0.75f;
        // 힌트는 있다는 걸 알리되 요약보다 앞서면 안 된다 — 낮은 알파로 고정.
        private const float TapHintAlpha = 0.55f;
        private const string TapHintText = "탭하여 계속";

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            PlayedCount = 0;
            if (_driver != null) _driver.Subscribe(ViewOrder.Overhead, OnCoreEvent);
        }

        // 뷰가 꺼져도 **판의 시간은 반드시 돌려준다** — 리스가 새면 판이 영영 멈춘다.
        // 이 유닛의 단일 최대 위험이라 teardown 경로를 전부 `Finish` 로 모은다.
        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            if (_seq.isAlive) _seq.Stop();
            Finish(stopSeq: false);
        }

        private void OnDestroy()
        {
            if (_labelOutlineMat != null) Destroy(_labelOutlineMat);
            if (_particleSprite != null)
            {
                if (_particleSprite.texture != null) Destroy(_particleSprite.texture);
                Destroy(_particleSprite);
            }
        }

        /// <summary>리빌을 재생한 횟수(스킵은 세지 않는다). 테스트의 증언 창.</summary>
        public int PlayedCount { get; private set; }

        /// <summary>리빌이 판의 시간을 붙들고 있나.</summary>
        public bool Holding => _holding;

        // 코어는 판 시작에 「이번 판의 기믹」을 사건으로 낸다(`GimmickHost.Begin` — 기믹 기능이 꺼진
        // 모드는 사건 자체가 없다). 인덱스 -1 = 뽑을 것이 없는 판 → 건너뛴다.
        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.GimmickAssigned:
                    BeginIntro(ResolveGimmick(e.Arg));
                    return;
                case CoreEventKind.MatchEnded:
                    if (_holding) Finish(stopSeq: true);
                    return;
            }
        }

        // 코어 인덱스 → 저작 에셋. 번호를 매긴 쪽(`MatchDefinitionBuilder.ToGimmickDefs` — 모드 풀에서
        // null 을 건너뛴 순서)과 **같은 규칙**으로 센다.
        private GimmickData ResolveGimmick(int index)
        {
            var mode = _driver != null ? _driver.Mode : null;
            if (index < 0 || mode == null || mode.gimmickPool == null) return null;
            int k = 0;
            for (int i = 0; i < mode.gimmickPool.Length; i++)
            {
                var g = mode.gimmickPool[i];
                if (g == null) continue;
                if (k++ == index) return g;
            }
            return null;
        }

        /// 매치 인트로 진입점. 리빌을 재생하든 스킵하든 **어떤 경로로든 판의 시간을 정확히 한 번
        /// 돌려준다** — `_holding` 이 그 보장의 주체다(옛 `_onDone` 의 후계).
        ///
        /// 옛 것은 리빌이 끝나면 배치 페이즈를 **직접 시작**했다(배치는 그 뒤에 열렸다). 새 코어는 판을
        /// 여는 순간 배치 창이 이미 열려 있으므로, 리빌은 배치 **앞**에 Battle 도메인을 0 으로 리스해
        /// 판의 시계를 세운다(메뉴 정지와 같은 기제). 끝나면 리스를 반납하고 배치 카운트다운이 그때 돈다.
        public void BeginIntro(GimmickData gimmick)
        {
            // 재진입(이전 리빌이 아직 살아있음) — 앞 것을 먼저 닫아 리스를 흘리지 않는다.
            if (_holding) Finish(stopSeq: true);

            // 스킵 — 기믹이 없는 판은 붙들지 않는다.
            if (gimmick == null || config == null) return;

            _holding = true;
            _lease = TimeManager.Instance.Request(TimeDomain.Battle, 0f);
            PlayedCount++;
            Play(gimmick);
        }

        private void Play(GimmickData gimmick)
        {
            if (!_built) BuildCanvas();
            var entry = config.Find(gimmick);
            Color tintColor = entry != null ? entry.tintColor : config.defaultTint;

            Populate(gimmick);
            ResetVisualState(tintColor);
            _panel.SetActive(true);
            _startedAt = Time.unscaledTime;

            SpawnVfx(entry);
            PlayRevealSfx(entry);
            LayoutParticles(tintColor);

            // ① 도장 → ② 명명 → ③ 한 줄 + 퇴장.
            _seq = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Color(_dim, WithAlpha(Color.black, config.dimAlpha), config.beatStampSec, Ease.OutQuad))
                .Group(Tween.Color(_tint, WithAlpha(tintColor, config.tintAlpha), config.beatStampSec, Ease.OutQuad));
            // 아이콘 미할당이면 GameObject 가 꺼져 있다 — 꺼진 대상에 트윈을 걸지 않는다.
            if (_icon.gameObject.activeSelf)
            {
                _seq.Group(Tween.Alpha(_iconGroup, 1f, config.beatStampSec * 0.6f, Ease.OutQuad));
                _seq.Group(Tween.Scale(_iconRect, Vector3.one, config.beatStampSec, Ease.OutBack));
            }
            BurstParticles();

            _seq.Chain(Tween.Alpha(_nameGroup, 1f, config.beatNameSec, Ease.OutQuad))
                .Group(Tween.UIAnchoredPosition(_nameRect, TitleRestPos, config.beatNameSec, Ease.OutCubic));

            // 요약은 이 연출의 핵심 정보다. ② 명명이 끝나기 전에 미리 들여보내 읽을 시간을 번다
            // (Chain 이 아니라 Group + startDelay 라 ② 와 겹친다). 탭 힌트는 반 박자 뒤.
            float lead = Mathf.Clamp(config.summaryLeadSec, 0f, config.beatNameSec);
            float summaryFade = config.beatOutSec * 0.5f;
            _seq.Group(Tween.Alpha(_summaryGroup, 1f, summaryFade, Ease.OutQuad,
                startDelay: config.beatNameSec - lead));
            _seq.Group(Tween.Alpha(_tapHintGroup, TapHintAlpha, summaryFade, Ease.OutQuad,
                startDelay: config.beatNameSec - lead + summaryFade));

            _seq.ChainDelay(config.summaryHoldSec);
            PlayExit();
        }

        // 퇴장 = 페이드아웃 → Finish. 위 시퀀스에 이어 붙는다.
        private void PlayExit()
        {
            _seq.Chain(Tween.Alpha(_rootGroup, 0f, config.beatOutSec, Ease.InQuad));
            // 자기 콜백 안에서 Stop 금지(BossWarningView 교훈) — 완주 경로는 시퀀스를 건드리지 않는다.
            _seq.ChainCallback(() => Finish(stopSeq: false));
        }

        private void OnPanelTapped()
        {
            if (!_holding) return;
            // grace — 연출 시작 직후 오탭이 통째로 날리는 걸 막는다.
            if (config != null && Time.unscaledTime - _startedAt < config.tapSkipGraceSec) return;
            Finish(stopSeq: true);
        }

        // 모든 종료가 지나는 단일 출구. `_holding` 을 먼저 내려 재진입에도 두 번 반납하지 않는다.
        private void Finish(bool stopSeq)
        {
            if (stopSeq && _seq.isAlive) _seq.Stop();
            DespawnVfx();
            if (_panel != null) _panel.SetActive(false);
            if (!_holding) return;
            _holding = false;
            _lease.Dispose();
            _lease = default;
        }

        private void Populate(GimmickData g)
        {
            string rule = !string.IsNullOrEmpty(g.ruleLabel) ? g.ruleLabel
                : (!string.IsNullOrEmpty(g.displayName) ? g.displayName : g.gimmickId);
            // 룰 라벨이 비어 displayName 으로 폴백했으면 같은 문구를 부제로 중복 노출하지 않는다.
            string sub = string.IsNullOrEmpty(g.ruleLabel) ? "" : g.displayName;

            _ruleLabel.text = rule;
            _subtitle.text = sub;
            _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            _summary.text = g.summary;
            _summary.gameObject.SetActive(!string.IsNullOrEmpty(g.summary));

            _icon.sprite = g.icon;
            _icon.gameObject.SetActive(g.icon != null);
        }

        private Vector2 TitleRestPos { get { return new Vector2(0f, config.titleOffsetY); } }

        // 레이아웃은 BuildCanvas(Awake) 가 아니라 여기서 매번 적용한다 — config 를 Play 중에
        // 만져도 다음 리빌에 바로 반영되고, Awake 시점 config null 을 걱정할 필요가 없다.
        private void ResetVisualState(Color tintColor)
        {
            _rootGroup.alpha = 1f;
            _dim.color = WithAlpha(Color.black, 0f);
            _tint.color = WithAlpha(tintColor, 0f);

            _iconRect.anchoredPosition = new Vector2(0f, config.iconOffsetY);
            _iconRect.sizeDelta = new Vector2(config.iconSize, config.iconSize);
            _iconGroup.alpha = 0f;
            _iconRect.localScale = Vector3.one * config.stampFromScale;

            ((RectTransform)_subtitle.transform).anchoredPosition = new Vector2(0f, -config.subtitleGap);
            _nameGroup.alpha = 0f;
            _nameRect.anchoredPosition = new Vector2(0f, config.titleOffsetY + config.titleRiseFrom);

            var summaryRt = (RectTransform)_summary.transform;
            summaryRt.anchoredPosition = new Vector2(0f, config.summaryOffsetY);
            // 두 줄이라 한 줄 기준 높이로는 잘린다.
            summaryRt.sizeDelta = new Vector2(summaryRt.sizeDelta.x, _summary.fontSize * 3.4f);
            _summaryGroup.alpha = 0f;

            ((RectTransform)_tapHint.transform).anchoredPosition = new Vector2(0f, config.tapHintOffsetY);
            _tapHintGroup.alpha = 0f;
        }

        // ── 절차 파티클 (신규 아트 0 — UiRoundedSprite 로 원을 만들어 흩뿌린다) ──

        // 버스트라 페이드-인 없이 처음부터 보이고, 날아가며 사라진다. 속성당 트윈 1개씩만
        // 걸어 같은 프로퍼티에 두 트윈이 겹치지 않게 한다.
        private void LayoutParticles(Color tintColor)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                p.color = WithAlpha(tintColor, ParticleStartAlpha);
                var rt = (RectTransform)p.transform;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }
        }

        private void BurstParticles()
        {
            if (_particles.Count == 0) return;
            float spread = config.particleSpread;
            float dur = config.beatStampSec + config.beatNameSec * 0.5f;
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                var rt = (RectTransform)p.transform;
                // 결정론적 방사 — 시드 RNG 없이 인덱스로 각/거리를 만든다(구조적 결정론 선호).
                float angle = (i / (float)_particles.Count) * Mathf.PI * 2f;
                float radius = spread * (0.55f + 0.45f * ((i % 3) / 2f));
                var target = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                _seq.Group(Tween.UIAnchoredPosition(rt, target, dur, Ease.OutCubic));
                _seq.Group(Tween.Scale(rt, Vector3.one * 0.4f, dur, Ease.OutQuad));
                _seq.Group(Tween.Color(p, WithAlpha(p.color, 0f), dur, Ease.InQuad));
            }
        }

        // ── 등장 효과음 (unit 2) ──

        // 클립 해석: 기믹 전용 → 공용 → 무음. 아이콘이 찍히는 ① 도장 시작과 같은 프레임에 낸다.
        // 탭 스킵으로 연출을 건너뛰어도 원샷이라 끊지 않는다(중간에 자르면 더 어색하다).
        private void PlayRevealSfx(GimmickRevealConfig.Entry entry)
        {
            var clip = entry != null && entry.sfxClip != null ? entry.sfxClip : config.defaultSfxClip;
            if (clip == null) return;
            var sound = SoundManager.Instance;
            if (sound != null) sound.PlayGimmickReveal(clip);
        }

        // ── 월드 VFX (있으면) ──

        private void SpawnVfx(GimmickRevealConfig.Entry entry)
        {
            var prefab = entry != null ? entry.revealVfxPrefab : null;
            if (prefab == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var camT = cam.transform;
            _vfxInstance = Instantiate(prefab,
                camT.position + camT.forward * vfxCameraDistance,
                Quaternion.LookRotation(camT.forward));
            // 스트립된 프리팹이 루트 비활성인 경우 아무도 안 켜서 통째로 안 보인다(벤더 VFX 함정).
            _vfxInstance.SetActive(true);
        }

        private void DespawnVfx()
        {
            if (_vfxInstance == null) return;
            Destroy(_vfxInstance);
            _vfxInstance = null;
        }

        // ── 빌드 ──

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder);

            _panel = new GameObject("GimmickRevealPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _panel.transform.SetParent(roots.FullBleedRoot, false);
            StretchFull((RectTransform)_panel.transform);
            _rootGroup = _panel.GetComponent<CanvasGroup>();

            // 딤 = 탭 캐처 겸용(풀블리드, raycast on). 화면 어디를 눌러도 스킵된다.
            _dim = _panel.GetComponent<Image>();
            _dim.color = WithAlpha(Color.black, 0f);
            _dim.raycastTarget = true;
            _panel.AddComponent<TapCatcher>().Clicked = OnPanelTapped;

            _tint = MakeFullBleedImage(_panel.transform, "Tint");
            _particleRoot = MakeCenterRect(_panel.transform, "Particles");
            BuildParticles();

            var content = MakeCenterRect(_panel.transform, "Content");

            _icon = MakeIcon(content, "Icon");
            _iconRect = (RectTransform)_icon.transform;
            _iconGroup = _icon.gameObject.AddComponent<CanvasGroup>();

            var nameBlock = MakeCenterRect(content, "Name");
            _nameRect = nameBlock;
            _nameGroup = nameBlock.gameObject.AddComponent<CanvasGroup>();
            _ruleLabel = MakeLabel(nameBlock, "Rule", "", 92f, Color.white, FontStyles.Bold);
            _subtitle = MakeLabel(nameBlock, "Subtitle", "", 34f,
                new Color(0.78f, 0.82f, 0.9f, 1f), FontStyles.Italic);

            // 요약은 두 줄(원인 / 결과)이고 강조는 에셋의 리치텍스트 색이 담당한다.
            // 그래서 라벨 기본색은 중립 밝은 톤 — 여기에 색을 넣으면 강조와 싸운다.
            _summary = MakeLabel((RectTransform)_panel.transform, "Summary", "", 40f,
                new Color(0.93f, 0.95f, 0.98f, 1f), FontStyles.Bold);
            _summary.lineSpacing = 12f;
            _summaryGroup = _summary.gameObject.AddComponent<CanvasGroup>();

            _tapHint = MakeLabel((RectTransform)_panel.transform, "TapHint", TapHintText, 26f,
                new Color(0.78f, 0.83f, 0.92f, 1f), FontStyles.Normal);
            _tapHintGroup = _tapHint.gameObject.AddComponent<CanvasGroup>();

            UiLayer.Apply(gameObject);
        }

        private void BuildParticles()
        {
            int count = config != null ? config.particleCount : 0;
            if (count <= 0) return;
            float size = config.particleSize;
            _particleSprite = UiRoundedSprite.MakeCircle(64, Color.white);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"P{i}", typeof(RectTransform));
                go.transform.SetParent(_particleRoot, false);
                var rt = (RectTransform)go.transform;
                Center(rt);
                rt.sizeDelta = new Vector2(size, size);
                var image = go.AddComponent<Image>();
                image.sprite = _particleSprite;
                image.raycastTarget = false;
                _particles.Add(image);
            }
        }

        private Image MakeFullBleedImage(Transform parent, string goName)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            StretchFull((RectTransform)go.transform);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform MakeCenterRect(Transform parent, string goName)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Center(rt);
            return rt;
        }

        // 위치·크기는 ResetVisualState 가 config 로 매번 덮는다 — 여기선 구조만 만든다.
        private static Image MakeIcon(Transform parent, string goName)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Center(rt);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string goName, string text, float size,
            Color color, FontStyles style)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Center(rt);
            rt.sizeDelta = new Vector2(UiCanvasSetup.ReferenceResolution.x * 0.8f, size * 1.6f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            // 라벨이 전부 같은 아웃라인이므로 머티리얼 인스턴스 1개를 공유한다.
            // fontMaterial(라벨당 인스턴스) 대신 fontSharedMaterial 할당 — OnDestroy 에서 해제.
            if (_labelOutlineMat == null && tmp.font != null && tmp.font.material != null)
            {
                _labelOutlineMat = new Material(tmp.font.material);
                _labelOutlineMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                _labelOutlineMat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                _labelOutlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            }
            if (_labelOutlineMat != null) tmp.fontSharedMaterial = _labelOutlineMat;
            return tmp;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // 풀블리드 딤에 붙는 경량 탭 캐처.
        private sealed class TapCatcher : MonoBehaviour, IPointerClickHandler
        {
            public Action Clicked;
            public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
        }
    }
}
