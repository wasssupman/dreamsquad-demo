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
        private const string EnemyApproachText = "악몽이 배치 영역 안으로 들어오면!";
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
            yield return WaitFor(() => _armed || _placed);

            // 3.1b 악몽이 배치 영역 안으로 들어올 때까지 기다린다.
            //
            // ⚠ 이 대기가 없으면 «적들의 머리위에 배치해보세요» 가 적이 아직 배치 영역 밖에
            // 있을 때 뜬다 — 문장과 화면이 어긋나고, 그렇게 놓은 배치 스킬은 아무도 못 때려서
            // 바로 다음 문구인 «전황을 유리하게» 가 통째로 희석된다(실제 Play 에서 그랬다).
            //
            // Duel 기준 적 구조물은 x=16·18 이고 배치 영역은 x≤14 라, 적은 **배치 불가 구역에서
            // 나와 배치 영역으로 걸어 들어온다** — 기다릴 만한 사건이 실제로 존재한다.
            //
            // 입력은 막은 채(딤 유지, 구멍 없음) **시간만 흘린다** — 정지 상태로 기다리면
            // 적이 영원히 안 온다. 배치 영역 하이라이트를 켜둬서 문구의 «배치 영역» 이
            // 화면에서 실제로 보이게 한다.
            //
            // ⚠ 기준은 **시간이 아니라 사건**이다. 배치 영역 진입으로 잡으면 너무 일러
            // (Duel 은 배치 영역이 x≤14 로 넓어 적이 스폰 직후 걸린다) 문구가 0초 노출되고
            // «그냥 지나간» 것처럼 보인다. 강(Env 타일)은 맵 한가운데를 가르는 눈에 보이는
            // 경계라 «저기까지 왔다» 가 화면에서 읽힌다.
            //
            // 강이 없는 맵을 위한 폴백은 배치 영역 진입이다 — 없으면 조건이 영영 안 서고,
            // 타임아웃이 없으므로(계약 11) 그대로 멈춘다.
            if (!_placed && bridge != null)
            {
                bool useRiver = bridge.MapHasEnvTiles;
                ShowPlaceable();
                guidance.ClearFocus();
                DimOnly();
                Unfreeze();
                guidance.ShowMessage(EnemyApproachText, false);
                yield return WaitFor(() => _placed || (useRiver
                    ? bridge.AnyEnemyNearEnvTile(config.riversideTiles)
                    : bridge.AnyEnemyInPlacementArea(tutorialUnit)));
                Freeze();
            }

            // 3.2 배치 — 어느 칸에 놓든 통과시킨다.
            // 딤+구멍으로는 드롭 칸을 제한할 수 없다(트레이→보드 드래그는 이미 시작된 UGUI
            // 드래그라 딤을 통과하고 드롭 셀은 보드 레이캐스트가 정한다). 원문도 영역 지시다.
            if (!_placed)
            {
                ShowPlaceable();
                guidance.ClearFocus();
                // ⚠ **이 구간만 딤을 내린다.** 배치는 보드 입력을 요구하는데 딤은 보드 탭을
                // 막는다(슬롯을 탭해 arm 한 뒤 보드를 탭하는 경로가 그대로 죽는다).
                //
                // `SetHoles(null)` 로 열 수 없다 — 그건 «구멍 없는 풀 dim» 이다(도구의 문서
                // 주석 그대로). 보드는 UGUI 가 아니라 감쌀 RectTransform 이 없고, 애초에
                // 구멍으로는 드롭 «칸» 을 제한할 수도 없다(트레이→보드 드래그는 이미 시작된
                // UGUI 드래그라 딤을 통과하고 드롭 셀은 보드 레이캐스트가 정한다).
                //
                // 대가: 이 짧은 구간에 플레이어가 손패를 열어 각성을 쓸 수 있다. 배치하는
                // 즉시 끝나는 구간이라 받아들인다 — 막으면 배치 자체가 불가능해진다.
                overlay.Hide();
                guidance.ShowMessage(PlaceText, false);
                yield return WaitFor(() => _placed);
            }
            ClearHighlights();
            if (drag != null)
            {
                drag.Armed -= OnArmed;
                drag.UserDragStarted -= OnDragStarted;
                drag.PlacementCommitted -= OnPlaced;
            }

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
            // 재개 구간은 **진짜 플레이**다 — 딤을 내린다.
            //
            // ⚠ 여기에 딤을 켜두면 B4 가 구조적으로 실패한다. Duel 은 배치 가능 칸이 곧 적이
            // 걷는 길이라, 방금 놓은 캐논 한 기가 5초를 혼자 버티지 못하고 죽는다. 그런데
            // 플레이어는 딤에 묶여 유닛을 더 놓지도 못하고 그걸 구경만 하게 된다 — 그리고
            // 부착할 대상이 없어 튜토리얼이 조용히 닫힌다(실제로 그렇게 물렸다).
            //
            // 대가로 이 창에 각성을 먼저 쓸 수 있지만(게이지 여유가 정확히 0), 안내가 손패를
            // 가리키지 않는 구간이라 위험이 낮고, 무엇보다 대상이 죽는 쪽이 확실한 실패다.
            guidance.Hide();
            overlay.Hide();
            Unfreeze();
            yield return WaitUnscaled(config.resumeBeforeAttachSeconds);

            // 부착할 유닛이 살아 있을 때까지 기다린다(딤은 계속 내려둔 채 — 플레이어가 더 놓을
            // 수 있어야 한다). 즉시 포기하면 캐논이 죽은 판은 전부 B4 를 못 본다.
            yield return WaitFor(() => TryResolveHost(out _, out _));
            if (!TryResolveHost(out _hostEntity, out var hostUnit)) yield break;

            Freeze();

            // 4.1 보드의 캐논 선택. 대상이 캐논이 아니면 **문구도 그 유닛 이름으로 바꾼다** —
            // 캐논이라 말하며 딴 유닛을 가리키지 않는다.
            string reselectText = hostUnit == tutorialUnit
                ? ReselectText
                : string.Format(ReselectFallbackFormat,
                    hostUnit != null ? hostUnit.displayName : "배치한");
            if (!TryMakeWorldProxy(_hostEntity, out var hostRect)) yield break;

            _selectionSet = false;
            if (handView != null) handView.SelectionTargetSet += OnSelectionSet;
            // ⚠ 이미 그 유닛이 선택돼 있으면 재탭이 «닫기»가 된다(SelectionTargetSet 미발화).
            bool alreadySelected = handView != null && handView.SelectionTarget == _hostEntity;
            if (!alreadySelected)
            {
                Focus(hostRect, reselectText);
                yield return WaitFor(() => _selectionSet);
            }
            if (handView != null) handView.SelectionTargetSet -= OnSelectionSet;

            // 4.2 카드 선택 — 지금 부착 가능한 Unit 카드에만 구멍을 뚫는다.
            // 액티브 카드는 즉발 탭이 거절되고(끌어서 사용) 커밋 경로가 AttachmentsChanged 를
            // 발화하지 않아, 열어두면 각성을 쓰고도 안내가 안 넘어간다.
            //
            // ⚠ **구멍을 한 번만 잡으면 안 된다.** 선택 직후엔 손패가 아직 딜인 중이라 카드가
            // 비활성이고, SetHoles 는 비활성 대상을 버린 뒤 다시 담지 않는다(오버레이 LateUpdate
            // 는 코너 «변화» 만 추적한다) → 구멍 0개 = 풀 dim 이라 카드를 못 누른다. 실제로
            // 그렇게 물렸다. 그래서 **부착이 성사될 때까지 매 프레임 구성을 보고 바뀌면 다시
            // 잡는다** — 딜인이 늦게 끝난 카드도 그때 열린다.
            yield return WaitFor(() => AttachableCardCount() > 0);
            if (AttachableCardCount() == 0) yield break;
            _attachBaseline = handController != null ? handController.AttachCountOf(_hostEntity) : -1;
            if (handController != null) handController.AttachmentsChanged += OnAttachmentsChanged;
            guidance.ClearFocus();

            int shownCount = -1;
            while (!(handController != null && _attachBaseline >= 0
                     && handController.AttachCountOf(_hostEntity) > _attachBaseline))
            {
                int now = AttachableCardCount();
                if (now != shownCount)
                {
                    shownCount = now;
                    overlay.SetHoles(CollectAttachableCardRects(out _));
                    // 문구는 «몇 장» 이 바뀔 때만 갱신한다 — 매번 부르면 말풍선이 다시 뜬다.
                    guidance.ShowMessage(now == 4 ? CardText : CardFallbackText, false);
                }
                yield return null;
            }
            if (handController != null) handController.AttachmentsChanged -= OnAttachmentsChanged;

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

        // 부착 호스트 해결: 온보딩이 가리키던 유닛 우선, 없으면 살아 있는 배치 유닛 아무나.
        // TryGetDeployedEntity 는 _em.Exists 로 생존까지 본다 — 죽은 유닛은 여기서 걸러진다.
        private bool TryResolveHost(out Entity entity, out DefenderUnitData unit)
        {
            entity = Entity.Null;
            unit = null;
            if (bridge == null) return false;
            if (bridge.TryGetDeployedEntity(tutorialUnit, out entity) && entity != Entity.Null)
            {
                unit = tutorialUnit;
                return true;
            }
            return TryGetAnyDeployed(out entity, out unit);
        }

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

        // «지금 구멍을 뚫을 수 있는 카드» 의 단일 술어. 세는 곳과 모으는 곳이 갈리면
        // 「n장 있다고 세어놓고 0개를 넘기는」 상태가 생긴다 — 실제로 그렇게 물렸다.
        //
        // ⚠ activeInHierarchy 를 여기서 본다. SetHoles 는 비활성 대상을 **버리고**, 버린 것은
        // 다시 담기지 않는다(오버레이의 LateUpdate 는 코너 «변화» 만 추적한다). 손패 딜인이
        // 끝나기 전에 넘기면 구멍 0개 = 풀 dim 이 되어 카드를 못 누른다.
        private bool IsAttachableSlot(DreamcatcherHandView.CardSlot slot)
        {
            if (slot == null || slot.rect == null || slot.card == null) return false;
            if (!slot.rect.gameObject.activeInHierarchy) return false;
            if (slot.card.type == CardType.Active) return false;   // 즉발 탭 거절 + AttachmentsChanged 미발화
            return slot.Playable;
        }

        private int AttachableCardCount()
        {
            if (handView == null || handView.Slots == null) return 0;
            int n = 0;
            var slots = handView.Slots;
            for (int i = 0; i < slots.Count; i++) if (IsAttachableSlot(slots[i])) n++;
            return n;
        }

        private RectTransform[] CollectAttachableCardRects(out int count)
        {
            count = 0;
            if (handView == null || handView.Slots == null) return null;
            var list = new System.Collections.Generic.List<RectTransform>();
            var slots = handView.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (IsAttachableSlot(slots[i])) list.Add(slots[i].rect);
            count = list.Count;
            return list.ToArray();
        }

        // 러너 시계는 unscaled 다 — 자기가 Battle 을 멈춰놓고 그 시계를 기다리면 영영 안 온다.
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // **타임아웃이 없다**(사용자 결정). 안내가 요구한 행동을 할 때까지 기다린다 —
        // 흘려보내면 그 판은 어차피 완료로 기록되지 않아 다음 판에 처음부터 다시 뜬다.
        // 「기다린다」가 「또 처음부터」보다 낫다.
        //
        // 대신 **모든 조건 대기는 만족 가능해야 한다**. 선행조건이 없으면 기다리지 말고
        // 진입 전에 건너뛴다(캐논이 소진/쿨타임이면 3.1 을 안 열고, 낼 수 있는 카드가 0이면
        // 4.2 를 안 연다). 정지 중에는 코스트·쿨타임이 안 도니 «기다리면 가능해진다»가 없다.
        private static IEnumerator WaitFor(System.Func<bool> done)
        {
            while (!done()) yield return null;
        }
    }
}
