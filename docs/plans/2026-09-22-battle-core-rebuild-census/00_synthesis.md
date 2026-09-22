# 전투 코어 재구축 — 키워드 census 종합 (초안 · 승인 전)

> 2026-09-22. 6영역 census(같은 폴더 `census-*.md`, 약 395행)를 한 장으로 접은 것.
> 성격: 브레인스토밍 산출물. spec 이 아니다. 여기서 사용자 결정이 나오면 `docs/spec/{new}/README.md` 로 옮긴다.
> 전제(사용자 확정 2026-09-22): ① 순수 C# 전투 코어 + Mono 드라이버/프레젠테이션 ② 옛 트레이스 parity 폐기 → 설계 규칙 + 거시 지표 ③ 서버권위 규율 제거.

---

## 1. census 총계

| 영역 | 행 | 확정 | 애매 | 원자료 |
|---|---|---|---|---|
| 개체·수명주기 | 55 | 44 | 11 | `census-entities.md` |
| 맵·이동 | 71 | 55 | 16 | `census-map-movement.md` |
| 전투 판정 | 88 | 81 | 7 | `census-combat.md` |
| 효과·스탯 | 58 | 41 | 17 | `census-effects.md` |
| 스킬·드림캐쳐·기믹 | 62 (+enum 전량·concrete 34 분류) | — | 8 | `census-skills.md` |
| 매치 규칙·프레젠테이션 계약 | 61 (+채널 31 분류·Entities 누수 27파일) | — | 15 | `census-match-presentation.md` |

**「확정」 = 그대로 옮긴다.** 판정 산식(제약 13)·SimEntityId·발밑=위치·몸=원·배치 단일 술어·통행층 파생·흐름장 슬롯·이동 결정 순서·START/RESOLVE·지속 락·히스테리시스·킬 귀속·실드 FIFO·DoT 2축·CC 5종·1킬 1점·EndMatch 3·웨이브 단일 RNG·시드 6계열… 전부 게임 규칙이고 아키텍처와 무관하다.

**census 가 정정한 전제 수치**: concrete 34(58 아님) · SimIntent 24+메타 2 · 관측 탭 22 · Entities 누수 런타임 24+Editor 3 · 골든 선언 9/베이크 8 · HazardSingleton 셀 해시는 이미 은퇴.

---

## 2. 애매 항목의 다섯 갈래

### A. 사용자 결정은 이미 있고 착수만 안 된 것 → 새 전투 코어 의 기준으로 채택
- **마음 N개 공유 체력**(`heart-stress-axis/12`, 2026-08-25). 새 전투 코어 은 이 위에 선다. `Health` 를 마음 엔티티에서 매치 상태로 옮기는 것이 재구축에서 가장 싸다.
- **폭1 은퇴 → 「단방향」 은퇴로 정정**(2026-08-12). `map-rework` units 8~13 은 대상 맵이 사라져 사문.
- **다칸 footprint**: 기계 유지·값만 1×1(2026-08-30). 새 전투 코어 도 기계는 유지.

### B. ECS 제약이 만든 «우연»이라 새 설계에서 «결정»으로 승격해야 하는 것 (가만두면 조용히 바뀐다)
| 우연 | 현행 | 권고 |
|---|---|---|
| 모디파이어 1프레임 지연 | 생산자 11 중 8 이 소비자 뒤 | **즉시 반영**(틱 안에서 적용 phase 가 이동·공격 앞) — 밸런스 미세 변동 수용 |
| `RequireForUpdate` 겸직 | 피해 버퍼 0 이면 재생도 멈춤 | phase 실행 조건 명시, 한 phase 에 이질 작업 겸직 금지 |
| 실드 부여 다음 프레임 드레인 | 의도라고 주석 | 즉시 반영으로 통일(부여→흡수 순서는 phase 가 보장) |
| 자기 히트가 자기 수면 안 깨움 | 시스템 순서 우연 | 명시 규칙: 「CC 적용은 피해 판정 뒤」 |
| `_running=false` 가 전투 코어 안 멈춤 | 결과 화면에서도 전진 | **멈춘다**(EndMatch 뒤 틱 0). 여운은 뷰 소관 |
| 슬로모 = dt 배율 | 전투 코어 결정론을 건드림 | **틱 발행률 조절**(1틱 = 항상 1/60s, 실시간당 틱 수만 줄임). pause = 0틱 |
| Ground 통행층 | 배치엔 있고 통행엔 없음 | 통행 2층(Path·Air) + 배치 3층 유지. 문서에 「Ground 는 배치 전용」 명시 |
| 효과 타일 시드 -1 | 같은 맵 = 매판 같은 칸 | 매치 시드 파생으로 전환(사용자 확인 필요 — 규칙 변화) |
| 코스트 재생 스위치 | UI 소유 | 전투 코어 의 매치 상태 소유(하네스≠라이브 갭 소멸) |
| `_spawnSpreadCounter` | 가변 상태 | `spawnOrdinal` 파생 |
| 분리 누적 순서 | 청크 순서 1 ULP | `SimEntityId` 정렬 → 고정 스텝 완전 재현 |
| 직접 쓰기 예외 4 · ECB 2갈래 | ECB 성질 | 소멸. 원자성 요구(잠+감시, 잠금+무적)만 「한 intent 한 함수」로 |

