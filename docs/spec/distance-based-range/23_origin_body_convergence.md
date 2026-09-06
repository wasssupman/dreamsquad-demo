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
| **B** | `SelfAreaBlastSkill` · `DeathSiteBlastSkill` · `BossLeap:230` → `ProjectileHitSystem:747` | **자기 자리 폭발**(브루저 배치폭발 · 궁지/실드/진동갑주 카드 · 시체폭발 · 작별선물 · 재앙의심장 · 퇴근운석 · 보스 도약 슬램) | 트리거 대상 유닛 | 칸 반폭 0.5 (**intent 경계 뒤**) | critic + audit |
| **C** | `Battle/Combat/AttackSystem.cs:331` · `:264` · `:403` (`PickFallbackTarget`) | **폭탄맨 평타 + 캐스터 4종(화염·냉기·독·차단)의 캐스트** — ⚠ **폴백이 아니라 «유일 경로»**(그 아키타입은 RESOLVE 를 안 타 `AttackReach` 를 **한 번도 안 지난다**), 쿨다운마다 상시 | 공격자 유닛 | **체비셰프 셀 ≤ ceil(range)** — 양쪽 몸 0, 도형이 **사각** | audit |
| **D** | `Skills/SkillCone.cs:38` ← `ConeBreathSkill:38·58` | 화염 브레스 **부채꼴 거리 게이트** | 시전자 유닛 | `TileRange × TileSize` — 양쪽 몸 0. ⚠ **프리필터만 고치면 변화 0**(콘이 중심 대 중심이라 구속하지 않는다) — 콘 자체를 고쳐야 명제 위반이 사라진다 | audit + critic |
| **E** | `Battle/Movement/MovementSystem.cs:317` | 포탈 입구 진입 | 칸(정당) | **대상 몸 누락** | audit |
| **F** | `Bridge/BattleBridge.cs:5970` `TryPickNearestEnemy` | 드롭 지점 최근접 적(살찌운 제물) | 탭 지점 | **대상 몸 누락** | audit |
| **G** | `Battle/Combat/AttackSystem.cs:2118` `AnchorCellOf` | 게이트 원점이 **앵커 셀**인데 랭킹 원점은 **베이스** | 유닛 | `FootprintMath` 가 「사거리 원점 = 베이스」로 못박은 것과 어긋나 **2×2 가 x 로 반 칸 치우친다** | audit |
| **H** | `Bridge/BattleBridge.cs:2809` `CollectAlliesInRange` | 아군 집계(**로그 전용**) | 칸(정당) | 후보를 **앵커 셀**로 접어 다칸 유닛의 몸 무시 | audit |

**초판 오류 — 2차 정정(감사 2건 독립 확인)**: 실드 파열의 브리지 분기는 **죽은 코드**다.
`:4566·4577·4615` 가 전부 `if (!routedToSkillLayer)` 이고, `OnShieldBreak × SelfTileAoe` →
`SelfAreaBlastSkill.Id`, `× AreaSleep` → `AreaSleepSkill.Id` 로 **항상 라우팅**된다
(`DcSkillRouting.cs:53-54`). 따라서:
- **판정 축 → 인벤토리에서 삭제.** 실제 판정은 결함 A(`AreaSleepSkill`)와 B(`SelfAreaBlastSkill`)가
  이미 덮는다. 별도 행으로 두면 **같은 결함을 두 번 센다.**
- **로그 축 → 남긴다(우선순위 하).** `CollectShieldBreakTargets` 는 셀 양자화 + 칸 반폭 +
  대상 몸 0 으로 뽑은 집합을 로그에 적어, 스킬 레이어가 실제로 재운 집합과 **다르다.**
  고치되 **「로그 전용」을 주석에 못박는다** — 안 그러면 다음 사람이 판정으로 오해해 이중화한다.
- **죽은 arm 철거는 별도 spec**(`skill-layer-migration unit 8` 의 잔여물).

**쟁점 해소**: `EmitPatternSkill:86`(`Euclidean`) — audit 이 재검토 후 **결함 아님**으로 판정을
뒤집었다(탄 비행 거리). 오늘 자 그대로 둔다.

