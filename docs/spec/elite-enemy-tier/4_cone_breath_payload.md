# 4 — `Cone` 도형 + `AreaBreath` 페이로드 (화염 브레스)

## 목적

드래곤의 3타 브레스를 성립시킨다. `AttackN(3)` 이 발동하면 **대상 방향 부채꼴** 안의 후보 전원에게
즉발 피해를 준다. `EffectArea` 의 두 번째 소비자가 되어 계약 7(첫 커밋부터 소비자 2개)을 닫는다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/EffectAreaMath.cs` — `Cone` 분기 (unit 1 에서 이미 구현·
  테스트됨. 여기서는 소비만)
- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.AreaBreath` +
  `coneHalfAngleDeg` 필드 (**둘 다 append-only**)
- `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs` — 반각 필드 1개
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 분기
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — arm (콘 적용)

## 구현

### 페이로드 저작 축

```
DcPayloadKind.AreaBreath = 20        // append-only (직전 = GrantShield = 19)
```

필드 재사용 규약을 따른다: `magnitude` = 피해 · `tileRange` = 사거리(타일) · **신규
`coneHalfAngleDeg`** = 반각(도).

반각을 `duration` 에 겸직시키지 않는 이유: `slamDamage` 선례와 같다 — 도형 파라미터는 이름으로
grep 돼야 하고, `duration` 은 «시간» 이라는 의미를 이미 갖고 있어 겸직하면 «시간인 줄 알고» 읽는
코드가 생긴다. 필드 1개 추가는 append-only 라 기존 카드 전부 무손상(0 = 반각 0 = 직선 1칸,
저작 누락이 조용히 전방위가 되지 않는다).

### 적용 지점 = `AttackSystem` arm (신규 시스템 0)

**투사체 캐리어를 만들지 않는다.** 브레스는 즉발이고(계약 9), `AttackSystem` 은 그 프레임에
이미 후보 배열(`targetEntities` · `targetTransforms` · `targetFactions` · `targetTraversalLayers`)을
손에 들고 있다 — 시전자의 타겟 마스크로 걸러진 **진영 대칭 풀**이다. 그 자리에서 순회한다:

```
발동 시:
  dir     = normalize((bestTargetPos - attackerPos).xz)      // 대상 방향
  origin  = WorldToCell(attackerPos)
  area    = { Cone, tileRange, dir, coneHalfAngleDeg }
  for 후보 i:
      if (!EffectAreaMath.Contains(area, origin, WorldToCell(targetTransforms[i]))) continue
      ecb.AppendToBuffer(targetEntities[i], new IncomingDamage { amount = magnitude, source = attacker })
```

- **대상 진영은 후보 배열이 이미 정한다** — 시전자의 마스크로 만들어진 풀이므로 아군 오사가
  구조적으로 불가능하다. 진영 파라미터를 새로 넣지 않는다.
- **`bestTarget` 이 없으면 발동하지 않는다.** 방향을 만들 수 없다. 카운트는 이미 소비된 상태로
  둔다(기존 계약 5 «반경 안에 적이 없어도 카운트는 소비» 와 동형).
- **`AoeTargetCap` 을 쓰지 않는다.** 부채꼴에 든 전원이 맞는 것이 이 능력의 요점이다(cap 0 =
  무제한과 동치이므로 호출 자체를 생략).
- **위협(`ThreatHitEvent`) 귀속을 하지 않는다.** 위협 테이블은 보스 전용 부속물이고 엘리트는
  갖지 않는다(unit 0 계약).
- **`Air` 층 교집합 게이트를 지킨다.** 적 공격의 대상층 필터는 후보 배열 생성 시 이미 적용되므로
  추가 판정이 없어야 한다 — 있으면 이중 필터다. 확인만 한다.

### 연출

피해와 같은 프레임에 VFX 를 1회 스폰한다. 브레스는 기존 어느 큐에도 맞지 않으므로
(`UnitAttackVisualEvents` 는 공격 1회 = 히트 1점 전제) **`VfxSpawner` 직접 호출 계열**에 붙인다 —
`object-pipeline-map` 의 «VFX(one-shot)» 아키타입 중 «BattleBridge 직접 호출» 분기다.
프리팹·스케일·수명은 unit 7 이 저작한다.

## 완료 기준

- [ ] compile 통과
- [ ] EditMode: `Cone` 판정 단언(unit 1) 이 그대로 통과
- [ ] EditMode 신규: bake 가 `coneHalfAngleDeg <= 0` 저작을 **loud warning** 한다
- [ ] PlayMode 신규 e2e: 드래곤 1기 + 부채꼴 안 방어유닛 2기 + **부채꼴 밖(옆·뒤)** 방어유닛 2기 →
      3번째 공격에서 **안쪽 2기만** HP 가 줄고 밖의 2기는 무피해
- [ ] PlayMode: 대상이 없는 프레임에 발동해도 예외·오사가 없다
- [ ] PlayMode 무회귀 — baseline 대비 실패 집합 동일
- [ ] 신규 ECS 시스템 0 · 신규 이벤트 채널 0 · 신규 컴포넌트 0 (슬롯 필드 1개만)
