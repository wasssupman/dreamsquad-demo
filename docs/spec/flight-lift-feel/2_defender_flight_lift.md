# 2 — 디펜더 비행이 lift 를 동반 전달

## 목적

D&D 드롭 하마와 재배치 던지기를 unit 1 의 규칙에 태운다. 두 연출은 아치 높이를 **절대 view 좌표에
통합**해서 넘기기 때문에(`SetFlightView(viewPos)`) 뷰가 "얼마나 떴는지"를 모른다. 높이 값 하나를
같이 실어 보낸다.

**좌표 체계는 손대지 않는다**(계약 5). 보스가 (평면, 높이) 2축으로 나눈 것은 `BoardSpace.ToView` 가
sim-Y 를 버리기 때문이고, 디펜더 비행은 애초에 view 좌표라 그 문제가 없다. 잘 도는 좌표계를 높이를
알기 위해 재구성하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `SetFlightView` 에 인자 1개 (`:291`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — 오버라이드 값에 lift 축 (`:116`~`:125`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 디펜더 피드의 오버라이드 소비 (`:2825`)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `RunDropDismount` (`:1205`)
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — 비행 루프 (`:243`~`:255`)

## 구현

### 전달 경로

```csharp
// BattleBridge.Relocation.cs
private readonly Dictionary<Entity, (float3 viewPos, float lift)> _defenderViewOverride = new();
public void SetDefenderViewOverride(Entity entity, Vector3 viewPos, float lift = 0f)
```

- **기본값 0** — 세 번째 인자를 안 주는 호출처가 있어도 항등이다.
- `TryGetDefenderViewOverride` 도 lift 를 함께 반환하고, 소비 지점(`BattleBridge.cs:2825`)이
  `flightSpine.SetFlightView(viewPos, lift)` 로 넘긴다.
- 디펜더 폴백 뷰(`defenderFallbackViewPool`, `transform.position` 직접 대입 경로)는 **N/A** —
  개발용 폴백이라 반응 없이 위치만 따라간다.

### 뷰

```csharp
public void SetFlightView(Vector3 viewPos, float lift = 0f, Vector3 groundAnchor = default)
```

`transform.position = viewPos` + 정렬 유지는 그대로 두고, unit 1 의 `UnitLiftVisual.Resolve(lift, …)` →
`_flightScale` / `_blob.SetFlight(...)` 갱신을 덧붙인다. 이 경로는 `ApplyRenderPosition` 을 타지 않으므로
lift 반영을 **여기서 직접** 해야 한다(보스는 피드가, 디펜더는 이 진입점이 소유).

### 접지 앵커 (rev — `1746a731`)

`groundAnchor` 는 **그림자가 서 있을 XZ** 다. 아치가 `camUp` 방향이라 `camUp.z = 0.866`(pitch 60°)
성분이 유닛의 월드 Z 를 밀어, 블롭이 착지 타일에서 **약 2타일 미끄러졌다 돌아왔다**. 그림자는 "어느 칸에
내려앉나"를 알려주는 앵커인데 하필 그 순간 엉뚱한 칸을 가리키는 셈이다.

호출측이 **아치 기저선**(`Lerp(start, end, t)` — lift 계산에 이미 쓰는 값)을 그대로 넘긴다. 실제 그림자도
그렇게 움직인다. 기본값 `default`(zero)면 앵커 없음 = 종전대로 유닛을 따라간다.

보스 도약·넉업은 아치가 순수 +Y 라 XZ 가 안 밀리므로 이 경로를 쓰지 않는다 — 정상 피드
(`ApplyRenderPosition`)가 매 프레임 앵커를 해제한다(`_flightHeight` 와 같은 자기해제 규약).

### lift 계산 (호출측 한 줄)

두 연출 모두 "기저선 대비 얼마나 떴나" 로 구한다. 기저선은 출발→도착 직선 보간이다.

```csharp
float lift = Mathf.Max(0f, Vector3.Dot(p - Vector3.Lerp(start, end, f), camUp));
```

- `RunDropDismount`(`:1227` 직후): `f` 는 이미 있는 정규화 시간, `camUp` 은 이미 인자로 받고 있다.
  반동 구간에서 dip 이 음수를 만들므로 `Max(0, …)` 이 필요하다 — **내려앉을 때는 반응이 없다.**
- `RunRelocationFlight`: `k`(OutCubic 이징 후 값)를 그대로 쓴다. `ThrowArcControls` 의 `boardRight`
  좌우 변주 성분은 `camUp` 투영에서 자동으로 떨어져 나가므로 별도 처리가 없다.
- 출발·도착 타일의 높이가 달라도 기저선 보간이라 성립한다.

## 완료 기준

- compile 클린 · EditMode 무회귀
- **드롭 Play**: 유닛을 드래그해 놓으면 반동으로 살짝 내려앉은 뒤(이때 크기 변화 **없음**) 솟구치며
  커지고, 그림자는 놓인 타일에 남아 작아졌다가 착지에 맞춰 원상 복귀
- **재배치 Play**: 배치된 유닛을 다른 칸으로 옮길 때 같은 반응. 던지기 곡선·좌우 변주는 무변경
- **팝 0 무회귀**: 착지 프레임에 오버라이드가 해제될 때 위치·크기 어느 쪽도 튀지 않는다
  (착지 최종점이 정상 피드 공식과 같은 좌표라는 기존 계약이 그대로 산다)
- **비행 창 ⊆ pending 창 무회귀**: 드롭 총 시간은 안 바뀌었으므로 공중 유닛이 활성화되지 않는다
- **취소 경로 무회귀**: 드래그 중 판매·맵 리빌드로 비행이 끊길 때(`AbandonDismount` /
  `FinishDismountsInstant`) 유닛이 커진 채 굳지 않는다 — 오버라이드 해제 = lift 0 = 항등

## 검증 기록

- 2026-08-01 · EditMode 1790 중 1788 통과·실패 0 · compile 클린 · 독립 코드 리뷰 반영(`c6f6405e`).
- 확인: 2026-08-02 · 사용자 Play 감각 확인 통과(드롭 2초·도약 2초로 늘려 관찰 후 원 수치 복귀).
