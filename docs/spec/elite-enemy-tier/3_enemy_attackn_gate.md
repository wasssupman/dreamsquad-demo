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

**`OnDeath` 도 열지 않는다** — unit 5 가 리뷰 H2 로 재설계되면서 분열이 슬롯을 전혀 쓰지 않게
됐다(브리지 킬 드레인이 죽은 적의 SO 를 직독한다). 이 spec 이 화이트리스트에 더하는 것은
**`AttackN` 단 하나**다.

### ② RESOLVE arm 의 진영 게이트

현행은 `defenderTagLookup.HasComponent(attackerEntity)` 로 방어유닛만 통과시킨다. 이 술어를
**제거**하고 `dcSlotLookup.HasBuffer(attackerEntity)` 만 남긴다 — 슬롯이 붙은 것 자체가 «이
유닛은 트리거를 갖는다» 는 선언이고, 적에게 슬롯을 붙이는 유일한 경로는 `BakeNightmareMechanics`
(= 저작된 메커니즘)이다. 진영별 분기를 새로 만들지 않는 것이 요점이다.

⚠⚠ **변경 지점은 dc-트리거 RESOLVE 외곽 가드 «1곳» 이다** (현재 ≈`AttackSystem.cs:1710`).
같은 파일에 `defenderTagLookup` 분기가 **총 8곳**(≈471 · 484 · 817 · 929 · 1075 · 1121 · 1471 ·
1710) 있고 나머지 7곳은 방어유닛 전용이어야 하는 기능이다 — 힐 대상 랭킹 · 끝을 보는 눈
(frontmost) · 포커스 락 · **HeavyStrike pre-scan** · ProjectileBounce · 위협 귀속 · 공격 시작
타이밍. **하나도 건드리지 않는다.** (리뷰 M1 — 초판 문구가 «이 술어를 제거» 라고만 써서
8곳 중 어디인지 지시하지 않았다.)

**하지만 페이로드 arm 은 진영을 안다.** 발사되는 캐리어의 대상 진영을 시전자에서 도출해야 한다:

- 기존 `ProjectileToTarget`(니들) arm 은 방어유닛 전제로 대상 진영이 잡혀 있다. 적이 이 페이로드를
  쓰면 **자기 진영을 쏜다.** 그래서 이 단위는 **`ProjectileToTarget` 을 적에게 열지 않는다** —
  `AttackN` 이 발동했는데 arm 이 없는 조합은 현행 규율대로 **loud warning + 카운트 소비**로 둔다.
  적이 실제로 쓰는 페이로드는 unit 4 의 `AreaBreath` 뿐이다.
- ⚠ **런타임 가드를 함께 넣는다**(리뷰 잔여위험 2). 지금 방어선은 bake 시점 경고뿐이라, 누가 적 SO
  에 `AttackN × ProjectileToTarget` 을 저작하면 `SpawnNeedleCarrier` 가 그대로 발사된다.
  arm 진입부에 «적 host + `ProjectileToTarget` → 경고 후 continue» 한 줄을 둔다.
- 즉 이 단위의 산출물은 «적의 `AttackN` 이 카운트를 세고 발동 신호를 낸다» 까지다. 발동해서
  무엇을 하는가는 unit 4 가 붙인다.

### ③ 무회귀의 실체

- `bestTarget != Entity.Null` · `!castCountedHosts.Contains(...)` · 게이트 술어(`DcGateKind`)
  평가 순서를 **바꾸지 않는다.** 순서가 «게이트 통과 사건만 카운트» 라는 기존 계약이다.
- ⚠⚠ **`DcTriggerFiredEvent` enqueue 는 방어유닛 게이트를 유지한다.** 초판은 «손패 UI 가 host 로
  필터하므로 적 host 는 무해하다» 고 적었는데 **확인해보니 거짓이다**(리뷰 H3).
  `DrainDcTriggerFiredEvents`(≈`BattleBridge.cs:3400`)는 `PulseCards`(카드 없으면 no-op) 다음에
  `spineUnitPool.TryGet(evt.host, …)` → `view.PlayPunch(); view.FlashWhite(); SpawnCardAbsorbVfx(…)`
  를 부르고, **적도 같은 `spineUnitPool` 에 등록돼 있다**(`SpawnUnit`). 그대로 열면 드래곤이 3타마다
  흰색 플래시 + «카드 흡수» VFX 를 낸다 — 이 큐의 생산자는 `AttackSystem` 3곳뿐이고 보스의
  PeriodicTimer/HealthThreshold arm 은 이 큐를 안 쓰므로 **이 아티팩트는 unit 3 이 처음 만든다.**
  해법: 외곽 가드(≈1710)에서는 술어를 빼되 **enqueue 지점(≈1734) 앞에 `defenderTagLookup` 을
  남긴다.** «8곳 중 1곳만» 원칙과 충돌하지 않는다 — arm 은 열고 연출 신호만 방어유닛에 한정한다.

## 완료 기준

- [ ] compile 통과
- [ ] EditMode: `EnemyTriggerArmed(AttackN) == true` · `OnShieldBreak == false` ·
      **`OnDeath == false`**(이 spec 은 끝까지 열지 않는다 — unit 5 ②)
- [ ] EditMode 전체 — 신규 실패 0
- [ ] **PlayMode 무회귀 대조** — baseline 대비 pass/fail 집합 동일. 방어유닛 트리거 카드
      (`AttackN` 계열: 응축된 일격 · 니들 · 온-히트 CC/스택)가 전부 그대로 발동한다
- [ ] 적 SO 에 `AttackN × (arm 없는 페이로드)` 를 저작하면 **loud warning 이 뜨고 조용히 넘어가지
      않는다** — 침묵 no-op 배제가 이 단위의 규율
- [ ] **Play: 적이 `AttackN` 을 발동해도 흰색 플래시·«카드 흡수» VFX 가 뜨지 않는다**(H3 게이트).
      방어유닛 카드 발동 시에는 **여전히 뜬다**
- [ ] 커밋에 콘텐츠 에셋이 **한 개도** 포함되지 않았다 (단독 커밋)
