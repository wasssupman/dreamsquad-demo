# 23 — 원점이 유닛이면 «그 유닛의 몸»을 쓴다 (unit 22 전수 확인의 누락분)

> 사용자 지시 2026-09-06: **「배치 외 모든 전투 판정은 같은 공식을 지난다」는 이 메커니즘의
> 핵심 명제다.** unit 22 가 「전수 확인 완료」로 닫았는데, 자기중심 광역 전체가 빠져 있었다.

## 명제 (재확인, 새 결정 아님)

```
도달 = |좌표 차| ≤ 범위 + «원점의 몸» + «대상의 몸»
```

**「원점의 몸」은 원점이 무엇이냐가 정한다** — 유닛이면 그 유닛의 `HitRadius`(= 가로/2),
칸이면 그 칸의 반폭(`CellHalfWidthTiles` = 0.5). 이건 예외 둘이 아니라 **한 규칙의 두 경우**다.
오늘 결함은 **원점이 유닛인데 칸의 몸을 쓰는** 자리들이다.

**적용 범위 = 전투에서 「닿나」를 묻는 전부** — 일반 공격 · **배치 스킬** · **액티브 스킬** ·
**드림캐쳐의 각종 효과** · 오라 · 장판 · 투사체 착탄 · 어그로/도발 · 감지, 그리고 **앞으로
추가되는 모든 전투 기능**. 예외는 판 위 **배치(placement) 판정 하나뿐**(격자 점유라 성격이 다름).
→ 2026-09-06 `CLAUDE.md` **절대 제약 13** 으로 승격됐다. 이 unit 은 그 제약의 **이행분**이다.

## 왜 unit 22 가 못 봤나 (재발 방지의 근거)

unit 22 는 `CellHalfWidthTiles` **직접 참조**를 세었고, 그 목록은 실제로 전부 칸 원점이 맞다.
빠진 것은 **함수 뒤에 숨은 간접 경로**다:

```
자기중심 광역 9종 → ctx.Opponents(…, RangeMetric.AreaCircle)
                  → SkillMath.TryShapeHalfWidth(AreaCircle) → CellHalfWidthTiles
```

호출부에 `0.5` 리터럴도 `CellHalfWidthTiles` 심볼도 **없다.** unit 22 의 일반화
(「흔적은 `0.5` 리터럴뿐이라 grep 이 못 잡는다」)가 자기 자신에게 적용된 사례다.

그리고 오도한 것은 주석이다 — `SkillMath` 의 *「광역: **폭발은 점이고** 후보가 칸이라…」*.
칸 조준 광역엔 참이지만 **자기중심 광역은 폭심이 몸 있는 유닛**이다. 둘이 `AreaCircle`
**한 metric 을 공유**해서, 한쪽에만 맞는 문장이 양쪽에 적용됐다.

## 전수 인벤토리 (판정 술어 호출 12곳 — 이번엔 술어 본체 기준으로 셌다)

| 분류 | 자리 | 원점 | 내 몸 항 |
|---|---|---|---|
| ✅ 정본 | `AttackReach`(발사·피해) · `HazardCastSystem` · `PatternScope` · `AreaSleepSkill`(근접 제외) · `AggroTargeting`(unit 22) | 유닛 | `HitRadius` |
| ✅ 정당 | `ProjectileHitSystem`(착탄) · `BounceRetarget`(탄 위치) · `MovementSystem`(회오리 `centerCell`) · `AllyBuffFieldSystem`(칸 조준 카드가 만든 장판) · `ZoneApplySystem`(해저드 존) | **칸** | 칸 반폭 |
| ❌ **결함** | **`EcsSkillContext:468`** — 자기중심 광역 **9종**(도발·CC·DoT·스택·수면·브레스·실드부여·오라 즉시적용 2) | **유닛** | 칸 반폭 |
| ❌ **결함** | **`BattleBridge.CollectShieldBreakTargets`** — 실드 파열 자기중심 폭발 | **유닛** | 칸 반폭 + **대상도 칸으로 접힘**(대상 몸도 안 셈) |

투사체 스윕(`SweepHitMath`)은 탄이 점이고 대상 몸을 더한다 — 정당.
`AttackSystem:2155` · `HazardCastSystem:135` 의 raw 거리는 **랭킹**이지 도달 판정이 아니다.

배스티온(몸 1.5) 기준 오차: 도발 반경 **2.75 → 3.75**(1.0칸). 오차 = `(가로 − 1) / 2`.

## 구현

1. **`SkillMath` 진입점을 둘로 가른다** — `ReachFromUnit(…, selfBodyRadius, …)` /
   `ReachFromCell(…)`. `InBodyReach` 직접 호출은 **금지**하고 두 진입점만 남긴다.
   호출부가 **원점이 무엇인지 선언하게** 만드는 것이 요점이다 — 상수를 손으로 넘길 수 있는 한
   같은 누락이 반복된다(unit 22 가 그렇게 살아남았다).
2. **`RangeMetric.SelfArea` 신설** — 자기중심 광역 9종이 이걸 쓰고, `EcsSkillContext.Collect`
   가 시전자 `HitRadius` 를 넣는다. `AreaCircle` 은 **칸 조준 전용**으로 좁아진다
   (`TileStatBurstSkill` 하나). 페이크(`TestSkillContext`)도 같은 분기를 갖는다.
3. **실드 파열** — `TileAoe.IsInRadius`(칸-칸) 대신 `ReachFromUnit`(폭심 몸 + 대상 몸).
4. **표기 동기** — `DcRangeCatalog` · `TilemapMapView` · `BattleBridge` 의 링/하이라이트가
   같은 자를 복사한다. 화면과 판정이 갈리면 unit 5 의 「규칙을 틀리게 가르친다」가 재발한다.

## 완료 기준

- [ ] compile · EditMode 전량 초록(선행 문안 2건 제외).
- [ ] **차등 단언**(unit 22 형태 재사용): 같은 스킬·같은 자리에서 **시전자 footprint 만 키우면
      대상 집합이 넓어진다**. 1×1 픽스처만 있으면 결함이 숨는다 — unit 22 가 그렇게 당했다.
- [ ] **금지 가드**: `InBodyReach` 직접 호출 0건(진입점 2개 외), 테스트로 고정.
- [ ] 배스티온 도발 반경 실측 3.75 · 실드 파열이 몸 큰 시전자에서 넓어짐.
- [ ] **골든 A/B 분리 측정**(unit 22 방식) — 자기중심 광역이 전부 넓어지므로 총계가 움직인다.
      움직임의 **방향과 크기를 미리 적고** 그 밖이면 원인을 찾는다.
- [ ] Play 육안: 배스티온 도발이 옆구리 적을 실제로 끌어오는가.
