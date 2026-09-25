using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity.Hud;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;
using Wassup.UI.Layout;

namespace Wassup.BattleCoreUnity.Cards
{
    // battle-core-rebuild unit 7c — **각성 항아리 독**(판독면). 옛 `AwakeningGaugeView`(771줄)의 이식이다 — 룩은 옛 것 그대로
    // (세로 항아리 · 큰 숫자 · 게이지 비례 Spine 피규어 더미 · 처치 위치에서 날아오는 흡수 비행 · ready 림 · 넘침 -N).
    //
    // 달라진 것 — **읽는 곳**. 옛 뷰는 손패 컨트롤러의 C# 이벤트 셋(`GaugeChanged` · `AwakeningOverflowed` · `AwakeningGainedAt`)을
    // 들었다. 새 코어에는 각성의 사건이 없다 — 게이지는 **연속값**이라 코스트 물통처럼 매 프레임 읽는다(`HandDeck.Gauge`).
    //   · 넘침 = `HandDeck.OverflowLost`(누계)의 증가분 — 코어가 그 손실을 세어 두는 이유가 이 화면이다(D6).
    //   · 흡수 비행의 **출발점** = 처치·사망 사건(`UnitSlain`)의 자리(값 스냅샷 — 죽은 개체를 되묻지 않는다). 게이지가 오른 프레임에
    //     그 자리들에서 피규어가 뜬다. 스킨 = 죽은 유닛의 저작(`ISpineUnitVisualData`).
    //   · 한 회분(ready 임계) = **가장 싼 카드 값**(정의표 `CardDef.Cost` — D15 「값은 카드가 정한다」). 옛 것은 종류별 저작 셋의 최솟값이었다.
    //
    // ⚠ **상시 어필 금지** — 평소 완전 정지, 회차가 오를 때만 0.3초(orb-dock unit 8 · 구현 7 — 되돌리지 말 것). 항아리 탭 진입구는
    // 꺼져 있다(2026-08-19 — 손패는 유닛 선택으로만 열린다). 그래서 이 독은 입력을 받지 않는다.
    [DisallowMultipleComponent]
    public sealed class CoreAwakeningGaugeView : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [Tooltip("항아리를 트레이 우측 엣지에 붙인다. 비면 폴백 반폭.")]
        [SerializeField] private CoreDefenderTray _tray;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numberFont;

        [Header("Jar Colors")]
        [SerializeField] private Color backingColor = new Color(0.09f, 0.08f, 0.15f, 0.95f);
        [SerializeField] private Color chargedColor = new Color(0.43f, 0.86f, 0.92f, 0.95f);
        [SerializeField] private Color maxColor = new Color(1f, 0.77f, 0.12f, 1f);
        [SerializeField] private Color rimColor = new Color(0.56f, 0.43f, 1f, 0.9f);
        [SerializeField] private Color dormantColor = new Color(0.62f, 0.58f, 0.7f, 0.7f);
        [SerializeField] private Color overflowColor = new Color(1f, 0.35f, 0.24f, 1f);
        [SerializeField] private float valuePunchScale = 1.18f;

        [Header("Placement")]
        [SerializeField] private float trayGap = 30f;
        [SerializeField] private float baselineY = 18f;
        [SerializeField] private float fallbackTrayHalf = 490f;

        [Header("Figure Pile — Spine miniatures")]
        [SerializeField] private int maxFigures = 44;
        [SerializeField] private float figureRadius = 11f;
        [SerializeField] private float figureGravity = 1500f;
        [SerializeField] private float figureDamping = 0.9f;
        [SerializeField] private AttackUnitData representativeUnit;
        [SerializeField] private Material figureSkeletonMaterial;
        [SerializeField] private string figureAnimation = "Idle";
        [SerializeField] private float figureScale = 0.22f;
        [SerializeField] private float figureFlightSeconds = 0.44f;
        [SerializeField] private float figureFlightArc = 140f;
        [SerializeField] private float figureFlightStagger = 0.05f;
        [SerializeField] private int maxConcurrentFlights = 4;
        [SerializeField] private float figureHopStrength = 2.1f;
        [SerializeField] private Color[] figureTints =
        {
            new Color(0.62f, 0.5f, 0.9f, 1f),
            new Color(0.45f, 0.82f, 0.88f, 1f),
            new Color(0.55f, 0.62f, 0.95f, 1f),
        };

