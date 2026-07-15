# 1 — 유닛 탭 선택 + 슬로우 모션

## 목적

보드 press 를 방어유닛 선택으로 번역하고, 선택 상태를 단일 소유하며, 선택 중 Battle 도메인 슬로우 lease 를 든다. 패널 뷰(unit 2)는 이 컨트롤러가 구동한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` (신규, namespace `Wassup.UI`)

## 구현

`[DefaultExecutionOrder(-50)]` — README 계약 4.

**SerializeField**: `BattleBridge bridge`, `Camera mainCamera`, `DreamcatcherHandController hand`, `DreamcatcherHandView handView`, `DefenderSelector defenderSelector`, `AwakeningConfig config`, `DcInspectPanelView panel`.

(`DefenderDragPlacementController` 를 직접 배선하지 않는 이유 = 런타임 `AddComponent` 라 씬에 없다. `DefenderSelector.DragController`(unit 0) 경유.)

(`hand.config` 는 `[SerializeField] private` 에 접근자가 없다 → 자체 `AwakeningConfig` 필드를 둔다. `DreamcatcherHandView.cs:31` 과 같은 관례.)

### Update

```
if (Blocked()) { Close(); return; }          // 게이트 — 배타 파트너가 잡으면 닫고 나간다
pointer = Pointer.current; if (null) return;
if (!pointer.press.wasPressedThisFrame) return;   // 계약 3 — press
screenPos = pointer.position.ReadValue();
if (IsOverUi(screenPos)) return;                  // 계약 5b — UI 위 press 는 무시
HandleTap(screenPos);
```

**`EventSystem.IsPointerOverGameObject()` 를 쓰지 말 것 (README 계약 5b).** 그 API 는 `EventSystem.Update`(실행순서 0)가 세운 **지난 프레임** 상태를 읽는데 이 컨트롤러는 -50 이라 먼저 돈다. 터치는 hover 가 없어 press 프레임에 pointer 상태 자체가 없으므로 **손가락이 UI 위에 있어도 false** 를 답한다 — 마우스에선 hover 잔상에 가려지는 **Android 전용 결함**이다. `IsOverUi` 는 실행 순서와 무관한 즉석 `EventSystem.RaycastAll` 이다:

```csharp
private bool IsOverUi(Vector2 screenPos)
{
    var es = EventSystem.current;
    if (es == null) return false;
    _uiHits.Clear();
    es.RaycastAll(new PointerEventData(es) { position = screenPos }, _uiHits);
    return _uiHits.Count > 0;
}
```

`Blocked()` = README 계약 5 의 파트너별 신호 OR:
- `GameManager.Instance.IsAiming`
- `handView.State == HandState.Hand`
- `defenderSelector.DragController?.IsDragging` (null = 아직 미생성 = 드래그 안 함)

`Blocked()` 가 true 면 **닫는다** — 손패를 열거나 드래그를 시작하면 열려 있던 패널이 사라져야 한다(계약 8).

### HandleTap

```
if (!TryPick(screenPos, out entity)) { Close(); return; }   // 빈 보드 → 닫기
if (entity == _selected) { Close(); return; }               // 재탭 → 토글
Select(entity);
```

`TryPick` = `bridge.TryPickDefenderAtScreen(cam, pos, out e, out _)` 1차, 실패 시 `bridge.TryScreenToCell(cam, pos, out cell) && bridge.TryGetDefenderAt(cell, out e)` 2차 (계약 2 — `DreamcatcherCardDragSlot.UpdateUnitHover` 와 같은 순서).

`Select(entity)`: `hand.GetAttachments(_scratch)` → host == entity 인 카드만 `_cards` 로 필터하고, 코스트는 `hand.CostOf(card)` 로 해석해 `_costs` 에 index 대응으로 채운다.
- `_cards.Count == 0` → `Close()` (계약 8 — 부착 0장은 열지 않고 **열린 것을 닫는다**).
- 앵커는 컨트롤러가 `bridge.TryGetUnitViewAnchor` 로 해석한다. 실패 시 `Close()`.
- 아니면 `_selected = entity`, `panel.Show(anchor, mainCamera, _cards, _costs)`, 슬로우 lease 획득.

**뷰는 `Entity`/`BattleBridge`/`DreamcatcherHandController` 를 모른다** — 컨트롤러가 앵커와 코스트를 해석해 plain 값으로 넘긴다(`DcIconStripSpawner`→`DcIconStripView` 와 같은 역할 분담).

### 슬로우 lease

```csharp
_slomoLease.Dispose();   // 멱등 — 기존 lease 교체 시 누수 방지
float scale = config != null ? Mathf.Max(0.01f, config.slomoTimeScale) : 0.3f;
_slomoLease = TimeManager.Instance.Request(TimeDomain.Battle, scale, priority: 50);
```

`DreamcatcherHandView.Open` 과 동일 정책(priority 50, `slomoTimeScale`, **절대 0 아님**). `Close()` 에서 `_slomoLease.Dispose()`.

### Close (멱등)

`_selected = Entity.Null` + `panel.Hide()` + `_slomoLease.Dispose()`. 미선택 상태에서 불려도 no-op.

### 구독

- `OnEnable`: `hand.AttachmentsChanged += OnAttachmentsChanged`, `GameManager.Instance.PhaseChanged += OnPhaseChanged`
- `OnDisable`: 대칭 해제 + `Close()` (계약 7·8)
- `OnAttachmentsChanged`: 선택 중이면 목록 재해석 → 0장이 됐으면(= 호스트 사망 회수) `Close()`, 아니면 `panel.Show` 재호출(리빌드)
- `OnPhaseChanged(phase)`: `phase != Placement && phase != Battle` → `Close()` (계약 9, `DreamcatcherHandView.OnPhaseChanged` 선례)

`GameManager` 는 `[DefaultExecutionOrder(-100)]` 이라 `-50` 시점엔 `Instance` 가 세워져 있다. 다만 `OnEnable` 순서는 실행 순서와 무관하므로 `GameManager.Instance` null 가드는 유지한다.

## 완료 기준

- compile 클린 (콘솔 에러 0).
- ECS 변경 0 — bridge 는 기존 read-only API 3개만 호출.
- 동작 검증(unit 3 Play, 뷰 배선 후):
  - 부착된 유닛 탭 → `TimeManager.Instance.ScaleOf(TimeDomain.Battle)` 가 `slomoTimeScale`(0.3) 로 떨어진다.
  - 재탭 / 빈 보드 탭 / 부착 0장 유닛 탭 → `ScaleOf` 가 1 로 복귀.
  - 손패 오픈 / 배치 드래그 시작 → 패널 닫힘 + `ScaleOf` 가 해당 파트너 lease 값으로 인계(1 로 튀지 않음).
  - 페이즈 이탈 → `ScaleOf` 1 복귀.
  - **로그가 아니라 `ScaleOf` 실측을 판정 기준으로 삼는다** (관측 가능한 값).

확인 2026-07-15 — Play 실측 통과. `TryPick` 이 정확한 엔티티 해석, 탭 시 `ScaleOf(Battle)` 1→0.3.
lease 대칭(누수 0)을 요청 목록의 priority-50 항목 수로 직접 계수해 확인:

| 경로 | selected | panel | 내 lease |
|---|---|---|---|
| 부착 유닛 탭 | 94 | 열림 | 1 |
| 같은 유닛 재탭(토글) | Null | 닫힘 | 0 |
| 빈 보드 탭 | Null | 닫힘 | 0 |
| 부착 0장 유닛 탭 | Null | 닫힘 | 0 |
| 손패 오픈 → Update 1회 | Null | 닫힘 | 0 (손패 lease 만 잔존) |
| 사망 회수(`AttachmentsChanged`) | Null | 닫힘 | 0 |
| 페이즈 이탈(Result) | Null | 닫힘 | 0 |

손패 오픈 순간 내 lease 와 손패 lease 가 **잠시 공존(2개)** 하지만 둘 다 priority 50 + 동일 scale 이라 유효 스케일 불변 — README 계약 7 의 예측대로다.
