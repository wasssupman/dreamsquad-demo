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
| **C** | `Battle/Combat/AttackSystem.cs:331` · `:264` · `:403` (`PickFallbackTarget`) | **폭탄맨·아틸러리 «일반 공격» 대상 선정** + 해저드/폭탄 구동 DC 니들 | 공격자 유닛 | **체비셰프 셀 ≤ ceil(range)** — 양쪽 몸 0, 도형이 **사각** | audit |
| **D** | `Skills/SkillCone.cs:38` ← `ConeBreathSkill:38·58` | 화염 브레스 **부채꼴 거리 게이트** | 시전자 유닛 | `TileRange × TileSize` — 양쪽 몸 0 | audit |
| **E** | `Battle/Movement/MovementSystem.cs:317` | 포탈 입구 진입 | 칸(정당) | **대상 몸 누락** | audit |
| **F** | `Bridge/BattleBridge.cs:5970` `TryPickNearestEnemy` | 드롭 지점 최근접 적(살찌운 제물) | 탭 지점 | **대상 몸 누락** | audit |

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

## ⛔ 착수 전 사용자 결정 1건 — 퇴근 운석의 자리

`Card_SeveranceMeteor`(OnRetire)의 폭심을 코드가 **명시적으로 「칸」이라고 부른다**
(`BattleBridge.cs:4384` — *「비워진 칸 중심 — 슬롯 불변」*).

규칙의 트리거 주체는 **퇴근한 유닛**이지만 기록된 것은 **그 유닛이 비운 칸**이라, 둘 다
규칙에 부합하게 읽힌다. 배스티온(몸 1.5)이 퇴근하면 운석이 +1.0 넓어지나, 아니면 칸 0.5 인가.

- **ⓐ 유닛의 몸** — 「비워진 칸」은 실제로 배스티온이면 **3×2 영역**이다. 큰 유닛이 비운 큰
  자리에 큰 운석이 떨어지는 것이 기하적으로도 맞고 화면에서도 읽힌다. (권고)
- **ⓑ 칸** — 유닛은 죽은 게 아니라 **걸어 나갔다**. 시체가 없으므로 몸도 없다.

어느 쪽이든 생산자 **한 줄**이다(`impactWorld` 를 계산하는 자리에서 몸도 같이 잡는다).
**23b 착수 전에 답이 필요하다.**
