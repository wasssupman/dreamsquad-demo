# 드림캐쳐 메커닉 이식 가이드 — 아키텍처 비의존 설계 + 적용 시행착오

> unit-trigger / attack-mod-bounce / content-1 / card-taxonomy / squad-warmup (전부 완료 2026-07-09) 산출물.
> **목적**: 드림캐쳐 메커닉을 "아키텍처에 의존하지 않는 정의"로 설계하고, 그 정의를 특정 아키텍처(여기선 하이브리드 ECS)에 **해석해 앉힐 때** 무엇을 새로 써야 하고 어떤 함정을 밟는지 남긴 지도. 코드 모듈 이식이 아니라 **설계 방법 + 실제 시행착오**의 이식이 목적이다.
> 카드 어휘 카탈로그가 아니다 — 조합 가능한 트리거/페이로드 목록은 각 spec README 가 source of truth. 여기선 **왜 이 구조인지**와 **적용의 지뢰**만 담는다.

---

## 1. 핵심 명제 — 2계층으로 아키텍처를 격리한다

메커닉을 **정의 계층**(아키텍처 무지)과 **해석 계층**(아키텍처 전담)으로 쪼갠다. 아키텍처를 교체하면 **해석 계층(번역자)만** 다시 쓴다.

- **정의 계층** = "무엇이 언제 무엇을 한다"만 순수 데이터로 선언. Entities/MonoBehaviour/특정 시스템 타입을 **참조하지 않는다**. enum·struct·SO 필드 + 인자 없는 순수함수.
- **해석 계층** = 그 선언을 읽어 실제 아키텍처의 상태(ECS 컴포넌트/버퍼, 시스템 arm)로 굽고 실행. 이 계층만 `EntityManager`·`ISystem`·`BattleBridge` 를 안다.

**왜 성립하나**: 게임 메커닉의 본질은 *이산 사건(trigger) → 산출(payload)* 의 조합이다. 이 조합은 좌표계나 실행 모델과 무관하게 선언될 수 있다. 아키텍처가 하는 일은 "언제 사건이 났는지 감지"와 "산출을 실제로 실행"뿐 — 둘 다 해석 계층의 국소적 훅.

---

## 2. 설계 계약 (아키텍처 무관)

메커닉을 정의할 때 지키는 규칙. 어떤 아키텍처로 해석되든 유지된다.

- **메커닉 = trigger × payload (× modifier).** 트리거는 *언제*(N회 공격/피격/사망/타이머만료/즉시), 페이로드는 *무엇을*(투사체/자기AOE/버프/다음공격개조), modifier 는 *상시 산출물 개조*(튕김/관통). 이 3축의 재조합으로 카드가 나온다.
- **수치는 전부 데이터.** period/magnitude/tileRange/duration/damageMul 은 정의 계층 필드에서. 코드 상수는 fallback 만.
- **카운터는 효과 인스턴스마다 독립.** 부착 시 instanceId 발급. 같은 카드 2장 = 독립 카운터 2개. (해석 계층이 어떤 컨테이너를 쓰든 이 의미론은 불변.)
- **1 사건 = 1 카운트.** 멀티 산출·근접/원거리 무관하게 "공격 1회=1카운트". 사건이 무산되면(타겟 소실 등) 카운트도 없다.
- **payload 는 기존 산출 프리미티브를 재사용한다.** 새 데미지/이동 경로를 만들지 않고 이미 있는 투사체·AOE·스탯·스택·해저드 파이프라인에 태운다. 정의 계층은 "어떤 프리미티브를 어떤 수치로" 만 말한다.
- **flat 수치 원칙.** 카드 페이로드는 공격자 스탯 배수를 곱하지 않는다(예측 가능). 스케일링이 필요하면 별도 파라미터로 명시.
- **append-only 직렬화.** enum 케이스·SO 필드는 항상 끝에 추가. zero-init 이 기존 에셋의 기존 동작을 보존해야 한다.
- **확장 비용은 명시적으로 판정한다.** "싸다(enum+arm+훅)" = 이산 사건 트리거 + 기존 프리미티브 파라미터화. "한 번 지불(국소 신규 조각)" = 상태형 조건("HP 50% 이하 동안")·조합 조건("A 그리고 B")·프리미티브 밖 페이로드(소환/지형)·트리거×페이로드 비호환·새 수명 부류(자폭 타이머). 보증은 "모든 조건이 공짜"가 아니라 **변경이 항상 국소적이고, 지불 여부가 부착 가드·미지원 LogWarning 으로 드러난다** 는 것.

---

## 3. 아키텍처 비의존 파일 (가져갈 것)

