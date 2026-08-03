# 전투 시뮬 재설계 최종 계획안 (v6)

> ECS 완전 제거 · 서버권위 지향 · 2026-08-03 확정. 설계 논의 3라운드 + critic 2트랙(아키텍처/이행 전략) + ECS 시맨틱 감사 6트랙 + **Codex 적대 리뷰(v5→v6 개정, 하단 §8)** 의 수렴 결과.
>
> 리뷰어 참고 — 레포 컨텍스트: Unity 6.4(6000.4.3f1) 하이브리드 ECS(Entities 6.4) 비동기 토너먼트 디펜스. 전투 시뮬만 ECS(`Assets/_Project/Scripts/Battle/`), 나머지 MonoBehaviour. `BattleBridge`(`Assets/_Project/Scripts/Bridge/`)가 유일 창구. 전투 2~3분, 판 중 실시간 상호작용(배치·스킬·드림캐쳐 카드). 백엔드는 Firebase 인증 + 토너먼트 seed/시도/결과 API(점수는 클라 계산·URL 파라미터 제출). 리플레이/고스트 로드맵 확정.
>
> **이 스펙의 산출물(M0~M2)은 서버 없이 완전 구동되는 클라 단독 프로젝트다.** 서버권위는 이 스펙의 목적이 아니라 이 스펙이 열어두는 후속 옵션(M3)이며, "서버 가정"의 실체는 런타임이 아니라 설계 규율이다.

---

## 0. 방향 선언

**목적지는 "Mono 게임"이 아니라 "엔진-프리 C# 시뮬 라이브러리 + Unity 프레젠테이션 클라이언트"다.** 서버가 판의 시뮬 로직 전부를 소유하는 서버권위가 최종형이고, 클라는 커맨드를 보내고 권위 이벤트를 받아 타격감으로 표현한다. 진행은 "계약은 지금, 배치는 나중" — 서버 형태의 심장을 지금 만들어 인프로세스로 구동하고, 네트워크 배치는 M3.

**확정 결정:**

| # | 결정 | 근거 |
|---|---|---|
| 1 | 계보 B(분리 틱 시뮬 + 뷰 보간) 유지 | 리플레이/고스트 로드맵 확정, 코드베이스가 이미 이 형태 |
| 2 | 설계는 백지 청사진("애초에 plain C#로 설계했다면"), 실행은 스트랭글러(conform/adapt/rewrite/discard 4등급) | "번역본 설계"와 "빅뱅 재작성" 동시 회피 |
| 3 | 리플레이 정본 = 이벤트 스트림(AMR), **무결성 정본 = 커맨드로그** | 클라 결정론 불요 + 서버 재시뮬 검증 확보. 고정소수점 이식 회피 |
| 4 | sim은 **이식 가능한 순수 관리 C# 소스**(Burst-off)로 유지 — 특정 런타임 가정 금지 | 클라는 Android **IL2CPP**, 검증 러너는 CoreCLR — 교차 실행이 전제라 런타임 하나에 결속하면 안 됨. 교차 골든(Editor/IL2CPP/CoreCLR)을 M1 게이트로 |
| 5 | 서버 스택(Unity headless vs 자체)은 M3의 순수 운영 결정 | sim이 엔진-프리면 양쪽 다 같은 lib 호스팅 |

**기본값으로 채택 (이견 시 변경 가능):**

- **콘텐츠 동결 정책**: 이식 개시 후 신규 콘텐츠는 **신 lib에만** 구현, 구 sim 조기 프리즈, parity 범위는 동결 시점 스냅샷 고정. (근거: Bridge 최근 60일 334커밋 — 이중 유지보수는 치명적)
- **지연 대응**: lag compensation 미채택(단순 유지), 대신 RTT 150ms 주입 상태에서 전 스킬·카드 디자인 리뷰 통과를 M1 수용 기준으로. 통과 실패 스킬이 나오면 그때 재론.

---

## 1. 타깃 아키텍처

