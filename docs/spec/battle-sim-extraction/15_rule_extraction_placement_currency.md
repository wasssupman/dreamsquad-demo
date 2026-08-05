# 15 — Bridge 규칙 적출 ② 배치 규칙 + 통화 5종

## 목적

커맨드 검증이 **sim 안에서 닫히게** 만든다. 지금은 배치 적법성은 Bridge, 코스트·쿨다운은
MonoBehaviour 런타임, 유출 허용치는 Bridge private 라 흩어져 있어 `DeployDefender` 하나를
검증하려면 세 계층을 왕복한다(청사진 ① §10-2).

## 변경 대상

- **배치 규칙**: `SpatialPlacementCheck`(이미 순수 static = conform) · `CanPlaceDefenderAt` ·
  `TryBeginDefenderDeployment` · `ActivateDeployedDefender` · `TriggerDeploymentOnPlaceSkill` ·
  `RelocationCheck`/`TryBeginDefenderRelocation`/`FinishDefenderRelocation` ·
  `ApplyOnPlaceEffect` · `RecomputeSynergyFor`/`NeutralizeActiveSynergy`
- **통화 5종 sim 이관**: `CostRuntime`(float — **고정소수점화 검토**, 현 `CanAfford` 는 float 비교) ·
  배치 쿨다운 `PlacementCooldownRuntime`(→ `OnCooldown` 거절 사유 신설의 근거) ·
  스킬 쿨다운 `SkillRuntime` · 유출 허용치(unit 14 에서 이미 이동) · 각성 게이지는 **unit 16**
- 은퇴 경로 삭제: `PlaceDefender`(랜덤픽 레거시) · `PlaceDefenderAs`(클릭 배치 은퇴) — salvage discard
- `DefenderSelector` — 쿨다운 시작 책임 제거(현재 UI 가 `StartCooldown` 호출: 청사진 ① §2 실측)

## 구현

- **활성화 지연을 sim 시퀀스로**(청사진 ① §2 `SetDeployFacing`): 현재 뷰 코루틴
  (`PlayDeploymentPresentation` 길이 + `placementSkillDelay`)이 sim 전이 시각을 소유한다 →
  Deploy 가 `activationTick` 을 예약하고 facing 커맨드는 그 전 도착분만 병합, 미도착 시 기본 +Y.
  **재배치 비행도 같은 형태**(`landTick`)로 sim 상태화한다.
- 통화가 sim 으로 오면 `TrySpend` 이중 검사(현 `CanAfford` 후 재호출)가 **커맨드 원자 검증 1회**로 접힌다.
- ⚠ **슬로모-통화 결합**(청사진 ① §10-4·리뷰 M10): `CostRuntime`·`PlacementCooldownRuntime` 이
  Battle 도메인 dt 로 tick 하므로 드래그/조준 슬로모가 지금 **코스트 회복·쿨다운을 늦춘다**.
  이 unit 은 그 결합을 **그대로 옮긴다**(행동 보존). 처분은 unit 19.

## 완료 기준

- compile 0 · EditMode 회귀 0 · **골든 7종 byte diff 0**.
- `DeployDefender`/`RelocateDefender`/`SetDeployFacing` receipt 의 거절 사유가 **sim 단독 판정**으로
  나온다 — `OnCooldown` 포함 EditMode 단정(현재는 UI 게이트뿐이라 커맨드 우회 시 무시됨).
- 배치 코스트·쿨다운이 sim 상태이므로 **스냅샷에 실린다**(청사진 ① §5 currencies) — 직렬화 왕복 테스트.
- Bridge/UI 에서 통화 직접 조작 0(grep): `TrySpend`·`StartCooldown`·`Consume` 호출이 sim 밖에 없다.

---

## 진행 상황 (2026-08-05) — 15-A 완료, 나머지 미착수

### 15-A 완료: 쿨타임 판정을 규칙으로, 시작 책임을 UI 에서 회수

