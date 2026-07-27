# Dreamcatcher Attack Decoupling — 붙으면 반드시 발동한다

> 상태: **초안 rev2 (승인 대기)** · 2026-07-27 · critic 2종(설계·ECS) 반영

## 문제

드림캐쳐 유닛 효과가 공격 파이프라인의 **특정 구현 분기**에 결합돼 있다. 새 공격 유형이 추가될 때마다(directional-volley, bomb-thrower, hazard-cast) 기존 카드가 조용히 무효가 되고, **부착은 되고 코스트만 나간다**. 어디에도 표면화되지 않는다.

실측된 구멍 (전수 조사):

| 카드 | host | 원인 | 근거 |
|---|---|---|---|
| 비수 `ProjectileToTarget` | 폭탄맨 | dc arm 이 RESOLVE 블록 안인데 폭탄맨은 그 위에서 early-`continue` | `AttackSystem.cs:215` |
| 비수 | 해저드 캐스터 4종 | `attackRange 0` → `bestTarget` 없음. 실제 사건은 Effects 의 `HazardCastSystem` | `Defender_{Fire,Ice,Poison,Blocking}Caster.asset` |
| 빙결·밀치기·자장가 `ApplyCcToTarget` | 폭탄맨·캐스터4 | 위와 동일(같은 arm) | `AttackSystem.cs:1246~1290` |
| 출혈·동상 `ApplyStackToTarget` | 폭탄맨·캐스터4 | 위와 동일 | 〃 |
| 통통구슬 `ProjectileBounce` | 머신거너 | 주입이 homing `else` 분기에만 + 재타겟이 `SingleSplash` arm 에만. MG 탄은 `DirectionalLinear × PathHit` | `AttackSystem.cs:801~846` · `ProjectileHitSystem.cs:265` |
| 통통구슬 | 폭탄맨 | `ProjectileRef` 는 있어 부착 게이트를 통과하나, 발사가 `GrenadeToCell` 하드코딩이라 주입 지점에 닿지 않음 | `AttackSystem.cs:181` |
| 통통구슬 | 아틸러리 | `BallisticArcToPoint` — 기존 계약 4 의 **의도된** 제외 | `Projectile_ArtilleryShell.asset` `flightMode: 1` |
| 끝을 보는 눈 `FrontmostTarget` | 머신거너 | facing 유닛은 우선순위 보너스를 포기(`fmChosenIsPriority = false`) | `AttackSystem.cs:470~476` |

> **대포는 구멍이 아니다.** `Projectile_Shuriken_GA.asset` 에 `flightMode` 키가 없어 기본값 `Homing` 이고, bounce 주입 분기를 정상적으로 탄다. (초안 rev1 의 사실 오류 — 정정.)

## 상위 목표

**"부착 가능하면 반드시 발동한다"를 계약으로 만든다.** 그 아래 두 축:

- **축 A — 사건·타게팅 탈결합**: RESOLVE 에 **구조적으로 도달할 수 없는 host**(폭탄맨·캐스터)에 대체 사건 지점을 준다. 페이로드가 대상을 못 받으면 스스로 고른다.
- **축 B — 적용성 판정 수렴**: `WouldApply` 에 임시방편으로 쌓인 host 게이트를 단일 판정으로 모으고, **게이트를 통과하는데 무효인 조합**을 없앤다. 지원 여부는 코드가 아니라 **선언된 데이터**로 설명된다.

방향탄 bounce 개통(통통구슬×머신거너)은 **별도 spec** 이다 — `defender-directional-volley/README.md:79` 의 사용자 결정("차단이 아니라 개통")이 살아 있고, 볼리 arm 적재 + `PathHit` pierce 소진 후 재조준이라는 독립된 검증 질문을 갖는다. 이 spec 은 그 조합을 **거절 상태로 명시**만 하고 개통하지 않는다.

## 검증 질문