```
Sim 라이브러리 (netstandard, UnityEngine 참조 = 컴파일 에러)
  MatchConfig    맵·웨이브플랜(생성 결과 물질화)·덱·seed·ruleset version
  Command        틱 타임스탬프드 플레이어 동사 전체 (배치·이동·스킬·카드·웨이브 강제)
  Sim.Tick       고정 틱 20~30Hz. 페이즈 순서 = 규칙 — **M0 유효 순서 캡처가 정본**
                 (스케치: 커맨드 반입 → CC/필드 적용 → 이동 → 타겟팅·공격 → 투사체
                 → 피해·사망 정산(2-phase delete) → **CC 감쇠(이동 후 — 현행 CcDecaySystem
                 [UpdateAfter(MovementSystem)] 준수)** → 스폰 → 점수 → 이벤트 플러시)
  이벤트 3분리    ① 내부 phase queue — 같은 틱 소비 계약(CastEvents→AttackSystem,
                   BlinkRequests 등). sim 내부 전용, 직렬화 안 함. 틱 끝 플러시로 미루면
                   공격·텔레포트가 1틱 늦어짐
                 ② authoritative semantic AMR — 상태 재구성 가능한 권위 기록 (리플레이 정본)
                 ③ presentation projection — 클라 연출 소비용 (고스트 요구 필드 포함)
  RNG            단일 seeded xorshift + 파생 서브스트림 (기존 Unity.Mathematics.Random 상수 계승)
  Snapshot       주기 키프레임 + 상태 해시 (재접속·리플레이 seek·CI 회귀용)

클라이언트 (Unity, 스트림 소비자)
  IMatchSession  SendCommand / OnTickEvents / InstallSnapshot(day-1) / 읽기 모델
  구현 4종        Local(인프로세스, RTT 주입 노브) · Remote · Replay · Ghost
  타격감 정책     커맨드 즉시 낙관 예비 연출(게임 사실 무발생) → 권위 이벤트에 본 연출+juice
                 → 거절 롤백. 코스트·손패·쿨다운은 얇은 예측 상태 + reconciliation(1급 설계)
  고스트         필터드 프로젝션(배치·점수·웨이브 마일스톤, 웨이브 인덱스 정렬)

서버 (M3)
  같은 sim lib 호스팅 + 세션 관리 + AMR 저장 + 점수 발급 + 커맨드로그 재시뮬 스팟체크
  솔로 판 suspend/resume (비동기 장르 최대의 비용 단순화)
```

이동 표현: **권위 웨이포인트 + 코스메틱 클라 보간.** 판정 관련 좌표(피격·사망·명중)는 이벤트에 동봉해 연출이 판정을 따르게. 스트림 크기 예산 압축 후 ~300KB/판.

---

## 2. 마일스톤

### M0 — 결정론 수복 + 골든 하네스 (모든 것의 선행)

감사 결과 비결정론의 원천은 사실상 하나(가변 dt)이고 주입점도 하나다:

1. **진짜 fixed-tick 드라이버**: dt 상수 주입만으로는 안 됨 — `BattleScaledRateManager`는 렌더 프레임당 1회 갱신이라 고정 dt 주입 시 프레임레이트에 비례해 게임 속도가 변함. **accumulator+최대 catch-up 또는 명시적 `StepOneTick()` 드라이버**를 만들고, Bridge 웨이브·코스트·쿨다운(`SkillRuntime`의 별도 `Time.deltaTime` 시계 포함)까지 같은 substep 안으로 편입.
2. **시계 단일화**: Mono `BattleBridge._battleClock`·ECS 시계·`SkillRuntime` 시계의 다원 구조 해소, 스폰·입력의 프레임 양자화 제거 — 입력을 벽시계가 아닌 **sim 시각 스케줄로 기록·주입**. **pause/조준 slow-mo/드래그 감속은 gameplay 시계 정책으로 명시** (presentation 전용으로 격하하거나 커맨드로그에 상태 전이로 기록 — 서버권위에서 "무제한 조준 시간"을 클라가 갖는 문제의 사전 차단).
3. **stable ID (`SimEntityId`) 도입**: `Entity.Index/Version`이 타겟팅 동률뿐 아니라 **발사 패턴 RNG seed에도 직접 들어감**(`AttackSystem`: `math.hash(int2(attackerEntity.Index, fireCountBase))`) — 할당 순서가 다르면 탄막까지 연쇄 변경. 매치 내 비재사용 `SimEntityId(spawnOrdinal)`를 **구 ECS에도 매핑**하고 타겟팅·RNG·커맨드·이벤트·스냅샷·뷰 키를 골든 생성 **전에** 이 ID로 통일.
4. **유효 시스템 총순서 캡처**: 어트리뷰트 그래프는 불완전(미선언 순서 존재) — 러닝 월드에서 실제 실행 순서를 덤프해 그것을 틱 파이프라인 명세의 입력으로.
5. **canonical MatchConfig blob + `configHash`**: 스탯 SO만으로 부족 — 씬 상주 gameplay knob(스폰 spread, 인접 시너지 토글 등)까지 포함한 불변 config blob으로 물질화, canonical serialization + `configHash`를 골든·AMR·커맨드로그에 공통 저장. 테스트 모드에서 LoginAutoImport 차단. 시트 드리프트 vs 코드 회귀 판독 절차 명시.
6. **골든 덤프 = `LegacyTraceV0`**: seed N개 → 이벤트 스트림(직렬화 왕복 통과)·최종 점수·상태 해시. M1 신규 스키마와 직접 비교 가능하도록 stable ID·config serializer를 이 시점에 포함 (골든이 M1 계약에 선행하는 모순 해소).
5. **parity 기준 선언**: 커맨드 receipt·semantic 이벤트·틱별 공개 read model·**최종 canonical 상태+RNG 해시·점수(전부 int)는 exact 비교**, 연속 물리값(위치·잔여시간)만 epsilon. **동률 6지점은 예외 명시**(§3). 골든 시나리오에 거절·pause/slow-mo·강제 웨이브·동시 사망·restart 포함. 사전 실패 테스트(CardBuffs PlayMode) 수리 또는 명시 제외.

