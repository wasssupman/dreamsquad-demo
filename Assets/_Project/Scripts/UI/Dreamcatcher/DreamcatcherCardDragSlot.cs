using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand unit 7~9 — drag-to-use for one hand card slot
    // (DefenderDragSlot pattern: the slot IS the drag source). Owned/bound by
    // DreamcatcherHandView, which supplies bridge/camera/controller.
    //
    // Aim modes (classified ONCE at drag start — single source, every handler
    // branches on it):
    // - Defender (Unit/Squad/Active-DefenderUnit): the card stays seated in the
    //   hand (StS style) and a dotted arrow runs to the pointer; the hovered
    //   defender shows a red spine tint. Touchup on a unit commits immediately.
    // - ActiveTile (Meteor 계열): the card follows the pointer with the skill
    //   range preview; touchup on a tile casts.
    // - ActivePortal: touchup = entry tile, then a second field tap picks the
    //   exit (old SkillBar two-tap machine).
    // Cancel = touchup inside the hand panel / ESC / phase exit — never spends.
    public class DreamcatcherCardDragSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private enum AimMode
        {
            None,         // unclassifiable (Active without a skill) — drag blocked
            Defender,     // arrow + unit tint + unit drop
            ActiveTile,   // card-follow + range preview + tile drop
            ActivePortal, // card-follow + entry drop → two-tap exit
            // subconscious-curse-expansion unit 3 — 살찌운 제물: arrow + 최근접 적 픽 드롭
            EnemyMark
        }

        private DreamcatcherHandView _view;
        private int _index;

        private bool _dragging;
        private AimMode _mode = AimMode.None;
        private Vector2Int? _hoverCell; // hovered defender's cell (Active-defender commit arg)
        private Entity _hoverEntity = Entity.Null;
        // unit 8 (M1) — Active aim mirrors GameManager.IsAiming (PlacementInput
        // mutual exclusion, old SkillBar lifecycle).
        private bool _activeAiming;
        private Vector2Int? _portalEntryCell;          // Portal two-tap: entry captured
        private Vector2Int _lastRangeCell = new(-1, -1); // aim range preview cache
        // dreamcatcher-attach-lockon — 조준 시작 attachable 스냅샷(부착수는 드래그 중 불변).
        private readonly System.Collections.Generic.List<(Entity entity, Rect rect)> _defRectBuf = new();
        private readonly System.Collections.Generic.HashSet<Entity> _attachable = new();
        private bool _enemyMarkHoverValid; // 적 표식: 현재 호버가 유효(미표식 적)인가
        private bool _enemyMarkOnUnit;     // 적 표식: 손가락이 아군 유닛 위(무효 사유 구분 — unit 3 브리핑)

        public bool IsDragging => _dragging;
        public bool IsPortalAiming => _portalEntryCell.HasValue;
        // hand-drag-clearance unit 0 — 카드가 포인터를 따라가는 모드(UpdateDragVisual 이
        // 위치를 **스크린 좌표**로 직접 쓴다). 손패 하강 중에도 카드는 손가락에 남아야 하므로
        // View 가 패널을 움직일 때 이 슬롯만 위치를 보존한다. Defender/EnemyMark 는 카드가
        // 손패 고정(화살표만 포인터 추종)이라 패널과 함께 내려가는 게 맞다.
        public bool IsPointerFollowing =>
            _dragging && (_mode == AimMode.ActiveTile || _mode == AimMode.ActivePortal);

        public void Bind(DreamcatcherHandView view, int index)
        {
            _view = view;
            _index = index;
        }

        private DreamcatcherHandView.CardSlot Slot => _view.Slots[_index];

        // hand-deal-in unit 1 — 눌러서 들기(press-to-lift, 모바일): press=focus, release=clear.
        // 뷰가 슬롯 target 을 조작(스프링이 해석). PointerDown 은 BeginDrag 보다 먼저 발화한다.
        // hand-drag-tooltip rev 4 — press 중 성능 툴팁 상시 노출(usable 무관).
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_view == null || _dragging) return;
            // 딜이 아직 진행 중이면 이 터치로 즉시 완주 → 게이트가 열려 같은 press 가 바로 집는다.
            _view.TryFastForwardDeal();
            _view.SetFocus(_index);
            // hand-card-face unit 3 — press 브리핑: 조작법 + 시작 상태(사용 불가 사유 포함).
            // 카드 설명은 카드 면이 담당하므로 여기는 조작 안내만.
            if (_view.CanPeek(_index))
            {
                string status = Slot.usable
                    ? "위로 끌어 올려 사용하세요"
                    : "<color=#FF9B8A>각성치가 부족합니다</color>";
                _view.ShowDragBriefing(ControlsFor(Classify(Slot.card), Slot.card), status);
            }
        }

        // 해제 시 숨김 — 단 드래그로 이어졌으면 EndInteraction 깔때기가, 포탈 조준은
        // 조준 종료가 걷는다. (PointerUp 은 EndDrag 보다 먼저 → 그 시점 _dragging 유지)
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_view == null) return;
            _view.ClearFocus(_index);
            if (!_dragging && !IsPortalAiming) _view.HideDragTooltip();
        }

        // ── selection-hand-attach unit 3 — 탭 즉발 부착 ───────────────────────
        // 유닛이 선택돼 있으면(손패가 그 대상을 들고 있다) 카드 **탭**만으로 그 유닛에 부착한다.
        // 커밋은 D&D 성공 경로와 완전히 같은 CommitAttach → HandChanged(Used) 를 지나므로
        // 유지/자동닫힘/재딜인/무차감 거절이 자동으로 승계된다(계약 4).
        //
        // ⚠ "드래그로 이어지면 UGUI 가 클릭을 삼킨다" 는 **거짓**이다(critic M1):
        // eligibleForClick 은 pointerPress != pointerDrag 일 때만 해제되는데 이 슬롯은 press·drag
        // 핸들러가 같은 GameObject 라 드래그 내내 eligible 이 유지되고, 클릭은 OnEndDrag 보다
        // **먼저** 발화한다. 즉 "손패로 되돌려 취소" 제스처도 클릭을 함께 낸다 → 가드 0 필수.
        // (레포 선례: DraftCardView 가 _dragHappened 로 명시 차단. DefenderDragSlot 의
        //  "끌기면 이건 안 옴" 주석은 같은 오해를 담고 있어 선례로 삼지 않는다.)
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_view == null) return;
            if (_dragging || IsPortalAiming) return;                 // 가드 0 — 드래그/조준의 릴리즈
            var target = _view.SelectionTarget;
            if (target == Entity.Null) return;                       // 선택 없음 = 즉발 개념 없음(움찔도 없다)
            if (!_view.CanPeek(_index)) return;                      // 전환 중/타 인터랙션/재딜 중
            var slot = Slot;
            if (slot.entryId < 0 || slot.card == null) return;

            // 즉발은 부착(Unit/Squad)만 — 사용자 결정 4. 적 표식(BountyMark)은 Classify 가
            // EnemyMark 를 돌려주므로 이 조건에서 자동 배제된다(card.type == Unit 만 보면 통과한다).
            if (Classify(slot.card) != AimMode.Defender || slot.card.type == CardType.Active)
            {
                // Active 는 선택 중 아예 사용 불가(selection-active-block) — 드래그로도 안 되니
                // "끌어서 쓰라" 고 안내하면 거짓이 된다. 적 표식은 드래그로는 여전히 된다.
                if (slot.card.type == CardType.Active) ShowSelectionBlocked();
                else Reject("이 카드는 <color=#FFD98A>끌어서</color> 사용하세요");
                return;
            }
            if (!slot.usable)
            {
                Reject("<color=#FF9B8A>각성치가 부족합니다</color>");
                return;
            }
            // D&D 의 _attachable 스냅샷과 동일한 판정 — 커밋 거절과 UI 를 일치시킨다(계약 5).
            if (!_view.Controller.CanAttachMore(target) ||
                !_view.Bridge.WouldDreamcatcherCardApply(target, slot.card))
            {
                Reject("<color=#FF9B8A>이 유닛에는 부착할 수 없습니다</color>");
                return;
            }

            // 발사점/스프라이트는 커밋이 손패를 소비하기 전에 캡처(D&D 와 동일 계약).
            int entryId = slot.entryId;
            Vector3 startUiWorld = slot.rect.position;
            Vector2 ghostSize = slot.rect.rect.size;
            Sprite face = slot.art != null ? slot.art.sprite : null;
            var host = target;
            CommitNow(() => _view.Controller.CommitAttach(entryId, host),
                () => _view.FlyCardToUnit(startUiWorld, ghostSize, face, host));
        }

        // selection-active-block — Active 차단 피드백(드래그 경로·탭 경로 공용). 헤더에 조작법을
        // 그대로 쓰면 "끌어서 시전" 과 "사용 불가" 가 한 화면에서 모순되므로, 헤더는 **해제 방법**을
        // 안내한다(막힌 이유만 알려주고 빠져나갈 길을 안 주면 안 된다).
        private void ShowSelectionBlocked()
        {
            _view.FlinchSlot(_index);
            _view.ShowDragBriefing(
                "유닛 선택을 해제한 뒤 사용하세요  ·  빈 곳을 탭하면 해제",
                "<color=#FF9B8A>유닛 선택 중에는 사용할 수 없습니다</color>");
        }

        // 즉발 거절 — 움찔 + 사유(기존 브리핑 표면 재사용, 신규 텍스트 위젯 없음). 차감 0.
        private void Reject(string reason)
        {
            _view.FlinchSlot(_index);
            _view.ShowDragBriefing(ControlsFor(Classify(Slot.card), Slot.card), reason);
        }

        private static AimMode Classify(DreamcatcherCard card)
        {
            if (card == null) return AimMode.None;
            switch (card.type)
            {
                // subconscious-curse-expansion unit 3 — BountyMark 카드는 적 타겟.
                // 정식 라우팅은 이 판별 하나 — CommitAttach 로 새어도 bake 의
                // trigger=None 가드가 무차감 거절한다(unit 2 계약).
                // hand-card-face — 판별은 DreamcatcherCard.HasBountyMark 로 단일화
                // (손패 태그 칩과 공유).
                case CardType.Unit when card.HasBountyMark():
                    return AimMode.EnemyMark;
                case CardType.Unit:
                case CardType.Squad: // unit 9 — host-bound, aims like Unit
                    return AimMode.Defender;
                case CardType.Active when card.skill != null:
                    if (card.skill.target == SkillTargetType.DefenderUnit) return AimMode.Defender;
                    return card.skill.effect == SkillEffectType.Portal
                        ? AimMode.ActivePortal : AimMode.ActiveTile;
                default:
                    return AimMode.None;
            }
        }

        // ── drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_view == null || _portalEntryCell.HasValue || !_view.CanStartDrag(_index)) return;
            var slot = Slot;
            _mode = Classify(slot.card);
            if (_mode == AimMode.None) return;
            // selection-active-block (사용자 결정 2026-07-29) — 유닛이 선택된 동안 Active 카드는
            // 쓸 수 없다. 선택 중 손패는 **부착 전용** 이라는 규칙이라 D&D 도 막는다(탭 즉발은
            // OnPointerClick 이 같은 사유로 거절). 차감·조준 진입 없이 물러난다.
            if (slot.card.type == CardType.Active && _view.SelectionTarget != Entity.Null)
            {
                _mode = AimMode.None; // 조준 상태를 남기지 않는다(EndInteraction 경유 안 함)
                ShowSelectionBlocked();
                return;
            }

            _dragging = true;
            _view.SetFocus(-1); // hand-deal-in — 드래그 시작 시 focus 해제(이웃 scatter 복귀)
            slot.rect.SetAsLastSibling(); // float above sibling cards
            if (_mode == AimMode.Defender || _mode == AimMode.EnemyMark)
                slot.rect.localScale = Vector3.one * 1.08f; // 선택 카드 강조(카드는 손패 고정)

            if (slot.card.type == CardType.Active && GameManager.Instance != null)
            {
                _activeAiming = true;
                GameManager.Instance.IsAiming = true;
                GameManager.Instance.SelectedDefender = null; // last-pressed-wins
            }
            // hand-card-face unit 3 — 조작 브리핑(조작법 고정 + 상태 실시간).
            _view.ShowDragBriefing(ControlsFor(_mode, slot.card), StatusFor(insideHand: false));
            BeginFocus(slot); // dreamcatcher-attach-lockon — dim/링/리티클/콜아웃 시작
            UpdateDragVisual(eventData.position);
        }

        // dreamcatcher-attach-lockon — 조준 종류별 포커스 연출 개시. Defender 부착
        // (Unit/Squad)만 attachable 스냅샷 + base-ring, Active-DefenderUnit 은 캐스트
        // 리티클(캡 무관), EnemyMark 는 dim 만(적 타겟은 별도 스코프).
        private void BeginFocus(DreamcatcherHandView.CardSlot slot)
        {
            if (_view.Focus == null) return;
            if (_mode == AimMode.Defender)
            {
                bool attach = slot.card != null &&
                    (slot.card.type == CardType.Unit || slot.card.type == CardType.Squad);
                if (attach)
                {
                    _view.Bridge.EnumerateDefenderScreenRects(_view.MainCamera, _defRectBuf);
                    _attachable.Clear();
                    for (int i = 0; i < _defRectBuf.Count; i++)
                    {
                        // 유효 = 부착 여유(캡) AND 이 카드가 이 유닛에 실제로 기여(통통구슬은
                        // 투사체 유닛만 등). 커밋 거절과 UI 를 일치시킨다.
                        var e = _defRectBuf[i].entity;
                        if (_view.Controller.CanAttachMore(e) && _view.Bridge.WouldDreamcatcherCardApply(e, slot.card))
                            _attachable.Add(e);
                    }
                    _view.Focus.Begin(DreamcatcherFocusPresenter.AimKind.AttachAim, _attachable);
                }
                else
                    _view.Focus.Begin(DreamcatcherFocusPresenter.AimKind.DefenderCast, null);
            }
            else if (_mode == AimMode.EnemyMark)
            {
                // 적은 portrait 가 없어 콜아웃 정체를 카드 아트+이름으로 표기.
                Sprite cardIcon = slot.art != null ? slot.art.sprite : null;
                string cardName = slot.nameLabel != null ? slot.nameLabel.text : "";
                _view.Focus.BeginEnemyMark(cardIcon, cardName);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            // 호버 판정을 먼저 — 화살표 색(유효 타겟)이 같은 프레임에 반영되도록.
            switch (_mode)
            {
                case AimMode.Defender: UpdateUnitHover(eventData.position); break;
                case AimMode.EnemyMark: UpdateEnemyHover(eventData.position); break;
                case AimMode.ActiveTile: UpdateAimRange(eventData.position, Slot.card.skill); break;
            }
            UpdateDragVisual(eventData.position);
            UpdateBriefingStatus(eventData.position); // unit 3 — 호버/취소영역 상태 줄
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            var slot = Slot;
            bool insideHand = _view.HandPanelRect != null && RectTransformUtility
                .RectangleContainsScreenPoint(_view.HandPanelRect, eventData.position, null);
            if (insideHand)
            {
                CancelDrag();
                return;
            }

            switch (_mode)
            {
                case AimMode.Defender:
                {
                    UpdateUnitHover(eventData.position);
                    if (_hoverEntity == Entity.Null) { CancelDrag(); return; } // no unit under touchup
                    var host = _hoverEntity;
                    var cell = _hoverCell ?? default;
                    int entryId = slot.entryId;
                    // dreamcatcher-taxonomy-cleanup unit 1 — Unit/Squad share one
                    // attach commit (host-bound, aims the same); only Active-
                    // DefenderUnit forks (casts a skill at the CELL, not an attach).
                    if (slot.card.type == CardType.Active)
                    {
                        // card-fly unit 2 — Active-Defender 는 셀(타일 월드)로 찰싹(유닛 없음 반응).
                        Vector3 startUiWorld = slot.rect.position;
                        Vector2 ghostSize = slot.rect.rect.size;
                        Sprite face = slot.art != null ? slot.art.sprite : null;
                        var cell2 = cell;
                        CommitNow(() => _view.Controller.CommitActiveDefender(entryId, cell),
                            () => _view.FlyCardToCell(startUiWorld, ghostSize, face, cell2));
                    }
                    else
                    {
                        // card-fly-to-target-absorb unit 0 — 발사점/스프라이트를 커밋 전에
                        // 캡처(성공 시 손패가 소비되므로). 유닛 케이스만 비행(타일=unit 2).
                        Vector3 startUiWorld = slot.rect.position;
                        Vector2 ghostSize = slot.rect.rect.size;
                        Sprite face = slot.art != null ? slot.art.sprite : null;
                        var host2 = host;
                        CommitNow(() => _view.Controller.CommitAttach(entryId, host),
                            () => _view.FlyCardToUnit(startUiWorld, ghostSize, face, host2));
                    }
                    return;
                }

                case AimMode.EnemyMark:
                {
                    // 픽 = 커밋 순간 스냅샷(최근접 적, 반경 SO 노브). 반경 내 적 없음 =
                    // 취소(무차감·카드 잔류 — contract 9).
                    UpdateEnemyHover(eventData.position);
                    if (_hoverEntity == Entity.Null) { CancelDrag(); return; }
                    var enemy = _hoverEntity;
                    int markEntryId = slot.entryId;
                    Vector3 mStart = slot.rect.position;
                    Vector2 mSize = slot.rect.rect.size;
                    Sprite mFace = slot.art != null ? slot.art.sprite : null;
                    var enemy2 = enemy;
                    CommitNow(() => _view.Controller.CommitMarkEnemy(markEntryId, enemy),
                        () => _view.FlyCardToUnit(mStart, mSize, mFace, enemy2));
                    return;
                }

                case AimMode.ActiveTile:
                    if (TryScreenToCell(eventData.position, out var tile))
                    {
                        ClearAimRange();
                        // card-fly unit 2 — 타일 캐스트. 카드는 포인터를 따라와 타겟 근처라 짧은 찰싹.
                        Vector3 tStart = slot.rect.position;
                        Vector2 tSize = slot.rect.rect.size;
                        Sprite tFace = slot.art != null ? slot.art.sprite : null;
                        var tile2 = tile;
                        CommitNow(() => _view.Controller.CommitActiveTile(slot.entryId, tile),
                            () => _view.FlyCardToCell(tStart, tSize, tFace, tile2));
                    }
                    else CancelDrag();
                    return;

                case AimMode.ActivePortal:
                    // Two-tap: touchup = entry tile, the NEXT field tap (Update
                    // below) picks the exit and commits. IsAiming stays true.
                    if (TryScreenToCell(eventData.position, out var entry))
                    {
                        _portalEntryCell = entry;
                        // unit 3 — 2탭 국면 전환 브리핑(툴팁은 조준 중 유지 계약).
                        _view.UpdateDragBriefingStatus(
                            "<color=#9FE6A0>입구 지정됨</color> — 출구 타일을 탭하세요");
                    }
                    else CancelDrag();
                    return;

                default:
                    CancelDrag();
                    return;
            }
        }

        // Portal second tap — polled like the old SkillBar aim loop.
        private void Update()
        {
            if (!_portalEntryCell.HasValue) return;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            var pos = pointer.position.ReadValue();

            bool insideHand = _view.HandPanelRect != null && RectTransformUtility
                .RectangleContainsScreenPoint(_view.HandPanelRect, pos, null);
            if (insideHand || !TryScreenToCell(pos, out var exitTile))
            {
                CancelDrag(); // hand-area tap or off-board = cancel, no spend
                return;
            }

            var entry = _portalEntryCell.Value;
            int entryId = Slot.entryId;
            _portalEntryCell = null;
            // card-fly unit 2 — 포탈 확정(두 번째 탭 = 출구). 카드는 출구 타일로 찰싹.
            var pSlot = Slot;
            Vector3 pStart = pSlot.rect.position;
            Vector2 pSize = pSlot.rect.rect.size;
            Sprite pFace = pSlot.art != null ? pSlot.art.sprite : null;
            var exit2 = exitTile;
            CommitNow(() => _view.Controller.CommitActivePortal(entryId, entry, exitTile),
                () => _view.FlyCardToCell(pStart, pSize, pFace, exit2));
        }

        // Touchup applies immediately: spend/cycle only happen inside a
        // successful Commit* (a failed commit — target died, cap reached,
        // cast rejected — costs nothing and the card snaps home).
        // card-fly-to-target-absorb unit 0 — onSuccess 는 커밋 성공(ok) 시에만
        // 발화(실패/취소는 비용 0 · 연출 없음 계약 유지). 비행 발사점(슬롯 위치)·
        // 고스트 스프라이트는 commit() 이 손패를 소비하기 전에 호출부에서 캡처한다.
        private void CommitNow(System.Func<bool> commit, System.Action onSuccess = null)
        {
            // dreamcatcher-attach-lockon 계약 #7/E — 성공 시 확정 비트(손끝 밖 펄스+햅틱).
            // selection-hand-attach unit 3 (critic M5) — 중심은 **커밋 전에** 캡처한다: 마지막
            // 사용 가능 카드의 커밋은 동기 HandChanged(Used) → OnCardUsed → Close() →
            // Focus.End() 를 태워 커밋 직후엔 락온 정보가 이미 지워져 있고, 그러면 Confirm() 이
            // 조용히 물러나 확정 비트가 사라진다. 펄스 자체는 독립 타이머라 End 후에도 완주한다.
            Vector2 pulseCenter = default;
            bool hasPulseCenter = false;
            if (_view.Focus != null) hasPulseCenter = _view.Focus.TryCaptureConfirmCenter(out pulseCenter);
            bool ok = commit();
            if (ok && hasPulseCenter) _view.Focus.Confirm(pulseCenter);
            EndInteraction();
            if (!ok) _view.RestoreSlotHome(_index);
            else onSuccess?.Invoke();
            _view.NotifyInteractionEnded();
        }

        public void CancelDrag()
        {
            _dragging = false;
            EndInteraction();
            _view.RestoreSlotHome(_index);
            _view.NotifyInteractionEnded();
            // deck-sfx — 카드 손패 복귀(취소/손패영역 드롭). 단일 취소 지점만(벌크 리셋 제외).
            Wassup.Core.SoundManager.Instance?.PlayCardReturn();
        }

        // Shared teardown for every interaction end (commit, cancel, disable):
        // arrow, hover tint, aim preview/IsAiming, portal state, mode.
        private void EndInteraction()
        {
            if (_mode == AimMode.Defender || _mode == AimMode.EnemyMark)
            {
                _view.TargetArrow?.Hide();
                _view.RestoreSlotHome(_index); // 확대 복원(성공 시 Refresh 가 재정렬)
            }
            _view.Focus?.End(); // dreamcatcher-attach-lockon — dim/링/리티클/콜아웃 정리
            ClearHover();
            ClearAimRange();
            _portalEntryCell = null;
            if (_activeAiming)
            {
                _activeAiming = false;
                if (GameManager.Instance != null) GameManager.Instance.IsAiming = false;
            }
            _mode = AimMode.None;
            // hand-drag-tooltip unit 1 — 종료 깔때기에서 숨김(포탈 첫 탭은 종료가
            // 아니라 조준 전환이므로 여기 안 옴 → 조준 중 유지 계약).
            if (_view != null) _view.HideDragTooltip();
        }

        // ── unit 3 — 조작 브리핑 문안 (header = 조작법 고정 / body = 상태 실시간) ──
        // 자명 분기 + 호출처 1 — 순수 함수 추출 대상 아님(CLAUDE.md 제약 10 판정).

        private static string ControlsFor(AimMode mode, DreamcatcherCard card)
        {
            switch (mode)
            {
                case AimMode.Defender:
                    return card != null && card.type == CardType.Active
                        ? "아군 유닛 위에서 놓으면 시전  ·  손패로 놓으면 취소"
                        : "아군 유닛 위에서 놓으면 부착  ·  손패로 놓으면 취소";
                case AimMode.ActiveTile:
                    return "원하는 타일에서 놓으면 시전  ·  손패로 놓으면 취소";
                case AimMode.ActivePortal:
                    return "놓아서 입구 지정 → 출구 타일 탭  ·  손패로 놓으면 취소";
                case AimMode.EnemyMark:
                    return "적 근처에서 놓으면 표식 부여  ·  손패로 놓으면 취소";
                default:
                    return "";
            }
        }

        private void UpdateBriefingStatus(Vector2 screenPos)
        {
            bool insideHand = _view.HandPanelRect != null && RectTransformUtility
                .RectangleContainsScreenPoint(_view.HandPanelRect, screenPos, null);
            _view.UpdateDragBriefingStatus(StatusFor(insideHand));
        }

        // 색 코딩: 초록 = 놓으면 커밋, 적색 = 불가/취소, 무색 = 안내.
        private string StatusFor(bool insideHand)
        {
            if (insideHand) return "<color=#FF9B8A>여기서 놓으면 취소</color>";
            switch (_mode)
            {
                case AimMode.Defender:
                    if (_hoverEntity == Entity.Null) return "아군 유닛 위로 끌어가세요";
                    if (!IsHoverAttachable()) return "<color=#FF9B8A>이 유닛에는 부착할 수 없습니다</color>";
                    return Slot.card != null && Slot.card.type == CardType.Active
                        ? "<color=#9FE6A0>놓으면 이 유닛에 시전</color>"
                        : "<color=#9FE6A0>놓으면 이 유닛에 부착</color>";
                case AimMode.EnemyMark:
                    if (_hoverEntity == Entity.Null) return "적에게만 쓸 수 있습니다";
                    if (_enemyMarkOnUnit) return "<color=#FF9B8A>적에게만 쓸 수 있습니다</color>";
                    return _enemyMarkHoverValid
                        ? "<color=#9FE6A0>놓으면 이 적에게 표식</color>"
                        : "<color=#FF9B8A>이미 표식이 있는 적입니다</color>";
                case AimMode.ActiveTile:
                    return "<color=#9FE6A0>놓으면 이 위치에 시전</color>";
                case AimMode.ActivePortal:
                    return "놓으면 입구가 지정됩니다";
                default:
                    return "";
            }
        }

        // ── aim visuals ──────────────────────────────────────────────────────

        private void UpdateDragVisual(Vector2 screenPos)
        {
            if (_mode == AimMode.Defender || _mode == AimMode.EnemyMark)
            {
                // 카드는 손패에 고정 — 화살표만 포인터를 따른다.
                var slot = Slot;
                Vector2 cardTop = (Vector2)slot.rect.position
                                  + new Vector2(0f, slot.rect.rect.height * 0.5f * slot.rect.localScale.y);
                // dreamcatcher-attach-lockon — 락온(Defender 부착/적 표식) 시 끝점을 대상
                // 중심으로 당겨 선이 대상에서 끝나게. 색은 3-상태(기본/가능/불가)로 화살표가 소유.
                Vector2? lockCenter = null;
                if (_hoverEntity != Entity.Null &&
                    _view.Bridge.TryGetUnitScreenRect(_hoverEntity, _view.MainCamera, out var hr))
                    lockCenter = hr.center;
                var state = _hoverEntity == Entity.Null
                    ? DreamcatcherTargetArrow.ArrowState.None
                    : (IsHoverAttachable() ? DreamcatcherTargetArrow.ArrowState.Valid
                                           : DreamcatcherTargetArrow.ArrowState.Invalid);
                _view.TargetArrow?.SetPath(cardTop, screenPos, state, lockCenter);
                return;
            }
            // ScreenSpaceOverlay canvas: RectTransform.position is in screen pixels.
            Slot.rect.position = screenPos;
        }

        // dreamcatcher-attach-lockon — 화살표/리티클 공유 유효성. 부착 가능(Unit/Squad=
        // 부착 여유 있음 / Active-Defender=항상 / EnemyMark=미표식)이면 true.
        private bool IsHoverAttachable()
        {
            if (_hoverEntity == Entity.Null) return false;
            if (_mode == AimMode.EnemyMark)
                return _enemyMarkHoverValid; // UpdateEnemyHover 가 결정(유닛=false, 미표식 적=true)
            var t = Slot.card != null ? Slot.card.type : CardType.Unit;
            if (t == CardType.Unit || t == CardType.Squad)
                return _attachable.Contains(_hoverEntity);
            return true; // Active-DefenderUnit (셀 캐스트) — 항상 유효 타겟
        }

        private void UpdateUnitHover(Vector2 screenPos)
        {
            Entity found = Entity.Null;
            Vector2Int? cell = null;
            // rev 4-3 — 1차: 스프라이트 스크린 렉트 픽킹(몸체 포인팅). 보드 평면 셀
            // 조회는 발밑을 정확히 가리킬 때만 맞아서 2차(폴백 quad 뷰 포함).
            if (_view.Bridge.TryPickDefenderAtScreen(_view.MainCamera, screenPos, out var picked, out var pickedCell))
            {
                cell = pickedCell;
                found = picked;
            }
            else if (TryScreenToCell(screenPos, out var c) && _view.Bridge.TryGetDefenderAt(c, out var entity))
            {
                cell = c;
                found = entity;
            }

            // dreamcatcher-attach-lockon 계약 #4 — 정체 히스테리시스: 현재 락온이 아직
            // 손가락 밑이면, 새 후보가 마진 이상 더 가깝지 않는 한 유지(밀집 플리커 차단).
            if (found != _hoverEntity && _hoverEntity != Entity.Null && found != Entity.Null)
            {
                float hyst = _view.FocusConfig != null ? _view.FocusConfig.lockSwitchHysteresisPx : 0f;
                if (hyst > 0f
                    && _view.Bridge.TryGetUnitScreenRect(_hoverEntity, _view.MainCamera, out var curRect)
                    && curRect.Contains(screenPos)
                    && _view.Bridge.TryGetUnitScreenRect(found, _view.MainCamera, out var newRect)
                    && Vector2.Distance(curRect.center, screenPos)
                       - Vector2.Distance(newRect.center, screenPos) < hyst)
                {
                    found = _hoverEntity;   // keep current lock
                    cell = _hoverCell;
                }
            }

            // 계약 #6 — 전체 빨강 틴트 제거. 정체 신호는 리티클(위치)+콜아웃(정체).
            _hoverCell = cell;
            _hoverEntity = found;
            _view.Focus?.SetAim(screenPos, _hoverEntity, _hoverCell ?? default);
        }

        // subconscious-curse-expansion unit 3 — 최근접 적 픽(반경 = AwakeningConfig 노브).
        // 하이라이트 없음(화살표 유효색이 피드백) — 적 스파인 틴트는 health-tint 와의
        // 합성 문제가 있어 비목표. ClearHover 의 un-highlight 는 무해(틴트 원복 방향).
        private void UpdateEnemyHover(Vector2 screenPos)
        {
            _hoverCell = null;
            // 손가락이 유닛 위면 잘못된 대상 → "유닛 불가" 빨강(적 표식은 유닛에 못 씀).
            // 유닛 위가 아니면 최근접 적(미표식=유효 / 이미 표식=무효).
            if (_view.Bridge.TryPickDefenderAtScreen(_view.MainCamera, screenPos, out var defender, out _))
            {
                _hoverEntity = defender;
                _enemyMarkHoverValid = false;
                _enemyMarkOnUnit = true; // unit 3 — 브리핑이 무효 사유(유닛 위)를 구분
                _view.Focus?.SetAimEnemyMark(screenPos, defender, valid: false, onUnit: true);
                return;
            }
            _hoverEntity = _view.Bridge.TryPickNearestEnemy(_view.MainCamera, screenPos,
                _view.Controller.EnemyPickRadiusTiles, out var enemy) ? enemy : Entity.Null;
            _enemyMarkHoverValid = _hoverEntity != Entity.Null && !_view.Bridge.IsEnemyMarked(_hoverEntity);
            _enemyMarkOnUnit = false;
            _view.Focus?.SetAimEnemyMark(screenPos, _hoverEntity, _enemyMarkHoverValid, onUnit: false);
        }

        private void ClearHover()
        {
            // 계약 #6 — 빨강 틴트 set 경로 제거로 un-highlight 는 no-op → 삭제.
            _hoverCell = null;
            _hoverEntity = Entity.Null;
        }

        // F1 — pointer→board-cell lives on the bridge (shared with the aim/
        // placement flows); this is a thin null-guarded forward.
        private bool TryScreenToCell(Vector2 screenPos, out Vector2Int cell)
        {
            cell = default;
            var bridge = _view.Bridge;
            return bridge != null && bridge.TryScreenToCell(_view.MainCamera, screenPos, out cell);
        }

        private void UpdateAimRange(Vector2 screenPos, SkillData skill)
        {
            if (!TryScreenToCell(screenPos, out var cell)) return;
            if (cell == _lastRangeCell) return;
            _lastRangeCell = cell;
            _view.Bridge.SetSkillAimRange(cell, skill);
        }

        private void ClearAimRange()
        {
            if (_lastRangeCell.x >= 0 && _view != null && _view.Bridge != null)
                _view.Bridge.ClearSkillAimRange();
            _lastRangeCell = new Vector2Int(-1, -1);
        }

        private void OnDisable()
        {
            // Panel deactivation mid-drag: never leave a stale hover tint,
            // aim preview, or IsAiming behind.
            _dragging = false;
            EndInteraction();
        }
    }
}