| 파일 | 역할 |
|---|---|
| `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` | 정의 계층 enum/struct (`DcTriggerKind`/`DcPayloadKind`/`DcAttackModKind`, `DcMechanic`/`DcPayloadSpec`/`DcAttackModSpec`). **ECS 무참조.** |
| `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` | 카드 SO. `mechanics[]`(트리거형) + `attackMods[]`(개조형) + `CardType`(덱 캡 키) + `placementWarmupSec`. |
| `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` | `DcTrigger.Tick(ref counter, period)` — 인자 없는 순수함수. EditMode 단독 테스트. |
| `Assets/_Project/Scripts/Battle/Combat/Projectile/BounceRetarget.cs` | `BounceRetarget.FindNext(...)` — Chebyshev 타일반경 최근접 재타겟 순수 기하. 아키텍처 중립. |
| `Assets/_Project/Scripts/Data/Dreamcatcher/DeckRuleConfig.cs` + `DeckRules.cs` | 덱 제약(크기/타입별 캡) config SO + 검증 로직. 수치는 SO, 상수는 fallback. |
| `Assets/_Project/Tests/EditMode/{DcTrigger,BounceRetarget,DeckRules}Tests.cs` | 순수함수 회귀 고정. |

순수함수·정의 계층은 그대로 옮기고, 아래 4·5 만 새 아키텍처에서 다시 쓴다.

---

## 4. 해석 계층 접점 (아키텍처마다 새로 쓰는 것)

새 아키텍처에서 작성할 것은 **베이크 + arm + 소유권 배선**뿐. 이 프로젝트(하이브리드 ECS)의 레퍼런스:

- **베이크 진입점** = `BattleBridge.ApplyDreamcatcherCardToUnit` / `ApplyDreamcatcherCard`. 카드를 읽어 슬롯/컴포넌트로 굽는다(MonoBehaviour↔ECS 유일 창구). 부착 가드(비-defender·근접 유닛 거절), instanceId 발급, 미래 배치 유닛 상속 레지스트리(`_activeDcEffects`/`_activeWarmups`)가 여기.
- **트리거 감지 arm** = 사건이 나는 시스템에 `DcTrigger.Tick` 훅. AttackN→AttackSystem RESOLVE, OnDamagedN→DamageApplicationSystem, OnDeath→UnitLifecycleSystem, 즉발→베이크 시점.
- **페이로드 실행 arm** = 기존 프리미티브 스폰 지점 재사용(투사체 request, TileAoe, StatModifier, DeadTag).
- **modifier 주입** = 산출물 생성 지점(AttackSystem 투사체 스폰)에서 슬롯 집계 주입, 해결 지점(ImpactSystem)에서 후처리.
- **소유권 배선** = 아래 §5 함정 1~2. 어떤 상태를 어떤 맥락이 소유하고, 크로스맥락은 어떤 채널로.

---

## 5. 함정 목록 (심각도 순 — 전부 실제로 겪음)

정의 계층은 깨끗했다. 시행착오는 전부 **해석 계층에서 아키텍처의 제약(맥락 경계·엔티티 수명·컴포넌트 유일성)과 부딪힐 때** 나왔다.