### M1 — seam 선행 적출

1. **백지 청사진 — 3장으로 캡, 1주 timebox**: ① IMatchSession 계약(커맨드·이벤트 스키마, 고스트 필드 포함) ② 컴포넌트 97종→plain struct 대응표 ③ 틱 파이프라인 순서도(M0 캡처 기반). 시스템별 상세는 이식 시점 지연 결정.
2. **salvage 판정 — 모듈 단위 ~60건** (시스템 44 + 채널 28 + Bridge 서브시스템): conform / adapt / rewrite / discard.
3. **IMatchSession을 기존 ECS sim 위 파사드로 먼저 도입** → 소비자 82파일 재배선을 구 sim 위에서 완료·머지. 스왑 커밋을 "세션 구현체 교체 1곳"으로 축소. **BattleBridge는 마지막에 죽는다.** (Bridge `_em.` 호출 305곳 = seam 인벤토리 정량 목표)
4. **Bridge 상주 매치 규칙 적출을 1급 작업으로**: 웨이브 스케줄·승패·배치 규칙·점수 산정·드림캐쳐 파셜 64KB — 이것 없이는 sim lib이 반쪽. (선행 머지 단위: 비주얼 statics 분리, `GetStackThresholds` 의존 역전, DebugMenu 퇴거)
5. **sim lib 이식** (맥락 4개 + 매치 규칙), 맥락당 테스트 포팅을 하위 작업으로 (World-조립 40파일은 어서션만 salvage, 골격 재작성).
6. **커맨드로그 이중 기록 시작** (무결성 정본).
7. A/B parity (M0 기준) → 스왑 → ECS asmdef 격리로 무력화 (패키지 물리 제거는 M2로 이연).
8. **RTT 150ms 수용 리뷰**: 전 스킬·카드 통과 확인.

### M2 — 스트림 정본화 + 무결성 가동

헤드리스 dotnet 러너 CI 상설(+코어당 동시 sim 벤치마크) · AMR 녹화 · **ReplaySession(seek 포함 — 스냅샷 경로를 유저 기능이 강제 운동)** · **커맨드로그 재시뮬 스팟체크 = CI/운영 배치 잡**(배포 서버 아님 — "서버 없는 M2" 원칙과 정합. IL2CPP↔CoreCLR float 편차 가능성 때문에 자동 제재 판정이 아니라 **advisory flag**로만 운용, 자동 판정 승격은 교차 골든 통과 후 M3에서) · 구버전 리플레이 코퍼스 재생 CI(스키마별 decoder/upcaster 보존 — additive-only만으론 의미 변경 오재생을 못 막음) · Entities·Entities Graphics 패키지 물리 제거.

### M3 — 토폴로지 전환

RemoteSession · 서버 스택 결정(순수 호스팅/비용 문제로 격하) · 재접속(이미 운동된 스냅샷+백로그 경로) · suspend/resume · 점수 발급을 서버 재시뮬 결과로 완전 이관(신뢰 공백 종결).

---

## 3. 이식 시맨틱 체크리스트 (감사 6트랙 수렴)

