# 1 — 리프트 시각 반응의 단일 정의

## 목적

"지면에서 뜬 높이(lift)" 하나에서 **유닛 크기 · 그림자 크기 · 그림자 알파** 셋을 함께 파생시킨다.
셋이 같은 입력에서 한 지점에서 나오는 것이 계약이다 — 따로 계산하면 서로 다른 lift 를 보고 갈라진다.

이 유닛만 끝나도 **보스 도약과 넉업이 즉시 반응한다.** 둘 다 이미 lift 를 뷰에 넘기고 있기 때문이다
(`SetFlightHeight` / `CurrentHopOffset()`). 디펜더 배관은 unit 2.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/UnitLiftVisual.cs` — **신규** static 헬퍼
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 스케일 합성 단일화 + lift 배선
- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs` — 동일(hop 없음, `_flightHeight` 만)
- `Assets/_Project/Scripts/Presentation/BlobShadow.cs` — 비행 반응 + 알파 2배수 합성
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 전역 노브 5개 (`BlobShadowSize` 계열 `:248`·`:1021`)

## 구현

### 전역 노브 (BattleBridge SerializeField → static)

`liftScalePerHeight`(기본 0.14) · `liftScaleMax`(1.35) · ~~`liftShadowMinScale`(0.55)~~ ·
`liftShadowMinAlpha`(0.35) · `liftShadowFullHeight`(3.0 — 그림자가 최소치에 닿는 높이).

> **rev 2026-09-04** — `liftShadowMinScale` 은 **은퇴**했다. `distance-based-range` unit 15 가
> 「그림자 지름 = 판정 몸」을 계약으로 세우면서 높이로 그림자를 줄이는 축이 사라졌고
> (`UnitLiftVisual.Resolve` 의 shadowScale 은 상수 1), 그 뒤로 이 노브는 인스펙터에서
> **아무 일도 안 하면서 조절 가능해 보였다.** 아래 「전 반응 OFF」 조합도 노브 2개로 줄었다.

**기본값이 곧 현행에 가까운 대역**이고, `liftScalePerHeight = 0` 이면 전 반응이 항등이다. 미배선
씬에서도 안전하다. 노브를 `DragSwaySettings`·`BattleBridge.BossLeap` 에 복제하지 않는다(계약 3).

### 헬퍼

```csharp
// flight-lift-feel unit 1 — lift(지면에서 뜬 view 공간 높이) → 세 배율.
// 한 지점에서 함께 파생하는 것이 계약 — 따로 계산하면 유닛과 그림자가 다른 lift 를 본다.
// BattleBridge 전역 static 을 직접 읽는다: Presentation 이 BlobShadowSize/Color 를 읽는 관용구 그대로.
public static class UnitLiftVisual
{
    public static void Resolve(float lift,
        out float unitScale, out float shadowScale, out float shadowAlpha)
}
```

- `unitScale = min(1 + lift × liftScalePerHeight, liftScaleMax)` — 단위 높이당 비율(계약 2).
- 그림자는 `r = clamp01(lift / liftShadowFullHeight)` 로 `Lerp(1, min…, r)`.
- `lift <= 0` 이면 셋 다 항등 — 반동으로 내려앉는 구간에서 반응이 안 생긴다.

### 스케일 합성 단일화 (두 뷰 공통)

`transform.localScale` 직접 대입을 **전부 걷어낸다.** 슬롯 3개와 합성 1지점:

```csharp
private float _flightScale = 1f;   // 매 프레임 피드
private float _punchScale = 1f;    // PunchRoutine 소유
private Vector3 _squash = Vector3.one;  // 착지 스쿼시(unit 3 에서 사용, 여기선 슬롯만)

private void ApplyRenderScale()
    => transform.localScale = Vector3.Scale(_baseScale * (_flightScale * _punchScale), _squash);
```

- `PunchRoutine`(`SpineUnitView.cs:353`)이 `transform.localScale = Lerp(...)` 하던 것을 `_punchScale`
  갱신 + `ApplyRenderScale()` 호출로 바꾼다. **시계는 unscaled 그대로**(카드 비행과 톤 일치).
- 왜 필요한가: 매 프레임 피드와 코루틴이 같은 필드를 다투면 피드가 펀치를 덮거나 펀치 종료의
  `= _baseScale` 이 비행 스케일을 지운다. 스쿼시까지 오면 3자 경합이다(계약 4).

### lift 배선

