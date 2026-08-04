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