**감사가 새로 올린 보류 3건** (이 unit 범위 밖 — 별도 판단):
- `Effects/FlowFieldBuilder.cs:188` `CollectDefenderSources` — **사격 칸 필드 소스가 체비셰프
  사각 + 앵커 셀** 기준이라 다칸 유닛의 몸을 모른다. `AttackReach` 헤더가 *「이동을 멈추는 근거가
  사격 가능 여부인 이상 셋이 같은 답을 받아야 한다」* 고 못박은 축인데 **여기만 격자 자**다.
  ⚠ 이 필드는 **어그로 추격판과 감지 추격판(enemy-detection-range unit 8)의 소스**이기도 하다.
- `Bridge/BattleBridge.cs:5970` `TryPickNearestEnemy` — 원점이 유닛도 칸도 아닌 **탭 지점**이라
  「원점의 몸」이 정의되지 않는다. 「대상의 몸」은 명제상 들어가야 하는데 없다.
- `Combat/AuraPulse.cs`(체비셰프 링) · `Data/FootprintMath.cs:58` `RectChebyshevDistance` —
  **프로덕션 소비처 0.** 삭제하거나 「은퇴」 표기. 살려 두면 다음 사람이 체비셰프를 재유입시킨다.

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

**적/보스**: 악몽 +0.115 · 마메모 +0.058 · 드래곤 ±0.

⚠ **규모를 배스티온 1기로 읽지 말 것**(리뷰 HIGH). **폭 2 가 25/31** 이라 자기중심 광역은
**전반적으로** +0.5 확대된다 — 반경 1 기준 **면적 +65%**. 배스티온(+1.0)은 그중 최대치일 뿐이다.

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

### 23a — **구현 완료 2026-09-06** (골든 제외)

- [x] **선행**: `TestSkillContext.Stat` 에 `UnitStat.BodyRadius` 케이스 추가(항상 0 이던 선행 결함).
- [x] 진입점 2개(`ReachFromUnit`/`ReachFromCell`) + 본문·상수 **private**. 술어 호출 12곳 전부 전환.
- [x] `RangeMetric` = `None(0)` · `Euclidean(1)` · `SelfArea(3)` · `CellArea(4)`. 기본값 **fail-closed**.
- [x] `TryOriginRadius(metric, casterBodyR, out)` — **매핑 하나**를 어댑터·페이크가 공유(리뷰 H-1 유지).
- [x] `CasterRef.BodyRadius` + `SkillDispatchSystem.BuildCaster` 가 **루프 밖에서 한 번** 읽는다.
- [x] 자기중심 광역 **9자리 / 8파일** → `SelfArea` · 칸 조준 1자리 → `CellArea`.
- [x] 표기 동기 — `DcRangeSpec` 이 **도형 반경 + 형**만 담고, 브리지 `RedrawAttachPreview` 가
      host 몸을 합성한다. **판정과 «같은 함수»**(`RadiusWithOrigin` → `TryOriginRadius`)를 부른다.
- [x] `SkillMath` 의 「광역을 유닛 몸으로 바꾸지 말 것」 경고 **명시 은퇴**(사유 무효 확인).
- [x] **가드** `ReachEntryPointGuardTests`(3) — sim·도메인의 표기 상수 참조 0건 + 본문 private + 옛 술어 부활 금지.
- [x] **차등 단언** — `SelfArea_WidensWithCasterBody_NotWithACellConstant`(폭1 제외 / 폭3 포함) ·
      `CellArea_DoesNotReactToCasterBody`(형이 갈렸다는 증거) · 카탈로그 2건.
- [x] **EditMode 2765건 중 실패 2건**(`boomerang`·`bomb_man` — 시트 소유 **선행 실패**).
      예측한 3건이 정확히 깨졌고 처방대로 복원됐다.
- [ ] **골든 A/B** — 미실행(워크트리 격리 선행 필요).

### 23b — **구현 완료 2026-09-06** (골든 제외)

