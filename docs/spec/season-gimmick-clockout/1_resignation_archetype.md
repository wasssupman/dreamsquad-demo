# 1. 사직서 아키타입 (Resignation + 뷰)

## 목적

맵 위 "사직서" 오브젝트의 존재를 세운다 — ECS 컴포넌트 + poll-reconcile 뷰. 스폰(퇴근 시)은 unit 2, 임계 소모는 unit 3. 기존 레드불 `Pickup` 뷰 파이프라인 동형.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Resignation.cs` — 신규 `IComponentData { int2 cell }`
- `Assets/_Project/Scripts/Battle/Effects/ResignationPresenter.cs` — 신규 MonoBehaviour (플레이스홀더 흰 종이 + idle 부양)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — resignation 뷰 SerializeField + 쿼리/맵/reconcile/clear (`ReconcilePickupViews` 동형)

## 구현

1. **`Resignation`**(Effects): `{ int2 cell }`. 유닛이 줍지 않음 — 전역 임계(unit 3)로만 destroy. `Pickup`(소비 주체=유닛)과 별개 아키타입.
2. **`ResignationPresenter`**: prefab 있으면 인스턴스, 없으면 절차적 흰 종이(얇은 박스). `Time.unscaledDeltaTime` idle 부양(정지/슬로우모 무관). `Init(prefab, baseLocalY)`.
3. **BattleBridge**: `resignationViewPrefab`/`resignationViewHeight` SerializeField + `_resignationViewQuery`/`_resignationVisualMap`/`_resignationReapBuffer` + `ReconcileResignationViews()`(LateUpdate) + `ClearResignationVisuals()`(teardown) + 쿼리 생성/해제. 전부 Pickup 뷰 코드 동형(셀중심→`BoardSpace.ToView`).

## 완료 기준

- compile 0 에러(Unity 재컴파일 콘솔).
- reconcile 배관이 Pickup 과 동형으로 배선(쿼리 생성/해제·LateUpdate 호출·teardown clear). 실제 사직서 스폰이 아직 없어(unit 2) **육안 뷰 검증은 unit 2 에서 실측** — 이 유닛은 배관까지.

확인 2026-07-16 — Unity 재컴파일(MCP refresh) `Wassup.Runtime.dll` ILPP 후처리 성공 · refresh 이후 error CS 0 (Editor.log) · editor_state clean/reloaded. (read_console 는 harness 분류기 일시 다운으로 미조회 — 로그 증거로 대체.)