> 유닛 카드가 **부착 가능하면 반드시 발동하고, 발동 불가 조합은 부착 시점에 거절되는가?**(전 카드 × 전 디펜더 행렬로 검증) 그리고 비수가 폭탄맨·해저드 캐스터에서 5회 공격마다 발동하면서, **기존 host 에서는 발동 시점·수치·빈도·대상이 하나도 바뀌지 않는가?**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 계약 | `0_applicability_layer.md` | 지원 행렬 + 적용성 판정 순수 계층 (`Wassup.Core`, ECS 무참조). 산출물: `DcApplicability.Evaluate(...)` + 행렬 데이터 1곳 |
| 1 | 수렴+거절 | `1_convergence_and_rejection.md` | UI preflight·커밋 bake 두 미러를 unit 0 판정으로 수렴 + 무효 조합 거절(무차감·loud). **전수 행렬 EditMode** |
| 2 | 타게팅 폴백 | `2_payload_target_fallback.md` | `ProjectileToTarget` 이 host `bestTarget` 없을 때 자체 탐색. 순수 함수 + SO/시트/문안 3점 갱신 |
| 3 | 사건 지점 (Combat) | `3_bomb_event_point.md` | 폭탄맨 발사 성사 지점에 카운트 훅. RESOLVE 는 손대지 않는다 |
| 4 | 사건 채널 (Effects) | `4_cast_event_channel.md` | 캐스트 사건 → NativeQueue → `AttackSystem` 상단 드레인 |
| 5 | 인계 | `5_handoff_summary.md` | 종료 요약 + **선행 spec 계약 갱신**(`unit-trigger` 3·10, `attack-mod-bounce` 4) |

**순서 근거**: 0(행렬 선언) → 1(그 행렬을 소비하는 판정)이어야 판정이 하드코딩을 피한다. 1 이 잠근 조합 중 일부를 3·4 가 다시 연다 — 각 단위 문서에 **잠금/해제 2열 표**를 두고, 4 완료 기준에 "1 의 잠금 목록 재확인"을 건다.

## Feature-wide 계약 (load-bearing)

1. **RESOLVE 카운트는 그대로 유지한다.** 구 `unit-trigger` 계약 3 은 **무효화되지 않는다**. 이 spec 이 추가하는 것은 RESOLVE 에 도달할 수 없는 host 를 위한 **대체 사건 지점**뿐이다. (카운트를 START 로 옮기면 ① 응축된 일격 pre-scan(`:640` `WouldFire`)이 이미 증가한 카운터를 읽어 강공이 어긋나고 ② 처형타 HP 게이트가 wind-up 이전 상태를 보고 ③ "지연 중 타겟 소멸 시 카운트 없음"이 사라져 빈도가 당겨진다. `hitDelaySec: 0.3` 유닛이 20종이다.)
2. **host 당 사건 지점은 정확히 1개.** 상호배타 불변식이며, 한 host 의 AttackN 카운터는 한 프레임에 최대 1 증가한다.

   | host 아키타입 | 카운트 지점 | 불발 시 |
   |---|---|---|
   | 근접·원거리(기본) | RESOLVE (현행) | 지연 만료로 타겟 소멸 → 카운트 없음 |
   | facing 볼리(머신거너) | RESOLVE 1회 = 볼리 1회 | 레인 공백 → 없음 |
   | BombThrow(폭탄맨) | **발사 성사**(`landValid == true`) | off-grid 로 안 던진 쿨다운 → 카운트 없음 |
   | HazardCast(캐스터 4종) | 캐스트 성사 | 쿨다운만 도는 프레임 → 없음 |
   | ShieldCast(실드셔틀) | **N/A** — 실드 캐스트는 공격이 아니며 host 는 일반 공격(`attackRange 2` + outputs)으로 이미 RESOLVE 카운트 |

   여기서 "캐스트" = `HazardCastSystem` 의 해저드 캐스트뿐. `ShieldCastSystem`·`BossPeriodicTriggerSystem` 은 대상이 아니다.
3. **타게팅: host 우선, 없을 때만 페이로드 자체 탐색** (2026-07-27 사용자 결정 — B안). host 가 `bestTarget` 을 확정했으면 페이로드는 **그것을 그대로 쓴다**. 자체 탐색은 host 가 대상을 못 고르는 경우(폭탄맨·캐스터)에만 발동한다. 이유: 기존 host 의 대상 선택 규칙(힐러 HP비율 `:366` / 최전방 flow-field `:429` / 레인 `:470` / 어그로 `:863`)을 니들이 덮어쓰지 않아 **회귀가 구조적으로 0**이 된다.
   - 자체 탐색 규칙(전부 순수 함수 + EditMode): **진영 = `Faction.Enemy` 고정**(host mask 재사용 금지 — 재사용하면 힐러 자해가 되돌아온다) · 진입 필터 = Chebyshev 타일(`GridMath.RangeToTiles`) · 랭킹 = 유클리드 XZ · 동점 tie-break = 후보 스냅샷 인덱스 순(결정론) · `PastGoalTag` 제외(`:334` 선례).
   - 반경은 `DcPayloadSpec.tileRange`(기존 필드 재사용). host `attackRange` 폴백은 **금지** — 캐스터가 0 이라 즉사한다.