### C. 코드·문서·사용자 판정이 갈려 있어 «먼저 판정»이 필요한 규칙 (플레이어가 겪는 규칙 = 질문 대상)
1. **자는 캐스터** — 캐스터가 CC 를 안 보고 계속 시전. 스펙은 사양, 사용자는 버그로 읽음(08-11). 권고: **버그**(행동 잠금을 캐스트에도 적용).
2. **선딜이 공속의 천장** — 실주기 = max(간격, 선딜), 공속은 간격에만. 권고: 공속 배율을 **실주기**에 적용(선딜도 같이 줄어든다).
3. **비율 합성 규약** — 현행 float `(1+Σadd)×Πmul` vs `unit-stats-and-modifiers` 스펙의 고정소수점. 권고: **float 현행 유지**(값 재조정 회피). 그 스펙 2편은 은퇴 표기.
4. **회수 모델** — 항등 덮어쓰기(Override 항등 없음·상한과 충돌·효과 타일 회수 불가). 권고: **슬롯 삭제**(dispel by key). 효과 타일 재배치 회수도 자연 해결.
5. **stackId 번호판** — 손 분양. 권고: 병합 키를 `(source, stat, op, slotTag)` 로 두되 tag 는 enum(OnPlace·Tile·AllyField·Card·StackDerived) — 번호판 폐기.
6. **스택 임계 규칙 소유** — StackKind 당 전역 1벌(드래곤·킨들러 공유). 권고: **출처(SO)별 규칙** — DoT 가 2축으로 푼 것과 같은 결.
7. **체비셰프 폴백**(폭탄맨·캐스터 4종 유일 경로) — 권고: 새 전투 코어 에선 예외 없이 제약 13 산식.
8. **wind-up 중 대상 소멸** — 권고: 현행 strict lapse 유지(빗나감).
9. **트리거 연쇄 순서** — 이력 없음. 권고: §3.5.
10. **진행형 상태 취소 어휘** — Begin* 5 에 취소 0. 권고: 상태 정의에 중단 정책 표(사망·퇴근·CC) 동봉.

### D. 죽은 축 — 새 전투 코어 이 태어나면서 안고 갈지
| 축 | 상태 | 권고 |
|---|---|---|
| 유출 카운터·`OpenBreachedCellsForLeak`·몽마의 계약·적 마음 | 도는데 판정 0 | **걷어냄**(적 마음은 거점으로만 잔존) |
| `spawnRoutes` 레인 기본 경로 | 저작 0 | 기계 유지(저작 비용 0) |
| `EnemyTier.Elite`·`Faction` 중립·`BodySize.Large`·`SelfWarmupBuff`·`AreaBarrage`·`StackPolicy` 2종 | 소비 0 예약 | enum 값 보존(시트 왕복), 코드 경로 없음 |
| `DefenderRarity` | 전투 코어 무관 | 전투 코어 에 안 실음 |
| `GamePhase.Tally` | 연출 은퇴 | 유지(카메라 asset 정수 직렬화) — 값만 |
| draft 폴백 진입·`OnRestartRequested`·`ThreatTable.Leader`·`hunterLookup` 좀비·`MapDocument_MovementStress` 고아 | dormant | 걷어냄 |

### E. 경계 결정
- **맵 정본은 프리팹 유지.** `MapStageScanner` 는 Unity 층에 남고 plain `MapSnapshot` 을 전투 코어 에 넘긴다(bake 없음 유지).
- **배치 InFlight** 는 뷰 시간이므로 전투 코어 은 `Deploying` 한 단계만 갖고, 착지 신호가 커맨드로 들어온다.
- **결정론 등급**: 고정 스텝 완전 재현(정렬로 닫는다).

---

## 3. 설계안

