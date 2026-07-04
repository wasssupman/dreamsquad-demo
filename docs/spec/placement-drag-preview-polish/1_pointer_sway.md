# 1 — 포인터 sway (감쇠 스프링)

## 목적

드래그 중 포인터를 좌우로 움직이면 프리뷰가 키링처럼 기울고, 멈추면 감쇠해 제자리로 돌아오는 sway 를 더한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`

## 구현

**파라미터 (SerializeField, 하드코딩 금지)**:

```csharp
[Header("Drag sway")]
[SerializeField] private float swayMaxAngle = 18f;      // deg clamp
[SerializeField] private float swaySpring = 90f;        // k (복원)
[SerializeField] private float swayDamping = 12f;       // c (감쇠)
[SerializeField] private float swayImpulseScale = 0.6f; // 수평 px delta → 각속도
```

**상태** (컨트롤러 또는 DragSession): `float _swayAngle; float _swayVel; float _lastPointerX; bool _hasLastPointer;`

**impulse (`UpdateDrag` 내 — 포인터 이동 시에만 발화)**:

```csharp
if (_hasLastPointer)
    _swayVel += -(screenPosition.x - _lastPointerX) * swayImpulseScale; // 부호는 Play 로 확정
_lastPointerX = screenPosition.x;
_hasLastPointer = true;
```

**적분·감쇠 (신규 `Update()`, 매 프레임 — F1 BLOCKER)**:

```csharp
if (!_session.active || _session.preview == null || _session.swayPivot == null) return;
float dt = Time.unscaledDeltaTime;
_swayVel += (-swaySpring * _swayAngle - swayDamping * _swayVel) * dt;
_swayAngle = Mathf.Clamp(_swayAngle + _swayVel * dt, -swayMaxAngle, swayMaxAngle);
// child(sway pivot) 에 local Z-roll 합성 — Billboard 는 root(LateUpdate) 만 소유
_session.swayPivot.localRotation = Quaternion.Euler(0f, 0f, _swayAngle);
```

- 입력 콜백(`OnDrag → UpdateDrag`)은 포인터가 **움직일 때만** 발화 → 스프링 적분은 **반드시 이 `Update()` 소유**.
  입력 콜백에서 적분하면 포인터가 멈추는 순간 각도가 얼어붙어 감쇠(settle)가 안 된다.
- `swayPivot` = unit 0 이 만든 child. `Billboard` 가 root 를 `LateUpdate` 로 덮어써도
  child local-Z 는 그 위에 합성된다.
- 기존 `DefenderDragPlacementController` 에는 `Update()` 가 없다 — 신규 추가(충돌 없음).

**정리**: `CleanupSession` 에서 `_swayAngle = 0; _swayVel = 0; _hasLastPointer = false;`(프리뷰는 이미 파괴됨).

## 완료 기준

- compile.
- Play(에디터 **포커스**): 포인터를 좌우로 흔들면 프리뷰가 좌우로 기울고,
  **멈추면 감쇠 진동 후 정지**(얼어붙지 않음). 45° 틸트 위에서 좌우 lean 으로 읽히고, 발 피벗이라 뜨지 않음.
- 배치된 실제 유닛에는 sway 없음. 드롭 / 취소 시 상태 리셋.
- 스크린샷 또는 짧은 육안으로 감쇠(settle) 확인.
