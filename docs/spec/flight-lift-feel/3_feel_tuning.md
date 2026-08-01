# 3 — 감각 튜닝: 시간 리듬 · 아치 높이 · 착지 스쿼시

## 목적

unit 0 의 재매핑을 두 연출에 적용하고, 눈으로 값을 맞춘다. 사용자가 지목한 두 아쉬움 —
**"솟구침이 약하다"** 와 **"착지 임팩트가 약하다"** — 을 같은 손잡이 하나(재매핑)로 잡고,
남는 부분을 아치 높이와 착지 스쿼시로 마감한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `RunDropDismount` (`:1225`~`:1236`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.BossLeap.cs` — `RunBossLeap` (`:136`~`:171`)
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑩ 섹션에 노브 2개 (`:125`~`:151`)
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `PlayLandingSquash` (unit 1 의 `_squash` 소비)

## 구현

### 시간 재매핑 적용 (두 연출 동형)

```csharp
float f  = Mathf.Clamp01(elapsed / duration);
float u  = Mathf.Clamp01((f - recoilFrac) / (1f - recoilFrac));
float t01 = f <= recoilFrac
    ? f
    : recoilFrac + (1f - recoilFrac) * KeyringSim.FlightTimeRemap(u, hangPower);
var p = KeyringSim.DismountPoint(..., t01);
```

반동 구간은 손대지 않는다(계약 6). 총 시간 불변이라 드롭의 pending 창 계약과 도약의 Battle 도메인
시계가 그대로 산다.

### 노브

| 노브 | 소유 | 기본 | 비고 |
|---|---|---|---|
| `dropHangPower` | `DragSwaySettings` ⑩ | 0.7 | 1 = 현행 항등 |
| `dropLandingSquash` | `DragSwaySettings` ⑩ | 0.10 | 0 = 스쿼시 없음 |
| `dropLandingSquashSeconds` | `DragSwaySettings` ⑩ | 0.05 | |
| `bossLeapHangPower` | `BattleBridge.BossLeap` | 0.7 | 기존 관례대로 드롭 값과 1:1 대응 |
| `bossLeapLandingSquash` | `BattleBridge.BossLeap` | 0.10 | 동일 |
| `bossLeapLandingSquashSeconds` | `BattleBridge.BossLeap` | 0.05 | 동일 |

스쿼시 기본값 근거: `defender-drop-dismount` README 후속 후보의 **"착지 스쿼시(y 0.9, 2~3프레임) —
unit 3 완료 후 육안 판단"**. `y 0.9` = amount 0.10, `2~3프레임` ≈ 0.05s 로 옮겼다. 확정값이 아니라
그 spec 이 남긴 제안이므로 Play 에서 다시 본다.

리듬·스쿼시는 **연출별 취향**이라 기존 궤적 기하와 같은 자리(이중 소유)에 둔다. unit 1 의 lift 반응
노브와 성격이 다르다 — 그쪽은 원근 보상이라 전역 단일 소유다(계약 3). 이 구분을 뒤집지 말 것.

### 아치 높이 재튜닝

솟구침의 나머지는 기존 노브로 올린다. **확정값: `dropArcMinHeight` · `bossLeapArcMinHeight` 둘 다
3.5 → 4.5 (`60041776`) → 6.0 (`ebf7238c`, 사용자 지시).** 제어점 높이 semantics 이므로 실제 apex 는
약 0.4배 = **2.4 world**(`0_dismount_arc_math.md` 계약).

⚠ 여기서 **크기 단서가 포화한다** — lift 2.4 에서 유닛 배율이 1.336 으로 상한 `liftScaleMax`(1.35)에
붙는다. 아치를 더 올리면 높이만 늘고 크기는 안 따라오므로 `liftScaleMax` 를 함께 올려야 한다.

드롭은 `dropArcHeightFactor` 가 **1.399** 로 이미 튜닝돼 있어(코드 기본값 0.5 아님) 실효 아치 =
`max(거리 × 1.399, 6.0)` 이다 — 먼 거리 드롭은 원래도 6.0 을 넘으므로 이 하한 상향은 **짧은 드롭에만**
영향을 준다. 보스는 factor 0.5 라 대부분의 도약이 하한에 걸려 전 구간이 올라간다.

### 착지 스쿼시

`PlayLandingSquash(amount, seconds)` — `_squash` 를 `(1+a, 1−a, 1+a)` 로 놓고 `seconds` 동안
`Vector3.one` 으로 복귀. unit 1 의 `ApplyRenderScale` 이 합성하므로 비행 스케일·펀치와 곱해질 뿐
서로를 지우지 않는다.

**시계는 두 소비처 공용 `unscaledDeltaTime`** (구현 중 확정 — 초판의 "각 연출의 시계를 따른다"에서
변경). 착지 눌림은 0.05초 순간 반응이라 슬로모(0.2x)에 늘어지면 임팩트가 통째로 죽는다. 대가는
슬로모 중 도약 착지에서 궤적과 눌림의 시계가 갈리는 것 — 눌림이 워낙 짧아 수용한다.

⚠ **k 는 시간 증분 앞에서 적용한다.** 증분을 먼저 하면 첫 렌더 프레임의 `k` 가 이미 1 미만이라
authored `amount` 에 영영 도달하지 못하고, 세기가 프레임레이트에 비례해 갈린다(60fps 0.67 /
30fps 0.33 → 실기기에서 절반 세기).

발화 지점은 **명시 호출 2곳**(계약 8):

- `RunDropDismount` 착지 블록(`:1234` 근처) — `ClearDefenderViewOverride` 직후
- `RunBossLeap` 의 `ResolveLanding` 진입 — **`abandoned` 면 호출하지 않는다.** 슬램 미발화와 같은 이유다

`PunchRoutine` 이 균등 펀치인 것은 "유닛은 3D 반응 주역" 이라는 판단이었다(`SpineUnitView.cs:355`).
착지는 다른 맥락이라 비균등을 쓴다 — 2D 스켈레톤이라 squash & stretch 가 오히려 어울린다.
어색하면 `amount = 0` 으로 끈다.

## 완료 기준

- compile 클린 · EditMode 무회귀
- **`hangPower = 1` · `squash = 0` 으로 두면 unit 2 종료 시점과 동일한 움직임** (재매핑 항등 경로 확인)
- **드롭 Play**: 놓는 순간 빠르게 솟고, 정점에서 한 박자 머물고, 내리찍듯 떨어진다. 착지 프레임에
  짧게 눌렸다 복귀
- **도약 Play**: 보스 도약이 같은 리듬. 착지 슬램 VFX 와 스쿼시 타이밍이 어긋나지 않는다
- **끝접선 수직 착지 무회귀**: 착지 직전 궤적이 여전히 수직으로 내리꽂힌다(재매핑은 끝속도를 키울 뿐)
- **비행 창 ⊆ pending 창 무회귀**: 드롭 유닛이 공중에서 활성화되지 않는다
- 최종 판정은 **사용자 Play 육안**. 정지 스크린샷으로는 애니메이션 리듬을 판정할 수 없다

## 검증 기록

- 2026-08-01 · EditMode 1790 중 1788 통과·실패 0 · compile 클린 · 독립 코드 리뷰 반영(`c6f6405e`).
- **사용자 Play 감각 확인은 미완** — 통과 시 이 줄 아래에 확인 일자를 추가한다.
