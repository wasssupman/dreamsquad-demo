# 23 — 원점의 몸 통일 (unit 22 「전수 확인」의 누락분 전부)

> 사용자 지시 2026-09-06: **「배치 외 모든 전투 판정은 같은 공식을 지난다」는 이 메커니즘의
> 핵심 명제다.** unit 22 가 「전수 확인 완료」로 닫았는데 누락이 있었고, 이 unit 의 **초판
> 인벤토리(12곳·결함 2건)도 또 부실했다.** 병렬 독립 감사 3건으로 다시 세운 것이 아래다.

## 명제 (`CLAUDE.md` 절대 제약 13 — 새 결정 아님)

```
도달 = |좌표 차| ≤ 범위 + «원점의 몸» + «대상의 몸»
```

- **원점 = 그 판정을 «트리거한 대상» 자신.** 유닛이면 `HitRadius`(방어유닛 = 가로/2,
  적 = 티어 파생/저작), 진짜 칸이면 칸 반폭 0.5.
- **시체·착탄점도 몸이 있다**(사용자 결정: *「있다. 모든건 트리거된 대상으로부터」*).
  대상이 이미 파괴됐으면 **발화 시점 스냅샷**으로 싣는다.
- **intent·투사체를 경유해도 원점은 안 바뀐다.**

## 왜 두 번이나 놓쳤나 (방법론이 결함이었다)

| 조사 | 방법 | 그 방법이 원리적으로 못 보는 것 |
|---|---|---|
| unit 22 | `CellHalfWidthTiles` **직접 참조** grep | 함수 뒤(`TryShapeHalfWidth`)에 숨은 간접 경로 |
| unit 23 초판 | **술어 본체 호출** 전수 | ⑴ 술어를 **안 부르는** 경로(intent 발신) ⑵ **인라인 셀 비교**(체비셰프) |

**intent 경계**가 특히 악질이다 — `SelfAreaBlast`·`DeathSiteBlast`·실드 파열은 `InBodyReach`
호출이 **0회**라 술어 조사에 후보로 등장조차 안 한다. 판정이 실제로 벌어지는
`ProjectileHitSystem:747` 은 **자기 문맥에서는 옳다**(「어떤 착탄점」). 틀린 것은 그 문맥을
만들어 보낸 쪽이고, 유일한 흔적은 `flightTime = 0f` 하나다.

> **일반화(제약 13 에 반영됨)**: 판정 지점의 국소 문맥만 보는 감사는 **원점이 무엇이었는지**를
> 복원할 수 없다. 원점 정보가 경계를 넘어 실려야 하고, 안 실리면 감사도 못 한다.

## 결함 인벤토리 (독립 감사 2건 교차 확인)

| # | 자리 | 무슨 판정 | 원점 | 오늘 | 확인 |
|---|---|---|---|---|---|
| **A** | `Battle/Skills/EcsSkillContext.cs:458·468` | **자기중심 광역 9자리 / 8파일** — 도발·CC·DoT·스택·수면·브레스·실드부여·오라 2 | 시전자 유닛 | 칸 반폭 0.5 | 감사 2건 일치 |
| **B** | `SelfAreaBlastSkill` · `DeathSiteBlastSkill` · 실드 파열(`BattleBridge:4568-4598`) → `ProjectileHitSystem:747` | **자기중심 폭발 3종**(진동갑주 자폭 · 사망폭발 · 퇴근 운석 · 실드 파열) | 트리거 대상 유닛 | 칸 반폭 0.5 (intent 경계 뒤) | critic |
| **C** | `Battle/Combat/AttackSystem.cs:331` · `:264` · `:403` (`PickFallbackTarget`) | **폭탄맨·아틸러리 «일반 공격» 대상 선정** + 해저드/폭탄 구동 DC 니들 | 공격자 유닛 | **체비셰프 셀 ≤ ceil(range)** — 양쪽 몸 0, 도형이 **사각** | audit |
| **D** | `Skills/SkillCone.cs:38` ← `ConeBreathSkill:38·58` | 화염 브레스 **부채꼴 거리 게이트** | 시전자 유닛 | `TileRange × TileSize` — 양쪽 몸 0 | audit |
| **E** | `Battle/Movement/MovementSystem.cs:317` | 포탈 입구 진입 | 칸(정당) | **대상 몸 누락** | audit |
| **F** | `Bridge/BattleBridge.cs:5970` `TryPickNearestEnemy` | 드롭 지점 최근접 적(살찌운 제물) | 탭 지점 | **대상 몸 누락** | audit |

**초판의 오류 정정**: `CollectShieldBreakTargets` 는 **`if (log != null)` 안에서만 도는 로그 전용**이다.
피해는 `SpawnProjectile` 이 낸다. **그 함수만 고치면 트레이스가 실제 피해보다 넓은 집합을
기록한다** — 「로깅은 첫 축」에 정면으로 걸린다. 고칠 대상은 **투사체 스폰 경로**(결함 B).

**미해결 쟁점 1건**: `EmitPatternSkill:86`(`Euclidean` → 내 몸 0). audit 은 결함,
코드 주석(`SkillMath.cs:91`)과 critic 은 「탄 비행 거리라 정당」. **「발사 명세의 조준 후보 수집」이
도달 판정인가 비행 거리인가**를 정해야 한다.

## 오차의 부호는 호스트마다 뒤집힌다 (상수 하나로 못 고친다)