- [x] **짝 배선** — `SkillFiredEvent.CasterBodyRadius`(↔`FiredPosition`) ·
      `EventBodyRadius`(↔`TargetPosition`). 「반경은 자리와 함께 온다」가 불변식.
      ⚠ 단일 필드로는 **시체폭발이 틀린다** — 시전자가 킬러이고 폭심은 죽은 적이라 둘이 갈린다.
- [x] **생산자 5자리** — 피격·실드파열·처치(`DamageApplicationSystem`) · 자기 죽음
      (`UnitLifecycleSystem`, 파괴 **직전** 스냅샷) · **퇴근 운석은 의도적으로 0**(자리형).
- [x] `SkillParams.EventBodyRadius` · `SimIntent.OriginBodyRadius` ·
      `ProjectileSpawnRequest`/`ProjectileState.originBodyRadius` — **intent 경계 너머까지** 나른다.
- [x] `SkillMath.ReachFromImpact` — 자리에 주인이 있으면 그 몸, 없으면 칸(= 종전).
      `ProjectileHitSystem` 이 실려 온 원점을 읽는다.
- [x] **자기 자리 폭발 전수** — 자폭(`SelfAreaBlastSkill`) · 사망/시체/퇴근(`DeathSiteBlastSkill`) ·
      **보스 도약 슬램** · **궁극기 강습 슬램**(감사가 추가로 찾은 2건).
- [x] **로그 스냅샷을 피해와 같은 자로** — `CollectShieldBreakTargets` 가 셀 양자화를 버리고
      `ReachFromImpact` 를 쓴다. **「로그 전용」을 헤더에 못박았다**(판정을 얹으면 이중화된다).
- [x] **배선 그물** `OriginBodyRadiusWiringTests`(5) — 「0 이 자리형인지 누락인지」를 이름으로 고정.
      특히 **시체폭발에 킬러 몸이 붙으면 실패**하고, **퇴근 운석의 의도된 0** 을 문장으로 지킨다.
- [x] **EditMode 2770건 중 실패 2건**(선행 2건).
- [ ] **골든 A/B** — 23a 와 함께 미실행(워크트리 격리 선행).

### 나머지

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

## 깨질 테스트 (6건 — 전수 확인)

⚠ **깨지는 방향이 «좁아지는» 쪽이다.** 페이크(`TestSkillContext`)의 시전자 몸이 **0** 이라
반경이 `2.5 → 2.0` · `1.5 → 1.0` 으로 **줄어든다**. 라이브에서는 몸이 ≥ 0.5 라 안 나는 현상이다.

| 파일:테스트 | 왜 |
|---|---|
| `AreaTauntSkillTests:75 Diagonal_InsideTheCircle_IsInRange` | 반경 2, 대각 d=2.121 → 2.0 으로 줄어 탈락 |
| `AreaCircleMembershipTests:79 AreaSleep_N1_KeepsDiagonalNeighbour` | 반경 1, 대각 d=1.414 → 1.0. **「반경 1 = 여덟 이웃」 계약이 깨진다** |
| `AreaCircleMembershipTests:32 RangeMetric_DefaultValue_IsAreaCircle` | 기본값 단언 — append(3)이면 통과하나 뜻이 약해져 보강 필요 |
| `DcRangeCatalogTests:34 AreaConcretes_AreCircles_OfRangePlusCellHalfWidth` | `2f + CellHalfWidthTiles` 상수 단언 |
| `DcRangeCatalogTests:82 DeathSiteBlast_SelfSiteTriggers_AreCircles` | 같은 상수 단언. **게다가 「형」 결정으로 OnDeath(몸형)와 OnRetire(자리형)를 한 케이스로 묶은 이 테스트 자체를 갈라야 한다** |
| `DcRangeCatalogTests:103 ResolveCard_PicksTheSpatialMechanic` | `1f + CellHalfWidthTiles` 상수 단언 |

