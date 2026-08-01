using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // gimmick-recognition-upgrade unit 1 — 배치 직전 기믹 리빌.
    //
    // 배치 페이즈 안에서 안내를 띄우면 30초 타이머·코스트·슬롯 선택과 인지 예산을 다투고
    // 진다(현행 우상단 카드가 그랬다). 그래서 배치 **앞**의 독립 페이즈로 빼내고, 텍스트
    // 3줄 대신 아이콘·색조·움직임으로 채널을 늘려 정보량은 줄이고 인지는 올린다.
    //
    // 3비트 ~2초: ① 도장(딤+틴트+아이콘) → ② 명명(룰 라벨+정서 카피) → ③ 한 줄 후 퇴장.
    // 끝나면 흔적 없이 사라진다 — 배치 화면에 기믹 UI 를 남기지 않는 게 계약이다.
    //
    // 진입은 GiftPhaseView.ProceedToPlacement() 단일 퍼널에서만. 스타일 선례는 BossWarningView.
    public class GimmickPhaseView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GimmickRevealConfig config;
        [Tooltip("첫 판 판정용. 미배선이면 fail-open — 리빌은 정상 재생된다.")]
        [SerializeField] private PlayerProfileSO profileSO;
        [Tooltip("선물 연출(30)과 배치 HUD(7) 사이.")]
        [SerializeField] private int sortingOrder = 20;
        [Tooltip("월드 VFX 를 카메라 앞 몇 미터에 띄울지.")]
        [SerializeField] private float vfxCameraDistance = 6f;

        private Action _onDone;
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
        // first-session-tutorial unit 24 — 튜토리얼 홀드. 진입 시 캐시하고 연출 도중 재평가하지
        // 않는다(선물 unit 7 선례). `_holding` 이 해제의 단일 가드다 — 탭과 만료 폴백이
        // 경쟁하므로, 먼저 진입한 쪽이 이걸 내려 두 번째를 막는다.
        private bool _tutorialMode;
        private bool _holding;
        private float _holdStartedAt;
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

        // 뷰가 꺼져도 콜백은 반드시 나간다 — 유실되면 배치 페이즈가 영영 시작되지 않는다.
        // 이 유닛의 단일 최대 위험이라 teardown 경로를 전부 여기로 모은다.
        private void OnDisable()
        {
            if (_onDone != null) Finish(stopSeq: true);
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

        // unit 24 — 튜토리얼 홀드 seam. 문구는 구독자(FirstSessionTutorialController) 소관이고
        // 이 뷰는 문구를 모른다. 구독자가 없어도 탭 진행은 뷰 단독으로 동작한다.
        // 홀드가 하나뿐이라 선물의 GiftTutorialHold 같은 enum 은 두지 않는다.
        public event Action TutorialHoldEntered;
        public event Action TutorialHoldReleased;

        /// 선물 페이즈가 배치로 넘어가기 직전에 호출. onDone 은 어떤 경로로든 정확히 한 번 불린다.
        public void BeginReveal(Action onDone)
        {
            // 재진입(이전 리빌이 아직 살아있음) — 앞 것을 먼저 닫아 콜백을 흘리지 않는다.
            if (_onDone != null) Finish(stopSeq: true);
            _onDone = onDone;

            var gm = gameManager != null ? gameManager : GameManager.Instance;
            var gimmick = gm != null ? gm.AssignedGimmick : null;

            // 스킵 — 페이즈 전이 없이 즉시 배치로. 첫 판 판정을 여기서 하는 이유:
            // 훅 지점(ProceedToPlacement)이 튜토리얼 스킵 경로까지 삼키는 퍼널이라
            // GiftPhaseView 의 판정만으로는 이 리빌이 뚫고 나온다.
            if (gimmick == null || config == null || TutorialProgress.ShouldRunCore(profileSO))
            {
                Finish(stopSeq: false);
                return;
            }

            // unit 24 — 구독자가 없으면 홀드하지 않는다. 선물(unit 7)은 "구독자 없어도 뷰 단독
            // 진행" 이지만 여기선 그러면 안 된다: 완료 저장의 주인이 구독자라, 미배선 상태에서
            // 홀드하면 문구 없는 정지가 **매 판** 반복된다(저장할 사람이 없으니 영원히 pending).
            // 참조 누락은 "안내 생략" 으로 떨어지는 것이 이 프로젝트의 fail-open 계약이다.
            _tutorialMode = TutorialHoldEntered != null &&
                            TutorialProgress.ShouldRunGimmickRevealHint(profileSO);

            // unit 24 — 이 SetPhase 는 반드시 Play() **앞**이어야 한다.
            // FirstSessionTutorialController.OnPhaseChanged 가 `phase != Placement` 에서
            // ResetAwakeningSession(hide: true) → guidance.Hide() 를 부르고 Gimmick 도 여기 걸린다.
            // SetPhase 는 동기 발화라, 여기서 부르면 그 Hide() 가 홀드 말풍선보다 몇 초 먼저
            // 지나간다. 순서를 뒤집으면 말풍선이 뜨자마자 지워진다.
            if (gm != null) gm.SetPhase(GamePhase.Gimmick);
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

            // unit 24 — 튜토리얼 모드는 여기서 시퀀스를 끝내고 무기한 홀드로 들어간다. 요약이
            // 다 뜬 자리라 화면에 정보가 전부 나와 있고, 튜토리얼은 거기에 구조 한 줄만 얹는다.
            if (_tutorialMode)
            {
                _seq.ChainCallback(EnterTutorialHold);
                return;
            }

            _seq.ChainDelay(config.summaryHoldSec);
            PlayExit();
        }

        // 퇴장 = 페이드아웃 → Finish. 일반 모드는 위 시퀀스에 이어 붙고, 튜토리얼 모드는
        // 홀드 해제 시점에 새 시퀀스로 재생한다.
        private void PlayExit()
        {
            _seq.Chain(Tween.Alpha(_rootGroup, 0f, config.beatOutSec, Ease.InQuad));
            // 자기 콜백 안에서 Stop 금지(BossWarningView 교훈) — 완주 경로는 시퀀스를 건드리지 않는다.
            _seq.ChainCallback(() => Finish(stopSeq: false));
        }

        // ── 튜토리얼 홀드 (unit 24) ──────────────────────────────────────────

        private void EnterTutorialHold()
        {
            if (_onDone == null) return; // 이미 정리된 뒤 도착한 콜백
            _holding = true;
            // grace 를 오탭 debounce 로 재사용한다 — 홀드 진입 순간의 잔여 탭이 문구를
            // 읽기도 전에 소진시키는 걸 막는다(선물 unit 7 과 같은 처리).
            _holdStartedAt = Time.unscaledTime;
            _startedAt = _holdStartedAt;
            TutorialHoldEntered?.Invoke();
        }

        private void Update()
        {
            if (!_holding || config == null) return;
            if (Time.unscaledTime - _holdStartedAt < config.tutorialHoldFallbackSec) return;
            // 만료 폴백 — 리빌엔 Skip 버튼이 없어서 홀드가 안 풀리면 배치에 영영 못 간다.
            ReleaseTutorialHold();
        }

        // 탭과 만료 폴백이 경쟁한다. 진입한 쪽이 `_holding` 을 즉시 내려 두 번째를 막는다 —
        // 없으면 퇴장 트윈이 같은 _rootGroup.alpha 에 두 번 걸려 깜빡이고 이벤트가 두 번 나간다.
        // Finish 가 _onDone 을 먼저 비우는 것과 같은 형태다.
        private void ReleaseTutorialHold()
        {
            if (!_holding) return;
            _holding = false;
            TutorialHoldReleased?.Invoke();
            // 현 불변식(`_holding` 은 EnterTutorialHold 에서만 서고 그쪽이 _onDone 을 이미 확인,
            // Finish 는 _onDone 을 비우기 **전에** _holding 을 내림)에서 이 분기는 도달하지 않는다.
            // 남겨두는 이유는 위 Invoke 때문이다 — 구독자가 나중에 종료 경로를 타게 되면
            // 여기가 유일한 방어선이 된다.
            if (_onDone == null) return;
            _seq = Sequence.Create(useUnscaledTime: true);
            PlayExit();
        }

        private void OnPanelTapped()
        {
            if (_onDone == null) return;
            // grace — 연출 시작 직후 오탭이 통째로 날리는 걸 막는다(GiftPhaseView 선례).
            if (config != null && Time.unscaledTime - _startedAt < config.tapSkipGraceSec) return;
            // unit 24 — 튜토리얼 모드에서 탭은 **홀드 중일 때만** 진행시킨다. 홀드 전 탭이
            // 연출을 통째로 날리면 읽을 것이 사라진다(기존 탭 스킵 비활성 — 선물과 같은 결정).
            if (_tutorialMode)
            {
                ReleaseTutorialHold();
                return;
            }
            Finish(stopSeq: true);
        }

        // 모든 종료가 지나는 단일 출구. onDone 을 먼저 비워 재진입에도 두 번 불리지 않는다.
        private void Finish(bool stopSeq)
        {
            if (stopSeq && _seq.isAlive) _seq.Stop();
            // unit 24 — 홀드 상태도 여기서 걷는다. 조용히 내리는 것이 요점이다: 이탈·재진입에서
            // TutorialHoldReleased 를 발행하면 구독자가 완료를 저장해, 플레이어가 문구를 읽지도
            // 않은 판에서 안내가 소진된다.
            _holding = false;
            _tutorialMode = false;
            DespawnVfx();
            if (_panel != null) _panel.SetActive(false);
            var callback = _onDone;
            _onDone = null;
            callback?.Invoke();
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

        // 풀블리드 딤에 붙는 경량 탭 캐처(GiftPhaseView 선례).
        private sealed class TapCatcher : MonoBehaviour, IPointerClickHandler
        {
            public Action Clicked;
            public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
        }
    }
}