| 호스트 | 몸 | Δ = 몸 − 0.5 |
|---|---|---|
| 방어유닛 폭 1(버스터즈) | 0.5 | **0** (우연히 정답) |
| 방어유닛 폭 2 (25종) | 1.0 | **+0.5** |
| 방어유닛 폭 3 (배스티온) | 1.5 | **+1.0** |
| 적 Small | 0.25 | **−0.25** ← 좁아진다 |
| 적 Medium / Large | 0.5 / 1.0 | 0 / +0.5 |
| 보스(저작) | 0.558~0.615 | +0.06~0.12 |

### 실제 영향 (에셋 guid 전수 대조)

**방어유닛 27종 중 자기중심 광역 보유 7종** — 영향 6 / 무영향 1:
배스티온 2.5→**3.5**(면적 **×1.96**) · 궁수 3.5→4.0 · 가디언·말파이트·난도질꾼·실드셔틀 2.5→3.0
(면적 ×1.31~1.44) · 버스터즈 **무변**. 캐논은 `EmitProjectilePattern` 이라 **대상 아님**.

**적/보스**: 악몽 +0.115 · 마메모 +0.058 · 드래곤 ±0. (결함 B·C·D 의 파급은 산출 대기)

## 구현

1. **상수를 감춘다** — `SkillMath.CellHalfWidthTiles` 를 `private` 으로 내리고
   `ReachFromCell(dx,dz,range,targetR)` 이 흡수. `ReachFromUnit(dx,dz,range,selfR,targetR)` 신설.
   ⚠ **진입점만 나누면 부족하다** — `ReachFromUnit(…, CellHalfWidthTiles, …)` 가 컴파일되면
   unit 22 의 실패 원인이 글자 그대로 생존한다. 가드는 「함수 직접 호출 0건」이 아니라
   **「sim 경로의 상수 참조 0건」**이어야 한다.
2. **`RangeMetric`** — `SelfArea` **= 3** (`Chebyshev = 2` 가 `[Obsolete(error:true)]` 로 점유).
   `AreaCircle` → **`CellArea`** 개명(어떤 에셋에도 직렬화 0 — 전수 확인, 번호 변경 무료).
   **`0 = None` + fail-closed loud** — 전환 후 0 이 「소비처 1곳짜리 칸 arm」이 되면 미래의
   자기중심 스킬이 인자를 빠뜨렸을 때 **이 버그가 그대로 재생산**되고, 폭 1 유닛에서는 안 보인다.
3. **매핑은 하나로 유지** — 페이크/라이브가 각자 분기하면 리뷰 H-1(fail-open ↔ fail-closed 갈림)이
   되살아난다. 시전자 반경을 **인자로** 받는다:
   `TryOriginRadius(RangeMetric m, float casterBodyR, out float originR)`.
4. **결함 B(intent 경계)** — `ProjectileSpawnRequest`/`SimIntent` 에 **원점 반경**을 실어
   `ProjectileHitSystem` 이 그것을 쓴다. 사망·퇴근은 **발화 시점 스냅샷**.
   ⚠ 이걸 안 하면 폭 2 유닛 **한 기 안에서 자가 둘**이 된다(`SelfArea` 1.0 / 폭발 0.5) —
   이 spec 이 없애려는 바로 그 갈림을 새로 만든다.
5. **결함 C** — `PickFallbackTarget` 의 체비셰프 사각을 `ReachFromUnit` 으로. **일반 공격이라
   가장 무겁다.**
6. **결함 D·E·F** — 부채꼴 거리 게이트·포탈 입구·드롭 최근접에 양쪽 몸.
7. **표기 동기** — `DcRangeCatalog` 는 도형 반경 `N` 만 돌려주고, **브리지
   `RedrawAttachPreview:8113` 이 host 몸을 합성**한다(브리지가 이미 host `Entity`·`LocalTransform` 을
   들고 있어 새 진입점 불필요 — 제약 12 통과). `PinCenteredRange`(칸 조준)는 **그대로 둔다.**

## 완료 기준

- [ ] **선행**: `TestSkillContext.Stat` 에 `UnitStat.BodyRadius` 케이스 추가.
      ⚠ 오늘 없어서 **항상 0** 이고, `AreaSleepSkill:81` 이 이미 그 값을 쓴다 —
      **차등 단언을 그 전에 쓰면 안 움직인다**(unit 22 를 숨긴 것과 같은 눈속임).
- [ ] compile · EditMode 전량 초록(선행 문안 2건 제외).
- [ ] **차등 단언**: 같은 스킬·같은 자리에서 **시전자 footprint 만 키우면 대상 집합이 넓어진다.**
      결함 A·B **각각**에 대해. 1×1 픽스처만 있으면 결함이 숨는다.
- [ ] **가드**: sim 경로의 `CellHalfWidthTiles` 참조 0건 + 이름 단언
      (`SkillAdapterDirectWriteTests` 관용구 재사용 — 개수만 세면 「하나 빼고 하나 더하면」 통과).
- [ ] 배스티온 도발 도형 반경 실측 **3.5** · 자폭/사망폭발/실드파열이 host 몸에 반응.
- [ ] **골든 A/B 분리 측정**(unit 22 방식) — 움직임의 **방향과 크기를 미리 적고** 그 밖이면 원인 규명.
- [ ] Play 육안: 배스티온 도발이 옆구리 적을 실제로 끌어오는가.

## 미결 (감사 회신 대기)

- 결함 B·C·D 의 밸런스 수치 · 드림캐쳐 카드 파급(같은 카드가 host 몸에 따라 달라지는가)
- 깨질 테스트 목록 · 골든 재베이크 범위 · 작업 단위 분할안
- `EmitPatternSkill`(`Euclidean`) 쟁점 판정