4. **적용성은 능력 판정이지 런타임 기회 보장이 아니다.** 판정 단위 = **메커닉/모드 1개**. 카드는 살아남는 메커닉이 하나라도 있으면 부착되고, **전량 무효일 때만 거절 + 무차감**. 부분 무효는 부착 **전에** 표시된다(조용한 부분 무효가 이 spec 이 없애려는 대상). 반면 "사거리 안에 적이 없다", "폭탄맨이 그리드 밖을 향해 배치됐다" 같은 런타임 무발동은 **버그가 아니다** — 그리고 Burst 경로에서 매 프레임 경고하지 않는다.
5. **니들 후보가 없으면 카운트는 소비된다.** `DcTrigger.Tick` 이 카운터를 리셋한 뒤 arm 이 도는 구조라 유예는 카운터 되돌리기를 요구한다. 발동은 했고 대상이 없어 불발한 것으로 취급한다(예측 가능한 주기 우선).
6. **판정 키는 SO 의 `flightMode` 가 아니라 host 의 실제 발사 경로다.** `BombLauncherState` 보유 host 는 `ProjectileRef` 선언과 무관하게 "수류탄 경로"로 판정한다 — `Projectile_Bomb.asset` 은 `flightMode: 0`(Homing)이라 SO 만 보면 bounce 지원으로 **오판**된다.
7. **맥락 경계 유지.** `DcTriggerSlot` 카운터 쓰기는 Combat 소유 그대로. 캐스트 사건은 NativeQueue 로 Combat 에 넘긴다(`AggroHitEventsSingleton` 역방향 선례). 큐는 **BattleBridge 가 소유**한다 — `Allocator.Persistent` 생성 + 엔티티 파괴 + Dispose 3점 세트 대칭(비대칭이면 재진입 시 `TryGetSingleton` 영구 실패, `BattleTimeScale` 전례). 이벤트 struct 는 **소비자 맥락**인 `Wassup.Battle.Combat` 에 둔다.
8. **기존 카드 완전 무손상.** 근접·원거리·머신거너에서 지금 발동하는 카드의 발동 시점·수치·빈도·**대상**이 바뀌지 않는다. 계약 1(RESOLVE 유지)과 계약 3(host 우선)이 이를 구조적으로 보장한다 — 완료 기준에서 실측한다.
9. **비수만 옮긴다.** `HeavyStrike`·`FrontmostTarget`·`ApplyCcToTarget`·`ApplyStackToTarget` 은 *그 공격의 대상*에 의미가 걸린 페이로드다. 자체 탐색 폴백을 주지 않는다 — 폭탄맨·캐스터에서는 **영구 거절**이 정답이다(계약 4 가 그렇게 판정한다).
10. **직렬화 append-only.** enum/필드는 끝에 추가. `tileRange` 는 payload kind 별로 의미가 다르므로(반경 5종 / `ApplyStackToTarget`=maxStack / `BountyMark`=피해감소%) 새 의미를 얹을 때 bake 주석 + 문안 포매터에 반드시 명기한다.

## 파이프라인 커버리지 (투사체 아키타입 대조)

| 정거장 | 현재 | 이 spec |
|---|---|---|
| 데이터 SO | `mechanics` / `attackMods` | `payload.tileRange` 를 `ProjectileToTarget` 폴백 반경으로 개통(unit 2) + 지원 행렬 데이터 신설(unit 0) |
| 스폰 진입점 | `AttackSystem` RESOLVE 1곳 | **+ 폭탄맨 발사 지점(unit 3) + 캐스트 드레인(unit 4)**. 기존 캐리어 패턴 재사용, 신규 시스템 0 |
| ECS 컴포넌트 (Combat) | `DcTriggerSlot` | 변경 없음(폴백 반경은 slot 의 기존 `tileRange`) |
| 시뮬 시스템 | `ProjectileMove`/`ProjectileHit` | 무변경 — 니들은 여전히 `HomingToEntity × SingleSplash` |
| 이벤트 큐 | 21채널 | **+1 = 22채널**(캐스트 사건). `CLAUDE.md` §"ECS 맥락 분리" 목록도 같은 커밋에서 갱신 |
| 시스템 순서 | `HazardCastSystem`·`AttackSystem` 둘 다 `[UpdateAfter(MovementSystem)]`, 상호 제약 **없음** | `AttackSystem [UpdateAfter(HazardCastSystem)]` 명시 — 같은 프레임 소비 보장(사이클 없음: 확인됨) |
| View/Pool · 씬 wiring | — | N/A — 신규 MonoBehaviour·프리팹 없음 |

