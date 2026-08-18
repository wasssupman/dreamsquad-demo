using System.Collections;
using UnityEngine;
using Unity.Entities;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.UI.Tutorial
{
    // first-run-tutorial units 3~6 — 계정 첫 판 온보딩의 배틀 구간.
    //
    // 이 컨트롤러는 **게임 규칙을 하나도 소유하지 않는다**(계약 1). 배치·부착·배치 스킬·
    // 코스트·게이지는 전부 기존 경로가 처리하고, 여기서 정하는 것은 «언제 멈출지»와
    // «무엇을 열어둘지» 둘뿐이다. 대신 눌러주는 동작은 없다.
    public sealed class FirstRunTutorialController : MonoBehaviour
    {
        // 문구는 사용자 원문 그대로다(띄어쓰기 포함). 다듬는 것은 후속 후보.
        private const string PlaceableText = "배치가능영역";
        private const string BlockedText = "배치 불가 영역";
        private const string GoalText = "게임목표: 최대한 많은 악몽 처치";
        private const string PickText = "유닛을 터치 해보세요";
        private const string PlaceText = "적들의 머리위에 캐논을 배치 해보세요!";
        private const string OnPlaceText = "강력한 배치스킬들을 활용하여 전황을 유리하게 이끌어 보세요";
        private const string ReselectText = "다시 캐논 유닛을 선택 해보세요";
        private const string ReselectFallbackFormat = "다시 {0} 유닛을 선택 해보세요";
        private const string CardText = "하단 드림캐쳐 4개중 맘에 드는것을 터치 해보세요";
        private const string CardFallbackText = "하단 드림캐쳐 중 맘에 드는것을 터치 해보세요";
        private const string AttachDoneText = "드림캐쳐를 유닛에게 부착하여 더 강해질 가능성을 열어보세요!";

        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private FirstRunTutorialConfig config;
        [Tooltip("온보딩이 가리킬 유닛(캐논). 문구의 «캐논»과 같은 것이어야 한다.")]
        [SerializeField] private DefenderUnitData tutorialUnit;

        [Header("안내 도구 (teardown 이 남긴 것만 쓴다)")]
        [SerializeField] private TutorialGuidanceView guidance;
        [Tooltip("Guidance 와 **다른 GameObject** 여야 한다 — 둘 다 자기 캔버스를 만들어 sortingOrder 를 다툰다.")]
        [SerializeField] private OutgameTutorialOverlay overlay;

        [Header("게임 계층")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private PlacementPhaseView placementPhaseView;
        [SerializeField] private DefenderSelector defenderSelector;
        [SerializeField] private DreamcatcherHandView handView;
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private Camera boardCamera;

        private bool _running;
        private bool _briefingDone;
        private bool _battleStarted;

        // 정지 lease 는 **구간**이 소유한다(계약 7). 성공·타임아웃·스킵·취소·씬 이탈이 전부
        // 아래 Unfreeze 하나를 지난다 — 해제를 성공 경로에만 걸면 스텝 하나를 흘려보낸
        // 순간 판이 0배속으로 남는다. EndMatch 에는 TimeManager.ResetAll 이 없다.
        private TimeLease _freeze;
        private bool _frozen;

        // 스텝 완료 신호
        private bool _armed;
        private bool _placed;
        private bool _selectionSet;
        private int _attachBaseline = -1;
        private Entity _hostEntity = Entity.Null;

        // 구간이 «제대로» 끝났는가. 하나라도 스킵/타임아웃이면 완료로 기록하지 않는다(계약 11).
        private bool _b3Completed;
        private bool _b4Completed;

        private RectTransform _worldProxy;

        private void Start()
        {
            if (profileSO == null || config == null || guidance == null || overlay == null)
            {
                enabled = false;
                return;
            }
            if (!profileSO.IsLoadedThisSession || !FirstRunTutorialConfig.ShouldRun(profileSO.profile))
            {
                enabled = false;
                return;
            }
            _running = true;
            if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
            // 배치 페이즈는 이 컨트롤러의 Start 보다 먼저 열릴 수 있다 — 그 경우도 잡는다.
            if (gameManager != null && gameManager.CurrentPhase == GamePhase.Placement) OnPhaseChanged(GamePhase.Placement);
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
            Unsubscribe();
            Unfreeze();
            if (placementPhaseView != null) placementPhaseView.ReleaseIntroHold();
        }

        private void OnDisable() => Unfreeze();

        private void OnPhaseChanged(GamePhase phase)
        {
            if (!_running) return;
            if (phase == GamePhase.Placement && !_briefingDone)
            {
                _briefingDone = true;
                StartCoroutine(RunBriefing());
            }
            else if (phase == GamePhase.Battle && !_battleStarted)
            {
                _battleStarted = true;
                StartCoroutine(RunBattle());
            }
        }

        // ── B1 맵 설명 ──────────────────────────────────────────────────────
        //
        // 카운트다운을 붙잡아 두고 돈다. 전투가 시작되지 않았고 적도 없으므로 정지가
        // 필요 없고, 입력은 홀드의 전면 차단막이 이미 막고 있다. 딤도 쓰지 않는다 —
        // 보여줄 것이 보드 전체라 그 위를 덮으면 무의미하다.
        private IEnumerator RunBriefing()
        {
            if (placementPhaseView != null) placementPhaseView.BeginIntroHold(config.introHoldMaxSeconds);

            guidance.ShowMessage(PlaceableText, false);
            for (int i = 0; i < config.briefingCycles; i++)
            {
                ShowPlaceable();
                guidance.ShowMessage(PlaceableText, false);
                yield return WaitUnscaled(config.briefingHoldSeconds);
                ShowBlocked();
                guidance.ShowMessage(BlockedText, false);
                yield return WaitUnscaled(config.briefingHoldSeconds);
            }
            ClearHighlights();
            guidance.ShowMessage(GoalText, false);
            yield return WaitUnscaled(config.goalMessageSeconds);
            guidance.Hide();

            if (placementPhaseView != null) placementPhaseView.ReleaseIntroHold();
        }

        private void ShowPlaceable()
        {
            if (bridge == null) return;
            bridge.HideBlockedHighlight();
            bridge.ShowPlacementHighlight(tutorialUnit);
        }

        private void ShowBlocked()
        {
            if (bridge == null) return;
            bridge.HidePlacementHighlight();
            bridge.ShowBlockedHighlight(tutorialUnit);
        }

        private void ClearHighlights()
        {
            if (bridge == null) return;
            bridge.HidePlacementHighlight();
            bridge.HideBlockedHighlight();
        }

        // ── B3 · B4 ─────────────────────────────────────────────────────────
        private IEnumerator RunBattle()
        {
            // 계약 5 — GO! 부터 딤이 떠 있다. 이 창을 열어두면 플레이어가 캐논을 먼저 놓거나
            // (maxOnBoard 1) 각성 카드를 먼저 써서(게이지 여유 정확히 0) 아래 구간이
            // 통째로 스킵 조건에 걸린다. 강제는 연출이 아니라 시퀀스의 성립 조건이다.
            DimOnly();
            yield return WaitUnscaled(config.battleFreezeAtSeconds);

            yield return RunPickAndPlace();
            yield return RunAttach();

            Close();
        }

        private IEnumerator RunPickAndPlace()
        {
            // ⚠ 지불 판정은 **정지 전에** 한다. 정지 중에는 코스트도 쿨타임도 회복되지 않아
            // 「기다리면 가능해진다」가 없다 — 불가면 아예 멈추지 않고 건너뛴다.
            if (defenderSelector == null || !defenderSelector.IsSlotUsableNow(tutorialUnit)) yield break;
            if (!defenderSelector.TryGetSlotRect(tutorialUnit, out var slotRect) || slotRect == null) yield break;

            Freeze();

            // 3.1 유닛 터치
            var drag = defenderSelector.DragController;
            _armed = false;
            if (drag != null)
            {
                drag.Armed += OnArmed;          // 탭 배치
                drag.UserDragStarted += OnDragStarted;  // ⚠ 드래그는 Armed 를 안 낸다 — 둘 다 받아야 한다
                drag.PlacementCommitted += OnPlaced;
            }
            Focus(slotRect, PickText);
            yield return WaitFor(() => _armed || _placed, config.stepTimeoutSeconds);

            // 3.2 배치 — 어느 칸에 놓든 통과시킨다.
            // 딤+구멍으로는 드롭 칸을 제한할 수 없다(트레이→보드 드래그는 이미 시작된 UGUI
            // 드래그라 딤을 통과하고 드롭 셀은 보드 레이캐스트가 정한다). 원문도 영역 지시다.
            if (!_placed)
            {
                ShowPlaceable();
                guidance.ClearFocus();
                overlay.SetHoles(null);   // 보드 전체를 연다 — 구멍으로 칸을 못 막으므로 막는 척하지 않는다
                guidance.ShowMessage(PlaceText, false);
                yield return WaitFor(() => _placed, config.stepTimeoutSeconds);
            }
            ClearHighlights();
            if (drag != null)
            {
                drag.Armed -= OnArmed;
                drag.UserDragStarted -= OnDragStarted;
                drag.PlacementCommitted -= OnPlaced;
            }
            if (!_placed) yield break;   // 타임아웃 — 완료로 치지 않는다(계약 11)

            // 3.3 배치 스킬 관람 — **정지를 푼다**(딤은 유지). 멈춘 채 문구만 띄우면
            // "전황을 유리하게"가 말뿐이 된다. 발동 자체는 기존 경로가 한다.
            guidance.Hide();
            DimOnly();
            Unfreeze();
            yield return WaitUnscaled(config.onPlaceWatchSeconds);

            Freeze();
            guidance.ShowMessage(OnPlaceText, false);
            yield return WaitUnscaled(config.goalMessageSeconds);
            _b3Completed = true;
        }

        private IEnumerator RunAttach()
        {
            // 재개 구간. 딤은 유지한다 — 이 창을 열면 플레이어가 각성 카드를 먼저 써버릴 수
            // 있고, 게이지 여유는 정확히 0(시작 20 / 부착 비용 20)이라 그 즉시 아래가 사라진다.
            guidance.Hide();
            DimOnly();
            Unfreeze();
            yield return WaitUnscaled(config.resumeBeforeAttachSeconds);

            Freeze();

            // 4.1 보드의 캐논 선택. 대상이 죽었으면 살아 있는 배치 유닛으로 바꾸고
            // **문구도 그 유닛 이름으로 바꾼다** — 캐논이라 말하며 딴 유닛을 가리키지 않는다.
            string reselectText = ReselectText;
            if (bridge == null || !bridge.TryGetDeployedEntity(tutorialUnit, out _hostEntity) || _hostEntity == Entity.Null)
            {
                if (bridge == null || !TryGetAnyDeployed(out _hostEntity, out var fallbackUnit)) yield break;
                reselectText = string.Format(ReselectFallbackFormat,
                    fallbackUnit != null ? fallbackUnit.displayName : "배치한");
            }
            if (!TryMakeWorldProxy(_hostEntity, out var hostRect)) yield break;

            _selectionSet = false;
            if (handView != null) handView.SelectionTargetSet += OnSelectionSet;
            // ⚠ 이미 그 유닛이 선택돼 있으면 재탭이 «닫기»가 된다(SelectionTargetSet 미발화).
            bool alreadySelected = handView != null && handView.SelectionTarget == _hostEntity;
            if (!alreadySelected)
            {
                Focus(hostRect, reselectText);
                yield return WaitFor(() => _selectionSet, config.stepTimeoutSeconds);
            }
            if (handView != null) handView.SelectionTargetSet -= OnSelectionSet;
            if (!_selectionSet && !alreadySelected) yield break;

            // 4.2 카드 선택 — 지금 부착 가능한 Unit 카드에만 구멍을 뚫는다.
            // 액티브 카드는 즉발 탭이 거절되고(끌어서 사용) 커밋 경로가 AttachmentsChanged 를
            // 발화하지 않아, 열어두면 각성을 쓰고도 안내가 안 넘어간다.
            var holes = CollectAttachableCardRects(out int openCount);
            if (openCount == 0) yield break;
            _attachBaseline = handController != null ? handController.AttachCountOf(_hostEntity) : -1;
            if (handController != null) handController.AttachmentsChanged += OnAttachmentsChanged;
            guidance.ClearFocus();
            overlay.SetHoles(holes);
            guidance.ShowMessage(openCount == 4 ? CardText : CardFallbackText, false);
            yield return WaitFor(() => _attachBaseline >= 0 && handController != null
                                       && handController.AttachCountOf(_hostEntity) > _attachBaseline,
                                 config.stepTimeoutSeconds);
            if (handController != null) handController.AttachmentsChanged -= OnAttachmentsChanged;
            bool attached = handController != null && _attachBaseline >= 0
                            && handController.AttachCountOf(_hostEntity) > _attachBaseline;
            if (!attached) yield break;

            // 4.3 마무리 — 문구만(구멍 없음)
            guidance.ClearFocus();
            yield return WaitUnscaled(config.attachSettleSeconds);
            overlay.SetHoles(null);
            guidance.ShowMessage(AttachDoneText, false);
            yield return WaitUnscaled(config.goalMessageSeconds);
            _b4Completed = true;
        }

        // ── 닫기 ────────────────────────────────────────────────────────────
        private void Close()
        {
            ClearHighlights();
            guidance.ClearFocus();
            guidance.Hide();
            overlay.Hide();
            Unfreeze();
            DestroyWorldProxy();

            // ⚠ 스킵/타임아웃으로 끝난 판은 완료로 기록하지 않는다(계약 11). 1회성이라
            // 기록해버리면 핵심을 한 번도 못 본 계정이 다시 볼 기회를 영영 잃는다.
            if (_b3Completed && _b4Completed && profileSO != null && profileSO.profile != null)
            {
                profileSO.profile.firstRunTutorialDone = true;
                ProfileStore.Save(profileSO.profile);
                Debug.Log("[FirstRunTutorial] 온보딩 완료 — 기록했다.", this);
            }
            else
            {
                Debug.Log($"[FirstRunTutorial] 미완료로 끝났다(b3={_b3Completed} b4={_b4Completed}) — 다음 판에 다시 뜬다.", this);
            }
            _running = false;
        }

        // ── 도구 ────────────────────────────────────────────────────────────

        // ⚠ 우선순위 100 이 필수다. 손패·유닛 선택이 같은 도메인을 priority 50 으로 요청하므로
        // 기본 0 으로 잡으면 유닛을 고르는 순간 판이 0.3배로 다시 흐른다. 선례는 MenuPopup.
        private void Freeze()
        {
            if (_frozen) return;
            _freeze = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
            _frozen = true;
        }

        private void Unfreeze()
        {
            if (!_frozen) return;
            _freeze.Dispose();
            _frozen = false;
        }

        private void DimOnly()
        {
            overlay.SetSortingOrder(guidance.DimSortingOrder);
            overlay.SetHoles(null);
            overlay.Show();
        }

        private void Focus(RectTransform target, string text)
        {
            overlay.SetSortingOrder(guidance.DimSortingOrder);
            overlay.SetHoles(new[] { target });
            overlay.Show();
            guidance.ShowMessage(text, false);
            guidance.FocusUi(target);
        }

        private void Unsubscribe()
        {
            var drag = defenderSelector != null ? defenderSelector.DragController : null;
            if (drag != null)
            {
                drag.Armed -= OnArmed;
                drag.UserDragStarted -= OnDragStarted;
                drag.PlacementCommitted -= OnPlaced;
            }
            if (handView != null) handView.SelectionTargetSet -= OnSelectionSet;
            if (handController != null) handController.AttachmentsChanged -= OnAttachmentsChanged;
        }

        private void OnArmed(DefenderUnitData _) => _armed = true;
        private void OnDragStarted() => _armed = true;
        private void OnPlaced(DefenderUnitData _) => _placed = true;
        private void OnSelectionSet() => _selectionSet = true;
        // AttachmentsChanged 는 회수로도 울린다 — 여기서는 신호만 받고 판정은 등록부 카운트로 한다.
        private void OnAttachmentsChanged() { }

        private bool TryGetAnyDeployed(out Entity entity, out DefenderUnitData unit)
        {
            entity = Entity.Null;
            unit = null;
            if (defenderSelector == null || bridge == null) return false;
            for (int i = 0; i < defenderSelector.SlotCount; i++)
            {
                var u = defenderSelector.SlotUnitAt(i);
                if (u == null) continue;
                if (bridge.TryGetDeployedEntity(u, out entity) && entity != Entity.Null) { unit = u; return true; }
            }
            return false;
        }

        // 보드 위 유닛을 구멍/포커스의 대상으로 쓰려면 화면 좌표를 감싸는 RectTransform 이 필요하다.
        // 오버레이의 host root(ScreenSpaceOverlay)에 매달아 좌표계를 맞춘다.
        private bool TryMakeWorldProxy(Entity entity, out RectTransform rect)
        {
            rect = null;
            if (bridge == null || boardCamera == null) return false;
            if (!bridge.TryGetUnitViewAnchor(entity, out var anchor) || anchor == null) return false;
            var host = overlay.EnsureHostRoot();
            if (host == null) return false;
            if (_worldProxy == null)
            {
                var go = new GameObject("TutorialWorldProxy", typeof(RectTransform));
                _worldProxy = (RectTransform)go.transform;
                _worldProxy.SetParent(host, false);
                _worldProxy.sizeDelta = new Vector2(180f, 180f);
            }
            Vector3 screen = boardCamera.WorldToScreenPoint(anchor.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(host, screen, null, out var local)) return false;
            _worldProxy.anchoredPosition = local;
            _worldProxy.gameObject.SetActive(true);
            rect = _worldProxy;
            return true;
        }

        private void DestroyWorldProxy()
        {
            if (_worldProxy == null) return;
            Destroy(_worldProxy.gameObject);
            _worldProxy = null;
        }

        private RectTransform[] CollectAttachableCardRects(out int count)
        {
            count = 0;
            if (handView == null || handView.Slots == null) return null;
            var list = new System.Collections.Generic.List<RectTransform>();
            var slots = handView.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.rect == null || slot.card == null) continue;
                if (slot.card.type == CardType.Active) continue;   // 즉발 탭 거절 + AttachmentsChanged 미발화
                if (!slot.Playable) continue;
                list.Add(slot.rect);
            }
            count = list.Count;
            return list.ToArray();
        }

        // 러너 시계는 unscaled 다 — 자기가 Battle 을 멈춰놓고 그 시계를 기다리면 영영 안 온다.
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator WaitFor(System.Func<bool> done, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (done()) yield break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