- `PlacementRejectReason.OnCooldown` 신설(맨 뒤 — 기존 직렬화 값 보존) → `CommandReject.Place_OnCooldown`
  으로 매핑. 그 enum 값은 unit 12 가 이미 예고해 뒀던 자리다.
- `CanPlaceDefenderAt` 이 쿨타임을 본다. **그 전까지 이 판정은 `DefenderSelector` 의 딤 처리에만
  있어서 뷰를 거치지 않는 배치 경로(세션 커맨드·클릭 배치·테스트)가 쿨타임을 통째로 무시했다.**
- 쿨타임 **시작**의 단일 소유자가 `BattleBridge.StartPlacementCooldown` 이다. 배치 성사 지점 두 곳
  (`PlaceDefenderAs`·`TryBeginDefenderDeployment`)이 부른다. `DefenderSelector` 의
  `PlacementCommitted` 구독은 그것만을 위한 배선이었으므로 **함께 제거**했다.
- `PlacementCooldownGateTests` 4건 — **골든은 이 회귀를 잡지 못한다**(하네스는 유닛 타입마다 1회만
  배치하고, 쿨타임은 정규 상태 라인에도 없다). 그래서 이 EditMode 4건이 유일한 증인이다.

### ⚠ 발견 — "은퇴 경로 삭제(`PlaceDefenderAs`)" 는 골든 코퍼스와 충돌한다

이 문서가 `PlaceDefenderAs` 를 salvage discard 로 적었지만, **골든 하네스가 그 함수로 배치한다**
(`LegacyTraceGoldenRunner.PlaceFirstValid`). 그리고 결정적으로:

| 경로 | 코스트 차감 주체 |
|---|---|
| `TryBeginDefenderDeployment` (드래그 배치) | **Bridge 자신** (`TrySpend`) |
| `PlaceDefenderAs` (클릭 배치) | **UI** (`PlacementInput.cs:99`) |

즉 하네스는 `PlaceDefenderAs` 로 배치하면서 **코스트를 전혀 쓰지 않는다.** 그런데 `cost` 는
골든의 정규 상태 라인에 실린다(`BattleBridge.LegacyTrace.cs`). ⇒ 코스트 차감을 sim 으로 모으거나
하네스를 배치 경로로 옮기는 순간 **코퍼스가 바뀐다.**

그래서 15-B 는 다음 중 하나를 **먼저 결정**해야 한다:

1. unit 19 의 재기준선 권한과 함께 진행한다(코퍼스 갱신을 그 커밋이 소유).
2. `PlaceDefenderAs` 를 하네스 전용 seam 으로 남긴다(은퇴 취소 — 이 문서 수정).

판정 순서는 그대로다: 해시가 다르면 설정, 해시는 같고 스트림이 다르면 sim.

### 15-B 이후 (미착수)

- **통화 상태의 sim 이관** — `CostRuntime`·`PlacementCooldownRuntime` 은 아직 MonoBehaviour 이고
  self-tick 한다. 읽기면은 unit 13-A3 이 이미 세션으로 옮겨 놨으므로 남은 일은 **상태와 tick 의
  이사**다. 주의: 그 tick 순서가 바뀌면 `cost` 상태 라인이 흔들려 골든이 붉어진다(위와 같은 사유).
- **배치 규칙 이관** — `CanPlaceDefenderAt`·`TryBeginDefenderDeployment`·`ActivateDeployedDefender`·
  재배치 3종·`ApplyOnPlaceEffect`·시너지 2종.
- **활성화 지연을 sim 시퀀스로**(`activationTick`/`landTick`) — 현재 뷰 코루틴이 sim 전이 시각을
  소유한다. 행동 변화가 큰 조각이라 골든이 강한 증인이 된다.
- **`PlacementInput` 의 `TrySpend` 제거** — 위 충돌 결정 후.
- ⚠ **슬로모-통화 결합**은 이 unit 에서 **그대로 옮긴다**(행동 보존). 처분은 unit 19.