## 검증 (완료 기준의 뼈대)

- **전수 행렬 EditMode**(unit 1): 지원 행렬이 total(미분류 쌍 0)임을 어서션 + 카탈로그 **전 카드 × 전 디펜더**에 대해 판정이 행렬과 일치. 이게 있어야 "반드시"가 검증 가능한 술어가 되고, 다음 유닛 추가 때 구멍이 재생산되지 않는다.
- **회귀 고정**(unit 3): "hitDelay 중 타겟 소멸 → AttackN 카운트 0" 을 테스트로 못박는다. 현 `DcTriggerTests` 는 순수 `Tick/WouldFire/GatePass` 만 덮어 계약 1 의 회귀를 잡지 못한다.
- **시트 왕복**(unit 2): SO 만 고치면 다음 로그인에 되돌아간다(`DcSheetApplier.cs:209` — 명시적 0 은 keep 이 아니다). `curl` 읽기 전용으로 `{baseUrl}/DcMechanics` 대조까지가 완료.
- **큐 수명**(unit 4): 로비 경유 **재진입 2회** 후 콘솔 무경고(육안). 3점 세트 누락은 컴파일·EditMode 어디에도 안 걸린다.
- **캐스터 가드**(unit 4): `HazardCastAbility` 보유 디펜더 SO 는 `attackRange == 0` ∧ `outputs` 비어 있음 — 위반 시 EditMode 실패. 계약 2 의 상호배타가 지금은 **에셋 값의 우연**에 기대고 있다.

## 스코프 밖 (명시적)

- **방향탄 bounce 개통** — 별도 spec. 이 spec 은 통통구슬×{머신거너, 폭탄맨, 아틸러리}를 거절 상태로 선언만 한다. 그 사이 카드 1장의 유효 host 가 궁수·레인저·마크스맨·스나이퍼·스카우트·대포 계열로 좁아진다(수용된 트레이드오프).
- **적/보스 AttackN 게이트 개방** — dc arm 3곳이 `defenderTagLookup` 게이트다. 단 이쪽은 **이미 표면화돼 있다**(`BattleBridge.cs:5596` 이 loud warning 후 skip, "개방 시 이 가드를 함께 푼다" 주석). 그 가드가 계속 유일한 창구다.
- **밸런스** — "공격 1회"의 실제 빈도는 유닛마다 다르다(머신거너 볼리 1회 = 10발). 구조를 먼저 세우고 시트에서 조정한다.
- **`unit-trigger` 계약 10(힐러 게이트) 철회** — unit 2 가 진영 Enemy 고정으로 원인을 없애면 철회 대상이지만, 철회 판단은 unit 2 완료 기준에서. 그 전까지 살아 있어야 한다.

## 후속 후보

- **방향탄 bounce 개통 spec** [M] · 볼리 arm 이 bounce 필드를 template·캐리어에 적재 + `PathHit` pierce 소진 후 재조준 + `pierceCount > 1` 합성 규칙(예산 곱 방지).
- **`FrontmostTarget` × facing 유닛** [S] · 경로 의존이 아니라 **타게팅 규칙 의존**이라 지원 행렬로는 표현이 어색하다. 행렬 키를 "host 속성"(궤적/타게팅 규칙/데미지 output)으로 일반화할지와 함께 판단.
- **`Projectile_Shuriken_GA` 데이터 위생** [S] · `flightMode` 미직렬화 상태(기본값 의존). 명시하지 않으면 다음 사람이 대포를 ballistic 으로 오독한다.
- **적용성의 UI 노출** [S] · 덱 빌더/손패에서 "이 유닛엔 안 붙는다"를 부착 시도 전에.
- **사건 빈도 정규화** [M] · 밸런스 편차가 실제 문제가 될 때.