**안 깨지는 것**(여유 확인 완료): `AreaSleepSkillTests`·`StatAuraSkillTests`·`GrantShieldSkillTests`·
`AllySpeedAuraSkillTests`·`ConeBreathSkillTests`·`AreaCircleMembershipTests:59`·`RangeDisplayContractTests`·
`RangePredicateInvariantsTests`·`AttackReachTests`·`TileAoeTests`·`SkillRoutingCoverageTests`·`DcSkillRoutingTests`.

**처방**: `TestSkillContext.cs:24-30` 픽스처 시전자에 `BodyRadius = 0.5` 를 주면 1·2 가 **뜻을 보존한 채**
초록 복원. 3 에 차등 단언을 덧붙이고, 4~6 은 기대값을 host 몸 기준으로 갱신.
**신규 2건**: 차등 단언(시전자 몸 0.5→1.5 면 대상 증가) · 금지 가드.

## 골든 코퍼스 — 재베이크 필요 (8건 중 7건 이동)

`BattleScene.unity:152-184` 기본 덱에 **Archer·Guardian·Bastion 이 전부** 있고 능력 트리거가
전부 `OnPlace` 라 배치 즉시 발화한다. `no_defense`(배치 0)만 보스 몫(+0.058·+0.115)으로 극미세.

**예측(부호 포함으로 적는다)**: Archer 3.5→4.0(×1.31) · Guardian 2.5→3.0(×1.44) ·
Bastion 2.5→3.5(×1.96) → `EnemyKilled` **증가** · `GoalReached` **감소** ·
`UnitAttack`/`AttackOutputLog`/`DamageNumber` **증가**.
**유일한 반대 부호** = 시체폭발(OnKill, 폭심 = 죽은 적)이 표준 잡몹 위에서 1.5→1.25(**면적 −31%**).
자리형 이벤트는 「형」 결정으로 **전부 무변동**.

**A/B 분리 (unit 22 방식)**
1. 변경 **전** 8건 `Verify` 로 기준선 통과 확인 — 무관 dirty 오염을 먼저 걸러낸다
2. `no_defense` → **적 축만**(방어유닛 0 이라 변동 전량이 보스 몸 몫)
3. `summoner` → **방어유닛 축**. 덱이 `{summoner, archer, cannon}` 고정(`SimHarnessRunner:71`)이고
   영향 유닛이 **Archer 하나뿐**이라 변동 전량을 「둔화 3.5→4.0」에 귀속할 수 있는 가장 깨끗한 판
4. 나머지 6건 = Guardian+Bastion+Malphite 몫. `long_boss` 가 **시체폭발 음수 Δ 를 검출할 유일한 장수 판**

**⚠ 함정 둘**
- `configHash` 는 **웨이브·덱에만** 반응해 이 변경으로는 **안 움직인다.** 실패가
  「hash 동일 + 이벤트만 갈림」으로 나타나니 **hash 동일을 무변동으로 읽지 말 것.**
- 재베이크 전 **워크트리 격리 필수** — 현재 dirty 에 시트 임포트분 유닛 스탯이 섞여 있어
  **남의 WIP 가 기준선에 구워진다.** 굽고 나면 **골든만 담은 별도 커밋**으로 분리하고,
  `6_golden_regen_and_tuning.md` 의 「7건」을 8건으로 정정한다.

## 미결 (감사 회신 대기)

- Q4 빠진 위험 · 작업 단위 분할안 확정(23a/23b 게이트·크기)
- `AttackSystem.PickFallbackTarget` 이 **언제 도는가**(상시/예외) — 결함 C 의 우선순위가 여기서 갈린다
- 표기·프리뷰 중 판정과 갈리는 자리 전수

## 사망·시체 폭발 — 사용자 결정이 코드와 일치함이 확인됐다

`SkillDispatchSystem.cs:244` — `eventPos = TargetPosition ?? FiredPosition`. 생산자 전수 추적 결과
**폭심은 예외 없이 「트리거된 대상」의 자리**이고, **몸 반경을 발화 시점에 읽을 수 있다**
(생산자가 전부 파괴 «전» 에 돈다):

