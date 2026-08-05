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
- 은퇴 경로 삭제: `PlaceDefender`(랜덤픽 레거시) · ~~`PlaceDefenderAs`(클릭 배치 은퇴)~~ — **`PlaceDefenderAs`
  은퇴는 unit 18 이후로 이관됨** (사용자 결정 2026-08-05, 아래 "코퍼스 동결" 참조)
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

### 리뷰 반영 (2026-08-05 투트랙)

- **쿨타임 청구 지점을 "커밋" 으로 옮겼다** — 타일 점유 직후, 엔티티 생성·뷰 작업보다 **앞**.
  코스트 차감과 같은 자리다(배치가 받아들여진 순간 두 통화가 함께 청구된다). 뒤에 있으면 뷰
  단계에서 예외가 났을 때 **타일은 점유됐는데 쿨타임은 안 걸린** 상태가 남고, 규칙을 검증하는
  테스트가 뷰 배선까지 세워야 한다(실측: `CreateDefenderEntity` 가 Grid 를 요구해 EditMode 에서
  막혔다). 골든 무영향 — 쿨타임은 정규 상태 라인에 없다.
- **Track A 의 HIGH 는 기각**: "15-A 가 라이브 드래그 배치에 쿨타임을 새로 강제한다(밸런스 변경)"
  는 지적이었으나, `BeginDrag` 의 유일한 호출처인 `DefenderDragSlot.OnBeginDrag` 가 이미
  *"쿨타임 중이면 세션 자체를 시작하지 않는다"* 로 막고 있고(탭 경로도 동일),
  `PlacementInput.clickPlacementEnabled` 는 기본 false(은퇴 경로)다. **라이브는 원래 막혀 있었고
  15-A 가 닫은 것은 뷰 우회 경로가 맞다.** 리뷰어가 `DefenderSelector`/`DefenderDragPlacementController`
  만 보고 이 스펙이 "드래그/탭 진입 게이트" 로 지목한 `DefenderDragSlot` 을 놓쳤다.
  ⇒ 뷰 게이트(드래그 시작 차단)와 규칙 게이트(판정)는 **의도된 이중 방어**다. 어느 쪽도 지우지 말 것.
- `OnCooldown` 에 뷰 라벨을 줬다(`DefenderDragPlacementController`). 정상 경로에선 도달하지 않지만
  커맨드/디버그 경로의 거절이 "배치 불가" 로 뭉개지면 진단이 안 된다.

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

### ✅ 결정 — 코퍼스 동결 (사용자, 2026-08-05)

**`PlaceDefenderAs` 를 하네스 전용 seam 으로 남기고, 은퇴를 unit 18 이후로 미룬다.** 골든 코퍼스는
units 15-B~18 구간(가장 위험한 이식 구간) 동안 **byte 동결**이며 계속 `byte diff 0` 로 판정한다.

따라올 결과:

- 코스트 차감을 sim 으로 모으는 조각, 통화 tick 순서를 바꾸는 조각은 **이 구간에서 하지 않는다**
  (`cost` 가 정규 상태 라인이라 코퍼스가 움직인다). `PlacementInput` 의 `TrySpend` 도 그대로 둔다.
- 15-B 에서 할 수 있는 것은 **코퍼스 무해한 조각**이다: 판정 로직의 이사, 순수화, 소유권 정리 중
  값·시점을 바꾸지 않는 것.
- 은퇴/재기준선은 unit 18 의 sim lib 스왑이 어차피 코퍼스를 다시 찍을 때 함께 처리한다.
- 판정 순서는 그대로다: 해시가 다르면 설정, 해시는 같고 스트림이 다르면 sim.

### 15-B 완료: 배치 판정을 순수 규칙으로

- `PlacementRejectReason` 이 `Wassup.Bridge` → **`Wassup.Sim.Match`** 로 이사(파일도 함께).
  거절 사유는 배치 규칙의 산출물이므로 규칙과 같은 쪽에 있어야 한다 — Bridge 에 두면 sim 모듈이
  Bridge 네임스페이스를 알아야 해서 **의존 방향이 뒤집힌다**(CLAUDE.md 제약 1 의 후계).
- 신규 `MatchPlacementRules` — `Spatial`(공간 4조건) + `Check`(전체 판정). **순수 static**:
  통화·풀·뷰 배선처럼 호출자만 아는 것은 `bool` 로 받으므로 이 타입은 `CostRuntime`·`GameManager`
  를 모른다. **판정 순서가 계약**이다(뷰가 그 사유로 거절 이유를 그린다).
- `BattleBridge.SpatialPlacementCheck` 는 **포워더**로 남긴다 — 하이라이트 수집과 EditMode
  테스트가 이 이름을 쓰고 있다(로직 중복이 아니다).
- `CanPlaceDefenderAt` 은 입력 수집 + 규칙 호출만 한다.

### 15-C 진행 중

코퍼스 동결 결정에 따라 **동결 구간에 가능한 것 / 해제 후에 할 것**으로 갈린다.

#### ✅ 15-C-1 완료: 재배치 판정 이관

`RelocationCheck` 본체가 `MatchPlacementRules.Relocation` 으로 이사했고 `BattleBridge` 쪽은
포워더만 남는다(`SpatialPlacementCheck` 와 같은 처분 — 프로덕션 호출처 1곳이 그 이름을 쓴다).
`RelocationCheckTests` 7곳은 sim 규칙을 직접 부르게 바꿨다 — 테스트가 Bridge 이름을 계속 쓰면
unit 17 asmdef 분리 후에도 **테스트 어셈블리가 Bridge 를 참조**하게 된다.

이 조각은 unit 17 정찰이 지목한 와트도 함께 없앤다: 적출 전 `RelocationCheck` 는 **MonoBehaviour 의
static 이 sim enum 을 반환**하는 모양이었다(`BattleBridge.Relocation.cs:22`).

