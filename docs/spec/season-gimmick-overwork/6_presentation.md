# 6. 레드불 픽업 뷰 (+ 상태 연출 위임 결정)

## 목적

레드불 픽업을 화면에 보이게 한다. 지금까지 픽업은 ECS 엔티티로만 존재해 로그로만 검증 가능했다. 이 unit 으로 픽업이 보드에 보이고, defender 를 그 셀에 배치하는 시각 검증이 가능해진다.

## 스코프 결정 (2026-07-15)

- **레드불 뷰**: 구현 (이 unit 핵심).
- **번아웃/라스트런 상태 연출**: **별도 제작 안 함 — 다른 세션의 Buffed/Debuffed 오라에 위임.**
  - 번아웃 = 임시 디버프 StatModifier(AS/DMG/MaxHP ×0.8), 라스트런 = 임시 버프(AS ×1.5)+디버프(MaxHP ×0.1). 모두 **임시 지속 슬롯** → `unit-buff-debuff-aura` 세션의 Debuffed/Buffed 분류기가 자동으로 아이콘을 띄운다.
  - 지금 별도 `Burnout` StatusFxKind 를 만들면 그 작업과 **중복·충돌**. 따라서 상태 연출은 그 오라 완성에 위임하고, 여기선 만들지 않는다. (로그로는 `Log Fatigue Stacks` 로 이미 검증됨.)

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/PickupPresenter.cs` — 절차적 플레이스홀더 뷰 (발광 큐브 + bob/spin)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ReconcilePickupViews`(LateUpdate) + `ClearPickupVisuals`(teardown) + `_pickupViewQuery` + `pickupViewPrefab`/`pickupViewHeight` SerializeField

## 구현

1. **PickupPresenter** (MonoBehaviour, Battle/Effects — BlockingHazardPresenter 동형 위치): `pickupViewPrefab` 미지정 시 절차적 플레이스홀더(Red Bull 블루 발광 큐브, unscaled bob+spin). 정식 아트는 후속.
2. **BattleBridge poll-reconcile**: Pickup 은 순수 ECS 스폰이라 이벤트가 없어 **매 프레임 조정**(StatusFx 방식):
   - `_pickupViewQuery`(Pickup) 로 살아있는 픽업 수집 → 신규 엔티티엔 셀 월드중심에 뷰 생성, `_em.Exists` 로 사라진(소비/만료) 엔티티 뷰 파괴.
   - `_running` 무관 (placement 중에도 스폰). `GridMath.CellToWorldCenter(cell, tileSize, pickupViewHeight, _boardOrigin)`.
   - 매치 teardown 시 `ClearPickupVisuals`.

## 완료 기준

- compile 통과 + 콘솔 클린.
- Play 진입 후 보드 위 이동/배치 타일에 **레드불 플레이스홀더(파란 발광 큐브)가 5초마다 나타나고**, 소비/만료 시 사라짐 (스크린샷 확인).
- defender 를 레드불 셀에 배치 → 소비되며 `Log Fatigue Stacks` 로 공속 ×1.5 → 최대체력 ×0.1 (라스트런 full 체인 시각+로그 동시 확인).

확인 2026-07-15 · 커밋 `c5040abc` — Play 스크린샷: 보드 이동/배치 타일에 레드불 플레이스홀더(파란 발광 큐브) 렌더 확인. 스폰/소비/crash 로그 정상, 에러 0. 상태 연출은 Buffed/Debuffed 오라 위임(문서 참조).

**수정 `57495fb3`**: 뷰가 셀→월드 배치 시 sim 좌표를 직접 써서 반 타일 어긋난 **모서리**에 놓이던 문제 → `GridToWorldCenter(sim) → BoardSpace.ToView` 경유로 **타일 중심** 안착. ⚠ **MonoBehaviour 뷰는 셀 배치 시 반드시 `BoardSpace.ToView` 를 거친다** (sim≠view; ToView 가 +0.5 로 Tilemap 셀 중심 보정, sim 높이는 무시 → hover 는 view world-up 으로).
