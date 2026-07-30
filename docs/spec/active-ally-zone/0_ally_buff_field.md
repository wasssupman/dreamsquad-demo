# 0 — 아군 버프 장판 캐리어 + 멤버십 갱신(Effects ISystem)

## 목적

시전 순간 유닛에 붙는 버프를, **수명을 가진 영역**으로 바꾼다. 영역 안에 있는 동안만 강화되고
이탈·만료는 자연 소멸로 처리한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/AllyBuffField.cs` (신규 — 캐리어)
- `Assets/_Project/Scripts/Battle/Effects/AllyBuffFieldSystem.cs` (신규 — 멤버십 갱신)
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` (스폰 헬퍼 + `AllyBuffApplySec`)
- `Assets/_Project/Scripts/Battle/Effects/EffectTickSystem.cs` (수명 감소·만료 파괴 루프 추가)

## 구현

1. **캐리어** (`TornadoField` 형태 — unmanaged, Effects 소유):
   ```
   AllyBuffField : IComponentData
       int2     centerCell  // 셀로 든다 — 멤버십 상대가 DefenderTile.cell(int2)
       int      tileRange   // GridMath.RangeToTiles(skill.range)
       StatKind stat        // DamageMul | AttackSpeedMul
       float    magnitude   // ×2.0 (배율 그대로; op 분류는 ModifierAuthoring)
       float    remaining   // 남은 수명 = skill.durationSec 에서 시작
   ```
   `EffectSpawner.SpawnAllyBuffField(...)` 로 생성(`SpawnTornadoField` 선례).

2. **수명 감소 + 만료 파괴 = `EffectTickSystem`**(확정, 둘 중 하나를 여기서 고른다).
   그 시스템이 "Effects 맥락 모든 컴포넌트의 수명 소유자" 이고 `TornadoField`/`PortalLink` 가 이미
   `remaining -= dt` → 0 이면 `ecb.DestroyEntity(entity)` 형태다. `RequireAnyForUpdate` 에
   `AllyBuffField` 를 추가하고 같은 루프를 하나 더 얹는다(≈8줄). **신규 tick 시스템 금지.**

3. **멤버십 갱신 = `AllyBuffFieldSystem`**(Effects, `[UpdateInGroup(typeof(BattleSimGroup))]`,
   `[UpdateBefore(typeof(ModifierApplySystem))]`). 살아 있는 `AllyBuffField` × 배치 완료 방어유닛
   (`DefenderTile` + `WithNone<PendingDeployment>`)을 체비셰프로 맞춰 매 프레임
   `StatModifierApplyEvent` 를 재발행한다.
   - **선례를 그대로 따른다**: `ZoneApplySystem`(`Battle/Effects/ZoneApplySystem.cs`)이 해저드 장판
     안 적에게 매 프레임 `restDuration` 짜리 `StatModifierApplyEvent` 를 재발행하는 것과 같은 형태.
     새로운 발명이 아니라 이 레포의 존 관용구다.
   - `op`/`magnitude` 는 `ModifierAuthoring.FromMultiplier`(순수 static)로 뽑고,
     `stackId = 3`(선행 spec 의 `SkillAllyBuffStackId` 와 동일 값 — 오라와 합산), `source = target`,
     `origin = ModifierOrigin.Skill`.
   - **duration 은 항상 `EffectSpawner.AllyBuffApplySec`**(계약 3-1). public const 로 두어 테스트가
     읽는다 — 프레임워크 지연 상한이며 밸런스 값이 아니다(`MulStatFloor`/`MulStatCeil` 선례).
   - 매 프레임 갱신이 repo norm 이다(`ZoneApplySystem` 이 적에게 그렇게 한다). cadence 누산기를
     두지 않는다 — 두면 정지/슬로모에서 어느 시계를 쓰느냐는 결정이 새로 생긴다.

4. **bridge 에 tick 을 두지 않는다.** `docs/TRD.md:302` 가 "MonoBehaviour 에 전투 로직 직접 작성"을
   금지하고, `BattleScaledRateManager` 는 정지(scale ≤ 0)에서 `BattleSimGroup` 을 **통째로 skip**
   한다 — bridge 에 두면 정지 중 `_statModifierQueue` 가 드레인 없이 쌓였다가 재개 프레임에 한꺼번에
   터진다. ECS 에 두면 정지·슬로모·순서·멤버십 권위가 전부 공짜다.
   bridge 가 하는 일은 **스폰 호출 + 로그용 스냅샷 카운트** 둘뿐이다.

5. **왜 revoke 가 아니라 갱신인가**: 정확한 이탈 회수는 `stackId` 제거 프리미티브 또는 항등원
   중립화 트릭이 필요하다. 갱신은 위 선례가 이미 쓰는 형태이고 이탈·만료·사망이 모두 같은 경로로
   자연 소멸한다. 소멸 지연 상한은 `AllyBuffApplySec`.

6. **멤버십 권위는 ECS**(`DefenderTile` + `PendingDeployment`), Mono `_defenderByTile` 이 아니다.
   `CollectAlliesInRange` 는 unit 1 이후 **로그용 스냅샷 카운트 전용**으로만 남는다.

## 완료 기준

- [ ] 캐스트 **직후 프레임**에 버프가 걸린다(지연 0 — 시스템이 매 프레임 도므로 자동).
- [ ] 슬롯 `remaining` 이 어떤 시점에도 `AllyBuffApplySec` 를 넘지 않는다.
- [ ] 만료 시 ≤ `AllyBuffApplySec` 안에 풀리고 캐리어 엔티티가 파괴된다.
- [ ] 장판 수명 중 그 안으로 **새로 배치**된 유닛도 강화된다.
- [ ] 재배치로 장판을 벗어나면 ≤ `AllyBuffApplySec` 안에 풀린다.
- [ ] 정지(pause) 중 큐가 쌓이지 않는다(그룹이 skip 되므로 구조적 — 회귀 확인만).
- [ ] 신규 NativeQueue 0, 신규 맥락 0, Burst 유지. 콘솔 에러/워닝 0.

> **재배치는 장판 안↔안이라도 버프가 끊긴다**: `TryBeginDefenderRelocation` 이 확정 프레임에
> `PendingDeployment` 를 붙이므로 비행 시간 + ≤`AllyBuffApplySec` 동안 멤버십에서 빠진다.
> on-place 오라 규칙과 같은 결이라 의도로 받아들인다(emergent 아님 — 여기 명시).
>
> **`AllyBuffApplySec` 의 하한**: Unity 의 `Maximum Allowed Timestep`(이 프로젝트 0.3333)보다 커야
> 한다. 작으면 히칭 프레임 한 번에 `StatModifierTickSystem` 이 갱신값을 넘어서 깎아 슬롯이 사라지고
> 그 프레임만 base 스탯이 된다. 정지·슬로모는 오히려 안전(델타가 작아진다) — 위험 구간은 정상 속도다.

> 순서 주의(L1): `EffectTickSystem` 은 `ModifierApplySystem` 과 명시 순서가 없어, 이번 프레임에
> 파괴되는 장판이 같은 프레임에 한 번 더 갱신될 수 있다. 수용된 `AllyBuffApplySec` 지연 안이라
> 무해하다 — 여기에 `[UpdateAfter]` 를 얹지 말 것.

> 확인 2026-07-30 — 커밋 `2b8b3efd` · 사용자 Play 육안 확인 완료.