1. **카운터 소유 맥락이 트리거마다 다르면 버퍼를 쪼갠다.** AttackN→`DcTriggerSlot`(Combat), OnDamagedN→별도 `DamagedCounter`(Units). 같은 "트리거 슬롯"이라고 한 버퍼에 몰면, Units 가 Combat 소유 버퍼를 쓰는 **맥락 경계 위반**(kind 로 분기해도 컴포넌트 단위 위반). 사건이 나는 맥락 = 카운터 소유 맥락.
2. **크로스맥락 핸드오프는 소비자-소유 컴포넌트 채널로.** ①가시갑옷: Units(피격 카운트)→Combat(더블파이어 소비). `NextAttackDoubleFire` 를 **Combat 소유**로 정의 — 생산자 Units 가 `AddComponent`, 소비자 Combat 이 read+`RemoveComponent`. 기존 `IncomingDamage`(Units 소유에 Combat append)의 역방향. NativeQueue 없이 컴포넌트로. (Buffer/NativeQueue 외 제3 패턴이라 TRD 명문화가 후속.)
3. **사망 페이로드는 파괴 전에 이벤트로 베이크한다.** OnDeath(작별선물): defender 는 death 프레임에 `ecb.DestroyEntity` → bridge 드레인 시점엔 **엔티티 이미 없음**. 페이로드(mag/tileRange/dataIndex)를 `UnitLifecycleSystem` 이 파괴 전 슬롯 RO 로 읽어 `DefenderDeathEvent` 에 실어 보낸다. bridge 는 이벤트 데이터로만 스폰(파괴된 엔티티 접근 금지). **설계 리뷰에서 파괴-후-접근 CRITICAL 로 사전 적발됨.**
4. **request 컴포넌트가 엔티티당 1개면 캐리어 엔티티로 우회.** `ProjectileSpawnRequest` 는 엔티티당 1개 → 같은 프레임 기본공격 + dc투사체(또는 더블파이어 2번째 샷)가 충돌. 추가 발사는 `ecb.CreateEntity` 캐리어 + `ProjectileRequestCarrier` 태그, drain 이 캐리어 **파괴**(기존 경로는 RemoveComponent 유지, additive 분기). 신규 드레인/큐 0.
5. **즉발형은 "trigger=None 무조건 거절" 가드를 깬다.** `SelfBuffLethal`(마지막불꽃)은 `trigger=None`+`payload≠None`. 기존 부착 가드가 None 트리거를 무조건 거절하면 막힌다 → **None 은 payload 도 None 일 때만 거절**로 재구조화. 슬롯 미저장 즉발 branch 는 `attached++` 를 명시적으로 올려야 부착 API 가 true 반환. (설계 리뷰 CRITICAL.)
6. **만료→상태전이 타이머는 이중 전이를 가드한다.** `LethalTimer`(자폭): `WithNone<DeadTag>` + `UpdateBefore(DamageApplicationSystem)`. 데미지 사망과 같은 프레임 이중 DeadTag 방지. DeadTag 부여는 기존 사망 경로 재사용(신규 death 채널 금지). 소속 맥락은 Units(DeadTag/소멸이 Units 도메인 — Effects 아님, 리뷰 지적으로 이동).
7. **"다음 공격 N연발"은 데미지 output 블록만 감싼다.** DcTriggerSlot 틱·CC 넉백·AttackOutputLog·쿨다운 리셋은 RESOLVE 당 1회 유지. 구현은 **cooldown=0** 접근(다음 프레임 재발사) — 중복 로직 없이 정확. 발행 블록 전체를 2회 도는 순진한 방식은 부수효과 중복.
8. **"N초 대기 후 버프"는 버프 즉시 + cooldown 으로 접는다.** 별도 지연 타이머 불필요: +50% 즉시 적용 + `cooldownRemaining=max(cur,2)` → 2초 공격 불가라 관측상 "2초 후 +50%" 와 완전 동일. 미래 배치 유닛은 레지스트리(axis,sec) 상속, BeginPlacement clear.
9. **"튕김"은 임팩트 해결의 후처리 생존 분기로.** 새 시스템/태그 없이: outputs/VFX/HitFlash 는 기존 SingleSplash arm 통과 후, `bounceRemaining>0 && 재타겟 성공`이면 `DestroyEntity` 대신 `ProjectileState` 갱신(target 교체·impactReached=false·remaining--). 엔티티 생존 → 뷰/풀/Trail 자동 연속. 감쇠는 `state.damage` 와 outputs 버퍼 magnitude **둘 다** 곱(outputs 보유 투사체는 outputs 가 데미지 소스).
10. **modifier 스택은 부착 시 병합하지 말고 스폰 주입 시점에 집계.** 개별 슬롯 유지해야 회수 시 개별 제거 가능. count=합·damageMul=곱·tileRange=max 는 주입 순간 계산.
11. **호환 안 되는 트리거×페이로드는 부착 가드에서 거절.** ProjectileBounce 는 Homing×SingleSplash 산출에만(ballistic/TileAoe 는 대상 개념이 달라 v1 비적용). 근접(ProjectileRef 없는) 유닛 부착은 warn+거절. "미지원 조합은 침묵 no-op 이 아니라 명시 거절/경고."
12. **투사체·튕김 히트는 발사시점 로그에 안 남는다.** AttackOutputLog 는 AttackSystem(발사) 채널이고 투사체는 shooter 를 모른다. 검증은 로그가 아니라 **적 Health 감소 / ProjectileHitEvents(히트 VFX) / 육안**으로. MCP Play 는 에디터 포커스 없으면 frame 정지라 mid-flight 라이브 측정 불가 — 구조 검증(로그·정적 조회) + 사용자 포커스 Play 로 분리.

---

## 6. 검증 질문 (이 설계가 이식됐는지)

- 새 카드가 정의 계층 **데이터 재조합**만으로 표현되나? 안 되면 어떤 축(트리거/페이로드/수명)이 "한 번 지불"인지 명시했나?
- 새 상태가 **사건이 나는 맥락**에 소유됐나? 크로스맥락은 소비자-소유 채널인가?
- 순수함수(Tick/FindNext)가 아키텍처 타입 무참조로 EditMode 단독 테스트되나?
- 미지원 조합이 침묵 no-op 이 아니라 부착 가드/LogWarning 으로 드러나나?