`ApplyRenderPosition`(`SpineUnitView.cs:184`)이 이미 `CurrentHopOffset() + _flightHeight` 를 합산한다.
**그 합이 곧 lift** 다. 그 값을 `UnitLiftVisual.Resolve` 에 넣어 `_flightScale` 을 갱신하고
`ApplyRenderScale()` + `_blob?.SetFlight(shadowScale, shadowAlpha)` 를 호출한다. 매 프레임 피드가
값을 다시 쓰므로(비행 아니면 lift 0 → 항등) **별도 clear 경로가 필요 없다** — `_flightHeight` 가
쓰던 것과 같은 규약이다.

`QuadUnitView` 는 hop 이 없어 lift = `_flightHeight`(`:127`·`:134`). 나머지는 동일.

### BlobShadow

`SetFlight(float scaleMul, float alphaMul)` 추가. `ApplyTransform` 의 `_size` 에 `scaleMul` 을 곱하고,
색은 **두 배수를 각각 보관해 곱한다**:

```csharp
private float _dimFactor = 1f, _flightAlphaFactor = 1f;
private void ApplyColor()
    => _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b,
                             _baseColor.a * _dimFactor * _flightAlphaFactor);
```

`SetDimAlpha` 가 base 색을 통째로 덮어쓰던 구조라, 그대로 두면 배치 dim 과 비행 알파가 서로 지운다.

지면 Y 고정(`transform.position = (p.x, _groundY, p.z)`)과 **부모 `lossyScale` 보정은 그대로 둔다** —
그 보정 덕분에 유닛이 커져도 그림자 월드 지름이 안 딸려 올라간다.

## 완료 기준

- compile 클린 · EditMode 무회귀(신규 테스트 없음 — 계산이 clamp+lerp 라 회귀 위험이 낮다)
- **보스 도약 Play**: 도약 중 보스가 눈에 띄게 커졌다 원래대로 착지하고, 그림자는 **지면에 남아**
  작아지고 옅어진다. 착지 순간 크기·그림자가 정확히 원상 복귀(팝 없음)
- **넉업 Play**: 말파이트 넉업에서 같은 반응이 짧게 일어난다(크기 깜빡임이 거슬리면 README 후속 후보)
- **펀치 무회귀**: 카드 흡수 임팩트(`PlayPunch`)가 이전과 같이 튀고 원래 크기로 복귀. 비행 중 펀치가
  들어와도 둘이 곱해질 뿐 어느 쪽도 사라지지 않는다
- **배치 드래그 dim 무회귀**: 드래그 중 적 그림자 페이드가 그대로 동작
- **전 반응 OFF = 노브 3개**: `liftScalePerHeight 0` + `liftShadowMinScale 1` + `liftShadowMinAlpha 1`.
  스케일과 그림자는 **독립 노브**라 `liftScalePerHeight` 하나만 0 으로 두면 유닛 크기만 원복되고
  그림자는 계속 줄고 옅어진다. 무회귀 판정 때 셋 다 내려야 한다.

## 그림자 반응은 블롭 경로에서만 산다 (해소됨 2026-08-02)

`SpineUnitView.ApplyTilemapShadow` 는 `BattleBridge.UseRealShadows` 가 참이면 **블롭을 만들지 않는다**
(둘은 상호배타, `tilemap-real-shadows` unit 2 계약). `UseRealShadows = useRealShadows && !isMobilePlatform`
이므로, 씬이 `useRealShadows: 1` 이던 동안에는 **에디터·데스크톱에서 `_blob == null` → 그림자 반응이
전부 no-op** 이었다. 게다가 실그림자 경로에서는 유닛 확대가 renderer 실루엣을 키워 **바닥 cast 그림자가
같이 커진다** — "뜰수록 작아진다"의 반대 신호다.

**해소**: 사용자 판정으로 씬의 `useRealShadows` 를 **0 으로 내렸다**(`bff474a1`). 이제 PC·모바일 모두
블롭 경로를 타므로 이 유닛의 그림자 계약이 전 플랫폼에서 성립한다. 근거는 성능이 아니라 룩이다 —
유닛 그림자는 조명이 아니라 "어느 칸에 있나"를 알려주는 **앵커**라, 광원 쪽으로 늘어지는 실루엣보다
발밑 타원이 판독성에서 낫다는 판단.

⚠ 되돌릴 경우(실그림자 복귀) 이 유닛의 그림자 절반은 다시 죽고 확대만 남는다. 그때는 계약을 "확대 전용"
으로 좁히든지, 확대가 cast 그림자를 키우는 문제를 별도로 풀어야 한다.

## 검증 기록

- 2026-08-01 · EditMode 1790 중 1788 통과·실패 0 · compile 클린 · 독립 코드 리뷰 반영(`c6f6405e`).
- 확인: 2026-08-02 · 사용자 Play 감각 확인 통과(드롭 2초·도약 2초로 늘려 관찰 후 원 수치 복귀).