| seam | 폭심 = 누구 | 생산자 |
|---|---|---|
| OnKill(시체폭발) | **죽인 적** | `DamageApplicationSystem:483` (가드가 생존 보장) |
| OnDeath(작별선물·재앙의심장) | 죽은 host | `UnitLifecycleSystem:264` (파괴 직전) |
| OnRetire(퇴근운석) | 퇴근한 방어유닛 | `BattleBridge:4382` |
| OnShieldBreak / OnDamagedN | host 자신 | `DamageApplicationSystem:395·318` |

→ 「드레인 시점에 시전자가 없어 스냅샷이 필요하다」는 우려는 **생산자 쪽에서 이미 해소**된다.

### ⚠ 배선 정정 — 반경은 «자리와 짝»으로 다닌다 (필드 하나로는 틀린다)

초안의 `CasterRef.BodyRadius` **하나로는 시체폭발이 틀린 값을 쓴다.**
`Card_CorpseBurst`(OnKill)는 `Caster = killerSource`(**킬러**)인데 폭심은
`TargetPosition = 죽은 적의 자리`다(`DamageApplicationSystem.cs:481-487`). 시전자 몸을 쓰면
**방어유닛(1.0)의 몸으로 적 시체 위 폭발 반경을 정하게 된다.**

`DeathSiteBlastSkill` 헤더가 이미 그 함정을 적어 뒀다 — *「호출처가 둘이고 **자리의 주인이
다르다**. `OnKill` 은 「내가 죽인 자리」, `OnDeath` 는 「내가 죽은 자리」다. **누구의 자리인가는
감지자가 정한다.**」*

**위치 필드가 둘이므로 반경도 둘이다. 그 이상은 필요 없다:**

```csharp
// SkillFiredEvent — 반경은 «자리 바로 옆»에. 「반경은 자리와 함께 온다」가 불변식.
public float3 FiredPosition;
public float  CasterBodyRadius;   // FiredPosition/Caster 의 몸. 0 = 안 실었다
public float3 TargetPosition;
public float  EventBodyRadius;    // TargetPosition 주인의 몸. 0 = 그 자리는 «칸» 이다
```

| 소비 | 쓰는 반경 |
|---|---|
| 자기중심 질의(도발·CC·DoT·오라·실드부여·수면) | `CasterBodyRadius` |
| 작별선물·재앙의심장(OnDeath) | 둘이 같은 값(`deathPos`) — 어느 쪽이든 동일 |
| **시체폭발·잿불(OnKill)** | **`EventBodyRadius` = 죽은 «적» 의 몸.** 생산자가 `_transformLookup[entity]` 를 읽는 그 줄에서 같이 읽는다(그 시점에 피해자는 아직 살아 있다) |

### 착탄 폭발 구분자 — **불필요. 규칙이 자동 해소한다**

**원점의 정체를 아는 주체 = 그 자리를 써 넣는 자.** 오늘 모든 생산자가 이미 자기 자리를
써 넣고, 그때 트리거 주체가 누군지 안다.

| 생산자 | 자리 | 트리거된 대상 | 실을 반경 |
|---|---|---|---|
| `AttackSystem` 폭탄 분기 | 탄도 착탄 셀(**날아간 곳**) | **없음** — 주체가 아니라 목적지 | **0 = 칸 반폭(종전)** |
| `SelfAreaBlastSkill:26` | `ctx.Position(caster.Unit)` | 시전자 | `caster.BodyRadius` |
| `DeathSiteBlastSkill:36` | `p.EventPosition` | 죽은 적 / 죽은 자신 | `p.EventBodyRadius` |

**수류탄에는 트리거된 대상이 없다** — 착탄점은 「던져서 도달한 좌표」지 누군가의 몸이 아니다.
안 실으면 되고 **기본 0 이 곧 종전 동작**이다(`ProjectileSpawnRequest` 의 「Defaults 0 = legacy」
선례 7건과 같은 형태). intent 필드로도 skillId 로도 **가를 필요가 없다** — 가름은 생산자 안에서
이미 끝나 있다. **헬퍼·팩토리 금지**(스폰 지점 5곳, 제약 「생성 패턴」).

### ⚠ 첫 «음수» Δ 가 여기서 나온다

