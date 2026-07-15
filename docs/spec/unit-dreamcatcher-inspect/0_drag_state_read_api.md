# 0 — 배치 드래그 상태 읽기 API (seam)

## 목적

`DefenderDragPlacementController` 의 드래그 진행 상태를 프레젠테이션이 읽을 수 있게 노출한다. `DcInspectController`(unit 1)의 배타 게이트(README 계약 5)가 이 신호를 요구한다.

배치 드래그는 **Battle 페이즈에서도 살아있고**(`DefenderSelector.OnPhaseChanged` 가 패널을 끄지 않고 슬림 리사이즈만 한다), `GameManager.IsAiming` 을 건드리지 않으며, 공개 상태가 하나도 없다. 즉 현재로선 관측 수단이 없다.

`unit-dreamcatcher-icons/0_attachments_read_api.md` 와 같은 형태의 seam — **기존 로직 변경 0, 읽기 노출만**.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 드래그 **상태** 노출
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 드래그 컨트롤러 **도달 경로** 노출

## 구현

seam 이 2점인 이유: 상태는 컨트롤러가 갖고 있지만, **컨트롤러 자체에 씬에서 도달할 수 없다**. `DefenderSelector.EnsureDragController`(`:449~`)가 런타임에 `gameObject.AddComponent<DefenderDragPlacementController>()` 로 붙이므로 씬에는 존재하지 않는다(`DefenderSelector.dragPlacementController` SerializeField 도 씬에선 비어 있다 — 실측 2026-07-15).

### 1. 상태 (`DefenderDragPlacementController`)

```csharp
public bool IsDragging => _session.active;
```

`_session.active`(내부 `DragSession` 구조체, `:62`)를 그대로 읽는다. 배치는 `BeginDrag` 에서 `_session = BuildSession(unitData)` 로 세션을 세우고 `CleanupSession()` 에서 무효화하며, 이는 드래그 슬로우모 lease(`_slowmoLease`)의 수명 구간과 동일하다.

**새 필드/상태를 만들지 않는다** — 두 개의 진실 소스가 생기면 어긋난다.

### 2. 도달 경로 (`DefenderSelector`)

```csharp
public DefenderDragPlacementController DragController => dragPlacementController;
```

수명 소유자가 노출한다. 아직 `AddComponent` 전이면 null — 호출측은 `null == "드래그 안 함"` 으로 읽는다.

`DcInspectController` 는 씬에 실재하는 `DefenderSelector`(`UIRoot/DefenderSelector`)를 배선하고 이 경유로 상태를 읽는다.

## 완료 기준

- compile 클린 (`refresh_unity` 후 Unity 콘솔 에러 0).
- 두 파일 모두 기존 로직 diff 0 — 프로퍼티 1줄 + 주석 추가만.
- 동작 검증은 unit 1 의 게이트 동작에서 확인(이 단계 단독으로는 관측 대상이 없다).

확인 2026-07-15 — compile 에러 0, 기존 로직 diff 0.