        const float DockWidth = 150f, DockHeight = 236f;
        const float JarWidth = 134f, JarHeight = 208f, JarBottom = 24f;
        const float JarBorder = 6f, InteriorPad = 9f;

        private GameObject _panel;
        private RectTransform _visualRoot;
        private Image _jarFrame;
        private Image _rim;
        private JarFigurePile _pile;
        private RectTransform _safeArea;
        private Sprite _figureSprite;
        private int _pendingFlights;
        private readonly List<Graphic> _ghostPool = new List<Graphic>();
        private int _flightGen;
        private TextMeshProUGUI _valueLabel;
        private TextMeshProUGUI _gainLabel;
        private bool _built;
        private bool _open;
        private int _lastShown = -1;
        private float _lastOverflow;
        private float _normalized;
        private float _readyThreshold = 1f;
        private bool _ready;
        private Coroutine _punch, _gain, _overflow, _chargeBurst;
        private int _unitCost;
        private Camera _figureCamera;

        // 처치·사망 자리 — 게이지가 오른 프레임에 여기서 피규어가 뜬다(사건 → 다음 프레임 판독).
        private readonly Queue<(Vector3 viewPos, ISpineUnitVisualData visual)> _gainSources =
            new Queue<(Vector3, ISpineUnitVisualData)>(8);

        private int FiguresCommitted => (_pile != null ? _pile.ActiveCount : 0) + _pendingFlights;

        /// <summary>지금 보이는 숫자(테스트 — 코어 게이지를 옮겨 적었나).</summary>
        public string ShownValue => _valueLabel != null ? _valueLabel.text : "";
        public bool IsVisible => _panel != null && _panel.activeInHierarchy;

        public void SetOpen(bool open)
        {
            _open = open;
            UpdateVisualState();
        }