`Card_CorpseBurst`(시체폭발)가 **표준 잡몹(몸 0.25)** 위에서 터지면 반경 `N+0.5` → `N+0.25`,
N=1 기준 **1.5 → 1.25 · 면적 −31%**. 「자기중심 광역은 전부 넓어진다」는 **이 경로에서 거짓**이다.
**골든 예측 문장은 반드시 부호를 포함해 쓴다.**

### 시체폭발은 반경이 «런타임 가변»인 최초의 카드가 된다

폭심이 적이므로 반경이 **웨이브 구성(적 티어 분포)에 따라 판마다 달라진다.** 프리뷰로 그릴 수 없다.
→ `DcRangeCatalog.cs:76-79` 가 `OnKill` 을 **fail-closed(None)** 로 두는 현행 판단이 **여전히 옳다.**
오늘도 프리뷰가 없으므로 **회귀 아님**(플레이어가 잃는 것이 없다).

## 효과의 «형» — 사용자 결정 2026-09-06 (퇴근 운석 건이 드러낸 제3의 축)

ⓐ/ⓑ(유닛이냐 칸이냐)로 물었더니 답이 **더 정확한 축**으로 왔다 — *「이런건 새로운 타입이라고
봐야겠다. 시체 폭발 정도는 **터지는 대상의 몸체 기준**으로 범위가 정해져도 되지만, 퇴근 운석은
**해당 타겟(특정 좌표) 기준으로 N거리**에 피해를 입히는 메커니즘이 되어야 한다」*

| 형 | 무엇인가 | 원점의 몸 | 예 |
|---|---|---|---|
| **몸에서 나오는 것** | 그 몸이 터진다 / 그 몸에서 뻗는다 | **그 몸의 `HitRadius`** | 사거리 · 자기중심 광역 · 자폭 · **시체 폭발** · 작별선물 · 오라 · 도발 |
| **자리에 떨어지는 것** | 어떤 좌표에 내린다 | **없음**(칸 반폭 0.5) | **퇴근 운석** · 투사체 착탄 · 수류탄 · 장판 · 회오리 |

**좌표를 «지정한» 유닛의 몸은 안 붙는다** — 지정은 기하가 아니라 귀속이다. 퇴근 운석은
퇴근한 유닛이 «부른» 것이지 그 유닛이 터지는 것이 아니다.

⚠ **같은 스킬이 두 형을 겸한다.** `DeathSiteBlastSkill` 은 `OnKill`·`OnDeath` 에서 앞의 형,
`OnRetire` 에서 뒤의 형이다. **형을 정하는 것은 스킬이 아니라 «감지자»**이고, 그 파일 헤더가
이미 *「누구의 자리인가는 감지자가 정한다」* 라고 적어 뒀다.

### 배선 — 새 필드가 필요 없다

앞 절의 짝 배선이 이 형 구분을 **이미 표현한다**:

```
EventBodyRadius > 0  →  「몸에서 나오는 것」 (그 몸의 반경)
EventBodyRadius = 0  →  「자리에 떨어지는 것」 (칸 반폭 — 종전 동작)
```

생산자별 배정:

| 생산자 | 형 | `EventBodyRadius` |
|---|---|---|
| `DamageApplicationSystem:483`(OnKill) | 몸 | **죽은 적의 `HitRadius`** |
| `UnitLifecycleSystem:264`(OnDeath) | 몸 | 죽은 host 의 `HitRadius` |
| `BattleBridge:4382`(**OnRetire**) | **자리** | **0 — 안 싣는다**(코드가 이미 「비워진 칸 중심」이라 부른다) |
| `DamageApplicationSystem:395·318`(OnShieldBreak·OnDamagedN) | 몸 | host 의 `HitRadius` |
| `AttackSystem` 폭탄 착탄 · 해저드 · 착지점(14곳) | 자리 | 0 (무변) |

**퇴근 운석은 «변경 대상이 아니다»** — 오늘 동작이 이미 옳다. 이 결정으로 unit 23b 의
편집 대상이 하나 줄었다.

