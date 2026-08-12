# 3 — 적에게 `AttackN` 트리거 개방 (단독 커밋)

## 목적

«N번째 공격마다» 사건을 적도 쓸 수 있게 한다. 드래곤의 3타 브레스가 이것 없이는 성립하지 않는다.

**이 spec 에서 가장 위험한 변경이다.** 방어유닛 카드 전체가 같은 arm 코드를 타므로 되돌릴 때
콘텐츠와 함께 딸려가면 안 된다 — **단독 커밋**으로 뺀다(`enemy-fire-stack-shooter` unit 0 선례).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — `EnemyTriggerArmed` 에 `AttackN` 추가
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE 의 arm 게이트
  (`[Defender only]` 블록, `defenderTagLookup.HasComponent(attackerEntity)` 술어)
- EditMode: `EnemyTriggerArmed` 를 고정하는 기존 테스트 갱신

## 구현

### ① 화이트리스트

```csharp
public static bool EnemyTriggerArmed(DcTriggerKind kind)
    => kind == DcTriggerKind.PeriodicTimer
    || kind == DcTriggerKind.HealthThreshold
    || kind == DcTriggerKind.AttackN;      // ← 이 단위
```

⚠ **`AttackN` 하나만 넣는다.** 이 술어의 주석이 경고하는 위험은 `OnShieldBreak` 다 — 적 전원이
`ShieldSlot` 을 갖게 된 뒤로 파열 감지가 적에서도 참이 되었고, 그것을 열면 브리지의 파열 드레인
(`CollectShieldBreakTargets` — 대상 풀이 `AttackUnitTag` 하드코딩)이 돌아 **보스의 파열 폭발이
자기 진영을 때린다.** 화이트리스트는 kind 단위이므로 `AttackN` 추가가 그 문을 열지 않는다.
`OnDeath` 는 unit 5 가 별도로 연다.

### ② RESOLVE arm 의 진영 게이트

현행은 `defenderTagLookup.HasComponent(attackerEntity)` 로 방어유닛만 통과시킨다. 이 술어를
**제거**하고 `dcSlotLookup.HasBuffer(attackerEntity)` 만 남긴다 — 슬롯이 붙은 것 자체가 «이
유닛은 트리거를 갖는다» 는 선언이고, 적에게 슬롯을 붙이는 유일한 경로는 `BakeNightmareMechanics`
(= 저작된 메커니즘)이다. 진영별 분기를 새로 만들지 않는 것이 요점이다.

**하지만 페이로드 arm 은 진영을 안다.** 발사되는 캐리어의 대상 진영을 시전자에서 도출해야 한다:

- 기존 `ProjectileToTarget`(니들) arm 은 방어유닛 전제로 대상 진영이 잡혀 있다. 적이 이 페이로드를
  쓰면 **자기 진영을 쏜다.** 그래서 이 단위는 **`ProjectileToTarget` 을 적에게 열지 않는다** —
  `AttackN` 이 발동했는데 arm 이 없는 조합은 현행 규율대로 **loud warning + 카운트 소비**로 둔다.
  적이 실제로 쓰는 페이로드는 unit 4 의 `AreaBreath` 뿐이다.
- 즉 이 단위의 산출물은 «적의 `AttackN` 이 카운트를 세고 발동 신호를 낸다» 까지다. 발동해서
  무엇을 하는가는 unit 4 가 붙인다.

### ③ 무회귀의 실체

- `bestTarget != Entity.Null` · `!castCountedHosts.Contains(...)` · 게이트 술어(`DcGateKind`)
  평가 순서를 **바꾸지 않는다.** 순서가 «게이트 통과 사건만 카운트» 라는 기존 계약이다.
- `DcTriggerFiredEvent` 는 그대로 발행한다 — 손패 UI 는 host 로 필터하므로 적 host 는 무해하다.
  (실제로 무해한지 드레인 쪽에서 확인할 것)

## 완료 기준

- [ ] compile 통과
- [ ] EditMode: `EnemyTriggerArmed(AttackN) == true`, `EnemyTriggerArmed(OnShieldBreak) == false`,
      `OnDeath == false`(unit 5 전까지)
- [ ] EditMode 전체 — 신규 실패 0
- [ ] **PlayMode 무회귀 대조** — baseline 대비 pass/fail 집합 동일. 방어유닛 트리거 카드
      (`AttackN` 계열: 응축된 일격 · 니들 · 온-히트 CC/스택)가 전부 그대로 발동한다
- [ ] 적 SO 에 `AttackN × (arm 없는 페이로드)` 를 저작하면 **loud warning 이 뜨고 조용히 넘어가지
      않는다** — 침묵 no-op 배제가 이 단위의 규율
- [ ] 커밋에 콘텐츠 에셋이 **한 개도** 포함되지 않았다 (단독 커밋)