### 3.1 어셈블리 (컴파일러가 경계를 지킨다 — 불변식 3 계승)
```
Wassup.Runtime (Unity)  ──▶  Wassup.BattleCore (noEngineReferences)  ──▶  Wassup.Skills · Wassup.UnitAi (기존)
  · SO 저작 · MatchMaterializer(SO→plain defs)   · 개체·틱·매치 규칙·이벤트·스냅샷        · concrete · intent · 술어층
  · BattleDriver(Mono: 누산기→Tick)               · ISkillContext 구현(CoreSkillContext)
  · BattleView(SimEntityId→뷰)·입력→커맨드
```
BattleBridge 는 넷으로 흩어진다: Materializer · Driver · View sync · (규칙은 전투 코어 안으로). `battlebridge-dissolution` 의 census 축(채널/뷰설정/규칙/수명주기)이 그 분류 기준.

### 3.2 개체 모델 — Unity 표준 검토 결과
공식 문서: MonoBehaviour 는 같은 스크립트 인스턴스 간 Update 순서를 보장하지 않고, FixedUpdate 는 물리 스텝에 묶인다. 따라서 결정론 전투 코어 은 «개별 Update» 에 살 수 없고 **단일 드라이버가 명시 순서로 plain 객체를 민다**(Unity 자체 권고인 update-manager 패턴 · 락스텝 장르 표준). 컴포넌트-per-behavior(D1 기각) 는 재확인 기각.
- 개체 = plain class(`Unit` · `Projectile` · `Hazard` · `FieldCarrier` · `Pickup` · `Resignation` · `Structure`) + `SimEntityId` 1급 정체성. 목록은 id 오름차순.
- 「컴포넌트 있나」 분기(AttackSystem 안 7곳) → **정책 값**(`DefenderAttackPolicy` 선례). 옵션 성질은 nullable 부분(`unit.Move`, `unit.Cast`).
- 버퍼 사전 부착 규칙(「누가 무엇을 받나는 스폰 시점에 못 박힌다」) → 개체 종류별 고정 필드.
- DeadTag → `unit.Dead` + 「표시 틱 ≠ 소멸 틱」 유지(2-phase delete). 퇴근은 Dead 를 안 켠다(불변식 11).

### 3.3 틱 파이프라인 (§4.2 밴드에서 재도출 · 1/60 고정 · 단일 스레드)
```
0 커맨드 반입(배치·퇴근·부착·액티브·착지·제출) ── Immediate seam 은 커맨드 적용 안
1 필드·상태 준비: 해저드 수명 · 장애물→흐름장 · 어그로 · **모디파이어 즉시 적용** · CC · 존 · 주기 트리거 ── [Periodic]
2 사망 수렴 · 배치 활성화
3 AI·이동: 도발 부여 · 적/방어 상태(UnitAi) · 거점 목적지 · 이동 · 분리 · 해저드 캐스트 ── [Cast]
4 틱·투사체·집계: 효과 틱 · 투사체 이동/착탄 · 스탯 만료·집계 · 최대체력 · 스택 · 열기/피로 · 픽업 · 사직서 임계
5 공격 ── [Attack] ── 피해 ── [Death] ── 후처리(드랍·순찰·기상·발사기·배럴·호접몽·CC 감쇠) ── 소멸 ── [Lifecycle] ── 경계 ── [Threshold] ── 궁극기 · 순간이동
6 매치 규칙(전투 코어 안으로 이사): 웨이브 · 코스트 · 쿨다운 · 점수 · 마음 · 종료 3통로 · 보너스 · **기믹 호스트 트리거**
7 이벤트 플러시 → 뷰 프로젝션(값 스냅샷, SimEntityId 키)
```
seam 은 밴드 사이의 **명시 호출**이다. 개수는 파이프라인이 정한다(계약 7 계승).

### 3.4 프레젠테이션 계약
- 채널 31 → **(a) 매치 규칙 소비는 전투 코어 내부 직접 호출** · **(b) 뷰 전용 20종은 `SimEvent` 값 스냅샷 리스트** · **(c) 실행 요청 3종은 전투 코어 내부**(해저드/순찰/메테오 정의를 Materializer 가 미리 plain 표로 넘긴다 → 브리지 SO 조회 소멸) · **(d) 내부 10종은 함수 호출**.
- 뷰 등록부 11종(`_defenderByTile` 등)은 전투 코어 상태로 이사(판 위 유닛의 진실원 = 전투 코어). 뷰는 읽기 모델만.
- Entities 누수 24파일은 전부 「Entity 를 키로 쓰는 사전/콜백」 → `SimEntityId` 기계 치환.
- 슬로모/일시정지 = 드라이버의 틱 발행률. 뷰는 틱 사이 보간.