## 표기·프리뷰 — 판정과 갈리는 자리 전수

| 자리 | 문제 |
|---|---|
| `BattleBridge.UltimateLeap.cs:68` → `:2781` | **오늘 이미 거짓말 중.** 착지 예고가 `(2N+1)²` **사각**인데 피해는 **원** — N≥2 부터 모서리가 거짓 예고. 주석의 「예고 셀 = 피해 셀 계약」은 unit 4b 이후 거짓 |
| `TilemapMapView:1094` ← `BattleBridge:8047` | 조준·텔레그래프 링 `N+0.5`. **오늘은 일치**하나 결함 A 를 고치면 같이 고쳐야 |
| `DcRangeCatalog:55·73` | 부착 프리뷰 원 `N+0.5`. 자기 주석이 「판정 입력의 복사본」이라 **자동으로 거짓이 된다** |
| `DreamcatcherCardText:267·274·314` | 문안 「반경 N칸」, 실제 `N + 0.5 + 대상몸` |
| `DcMechanic:364·433` · `ProjectilePatternData:75` · `BattleBridge.Dreamcatcher:806` | 저작 툴팁·주석이 전부 **"Chebyshev"** 인데 실제 자는 **원** — 저작자가 툴팁을 믿고 값을 정하면 어긋난다 |

⚠ **칸 조준 텔레그래프(스킬 조준·메테오 예고)는 «제외 대상»이다**(리뷰 MEDIUM 8). 원점이 진짜
칸이라 지금이 옳다 — 「표기 N곳」으로 뭉뚱그리면 예고 원이 **틀린 방향으로** 넓어진다.

## 빠진 위험 (리뷰 Q4 — 완료 기준에 반영)

| 등급 | 위험 |
|---|---|
| HIGH | **`EventBodyRadius = 0` 이 「자리형」과 「생산자가 안 실었다」를 겸직**한다 — 배선 누락이 의도된 자리형으로 **위장돼 조용히 산다.** 이 spec 이 반복해 당한 fail-open 모양이라 **생산자 전수(4곳)를 이름으로 고정하는 단언**이 필요하다 |
| HIGH | **골든 「총계 무변」은 성공이 아니라 «측정 실패» 신호다.** 코퍼스가 자기중심 광역 경로를 안 밟으면 무변이 나온다 — 완료 기준에 그렇게 못박는다 |
| HIGH | 밸런스 규모 오기재(위에서 정정) · 유일한 감소 항목(시체폭발) 오독 위험 |
| MEDIUM | **차등 단언의 호스트를 «폭 2 이상»으로 강제**해야 한다. 폭 1 유닛 5기에서는 전후 답이 같아 기존 픽스처가 전부 초록이다 — unit 22 가 당한 그것 |
| MEDIUM | 부착 프리뷰 반경이 host 의존이 되면 「카드의 도형은 드래그 동안 불변」 전제가 깨져 **호버마다 재계산**이 필요하고 기존 단언 3건이 같이 바뀐다 |
| MEDIUM | **23b 가 바꾸는 사망폭발 카드는 부착 프리뷰에 이미 링이 뜬다** — 23b 만 착지하면 화면이 거짓말하는 구간이 생긴다 |
| MEDIUM | `SkillMath:83` 의 「광역을 유닛 몸으로 바꾸지 말 것」 경고를 **같은 커밋에서 명시적으로 은퇴**시키지 않으면 unit 22 의 「문서에 있던 전제가 재검토되지 않았다」가 재현된다 |
| LOW | 실드 부여의 **「반경 0 = 자기만」 가드가 몸 덧셈 «앞»에** 있어야 이웃이 안 걸린다 · 시전자 몸을 후보 루프 안에서 조회하지 말 것(짝 배선이면 불필요) · 상수 private 화 시 표기 3곳이 접근자를 요구(그중 1곳은 원점 미확인) |

## 작업 단위 (확정)

