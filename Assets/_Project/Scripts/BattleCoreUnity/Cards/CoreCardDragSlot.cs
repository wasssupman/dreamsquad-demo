using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.BattleCoreUnity.Cards
{
    // battle-core-rebuild unit 7c — **손패 카드 한 장의 제스처.** 옛 `DreamcatcherCardDragSlot`(852줄)의 이식이다(슬롯이 곧 드래그
    // 원천 — `DefenderDragSlot` 패턴). 카드는 어느 조준에서도 **손패에 남고 화살표가 겨눈다**(active-dreamcatcher-tile-aim 1).
    //
    //   · Defender(부착) — 유닛 락온. 유닛 위에서 뗌 = 부착(지연 커밋 비행 · 도착 프레임에 커맨드).
    //   · TileAim(액티브) — 화살표 끝이 칸을 물고 범위가 따라온다. 뗌 = 시전. 포탈은 뗌이 입구, 두 번째 탭이 출구.
    //   · EnemyMark(표식) — 손끝 반경 안 최근접 적.
    //   취소 = 손패(취소 존) 안에서 뗌 · 판 밖 · ESC · 국면 이탈 — **절대 값을 치르지 않는다**(커맨드를 안 건다).
    //
    // ⚠ **판정 0**(구현 6·9): 유효 대상 집합(`_attachable`)은 드래그 시작에 **코어에 한 번 물어**(`WouldAttach`) 만든다 — 드래그 중
    // 부착 수는 불변이라 스냅샷이면 충분하다(옛 규약). 표식의 「이미 표식됨」도 같은 함수(`DuplicateState`)다. 범위 링 반경은
    // `RangeCatalog` → `RadiusWithOrigin(host 몸)`(구현 4 — 모르는 concrete 는 **안 그린다**).
    //
    // 제스처 창구(`Press`·`BeginDragAt`·`DragTo`·`EndDragAt`·`Tap`·`BoardTap`)는 UGUI 핸들러가 부르는 그 함수다 — 테스트가 포인터
    // 장치 없이 같은 경로를 탄다(5b `DragPlacementInput` 과 같은 이유: 가상 마우스는 에디터 포커스에 따라 이벤트가 안 흐른다).
    public sealed class CoreCardDragSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private CoreHandView _view;
        private int _index;

        private bool _dragging;
        private CoreCardAim _mode = CoreCardAim.None;
        private SimEntityId _hover = SimEntityId.None;
        private int2? _portalEntryCell;
        private int2 _lastRangeCell = new int2(-1, -1);
        private int2? _aimCell;
        private readonly List<int2> _portalCells = new List<int2>(2);
        private readonly List<(SimEntityId id, Rect rect)> _defRectBuf = new List<(SimEntityId, Rect)>();
        private readonly HashSet<SimEntityId> _attachable = new HashSet<SimEntityId>();
        private RangeSpec _attachRange = RangeSpec.None;
        private bool _enemyMarkHoverValid;
        private bool _enemyMarkOnUnit;
        private Receipt _lastReceipt = Receipt.Ok;

        public bool IsDragging => _dragging;
        public bool IsPortalAiming => _portalEntryCell.HasValue;

        /// <summary>테스트 — 지금 조준 모드 · 락온 대상 · 조준 칸.</summary>
        public CoreCardAim Mode => _mode;
        public SimEntityId Hover => _hover;

        public void Bind(CoreHandView view, int index)
        {
            _view = view;
            _index = index;
        }

        private CoreHandView.CardSlot Slot => _view.Slots[_index];

        // ── UGUI 핸들러 → 제스처 창구 ─────────────────────────────────────────

        public void OnPointerDown(PointerEventData e) => Press();
        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerClick(PointerEventData e) => Tap();
        public void OnBeginDrag(PointerEventData e) => BeginDragAt(e.position);
        public void OnDrag(PointerEventData e) => DragTo(e.position);
        public void OnEndDrag(PointerEventData e) => EndDragAt(e.position);

        /// <summary>누름 — 들기 + 브리핑(조작법 + 시작 상태 · 사용 불가 사유 포함).</summary>
        public void Press()
        {
            if (_view == null || _dragging) return;
            _view.TryFastForwardDeal();
            _view.SetFocus(_index);
            if (_view.CanPeek(_index))
                _view.ShowDragBriefing(Controls(), CoreCardText.PressStatus(Slot.usableReason, Slot.attachBlocked));
        }

        public void Release()
        {
            if (_view == null) return;
            _view.ClearFocus(_index);
            if (!_dragging && !IsPortalAiming) _view.HideDragTooltip();
        }

        /// <summary>
        /// 탭 즉발 부착 — 유닛이 선택돼 있으면 카드 탭만으로 그 유닛에 붙인다(`selection-hand-attach` 3). ⚠ 드래그로 이어진 누름도
        /// 클릭을 낸다(press·drag 핸들러가 같은 오브젝트) — 가드 0 필수(옛 critic M1).
        /// </summary>
        public void Tap()
        {
            if (_view == null || _dragging || IsPortalAiming) return;
            var target = _view.SelectionTarget;
            if (!target.IsEntity) return;
            if (!_view.CanPeek(_index)) return;
            var slot = Slot;
            if (slot.entryId < 0 || slot.cardIndex < 0) return;
            if (_view.Input.AimOf(slot.cardIndex) != CoreCardAim.Defender)
            {
                Reject(CoreCardText.DragInstead);
                return;
            }
            if (!slot.usable) { Reject(CoreCardText.Red(CoreCardText.RejectTextOf(slot.usableReason))); return; }
            var would = _view.Input.WouldAttach(slot.cardIndex, target);
            if (would != RejectReason.None) { Reject(CoreCardText.Red(CoreCardText.RejectTextOf(would))); return; }

            int entryId = slot.entryId;
            Vector3 start = slot.rect.position;
            Vector2 size = slot.rect.rect.size;
            Sprite face = slot.art != null ? slot.art.sprite : null;
            var host = target;
            Vector2 pulse = default;
            bool hasPulse = _view.Focus != null && _view.Focus.TryCaptureConfirmCenter(out pulse);
            if (_view.FlyCardToUnitDeferred(_index, entryId, start, size, face, host, () => CommitAttach(entryId, host)))
            {
                if (hasPulse) _view.Focus?.Confirm(pulse);
                EndInteraction();
                _view.NotifyInteractionEnded();
                return;
            }
            CommitNow(() => _view.Input.Attach(entryId, host), () => _view.FlyCardToUnit(start, size, face, host));
        }

        private void Reject(string reason)
        {
            _view.FlinchSlot(_index);
            _view.ShowDragBriefing(Controls(), reason);
        }

        private string Controls()
        {
            var slot = Slot;
            if (slot.cardIndex < 0) return "";
            return CoreCardText.ControlsFor(_view.Input.AimOf(slot.cardIndex), _view.Input.NeedsTwoCells(slot.cardIndex));
        }

        // ── 드래그 ────────────────────────────────────────────────────────────

        public void BeginDragAt(Vector2 screen)
        {
            if (_view == null || _portalEntryCell.HasValue || !_view.CanStartDrag(_index)) return;
            var slot = Slot;
            _mode = _view.Input.AimOf(slot.cardIndex);
            if (_mode == CoreCardAim.None) return;
            // 부착이 아닌 조준(액티브·표식)은 겨누는 대상이 선택 유닛이 아니다 — **선택을 놓고** 필드 문맥으로 나온다.
            // ⚠ `BeginFocus` **앞**이어야 한다(뒤면 해제가 방금 시작한 조준 세션을 지운다).
            if (_mode != CoreCardAim.Defender && _view.SelectionTarget.IsEntity) _view.NotifySelectionReleasedForAim();

            _dragging = true;
            _view.SetFocus(-1);
            slot.rect.SetAsLastSibling();
            slot.rect.localScale = Vector3.one * 1.08f;
            _view.ShowDragBriefing(Controls(), StatusFor(insideHand: false));
            BeginFocus(slot);
            UpdateDragVisual(screen);
        }

        private void BeginFocus(CoreHandView.CardSlot slot)
        {
            if (_mode == CoreCardAim.Defender)
            {
                _view.Targets.EnumerateDefenderScreenRects(_defRectBuf);
                _attachable.Clear();
                for (int i = 0; i < _defRectBuf.Count; i++)
                {
                    var id = _defRectBuf[i].id;
                    if (_view.Input.WouldAttach(slot.cardIndex, id) == RejectReason.None) _attachable.Add(id);
                }
                _attachRange = CardRangeOf(slot.cardIndex);
                _view.Focus?.Begin(CoreCardFocusPresenter.AimKind.AttachAim, _attachable);
            }
            else if (_mode == CoreCardAim.EnemyMark)
            {
                Sprite icon = slot.art != null ? slot.art.sprite : null;
                string n = slot.nameLabel != null ? slot.nameLabel.text : "";
                _view.Focus?.BeginEnemyMark(icon, n);
            }
        }

        /// <summary>
        /// 카드 한 장의 범위 도형(옛 `DcRangeCatalog.ResolveCard` — 카드 단위 몫은 7c). 규칙 줄을 돌며 **첫 공간 도형**을 고른다.
        /// 판정은 전부 `RangeCatalog.Resolve(트리거, 페이로드, 반경)` 이다 — 여기서 concrete 를 다시 가르지 않는다.
        /// </summary>
        public static RangeSpec CardRangeOf(MatchDefinition def, int cardIndex)
        {
            if (def == null || cardIndex < 0 || cardIndex >= def.Cards.Length) return RangeSpec.None;
            var rows = def.Cards[cardIndex].Bindings;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                int r = rows[i];
                if (r < 0 || r >= def.Bindings.Length) continue;
                ref var b = ref def.Bindings[r];
                var spec = RangeCatalog.Resolve(b.Trigger, b.Payload, b.TileRange);
                if (spec.Shape != RangeShape.None) return spec;
            }
            return RangeSpec.None;
        }

        private RangeSpec CardRangeOf(int cardIndex) => CardRangeOf(_view.Driver.Definition, cardIndex);

        public void DragTo(Vector2 screen)
        {
            if (!_dragging) return;
            switch (_mode)
            {
                case CoreCardAim.Defender: UpdateUnitHover(screen); break;
                case CoreCardAim.EnemyMark: UpdateEnemyHover(screen); break;
                case CoreCardAim.TileAim: UpdateTileAim(screen); break;
            }
            UpdateDragVisual(screen);
            _view.UpdateDragBriefingStatus(StatusFor(InsideCancelZone(screen)));
        }

        public void EndDragAt(Vector2 screen)
        {
            if (!_dragging) return;
            _dragging = false;
            var slot = Slot;
            if (InsideCancelZone(screen)) { CancelDrag(); return; }

            switch (_mode)
            {
                case CoreCardAim.Defender:
                {
                    UpdateUnitHover(screen);
                    if (!_hover.IsEntity) { CancelDrag(); return; }
                    var host = _hover;
                    int entryId = slot.entryId;
                    Vector3 start = slot.rect.position;
                    Vector2 size = slot.rect.rect.size;
                    Sprite face = slot.art != null ? slot.art.sprite : null;
                    Vector2 pulse = default;
                    bool hasPulse = _view.Focus != null && _view.Focus.TryCaptureConfirmCenter(out pulse);
                    if (_view.FlyCardToUnitDeferred(_index, entryId, start, size, face, host, () => CommitAttach(entryId, host)))
                    {
                        if (hasPulse) _view.Focus?.Confirm(pulse);
                        EndInteraction();
                        _view.NotifyInteractionEnded();
                        return;
                    }
                    CommitNow(() => _view.Input.Attach(entryId, host), () => _view.FlyCardToUnit(start, size, face, host));
                    return;
                }
                case CoreCardAim.EnemyMark:
                {
                    UpdateEnemyHover(screen);
                    if (!_hover.IsEntity || _enemyMarkOnUnit) { CancelDrag(); return; }
                    var enemy = _hover;
                    int entryId = slot.entryId;
                    Vector3 start = slot.rect.position;
                    Vector2 size = slot.rect.rect.size;
                    Sprite face = slot.art != null ? slot.art.sprite : null;
                    CommitNow(() => _view.Input.Attach(entryId, enemy), () => _view.FlyCardToUnit(start, size, face, enemy));
                    return;
                }
                case CoreCardAim.TileAim:
                {
                    UpdateTileAim(screen);
                    if (!_aimCell.HasValue) { CancelDrag(); return; }
                    var tile = _aimCell.Value;
                    if (_view.Input.NeedsTwoCells(slot.cardIndex))
                    {
                        _portalEntryCell = tile;
                        _lastRangeCell = new int2(-1, -1);
                        _view.UpdateDragBriefingStatus(CoreCardText.Green(CoreCardText.EntrySet) + CoreCardText.TapExit);
                        return;
                    }
                    ClearAimRange();
                    Vector3 start = slot.rect.position;
                    Vector2 size = slot.rect.rect.size;
                    Sprite face = slot.art != null ? slot.art.sprite : null;
                    int entryId = slot.entryId;
                    CommitNow(() => _view.Input.Cast(entryId, tile), () => _view.FlyCardToCell(start, size, face, tile),
                              TilePulseCenter(tile));
                    return;
                }
                default:
                    CancelDrag();
                    return;
            }
        }

        // 포탈 2단계 — 손을 뗀 상태라 OnDrag 가 안 돈다. 출구 조준을 매 프레임 갱신하고 두 번째 누름을 커밋으로 받는다.
        private void Update()
        {
            if (!_portalEntryCell.HasValue) return;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();
            StepPortal(pos);
            if (pointer.press.wasPressedThisFrame) BoardTap(pos);
        }

        /// <summary>포탈 출구 조준을 그 손끝으로 갱신(매 프레임).</summary>
        public void StepPortal(Vector2 screen)
        {
            if (!_portalEntryCell.HasValue) return;
            UpdateTileAim(screen);
            UpdateDragVisual(screen);
            _view.UpdateDragBriefingStatus(StatusFor(InsideCancelZone(screen)));
        }

        /// <summary>포탈 2단계의 두 번째 탭. 손패 안·판 밖 = 취소, 입구와 같은 칸 = **무시**(조준 유지), 그 밖 = 커밋.</summary>
        public void BoardTap(Vector2 screen)
        {
            if (!_portalEntryCell.HasValue) return;
            bool insideHand = InsideCancelZone(screen);
            UpdateTileAim(screen);
            if (insideHand || !_aimCell.HasValue) { CancelDrag(); return; }
            if (_aimCell.Value.Equals(_portalEntryCell.Value)) return;
            var entry = _portalEntryCell.Value;
            var exit = _aimCell.Value;
            _portalEntryCell = null;
            var slot = Slot;
            int entryId = slot.entryId;
            Vector3 start = slot.rect.position;
            Vector2 size = slot.rect.rect.size;
            Sprite face = slot.art != null ? slot.art.sprite : null;
            CommitNow(() => _view.Input.CastPair(entryId, entry, exit), () => _view.FlyCardToCell(start, size, face, exit),
                      TilePulseCenter(exit));
        }

        // 지연 커밋(도착 프레임). 거절 사유는 코어 답 그대로 옮긴다.
        private bool CommitAttach(int entryId, SimEntityId host)
        {
            _lastReceipt = _view.Input.Attach(entryId, host);
            if (_lastReceipt.Accepted) { _view.OnCardUsed(); return true; }
            ShowRejected(_lastReceipt.Reason);
            return false;
        }

        // 뗌 = 즉시 적용. 값·순환은 수락된 커맨드 안에서만 일어난다(실패 = 무차감 · 카드 복귀).
        private void CommitNow(System.Func<Receipt> commit, System.Action onSuccess = null, Vector2? pulseOverride = null)
        {
            Vector2 pulse = default;
            bool hasPulse = false;
            if (pulseOverride.HasValue) { pulse = pulseOverride.Value; hasPulse = true; }
            else if (_view.Focus != null) hasPulse = _view.Focus.TryCaptureConfirmCenter(out pulse);
            _lastReceipt = commit();
            bool ok = _lastReceipt.Accepted;
            if (ok && hasPulse) _view.Focus?.Confirm(pulse);
            EndInteraction();
            if (!ok)
            {
                _view.RestoreSlotHome(_index);
                ShowRejected(_lastReceipt.Reason);
            }
            else
            {
                onSuccess?.Invoke();
                _view.OnCardUsed();
            }
            _view.NotifyInteractionEnded();
        }

        private void ShowRejected(RejectReason reason)
        {
            int idx = _view.IndexOfEntry(Slot.entryId);
            _view.FlinchSlot(idx >= 0 ? idx : _index);
            _view.ShowDragBriefing(Controls().Length > 0 ? Controls() : " ", CoreCardText.Red(CoreCardText.RejectTextOf(reason)));
        }

        /// <summary>마지막 커맨드의 receipt(테스트 — 화면 문구가 코어 답과 같은가).</summary>
        public Receipt LastReceipt => _lastReceipt;

        public void CancelDrag()
        {
            _dragging = false;
            EndInteraction();
            _view.RestoreSlotHome(_index);
            _view.NotifyInteractionEnded();
            SoundManager.Instance?.PlayCardReturn();
        }

        private void EndInteraction()
        {
            if (_mode != CoreCardAim.None)
            {
                _view.TargetArrow?.Hide();
                _view.RestoreSlotHome(_index);
            }
            _view.Focus?.End();
            _hover = SimEntityId.None;
            ClearAimRange();
            _view.Overlay?.HideAttachRange();
            _attachRange = RangeSpec.None;
            _aimCell = null;
            _portalEntryCell = null;
            _mode = CoreCardAim.None;
            if (_view != null) _view.HideDragTooltip();
        }

        // ── 브리핑 상태 줄 ───────────────────────────────────────────────────

        private bool InsideCancelZone(Vector2 screen)
        {
            var rect = _view != null ? _view.CancelRect : null;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);
        }

        private string StatusFor(bool insideHand)
        {
            if (insideHand) return CoreCardText.Red(CoreCardText.CancelHere);
            switch (_mode)
            {
                case CoreCardAim.Defender:
                    if (!_hover.IsEntity) return CoreCardText.DragToAlly;
                    if (!_attachable.Contains(_hover))
                        return CoreCardText.Red(CoreCardText.RejectTextOf(_view.Input.WouldAttach(Slot.cardIndex, _hover)));
                    return CoreCardText.Green(CoreCardText.DropToAttach);
                case CoreCardAim.EnemyMark:
                    if (!_hover.IsEntity) return CoreCardText.EnemyOnly;
                    if (_enemyMarkOnUnit) return CoreCardText.Red(CoreCardText.EnemyOnly);
                    return _enemyMarkHoverValid ? CoreCardText.Green(CoreCardText.DropToMark)
                                                : CoreCardText.Red(CoreCardText.AlreadyMarked);
                case CoreCardAim.TileAim:
                    if (!_aimCell.HasValue) return CoreCardText.DragToTile;
                    if (_portalEntryCell.HasValue)
                        return _aimCell.Value.Equals(_portalEntryCell.Value)
                            ? CoreCardText.Green(CoreCardText.EntrySet) + CoreCardText.TapExit
                            : CoreCardText.Green(CoreCardText.DropToLink);
                    if (_view.Input.NeedsTwoCells(Slot.cardIndex)) return CoreCardText.DropSetsEntry;
                    return CoreCardText.Green(CoreCardText.DropToCast);
                default:
                    return "";
            }
        }

        // ── 조준 그림 ────────────────────────────────────────────────────────

        private void UpdateDragVisual(Vector2 screen)
        {
            var slot = Slot;
            Vector2 origin = ArrowOrigin(slot);
            Vector2? lockCenter = null;
            DreamcatcherTargetArrow.ArrowState state;
            if (_mode == CoreCardAim.TileAim)
            {
                if (_aimCell.HasValue && TilePulseCenter(_aimCell.Value) is Vector2 c) lockCenter = c;
                bool onEntry = _portalEntryCell.HasValue && _aimCell.HasValue && _aimCell.Value.Equals(_portalEntryCell.Value);
                state = !_aimCell.HasValue || onEntry
                    ? DreamcatcherTargetArrow.ArrowState.None
                    : DreamcatcherTargetArrow.ArrowState.Valid;
            }
            else
            {
                if (_hover.IsEntity && _view.Targets.TryGetUnitScreenRect(_hover, out var hr)) lockCenter = hr.center;
                state = !_hover.IsEntity ? DreamcatcherTargetArrow.ArrowState.None
                    : (IsHoverValid() ? DreamcatcherTargetArrow.ArrowState.Valid : DreamcatcherTargetArrow.ArrowState.Invalid);
            }
            _view.TargetArrow?.SetPath(origin, screen, state, lockCenter);
        }

        private Vector2 ArrowOrigin(CoreHandView.CardSlot slot)
        {
            if (_portalEntryCell.HasValue && TilePulseCenter(_portalEntryCell.Value) is Vector2 entry) return entry;
            return (Vector2)slot.rect.position + new Vector2(0f, slot.rect.rect.height * 0.5f * slot.rect.localScale.y);
        }

        private Vector2? TilePulseCenter(int2 cell)
            => _view.Targets.TryGetCellScreenCenter(cell, out var s) ? s : (Vector2?)null;

        private void UpdateTileAim(Vector2 screen)
        {
            var slot = Slot;
            if (slot.cardIndex < 0 || !_view.Targets.TryScreenToCellStrict(screen, out var cell))
            {
                _aimCell = null;
                if (_portalEntryCell.HasValue) PaintPortalCells(_portalEntryCell.Value, null);
                else ClearAimRange();
                _lastRangeCell = new int2(-1, -1);
                return;
            }
            _aimCell = cell;
            if (cell.Equals(_lastRangeCell)) return;
            _lastRangeCell = cell;
            PaintAimCells(cell, slot.cardIndex);
        }

        // 액티브 범위 = **칸에 떨어지는 것**(원점 항 = 칸 반폭). 반경은 규칙 줄의 값이고, 원점 항은 `RadiusWithOrigin` 이 판정과
        // 같은 매핑(`SkillMath.TryOriginRadius`)으로 더한다 — 상수를 다시 쓰지 않는다. 반경 0(포탈)은 칸 자체를 칠한다.
        private void PaintAimCells(int2 cell, int cardIndex)
        {
            if (_portalEntryCell.HasValue) { PaintPortalCells(_portalEntryCell.Value, cell); return; }
            var def = _view.Driver.Definition;
            int row = def.Cards[cardIndex].ActiveBinding;
            int tileRange = row >= 0 && row < def.Bindings.Length ? def.Bindings[row].TileRange : 0;
            if (tileRange <= 0)
            {
                _portalCells.Clear();
                _portalCells.Add(cell);
                _view.Overlay?.ShowAimCells(_portalCells);
                return;
            }
            float radius = new RangeSpec(RangeShape.Circle, tileRange, Wassup.Skills.RangeMetric.CellArea).RadiusWithOrigin(0f);
            _view.Overlay?.ShowAimRing(_view.Driver.Match.Map.CenterOf(cell), radius);
        }

        private void PaintPortalCells(int2 entry, int2? exit)
        {
            _portalCells.Clear();
            _portalCells.Add(entry);
            if (exit.HasValue && !exit.Value.Equals(entry)) _portalCells.Add(exit.Value);
            _view.Overlay?.ShowAimCells(_portalCells);
        }

        private void ClearAimRange()
        {
            if (_lastRangeCell.x >= 0 || _portalEntryCell.HasValue) _view?.Overlay?.HideAim();
            _lastRangeCell = new int2(-1, -1);
        }

        private bool IsHoverValid()
        {
            if (!_hover.IsEntity) return false;
            if (_mode == CoreCardAim.EnemyMark) return _enemyMarkHoverValid;
            return _attachable.Contains(_hover);
        }

        private void UpdateUnitHover(Vector2 screen)
        {
            var cfg = _view.FocusConfig;
            _view.Targets.TryPickDefender(screen, out var found,
                cfg != null ? cfg.unitPickPaddingPx : 0f, cfg != null ? cfg.unitPickMagnetPx : 0f, _attachable);

            // 정체 히스테리시스 — 새 후보가 마진 이상 우세할 때만 전환(밀집 플리커 차단 · 점→렉트 거리 비교).
            if (found != _hover && _hover.IsEntity && found.IsEntity)
            {
                float hyst = cfg != null ? cfg.lockSwitchHysteresisPx : 0f;
                if (hyst > 0f
                    && _view.Targets.TryGetUnitScreenRect(_hover, out var cur)
                    && _view.Targets.TryGetUnitScreenRect(found, out var next)
                    && CoreCardTargets.ScreenDistanceToRect(cur, screen)
                       - CoreCardTargets.ScreenDistanceToRect(next, screen) < hyst)
                    found = _hover;
            }

            // 락온 획득/전환 순간 유효 대상이 몸으로 반응한다(스케일 펀치).
            if (found != _hover && found.IsEntity && _attachable.Contains(found) && _view.Targets.TryGetUnitView(found, out var lockView))
                lockView.PlayPunch();

            // 범위 링은 **유효 락온 전환 순간**에만 켜고 끈다(히스테리시스 뒤 — 손끝 흔들림에 안 옮겨 다닌다). 추종은 오버레이가 한다.
            if (found != _hover && _view.Overlay != null)
            {
                if (found.IsEntity && _attachable.Contains(found) && cfg != null && _attachRange.Shape != RangeShape.None)
                    _view.Overlay.ShowAttachRange(found, _attachRange, cfg.attachRangeStyle);
                else
                    _view.Overlay.HideAttachRange();
            }

            _hover = found;
            _view.Focus?.SetAim(screen, _hover);
        }

        private void UpdateEnemyHover(Vector2 screen)
        {
            // 손끝이 아군 유닛 위면 잘못된 대상(표식은 적에게만).
            if (_view.Targets.TryPickDefender(screen, out var defender))
            {
                _hover = defender;
                _enemyMarkHoverValid = false;
                _enemyMarkOnUnit = true;
                _view.Focus?.SetAimEnemyMark(screen, defender, valid: false, onUnit: true);
                return;
            }
            _hover = _view.Targets.TryPickNearestEnemy(screen, _view.EnemyPickRadiusTiles, out var enemy) ? enemy : SimEntityId.None;
            // 「이미 표식됨」은 코어가 답한다(`WouldAttach` → `DuplicateState`).
            _enemyMarkHoverValid = _hover.IsEntity && _view.Input.WouldAttach(Slot.cardIndex, _hover) == RejectReason.None;
            _enemyMarkOnUnit = false;
            _view.Focus?.SetAimEnemyMark(screen, _hover, _enemyMarkHoverValid, onUnit: false);
        }

        private void OnDisable()
        {
            _dragging = false;
            if (_view != null) EndInteraction();
        }
    }
}