**형태 보존 필수:**
- ECB는 전부 로컬-즉시(자기 OnUpdate 내 `Allocator.Temp` Playback, Begin/EndSimulationECB 사용 0건)라 immediate 전환 안전 — 단 **"루프 중 기록, 루프 후 적용" 형태 유지**. 동일 엔티티 2연산 함정은 드레인 루프 시스템 전수 점검(`ModifierApplySystem` 선례: ECB AddBuffer 중복이 슬롯 덮어쓰기 버그를 냈고 즉시 적용으로 탈출).
- **사망 4단계 2-phase delete** (DamageApplication의 DeadTag 마킹 → HealthDeath 보완 → PatrolLifecycle/ResignationDrop 후처리 → UnitLifecycle 일괄 삭제). 즉시 삭제 단순화 금지.
- **`RequireForUpdate` 게이트 시맨틱을 가드로 정확 복제** (**39개 시스템** — 예: IncomingDamage 엔티티 0이면 DamageApplicationSystem 자체가 안 돌아 RegenPerSec 힐도 정지). `WithNone<>` 같은 **"컴포넌트 부재 = 상태"** 로직 포함 — 시스템별 all/any 게이트·optional presence·tag include/exclude를 기계 추출한 **이식 매트릭스** 작성.
- RNG는 xorshift 상수 계승 (System.Random 치환 금지).
- `ModifierStatsDirty`(유일 enableable) dirty-only 순회 → 명시 dirty flag/set.

**명시 결정 필요 (현재 암묵 — Unity 토폴로지 tie-break이 동작을 결정 중):**
- 미선언 순서: 모디파이어 클러스터(9개 생산자 시스템이 ModifierApplySystem과 순서 무관계), 투사체 체인(Move/Hit) vs Movement/Attack 체인, LastRunSystem→IncomingDamage, EffectTickSystem→IncomingDamage — **M0 캡처 순서로 고정**.
- **동률 6지점**: KillAttribution(등량 데미지 킬 귀속=버퍼 적재 순서)·Aggro capacity FIFO·CcEffectMerge/ApplyStat·ApplyStack/DotEffectMerge(last-writer-wins)·HazardCastSystem 최근접 tiebreak 부재(`HazardCastSystem.cs:83-88`)·HazardSingleton NativeParallelMultiHashMap 셀 순회. **권고: 프로젝트 자체 관례(Entity.Index/Version identity tiebreak — 타겟팅 유틸은 이미 전부 이 방식)로 승격하고 행동 변경 문서화.**

**비보존 (Burst/ECS 아티팩트):**
- `RequireAnyForUpdate` 비-Burst 분리, ParallelWriter 방어적 타이핑(실제 `Schedule`/`ScheduleParallel` 호출 0건 — 전 시뮬 메인스레드 단일), 죽은 ECB(ModifierApplySystem), `[InternalBufferCapacity]` 레이아웃 힌트.
- epsilon 분기(`lengthsq>1e-6f` 11+곳)의 Burst↔CLR ULP 차이는 parity 기준(행동적 동치)이 흡수.
- Burst 상실 성능은 M1 스왑 게이트에서 실기기 실측.

---

## 4. 상설 가드 6

① sim asmdef 격리(UnityEngine 참조 = 컴파일 에러) ② 골든 하네스의 직렬화 왕복 통과 기록 ③ LocalSession RTT 주입 노브(엔지니어링 도구가 아니라 수용 기준) ④ 헤드리스 dotnet 러너 CI(서버 실행 파일의 배아) ⑤ 구버전 리플레이 호환 CI(additive-only 스키마 + 관용 리더 + 에셋 간접 테이블 동결) ⑥ 커맨드로그 재시뮬 스팟체크.

---

## 5. 리스크와 감시 신호

- **최대 리스크 = M3 영구 연기로 인한 클라권위 부패.** 방어는 가드와 M2. **M2까지 밀리면 계획이 부패 중이라는 신호.**
- M1~M2 신뢰 공백(점수 클라 제출)은 커맨드로그 스팟체크가 조기 완화 — 완전 종결은 M3.
- 레포 고유: 시트 자동 임포트가 SO 스탯을 덮어 골든 오염(M0-4로 방어), 병행 세션 공유 워크트리(경로 명시 스테이징·amend 금지), `dotnet build` 거짓 통과(신규 .cs 미포함).
- Phase 단위로 main 머지 가능 상태 유지 — "항상 플레이 가능"의 실체는 seam-선행 순서가 담보.

## 6. 실측 기준치 (감사 보정)