| 단위 | 내용 | 게이트 | 크기 |
|---|---|---|---|
| **23a** | **도달 질의의 자** — 진입점 2개 · 상수 private · `SelfArea` **8곳**(브레스 제외) · `CasterRef` 몸 · 부착 프리뷰 host 인자 · 금지 가드 · 차등 단언(**폭 2 이상**) · 골든 A/B | 없음 | 파일 ~12 · 테스트 3종 · 골든 1회 |
| **23b** | **폭발의 원점** — 요청 struct 짝 필드 · 생산자 3곳 · 자기중심 폭발 3종 · **로그 스냅샷을 피해와 같은 자로** · `SkillMath` 경고 은퇴 · **사망폭발 계열 표기 동기(23c 흡수)** | 없음 | 파일 ~11 · 골든 1회 |
| 별건 | 결함 C(캐스터 5종 사각) · D(콘) · E·F·G·H · 죽은 arm 철거 · 저작 툴팁 "Chebyshev" 정정 | — | 순서 자유 |

**순서 근거**: 23a 는 「닿나」(질의), 23b 는 「범위 안인가」(도형)로 축이 달라 서로를 막지 않는다.
**표기를 뒤로 미루는 분할만 금지** — 그 구간에서 화면이 판정을 틀리게 가르친다.

## 골든 A/B — 사전 등록 예측 (2026-09-06, 측정 «전» 에 적는다)

### 먼저: 코퍼스는 이미 «내» 저작으로 드리프트해 있었다

검증 결과 8/8 이 **`configHash` 불일치**(조건 드리프트)였고, 원인을 추적하니 코퍼스 마지막
베이크(`39020371`) 이후 적/방어유닛/웨이브 저작을 바꾼 커밋은 **`032033f1`·`02298532`
— `enemy-detection-range` 의 내 커밋 둘뿐**이었다.

⚠ **그 spec 의 handoff 에 적은 「코퍼스는 이 spec 이전부터 stale」은 오진이다.** 당시 실험이
`DetectionRange` **attach 줄만** 껐고 **에셋 필드 변경**(`huntsDefenders` → `detectionRange`, 24종)은
그대로 뒀는데, `configHash` 는 웨이브에 등장하는 적 SO 를 `PutAsset` 으로 **통째 해시**하므로
필드 이름·값만 바뀌어도 갈린다. 실험이 그 축을 아예 안 건드렸다.

> 부수 정정: `configHash` 의 범위는 「웨이브·덱」보다 넓다 — **맵 타일·placeMask·스폰·골·
> 웨이포인트 · 적 SO · 방어유닛 SO · 기믹**까지 담는다(`BattleBridge.CollectMatchConfig`).

### 측정 방법 (코드 되돌리기 없이)

`SkillMath` 에 **임시 A/B 스위치** 한 줄을 두어 원점 항을 옛 동작(항상 칸 반폭)으로 되돌린 뒤
**현재 저작 조건**에서 기준선을 굽고, 스위치를 원복해 `Verify` 한다. 차이 = **unit 23 순효과.**
⚠ 스위치는 **커밋하지 않는다.**

### 예측 (이 밖이면 원인을 찾는다)

| 시나리오 | 기대 | 근거 |
|---|---|---|
| `summoner` | **가장 깨끗한 신호.** 킬 ↑ | 덱 `{summoner, archer, cannon}` 고정, 영향 유닛이 **Archer 하나**(둔화 오라 3.5→4.0) |
| `basic`·`seed_b`·`seed_c`·`restart`·`force_wave` | 킬 ↑ · 골 도달 ↓ · 공격/피해 이벤트 ↑ | 기본 덱에 Archer·Guardian·Bastion, 트리거가 전부 `OnPlace` |
| `no_defense` | **거의 무변**(보스 몫 +0.058~0.115) | 방어유닛 0 |
| `long_boss` | 위 방향 + **시체폭발 음수 Δ 를 검출할 유일한 판** | 장수 판이라 OnKill 이 충분히 쌓인다 |

**⚠ 「총계 무변」은 성공이 아니라 «측정 실패» 신호다** — 코퍼스가 자기중심 광역 경로를
안 밟았다는 뜻이므로, 그때는 시나리오 구성을 의심한다.
