# 3 — BattleBridge dim 상태 + sync 합성

**작업 구분**: bridge

## 목적

dim 상태를 소유하고, 매 프레임 적 뷰에 반투명을 적용한다. 페이드는 unscaled lerp. 튜닝은 serialized.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

- SerializeField(하드코딩 금지):
  ```csharp
  [SerializeField] private float enemyDragDimAlpha = 0.3f;   // 목표 불투명도
  [SerializeField] private float enemyDragDimFadeSpeed = 8f; // 초당 lerp 속도
  ```
- 런타임 상태:
  ```csharp
  private bool _enemyDimActive;
  private float _enemyDimAlpha = 1f;
  public void SetEnemiesDimmed(bool active) => _enemyDimActive = active;
  ```
- `Update()`(기존 1715 부근)에 페이드:
  ```csharp
  float target = _enemyDimActive ? Mathf.Clamp01(enemyDragDimAlpha) : 1f;
  _enemyDimAlpha = Mathf.MoveTowards(_enemyDimAlpha, target,
      enemyDragDimFadeSpeed * Time.unscaledDeltaTime);
  ```
- `SyncMonoUnitViews()` 적 루프(1794~1816)의 두 분기에 `SetDimmed` 삽입.
  `SetDimmed` 를 `SetHealthTint` **앞에** 호출(Quad 는 SetHealthTint 가 알파 반영):
  ```csharp
  bool transparent = _enemyDimAlpha < 0.999f;
  // spine 분기
  spineView.SetDimmed(transparent, _enemyDimAlpha);
  spineView.SetHealthTint(tint);
  // quad 분기
  view.SetDimmed(transparent, _enemyDimAlpha);
  view.SetHealthTint(tint);
  ```
- 디펜더 루프(`_defenderByTile`)는 **건드리지 않음** — 적만 dim.

## 완료 기준

- compile 통과, 콘솔 무에러.
- `SetEnemiesDimmed(true)` → 몇 프레임 내 적(Spine·Quad)이 `enemyDragDimAlpha` 로 페이드,
  `false` → 1f 로 복귀(unit 4 배선 후 실증).
- 인스펙터에서 `enemyDragDimAlpha`/`enemyDragDimFadeSpeed` 조정이 실시간 반영.
- 디펜더는 불투명 유지(경계 확인).