### 3.5 「트리거 → 발동」 통합 레이어 — 제안 (사용자 안 + 변형 3)
사용자 안(트리거-발동 추상 + concrete 컴포넌트식, ISkill 참고)에 **동의**한다. 프로젝트가 이미 80% 와 있다(계약 1~12). 재구축에서 바꿀 것 셋:

**변형 ① 트리거에 «주체(subject)» 축을 1급으로.** 지금 트리거는 전부 「host 자신에게 난 사건」이고, 게이트 축(`Self/EventTarget`)이 반쪽만 열어 뒀다. `Trigger = (Event, Subject, Gate, Count/Period)` 로 두면:
- 기믹 4종 = **호스트가 «판(Match)»** 인 슬롯 (`PeriodicTimer × 전 유닛` · `OnDeath(any defender)` · `OnPickupConsumed` · `OnResignationThreshold`). 활성화는 시즌 게이팅 그대로.
- Squad 카드 = Match 호스트 `OnPlace(any defender) × ApplyStat` + 시작 시 1회 — 전투 코어 밖 존재가 끝난다.
- **PlacementAura(어휘 밖 «시제»)** = 유닛 호스트 `OnPlace(any) × ApplyCc(EventTarget)` — host 사망 = 리스너 제거. 시제 문제가 주체 축으로 접힌다.
- SplitOnDeath = `OnDeath(self) × SpawnUnits`. RecallAttachedToFront·GainCost·ReduceCooldown = 메타 intent(현행).
- 어휘 밖 잔여 = **HeavyStrike 하나**. 자기를 부른 공격의 출력을 바꾸는 것은 사건이 아니라 **공격 출력 수식자**(foundation 판별 기준 계승). 별 축으로 둔다(`AttackMod` = AttackContext 위 순수 함수).

**변형 ② skillId·`DcSkillRouting` 간접층 은퇴.** 존재 이유가 Burst(managed 레지스트리 못 읽음)였다. 트리거별 라우팅 분기(죽은 자리 ↔ 내 발밑)는 **이벤트가 «자리(Site)»를 싣는 것으로 흡수** — 감지자가 `Site{pos, originBody, faction}` 를 채우고 concrete 는 payload 당 하나(`AreaBlast` 가 `SelfAreaBlast`/`DeathSiteBlast` 를 대체). 「누구의 자리인가는 감지자가 정한다」 원칙은 그대로, 분기만 사라진다. 저작 enum 값은 시트 왕복 때문에 **번호 보존**.

**변형 ③ 연쇄 규칙 = 세대(generation) BFS + 깊이 예산.** 발동 중 생긴 사건은 다음 세대 큐 → 같은 seam 창에서 세대 순으로 소진, 깊이 D(제안 8) 초과는 loud 폐기(`Report`). 세대 안 순서 = 생산 순서(단일 스레드 + id 정렬이라 결정적). HS 트리거 큐와 동형. **플레이어 규칙이므로 질문.**

유지: `ISkill` 무상태 · 호출자=소유자 · `ctx.Emit` 만(적용은 CoreSkillContext) · 이벤트 값 스냅샷 · 자리↔몸 짝 · 감지 분산/실행 단일 · fail-closed bake 게이트 3 · `DcApplicability`/`DcRangeCatalog` 단일 함수.

### 3.6 검증 체계
- 설계 규칙 테스트: 제약 13 산식 · 불변식 17 · 7축 → EditMode 단언(월드 없음).
- 새 전투 코어 자기 골든: `LegacyTraceV0` 포맷 계승(채널 append-only).
- 옛 코퍼스 8종 = 거시 지표 대역(시드별 킬·유출·생존) — A/B 판정.
- 실기기 게이트: Android ARM64 IL2CPP 피크 웨이브 틱 p95(Burst 상실 실측).

---

## 4. 사용자에게 되묻는 것 (번호로 답하면 됨)
1. §3.5 변형 ①②③ — 채택? (③ 의 깊이 예산 값 포함)
2. §2-B 「즉시 반영」 전환(모디파이어·실드) — 밸런스 미세 변동 수용?
3. 슬로모 = 틱 발행률(판 길이 불변·전투 코어 불변) — 확정?
4. §2-C 1~8 권고 — 각각 채택/보류.
5. §2-D 죽은 축 — 권고대로 걷어냄?
6. 효과 타일 시드를 매치 시드로(매판 다른 칸) — 규칙 변화라 확인.
7. 고정 틱 60Hz 유지(30Hz 는 후속 측정) — 확정?
