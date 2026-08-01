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

솟구침의 나머지는 기존 노브로 올린다. 후보 대역: `dropArcMinHeight` 3.5 → 4.5,
`bossLeapArcMinHeight` 3.5 → 4.5. **제어점 높이 semantics 이므로 실제 apex 는 약 0.4배**다
(`0_dismount_arc_math.md` 계약). 최종값은 Play 로 정한다.

### 착지 스쿼시

`PlayLandingSquash(amount, seconds)` — `_squash` 를 `(1+a, 1−a, 1+a)` 로 놓고 `seconds` 동안
`Vector3.one` 으로 복귀. unit 1 의 `ApplyRenderScale` 이 합성하므로 비행 스케일·펀치와 곱해질 뿐
서로를 지우지 않는다. 시계는 각 연출의 시계를 따른다(드롭 unscaled / 도약 Battle).

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