계약 보존: `from == to` 검사가 공간 판정보다 **선행**해야 한다 — `from` 이 아직 점유 집합에 있어서
순서를 바꾸면 제자리 재배치가 `Occupied` 로 오판된다. 재배치는 배치와 달리 **쿨타임·코스트를 보지
않는다**(같은 유닛을 옮기는 것이라 새 배치가 아니다) — 그래서 `Check` 가 아니라 별도 함수다.

#### ✅ 15-C-2 완료: 시너지 판정 적출 + `visualMaterial` 계층 정리

**시너지** — 신규 `MatchSynergyRules`(`Sim/Match/`, **엔진 무참조** — `System` 만 쓴다). 규칙이 받는
것은 **타입 키의 5×5 창**(`int`, 0 = 활성 디펜더 없음)이고 돌려주는 것은 블록 9칸의 **이웃 수**다.
Bridge 에 남은 것은 규칙이 알 수 없는 것뿐이다: ① 어느 칸이 "활성" 인가(`Exists` +
`PendingDeployment` = ECS 조회) ② 배율을 어느 채널로 흘리는가 ③ 활성화 카운터 진단.

- **배율이 아니라 이웃 수를 내보낸다.** 이웃 0 과 미점유는 다른 사건인데 배율로는 둘 다 1.0 이라
  구분이 사라진다 — 전자만 항등값 refresh 를 받아야 한다(모디파이어 슬롯이 남지 않게 하는 계약).
- **종류 동일성**의 정본은 SO 참조 동일성(`n.data == here.data`)이었고, 인스턴스 ID 가 그것과 1:1
  이라 판정을 바꾸지 않으면서 규칙이 엔진 타입을 모르게 된다.
- **블록 순회 순서가 계약**이다 — 그 순서가 곧 채널 enqueue 순서이고 골든의 `StatModifierSlot`
  라인에 실린다. 적출 전 배열 리터럴 순서를 그대로 옮겼다.
- 창이 5×5 인 이유: 블록이 3×3 이고 그 각각이 다시 8 이웃을 세므로 실제로 읽히는 범위가 ±2 다.
- 증인은 `MatchSynergyRulesTests` 10건. **골든은 이 규칙을 증인하지 못한다** — 하네스는 유닛
  타입마다 1회만 배치해서 같은 종류가 인접하는 배치를 만들지 않는다.

**`visualMaterial`** — `unitValid` 는 이제 "유닛 정의가 있는가" 뿐이다. 걷어낼 수 있었던 근거는
문서의 전제("지금 빼면 렌더에서 터진다")가 **실측과 달랐다**는 것이다: 디펜더 뷰 2경로가 이미
`ResolveUnitMaterial` 로 null 을 런타임 폴백 머티리얼로 바꾸고 있었다. 실제로 무방비였던 곳은
배치 펄스 연출 하나뿐이라(`sharedMaterial = null`) 같은 폴백을 태웠다. 저작 실수는 신설
`ResolveFallbackViewMaterial` 이 경고로 잡는다 — **Spine 이 뜬 디펜더는 이 경로에 오지 않으므로**
배치 시점이 아니라 폴백 렌더가 실제로 필요해진 지점에서 묻는다(적 유닛 경로와 같은 처분).

#### ⏸ `ApplyOnPlaceEffect` 는 unit 18 로 이관한다

이 문서가 시너지와 한 묶음으로 적었지만 성격이 다르다. 8분기를 전수로 읽은 결과 **분리 가능한
판정이 사실상 없다**:

- 각 분기의 "판정"은 `magnitude <= 0` · `duration <= 0` · `range <= 0` · `queue.IsCreated` 같은
  **한 줄짜리 가드**이고 호출처가 각각 1곳뿐이다 → 지금 빼면 제약 10 이 명시한 과잉 추상화다.
- 실체는 **페이로드 구성**인데 그 타입이 전부 ECS 소유다(`StackModifierApplyEvent` · `EnemyCcEvent` ·
  `DotEffect` · `IncomingDamage`) 또는 MonoBehaviour 런타임 호출이다(`CostRuntime.AddCost` ·
  `SkillRuntime.ReduceAllCooldowns`). 이 타입들이 plain struct 가 되는 시점이 **unit 18** 이므로,
  지금 규칙을 세우면 그때 통째로 다시 쓴다.
- 대상 선정(`CollectEnemiesInTileRange`)의 순수 부분은 `GridMath` 이고 그건 **unit 17** 이 가져간다.

⇒ 동결 구간에서 이 함수로부터 얻을 수 있는 것은 없다. unit 18 의 Effects 이식과 같은 커밋에서 옮긴다.

**코퍼스 동결 해제(unit 18 스왑) 후에** 할 것:

- **통화 상태의 sim 이관** — `CostRuntime`·`PlacementCooldownRuntime` 은 아직 MonoBehaviour 이고
  self-tick 한다. 읽기면은 unit 13-A3 이 이미 세션으로 옮겨 놨으므로 남은 일은 **상태와 tick 의
  이사**다. tick 순서가 바뀌면 `cost` 상태 라인이 흔들린다.
- **`PlacementInput` 의 `TrySpend` 제거** + `PlaceDefenderAs` 은퇴 + 하네스를 배치 경로로 전환.
- **활성화 지연을 sim 시퀀스로**(`activationTick`/`landTick`) — 현재 뷰 코루틴이 sim 전이 시각을
  소유한다. 행동 변화가 큰 조각이라 골든이 강한 증인이 된다.
- ⚠ **슬로모-통화 결합**은 이 unit 에서 **그대로 옮긴다**(행동 보존). 처분은 unit 19.