        private void Awake()
        {
            if (_driver == null) _driver = FindAnyObjectByType<BattleDriver>();
            if (_tray == null) _tray = FindAnyObjectByType<CoreDefenderTray>();
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Hand, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            if (_visualRoot != null) { _visualRoot.localScale = Vector3.one; _visualRoot.localRotation = Quaternion.identity; }
            CancelFlights();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    CancelFlights();
                    _gainSources.Clear();
                    _pile?.Clear();
                    _lastShown = -1;
                    _lastOverflow = 0f;
                    break;
                case CoreEventKind.UnitSlain:
                    // 각성은 처치·사망의 보상이다(D7) — 그 자리를 적어 둔다. 보상이 0 인 개체면 게이지가 안 올라 비행도 없다.
                    _gainSources.Enqueue(((Vector3)BoardSpace.ToView(e.SiteTarget.Pos), VisualOf(e)));
                    while (_gainSources.Count > 16) _gainSources.Dequeue();
                    break;
            }
        }

        private ISpineUnitVisualData VisualOf(CoreEvent e)
        {
            if (_driver == null || e.DefIndex < 0) return null;
            if (e.Faction == Wassup.Battle.Units.Faction.EnemyUnit)
                return e.DefIndex < _driver.EnemyAssets.Count ? _driver.EnemyAssets[e.DefIndex] : null;
            return e.DefIndex < _driver.DefenderAssets.Count ? _driver.DefenderAssets[e.DefIndex] : null;
        }

        private void Update()
        {
            bool visible = _driver != null && _driver.Running && !_driver.Match.Clock.Ended
                           && _driver.Match.Clock.Phase == MatchPhase.Battle;
            if (_panel != null && visible != _panel.activeSelf)
            {
                _panel.SetActive(visible);
                if (visible) { ResolveReadyThreshold(); _lastShown = -1; }
                else CancelFlights();
            }
            if (!visible) { _gainSources.Clear(); return; }

            var hand = _driver.Match.Hand;
            int value = Mathf.FloorToInt(hand.Gauge);
            float lost = hand.OverflowLost;
            if (lost > _lastOverflow + 1e-4f)
            {
                OnOverflow(Mathf.RoundToInt(lost - _lastOverflow));
                _lastOverflow = lost;
            }
            if (value != _lastShown)
            {
                bool first = _lastShown < 0;
                int prev = _lastShown;
                Refresh(value, animate: !first);
                if (!first && value > prev) LaunchFlights(value);
            }
            _gainSources.Clear();
        }

        private void OnOverflow(int lost)
        {
            if (_panel == null || !_panel.activeInHierarchy) return;
            if (_overflow != null) StopCoroutine(_overflow);
            _overflow = StartCoroutine(OverflowFlashRoutine());
            if (lost > 0)
            {
                if (_gain != null) StopCoroutine(_gain);
                _gain = StartCoroutine(ShowLoss(lost));
            }
        }

        private int GaugeMax => _driver != null && _driver.Running ? Mathf.Max(0, Mathf.FloorToInt(_driver.Match.Hand.GaugeMax)) : 0;

        private int FiguresForGauge(int gauge)
        {
            int max = GaugeMax;
            if (max <= 0 || _pile == null) return 0;
            return Mathf.Clamp(Mathf.RoundToInt((float)gauge / max * _pile.Capacity), 0, _pile.Capacity);
        }

        private void TrimToTarget(int gauge)
        {
            if (_pile == null) return;
            int target = FiguresForGauge(gauge);
            while (_pile.ActiveCount > target) _pile.RemoveTop();
        }

        private void LaunchFlights(int gauge)
        {
            if (_pile == null) return;
            int delta = FiguresForGauge(gauge) - FiguresCommitted;
            if (delta <= 0) return;
            bool canFly = _panel != null && _panel.activeInHierarchy && _safeArea != null && _figureSprite != null;
            Vector2 endLocal = default;
            bool haveEnd = canFly && TryJarTopLocal(out endLocal);
            var srcs = _gainSources.ToArray();
            for (int i = 0; i < delta; i++)
            {
                var src = srcs.Length > 0 ? srcs[i % srcs.Length] : (default(Vector3), (ISpineUnitVisualData)null);
                Vector2 startLocal = default;
                bool haveStart = canFly && srcs.Length > 0 && TryWorldToSafeAreaLocal(src.Item1, out startLocal);
                if (haveStart && haveEnd && _pendingFlights < maxConcurrentFlights)
                {
                    StartCoroutine(FlightRoutine(startLocal, endLocal, i * figureFlightStagger, src.Item2));
                    _pendingFlights++;
                }
                else _pile.SpawnAtTop(src.Item2);
            }
        }

        private bool TryJarTopLocal(out Vector2 local)
        {
            local = default;
            if (_jarFrame == null || _safeArea == null) return false;
            Vector3 worldTop = _jarFrame.rectTransform.TransformPoint(new Vector3(0f, JarHeight, 0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, worldTop);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeArea, screen, null, out local);
        }

        private bool TryWorldToSafeAreaLocal(Vector3 world, out Vector2 local)
        {
            local = default;
            if (_safeArea == null) return false;
            if (_figureCamera == null) _figureCamera = Camera.main;
            if (_figureCamera == null) return false;
            Vector3 screen = _figureCamera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeArea, screen, null, out local);
        }

        private Graphic GetGhost()
        {
            for (int i = 0; i < _ghostPool.Count; i++)
                if (_ghostPool[i] != null && !_ghostPool[i].gameObject.activeSelf) return _ghostPool[i];
            var go = new GameObject("AbsorbGhost", typeof(RectTransform));
            go.transform.SetParent(_safeArea, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Graphic g;
            if (SpineFigureBuilder.CanBuild(representativeUnit, figureSkeletonMaterial))
                g = SpineFigureBuilder.Build(go, representativeUnit, figureSkeletonMaterial, figureAnimation);
            else
            {
                rt.sizeDelta = new Vector2(figureRadius * 2f, figureRadius * 2f);
                var img = go.AddComponent<Image>();
                img.sprite = _figureSprite;
                img.raycastTarget = false;
                g = img;
            }
            go.SetActive(false);
            _ghostPool.Add(g);
            return g;
        }

        private void CancelFlights()
        {
            _flightGen++;
            _pendingFlights = 0;
            for (int i = 0; i < _ghostPool.Count; i++)
                if (_ghostPool[i] != null) _ghostPool[i].gameObject.SetActive(false);
        }

        private IEnumerator FlightRoutine(Vector2 startLocal, Vector2 endLocal, float delay, ISpineUnitVisualData killedVisual)
        {
            int gen = _flightGen;
            var ghost = GetGhost();
            if (ghost is SkeletonGraphic sg) SpineFigureBuilder.Reskin(sg, killedVisual);
            var grt = ghost.rectTransform;
            float baseScale = ghost is SkeletonGraphic ? figureScale : 1f;
            float spinDir = ((_pendingFlights & 1) == 0) ? 1f : -1f;
            grt.anchoredPosition = startLocal;
            grt.localRotation = Quaternion.identity;
            grt.localScale = Vector3.one * baseScale;
            ghost.gameObject.SetActive(true);
            float wait = 0f;
            while (wait < delay)
            {
                if (_flightGen != gen) { ghost.gameObject.SetActive(false); yield break; }
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            float dur = Mathf.Max(0.05f, figureFlightSeconds);
            float t = 0f;
            while (t < dur)
            {
                if (_flightGen != gen) { ghost.gameObject.SetActive(false); yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float ease = 1f - (1f - k) * (1f - k);
                Vector2 p = Vector2.Lerp(startLocal, endLocal, ease);
                p.y += Mathf.Sin(k * Mathf.PI) * figureFlightArc;
                grt.anchoredPosition = p;
                grt.localRotation = Quaternion.Euler(0f, 0f, spinDir * k * 340f);
                grt.localScale = Vector3.one * baseScale * Mathf.Lerp(1.25f, 0.7f, k);
                yield return null;
            }
            ghost.gameObject.SetActive(false);
            _pendingFlights = Mathf.Max(0, _pendingFlights - 1);
            if (_pile != null)
            {
                _pile.SpawnAtTop(killedVisual);
                TrimToTarget(_driver != null && _driver.Running ? Mathf.FloorToInt(_driver.Match.Hand.Gauge) : 0);
            }
        }

        private void LateUpdate()
        {
            if (_panel == null || !_panel.activeInHierarchy) return;
            float half = fallbackTrayHalf;
            var strip = _tray != null ? _tray.StripRect : null;
            if (strip != null)
            {
                float w = strip.rect.width;
                if (w > 1f) half = w * 0.5f;
            }
            var rt = (RectTransform)_panel.transform;
            var target = new Vector2(half + trayGap, baselineY);
            if ((rt.anchoredPosition - target).sqrMagnitude > 0.01f) rt.anchoredPosition = target;
        }

        private void Refresh(int value, bool animate)
        {
            if (_valueLabel == null) return;
            int max = GaugeMax;
            _valueLabel.text = value.ToString();
            _normalized = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
            TrimToTarget(value);
            int delta = _lastShown >= 0 ? value - _lastShown : 0;
            bool live = _panel != null && _panel.activeInHierarchy;
            if (animate && delta > 0 && live)
            {
                if (_gain != null) StopCoroutine(_gain);
                _gain = StartCoroutine(ShowGain(delta));
            }
            // 회차 상승 한방 — 한 회분 경계를 넘어 「쓸 수 있는 횟수」가 오른 순간에만(두 회분을 한 번에 넘겨도 한방).
            if (animate && live && _lastShown >= 0
                && AwakeningCharge.CountOf(value, _unitCost) > AwakeningCharge.CountOf(_lastShown, _unitCost))
            {
                if (_chargeBurst != null) StopCoroutine(_chargeBurst);
                _chargeBurst = StartCoroutine(ChargeBurstRoutine());
            }
            _lastShown = value;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            bool dormant = _normalized <= 0.001f && !_open;
            _ready = _normalized >= _readyThreshold;
            if (_rim != null)
            {
                Color c = _ready ? maxColor : rimColor;
                c.a = dormant ? 0f : (_ready || _open ? 1f : Mathf.Lerp(0.12f, 0.5f, _normalized));
                _rim.color = c;
            }
            if (_jarFrame != null) _jarFrame.color = dormant ? dormantColor : Color.white;
        }

        // 한 회분 = 가장 싼 카드 값(정의표). ready 림 임계와 회차 연출이 **같은 값**을 쓴다 — 「쓸 수 있게 된 순간」은 하나다.
        private void ResolveReadyThreshold()
        {
            int max = GaugeMax;
            int cheapest = 0;
            var cards = _driver != null && _driver.Definition != null ? _driver.Definition.Cards : null;
            if (cards != null)
                for (int i = 0; i < cards.Length; i++)
                    if (cards[i].Cost > 0 && (cheapest == 0 || cards[i].Cost < cheapest)) cheapest = cards[i].Cost;
            _unitCost = cheapest;
            _readyThreshold = (_unitCost <= 0 || max <= 0) ? 1f : Mathf.Clamp01((float)_unitCost / max);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;
            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);
            _safeArea = roots.SafeAreaRoot;
            // SkeletonGraphic 미니어처는 uv1/uv2/normal/tangent 가 실려야 정상 렌더(구현 10).
            if (roots.Canvas != null)
                roots.Canvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 |
                    AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

            // 입력을 받지 않는 판독면 — 히트를 놓아 손패의 바깥 탭 캐처가 항아리 위에서도 성립하게(옛 unit 8).
            _panel = new GameObject("DreamcatcherJarDock", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(fallbackTrayHalf + trayGap, baselineY);
            panelRect.sizeDelta = new Vector2(DockWidth, DockHeight);

            var visualGO = new GameObject("JarVisual", typeof(RectTransform));
            visualGO.transform.SetParent(_panel.transform, false);
            _visualRoot = (RectTransform)visualGO.transform;
            _visualRoot.anchorMin = Vector2.zero;
            _visualRoot.anchorMax = Vector2.one;
            _visualRoot.offsetMin = Vector2.zero;
            _visualRoot.offsetMax = Vector2.zero;

            var jarGO = new GameObject("JarBody", typeof(RectTransform), typeof(Image));
            jarGO.transform.SetParent(_visualRoot, false);
            var jarRect = (RectTransform)jarGO.transform;
            jarRect.anchorMin = jarRect.anchorMax = new Vector2(0.5f, 0f);
            jarRect.pivot = new Vector2(0.5f, 0f);
            jarRect.anchoredPosition = new Vector2(0f, JarBottom);
            jarRect.sizeDelta = new Vector2(JarWidth, JarHeight);
            _jarFrame = jarGO.GetComponent<Image>();
            _jarFrame.sprite = UiRoundedSprite.Make(18f, JarBorder, backingColor, new Color(0.3f, 0.26f, 0.42f, 1f));
            _jarFrame.type = Image.Type.Sliced;
            _jarFrame.raycastTarget = false;

            float interiorW = JarWidth - 2f * InteriorPad;
            float interiorH = JarHeight - 2f * InteriorPad;
            var pileGO = new GameObject("FigurePile", typeof(RectTransform));
            pileGO.transform.SetParent(jarGO.transform, false);
            var pileRect = (RectTransform)pileGO.transform;
            pileRect.anchorMin = pileRect.anchorMax = new Vector2(0.5f, 0f);
            pileRect.pivot = new Vector2(0.5f, 0f);
            pileRect.anchoredPosition = new Vector2(0f, InteriorPad);
            pileRect.sizeDelta = new Vector2(interiorW, interiorH);
            _pile = pileGO.AddComponent<JarFigurePile>();
            _figureSprite = UiRoundedSprite.MakeCircle(48, Color.white, 5f, new Color(0.2f, 0.16f, 0.32f, 1f));
            var pileParams = new JarSimParams { gravity = figureGravity, damping = figureDamping, sleepMotionSq = 0.02f };
            _pile.Configure(maxFigures, figureRadius, pileParams, representativeUnit, figureSkeletonMaterial,
                figureScale, figureAnimation, _figureSprite, figureTints);
            _pile.SetHopStrength(figureHopStrength);

            var valueGO = new GameObject("Value", typeof(RectTransform));
            valueGO.transform.SetParent(jarGO.transform, false);
            var valueRect = (RectTransform)valueGO.transform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(0.5f, 0f);
            valueRect.pivot = new Vector2(0.5f, 0.5f);
            valueRect.anchoredPosition = new Vector2(0f, JarHeight * 0.5f);
            valueRect.sizeDelta = new Vector2(JarWidth - 8f, 78f);
            _valueLabel = valueGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _valueLabel.font = numberFont;
            _valueLabel.text = "0";
            _valueLabel.fontSize = 54f;
            _valueLabel.fontStyle = FontStyles.Bold;
            _valueLabel.color = Color.white;
            _valueLabel.alignment = TextAlignmentOptions.Center;
            _valueLabel.raycastTarget = false;
            ApplyNumberOutline(_valueLabel);

            var rimGO = new GameObject("Rim", typeof(RectTransform), typeof(Image));
            rimGO.transform.SetParent(jarGO.transform, false);
            var rimRect = (RectTransform)rimGO.transform;
            rimRect.anchorMin = Vector2.zero; rimRect.anchorMax = Vector2.one;
            rimRect.offsetMin = Vector2.zero; rimRect.offsetMax = Vector2.zero;
            _rim = rimGO.GetComponent<Image>();
            _rim.sprite = UiRoundedSprite.Make(18f, JarBorder, Color.clear, Color.white);
            _rim.type = Image.Type.Sliced;
            _rim.color = Color.clear;
            _rim.raycastTarget = false;

            var gainGO = new GameObject("GainDelta", typeof(RectTransform));
            gainGO.transform.SetParent(jarGO.transform, false);
            var gainRect = (RectTransform)gainGO.transform;
            gainRect.anchorMin = gainRect.anchorMax = new Vector2(0.5f, 0f);
            gainRect.pivot = new Vector2(0.5f, 0.5f);
            gainRect.anchoredPosition = new Vector2(0f, JarHeight * 0.5f + 34f);
            gainRect.sizeDelta = new Vector2(90f, 40f);
            _gainLabel = gainGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _gainLabel.font = numberFont;
            _gainLabel.fontSize = 28f;
            _gainLabel.fontStyle = FontStyles.Bold;
            _gainLabel.alignment = TextAlignmentOptions.Center;
            _gainLabel.raycastTarget = false;
            ApplyNumberOutline(_gainLabel);
            gainGO.SetActive(false);

            var labelGO = new GameObject("DockLabel", typeof(RectTransform));
            labelGO.transform.SetParent(_visualRoot, false);
            var labelRect = (RectTransform)labelGO.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            labelRect.sizeDelta = new Vector2(DockWidth, 20f);
            var dockLabel = labelGO.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) dockLabel.font = labelFont;
            dockLabel.text = "드림캐쳐";
            dockLabel.fontSize = 16f;
            dockLabel.fontStyle = FontStyles.Bold;
            dockLabel.color = new Color(0.86f, 0.82f, 0.96f, 0.92f);
            dockLabel.alignment = TextAlignmentOptions.Center;
            dockLabel.raycastTarget = false;

            UiLayer.Apply(gameObject);
            UpdateVisualState();
        }

        private IEnumerator PunchValue()
        {
            var rt = _valueLabel.rectTransform;
            const float duration = 0.16f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Abs(2f * Mathf.Clamp01(time / duration) - 1f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, valuePunchScale, k);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _punch = null;
        }

        private IEnumerator ShowGain(int delta)
        {
            if (_gainLabel == null) yield break;
            const float duration = 0.58f;
            var rt = _gainLabel.rectTransform;
            Vector2 start = new Vector2(0f, JarHeight * 0.5f + 34f);
            Vector2 end = new Vector2(0f, JarHeight * 0.5f + 72f);
            _gainLabel.text = $"+{delta}";
            _gainLabel.gameObject.SetActive(true);
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, 1f - (1f - k) * (1f - k));
                var c = chargedColor;
                c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f);
                _gainLabel.color = c;
                yield return null;
            }
            _gainLabel.gameObject.SetActive(false);
            _gain = null;
        }

        private IEnumerator ShowLoss(int lost)
        {
            if (_gainLabel == null) yield break;
            const float duration = 0.62f;
            var rt = _gainLabel.rectTransform;
            Vector2 start = new Vector2(0f, JarHeight * 0.5f + 18f);
            Vector2 end = new Vector2(0f, JarHeight * 0.5f - 26f);
            _gainLabel.text = $"-{lost}";
            _gainLabel.gameObject.SetActive(true);
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, 1f - (1f - k) * (1f - k));
                var c = overflowColor;
                c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f);
                _gainLabel.color = c;
                yield return null;
            }
            _gainLabel.gameObject.SetActive(false);
            _gain = null;
        }

        private IEnumerator ChargeBurstRoutine()
        {
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(PunchValue());
            _pile?.Hop();
            const float duration = 0.3f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                float bump = Mathf.Sin(k * Mathf.PI);
                if (_visualRoot != null) _visualRoot.localScale = Vector3.one * (1f + bump * 0.08f);
                if (_rim != null)
                {
                    var c = maxColor;
                    c.a = Mathf.Max(_rim.color.a, bump);
                    _rim.color = c;
                }
                yield return null;
            }
            if (_visualRoot != null) _visualRoot.localScale = Vector3.one;
            _chargeBurst = null;
            UpdateVisualState();
        }

        private IEnumerator OverflowFlashRoutine()
        {
            const float duration = 0.6f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                float flash = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k);
                if (_rim != null)
                {
                    var c = overflowColor;
                    c.a = Mathf.Max(0.4f, flash);
                    _rim.color = c;
                }
                yield return null;
            }
            _overflow = null;
            UpdateVisualState();
        }

        private static void ApplyNumberOutline(TextMeshProUGUI label)
        {
            if (label.font == null) return;
            var material = label.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.11f, 0.04f, 0.22f, 1f));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.04f, 0.01f, 0.1f, 0.8f));
        }
    }
}