Battle 227파일/13.5k줄(U40/M12/C74/E98) · ISystem 44(SystemBase 0·Baker 0) · IComponentData 97·IBufferElementData 21 · NativeQueue 채널 28 · Bridge 본체 7,157줄/파셜 합 8,967줄/`_em.` 호출 305곳 · 실코드 소비자 82파일 · Battle→Bridge 실결합 6파일(sim 시스템 1건: `StackModifierTickSystem.cs:74`) · Entities 참조 테스트 72(그중 World-조립 40) · Entities Graphics 실사용 0(dead using 1줄).

## 7. 다음 수순

이 계획안을 `docs/spec/{feature-slug}/` 분산 스펙(README + M0 작업 단위 파일부터)으로 옮긴다.

---

## 8. v6 개정 로그 — Codex 적대 리뷰 반영 (2026-08-03, gpt-5.6 xhigh)

스팟체크 검증(실코드 대조) 후 수용한 발견. 아티팩트: `.omc/artifacts/ask/codex-omc-plans-2026-08-03-ecs-removal-final-plan-md-*.md`

**CRITICAL 5건 — 전부 수용 (1건 뉘앙스):**
1. ~~CoreCLR 고정~~ → 이식 가능한 순수 C# 소스로 재정의 (클라 IL2CPP·러너 CoreCLR 교차 실행이 전제. 결정 #4 개정). *뉘앙스: 리플레이=스트림 플레이백이므로 클라 결정론 불요 원칙 자체는 생존 — 무너진 건 커맨드 재시뮬을 exact 자동 판정으로 쓰는 것. M3 전까지 advisory flag로 격하.*
2. fixed-dt 노브 ≠ fixed tick (RateManager는 프레임당 1회) → StepOneTick 드라이버/accumulator + Mono측 시계 편입 (M0-1 개정).
3. stable ID 부재 — Entity.Index가 타겟팅 동률 + **발사 RNG seed**에 직접 사용 → `SimEntityId`를 M0로 승격, 구 ECS 매핑 포함 (M0-3 신설).
4. 28채널 단일 스트림 붕괴는 내부 phase 메시지와 AMR 혼합 → 이벤트 3분리 계약 (§1 개정).
5. 커맨드로그 계약 부재(멱등성·순번·acceptedTick·거절사유) + "서버 없는 M2의 서버 재시뮬" 자기모순 → accepted-command receipt 계약(`matchId/configHash/clientSeq/acceptedTick/order/rejectReason`) day-1 + M2 스팟체크는 CI/배치 잡·advisory (M2 개정).

**MAJOR 수용:** CC 감쇠 순서(계획 스케치가 현행과 모순 — 이동 후 감쇠로 수정) · M0 골든이 M1 스키마에 선행하는 모순(LegacyTraceV0+stable ID+config serializer를 M0에 — M0-6) · façade 단일 drain 소유권(LegacyMatchSessionAdapter가 유일 drain·Bridge presentation은 소비자로) · 다단계 gameplay 트랜잭션(카드 부착=효과→지불→소비)의 sim 내부 원자 커맨드화 · pause/조준 slow-mo/드래그 감속의 gameplay 시계 정책화 + `SkillRuntime` 별도 시계 편입(M0-2) · RequireForUpdate 17→**39** 시스템 + `WithNone` 부재-상태 포함 이식 매트릭스 · 씬 상주 knob까지 canonical config blob+configHash(M0-5) · parity 기준 재정의(receipt·semantic 이벤트·read model·최종 상태+RNG 해시·**정수 점수는 exact**, epsilon은 연속값만) · 스냅샷 범위 열거(future wave·예약 커맨드·pending projectile/hazard·RNG 서브스트림·ID allocator + `snapshotTick/eventSeq/lastAcceptedCommandSeq/sessionEpoch/configHash`) + backlog exactly-once · Burst 제거 성능 게이트 수치화(Android ARM64 IL2CPP 피크 웨이브 soak, tick p95/p99, GC steady-state — 스왑 전).

**MINOR 수용:** 점수 ±ε 폐기(int exact) · 스키마 upcaster/decoder 보존 · RTT 수용 매트릭스(50/150/300ms + jitter + reconnect + rejection burst)로 확장.

**총평(Codex)**: "방향은 건전하나 v5는 프로토콜 계약이 비어 있는 청사진. 스왑 선행조건 = stable ID·fixed-tick 드라이버·accepted-command 계약·이벤트 3분리·canonical config/snapshot — 이 5개를 M0 앞부분으로." → v6에서 전부 M0/day-1로 이동 완료.
