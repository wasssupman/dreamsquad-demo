# battle-sim-extraction — 전투 시뮬의 엔진-프리 라이브러리화 (ECS 제거)

상태: **M0 완료 (2026-08-04, 리뷰 APPROVE) · M1 진행 중 — units 7~17 완료
(17-F·16-F 는 unit 18 로 이관) · unit 18 진행 중 — 조각 A·C·D·E·F·G·H 완료,
44 시스템 중 33 이식 · 다음은 #33 AttackSystem(1,729줄) · units 19~20 미착수**

> **의존 방향을 컴파일러가 강제하기 시작했다** (unit 17, 2026-08-05). `Scripts/Sim/Lib/` 는
> `Wassup.Sim.asmdef`(`noEngineReferences: true`) 안에 있고, 거기에 `using UnityEngine;` 을 넣으면
> **CS0246 으로 빌드가 깨진다**(1회 실측 후 되돌림). 이후 unit 은 이 게이트 안에서 코드를 쓴다.
> `Sim/` 중 `Sim/Lib/` **밖**은 아직 스테이징(=`Wassup.Runtime` 소속)이라 텍스트 게이트가 지킨다.

| unit | 상태 | 커밋 |
|---|---|---|
| 7·9·10 청사진·판정표 | 완료 | — |
| 8 데이터 매핑(리뷰 보완 반영) | 완료 | — |
| 11 선행 머지 1·2·3 | 완료 | `b0681da6` 에서 골든 parity 확인 |
| 12 세션 파사드 | 완료 | — |
| 13 소비자 재배선 (A1~C3) | 완료 · **B2 잔여** | `1ce4407c` `cbd830c3` `0cd3e04c` `e588d6b5` `18ed2315` `a71e8088` `e41a30a4` `52454aa4` |
| — 골든 게이트 복구 | 완료 | `6f1bf77f` (unit 3 결함 수리 — 로드아웃 벽시계 시드) |
| 14 규칙 적출 ① 웨이브·승패·점수 | 완료 | `773e57b2` (골든 7종 byte diff 0) |
| 15-A 배치 쿨타임을 규칙 판정으로 | 완료 | `45692e47` (골든 byte diff 0) |
| 15-B 배치 판정 순수화 + 사유 이관 | 완료 | `87eb80fc` (골든 byte diff 0) |
| 15-C-1 재배치 판정 이관 | 완료 | `afdea076` (골든 byte diff 0) |
| 15-C-2 시너지 판정 적출 + `visualMaterial` 계층 정리 | 완료 | `c76cf833` (골든 byte diff 0) |
| **15 종료** | `ApplyOnPlaceEffect` 는 unit 18 로, 통화 상태 이관은 동결 해제 후로 이관 | — |
| 17-A~E·G asmdef 격리 | 완료 — **코드 변경 0** | `1c94f5f0` (골든 byte diff 0) |
| 16-C 카드 판정 순수화 | 완료 | `8f47b8ee` (골든 byte diff 0) |
| 16-E 거절 사유를 receipt 로 | 완료 | `e40844f9` (골든 byte diff 0) |
| 16-G 게이지 소유권 + 읽기 모델 서빙 | 완료 | `d18449db` (골든 byte diff 0) |
| 16-A · 16-B | **소멸** — unit 17 이 전제를 바꿨다(16 문서 참조) | — |
| 16-D 적용성을 검증으로 + UI 술어 통합 | 완료 — **unit 16 종료** | (골든 byte diff 0) |
| 16-F | unit 18 로 이관 — 진짜 중복은 eval↔bake 이고 그 본체가 480줄이다 | — |
| 17-F | unit 18 과 같은 커밋으로 이관 | — |
| 13-B2 뷰 소유권 3건 | 완료 — **unit 13 종료** | `d3528bb5` (골든 byte diff 0) |
| **18-A** 스캐폴딩 4계약 | 완료 | `4e6e1c59` `2eeb1fdf` `beb5931d` `17f1e5b0` |
| 18-C/1 모디파이어 어휘·산식 | 완료 | `77752f41` |
| 18-C/2 오라클 0 시스템 2개 특성화 선행 | 완료 | `f57c80e8` (EditMode 2027 / 실패 0) |
| **18-C 시스템 몸체 6/6** | 완료 | `afc75890` `aeea0561` `6b6171d7` `e7574555` (EditMode 2086 / 실패 0) |
| 18-C/7 성능 프로브 (중단 기준 ④) | 완료 — **통과** ×0.52 | `95a6075c` (EditMode 2087 / 실패 0) |
| 18-B | **삭제** — 조각이 아니다(게이트엔 독립 관측점이 없다). 처분은 plan §"게이트 53 의 처분" | — |
| **18-D** CC/DoT 4/4 + I2 검출기 | 완료 | `f03f05d3` (EditMode 2161 / 실패 0) |
| **18-E** 필드·존·해저드 7/8 (#18 은 18-I 로 이관) | 완료 | `7876cc3d` `ba61015f` `ade2ebc6` `ae9fc480` `05ad9181` `02708802` |
| **18-F** 어그로·AI·이동 5/5 | 완료 | `bfd1de38` `3cc8d677` `f14b9e3a` `b59b8675` (EditMode 2325 / 실패 0) |
| **18-G** 피해·실드·사망 릴레이 7/7 | 완료 | `b7bbcb89` `3d906d68` `5758b211` `f3f5d8be` `27587de6` (EditMode 2439 / 실패 0) |
| **18-H** 투사체 3/3 | 완료 | `b19ee78e` `e7366ca4` `a10cbebb` `21034e4d` (EditMode 2554 / 실패 0) |
| 18-I/1 #18 HazardCast (18-E 이관분 회수) | 완료 | `5444bde6` (EditMode 2566 / 실패 0) |
| 18-I/2~18-L | **진행 중** — [`m1_unit18_plan.md`](m1_unit18_plan.md) · 인계는 [`m1_unit18_handoff.md`](m1_unit18_handoff.md) | — |
| 19~20 | **미착수** | — |

### 골든 코퍼스 동결 (사용자 결정 2026-08-05)

units 15-C~18 구간 동안 골든 7종을 **byte 동결**로 두고 계속 `byte diff 0` 으로 판정한다.
`PlaceDefenderAs` 은퇴는 unit 18 이후로 이관됐다 — 하네스가 그 함수로 배치하고 코스트 차감 주체가
경로마다 달라(드래그=Bridge, 클릭=UI), 차감을 sim 으로 모으면 `cost` 정규 상태 라인이 움직여
코퍼스가 바뀐다. 그래서 이 구간의 작업은 **값·시점을 바꾸지 않는 조각**으로 한정한다.
분류표는 [`15_rule_extraction_placement_currency.md`](15_rule_extraction_placement_currency.md) 참조.

이 결정은 새 정책이 아니라 **아래 "골든 계약"의 집행**이다 — 그 계약은 처음부터 "골든을 바꾸는
unit 은 19 하나" 라고 못박았다. unit 15 문서의 "은퇴 경로 삭제" 항목이 그것과 충돌하고 있었고,
충돌을 계약 쪽으로 해소한 것이다.

### 남은 유닛의 규모 (2026-08-05 판단)

- ~~15-C · 16 · 17~~ — 완료(2026-08-05). **19 — 세션 규모.**
- **18 (context port) 은 단일 세션 규모가 아니다** — ECS 시스템 전체와 27개 이벤트 채널을
  엔진-프리로 옮기는 이 spec 의 실질적 본체. 별도 계획으로 쪼갤 것.
- **20 (A/B parity) 은 ARM64 실기기 게이트** — 장비 없이는 완료 불가.

> **unit 18 을 이어받는다면 [`m1_unit18_handoff.md`](m1_unit18_handoff.md) 를 먼저 읽는다** —
> 되돌리면 안 되는 의도 5건 · 이식 함정 6건 · **운영 함정 3건**(신규 파일 csproj 등록 · 골든 전
> `ReimportData` · 러너 트리거 4함정)이 거기 있다. 그 다음 [`m1_unit18_plan.md`](m1_unit18_plan.md).
>
> M1 리뷰 판정과 재리뷰 게이트 6건은 [m1_review.md](m1_review.md) 가 정본이다.
> 세션 인계는 [m1_unit13_handoff.md](m1_unit13_handoff.md) → [m1_unit14_handoff.md](m1_unit14_handoff.md).

설계 정본: [`docs/plans/2026-08-03-battle-sim-extraction-design.md`](../../plans/2026-08-03-battle-sim-extraction-design.md) (v6 — Claude critic 2트랙 + ECS 시맨틱 감사 6트랙 + Codex 적대 리뷰 수렴). 이 README는 그 계획의 실행 인덱스다. 근거·감사 상세는 설계 문서를 읽는다.

## 상위 목표

전투 ECS를 완전히 제거하되, 목적지는 "Mono 게임"이 아니라 **엔진-프리 순수 C# 시뮬 라이브러리 + Unity 프레젠테이션 클라이언트**다. **이 spec의 산출물(M0~M2)은 서버 없이 완전 구동되는 클라 단독 프로젝트**이며(sim lib을 `LocalSession`으로 인프로세스 내장), 서버권위는 이 spec이 열어두는 후속 옵션(M3)이다. "서버 가정"의 실체는 런타임이 아니라 설계 규율이다.

마일스톤 지도: **M0** 결정론 수복+골든 하네스(units 0~4, 이 문서) → **M1** seam 선행 적출(IMatchSession 파사드→소비자 재배선→sim lib 이식→스왑) → **M2** 스트림 정본화(헤드리스 러너·AMR·ReplaySession) → **M3** 토폴로지 전환(RemoteSession·서버). M1+ 작업 단위는 M0 완료 후 뒤 번호로 이어 쓴다.

## 작업 단위 목록 (M0)

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [m0_implementation_summary.md](m0_implementation_summary.md) | 구현 요약 | M0 실제 변경·런타임 흐름·검증·잔여 경계 종합 |
| [0_system_order_capture.md](0_system_order_capture.md) | 순서 박제 | 유효 시스템 총순서 덤프 + 미선언 순서 어트리뷰트 핀 |
| [1_sim_entity_id.md](1_sim_entity_id.md) | stable ID | `SimEntityId` 도입, 타겟팅 동률·발사 RNG seed 축 교체 |
| [2_fixed_step_harness_driver.md](2_fixed_step_harness_driver.md) | 시간 결정론 | 하네스 모드 `StepOneTick` 드라이버 + 입력 sim-tick 스케줄 주입 |
| [3_canonical_match_config.md](3_canonical_match_config.md) | 조건 물질화 | MatchConfig blob + `configHash` + LoginAutoImport 차단 |
| [4_legacy_trace_golden.md](4_legacy_trace_golden.md) | 골든 하네스 | `LegacyTraceV0` 기록·직렬화 왕복·seed 코퍼스·parity 기준 확정 |
| [5_handoff_summary.md](5_handoff_summary.md) | 인계 문서 | 클론 세션용 헛발 방지 + 레포 함정 |
| [6_decision_record.md](6_decision_record.md) | 결정 기록 | 기각 대안·자기 철회·재론 조건 (ADR) — 재론 전 필독 |

## 작업 단위 목록 (M1 — seam 선행 적출)

> 분해 원칙: 청사진(7~9)은 **3장 캡·1주 timebox**(설계 정본 M1-1). 상세 unit(12~20)은 청사진이
> 계약을 확정한 뒤 도출했다. CLAUDE.md 제약 1~4 이행은 unit 11 커밋에서 명문화 완료.
>
> **순서 의존**: 12(파사드) → 13(재배선) → 14·15·16(규칙 적출, 이 셋은 상호 독립) → 17(asmdef) →
> 18(이식) → 19(시계·로그) → 20(parity·스왑). 12·13 을 구 sim 위에서 끝내는 것이 스왑 반경을
> 1곳으로 만드는 전제이고, 17 을 18 앞에 두는 것은 이식 중 Unity 유입을 컴파일러가 막게 하려는 것이다.
>
> **골든 계약**: units 12~18 은 **byte diff 0** 이 완료 기준이다(규칙 이동이 결과를 바꾸지 않았다는
> 증인). 골든을 바꾸는 unit 은 **19 하나**(슬로모 격하가 통화 누적 rate 를 바꾼다)이고, 20 은 A/B
> parity 로 신 sim 을 구 sim 기준선에 맞춘다.

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [m1_review.md](m1_review.md) | 리뷰 기록 | HEAD `c0a361cb` 기준 units 7~11 완료 판정 · Track A/B 발견사항 · 재검증 게이트 |
| [7_session_contract_blueprint.md](7_session_contract_blueprint.md) | 청사진 ① | `IMatchSession` 계약 — 커맨드/receipt/이벤트 3분리/스냅샷/읽기 모델 스키마 (고스트 필드 포함) |
| [8_data_mapping_blueprint.md](8_data_mapping_blueprint.md) | 청사진 ② | IComponentData 96 + IBufferElementData 21 → plain struct 대응표 + `RequireForUpdate` 35 게이트 이식 매트릭스 |
| [9_tick_pipeline_blueprint.md](9_tick_pipeline_blueprint.md) | 청사진 ③ | order-capture 기반 틱 페이즈 순서도 + 동률 예외·병합 duration 정책 명문화 |
| [10_salvage_matrix.md](10_salvage_matrix.md) | 판정표 | 시스템 44 · 채널 27 · Bridge 서브시스템 ≈60건 conform/adapt/rewrite/discard |
| [11_preparatory_merges.md](11_preparatory_merges.md) | 선행 머지 3건 | 뷰 상수 분리(✅ `b564e768`) · 스택 임계 의존 역전(✅ `c0a361cb`) · 비-sim 코드 퇴거(✅ `562f83b7`) — ✅ 검증 완료(`b0681da6` 골든 parity) |
| [12_session_facade.md](12_session_facade.md) | seam 도입 | `IMatchSession` + `LegacyMatchSessionAdapter` — 구 sim 위 파사드, 소비자 변경 0 |
| [13_consumer_rewiring.md](13_consumer_rewiring.md) | 재배선 | 소비자 82파일을 A(폴링→읽기 모델)·B(push→이벤트)·C(입력→커맨드) 3묶음으로 |
| [14_rule_extraction_wave_outcome.md](14_rule_extraction_wave_outcome.md) | 규칙 적출 ① | 웨이브·승패·타이머·점수/유출 — 읽기 모델의 신설 카운터를 여기서 채운다 |
| [15_rule_extraction_placement_currency.md](15_rule_extraction_placement_currency.md) | 규칙 적출 ② | 배치 규칙 + 통화 5종(코스트·쿨다운 2·유출·게이지) — 커맨드 검증이 sim 안에서 닫힌다 |
| [16_rule_extraction_dreamcatcher.md](16_rule_extraction_dreamcatcher.md) | 규칙 적출 ③ | 드림캐쳐 덱 소유권 + 카드 5단계→원자 트랜잭션(롤백 경로 소멸) |
| [17_sim_lib_skeleton.md](17_sim_lib_skeleton.md) | asmdef 격리 | `Wassup.Sim` 골격(UnityEngine 참조 = 컴파일 에러) + conform 유틸 이주 |
| [m1_unit18_plan.md](m1_unit18_plan.md) | **실행 계획** | unit 18 조각 배정(A~L) · 증인 명세 · 12세션 분할 · 중단 기준. **18 착수 전 필독** |
| [18_context_port.md](18_context_port.md) | 이식 본체 | 맥락 4단계(Units→Movement→Effects→Combat) — 청사진 ③ 을 코드로, 단계별 골든 대조 |
| [19_clock_policy_commandlog.md](19_clock_policy_commandlog.md) | 시계·로그 | UI 슬로모 처분(골든 재생성 유일 지점) + 커맨드로그 기록 개시 |
| [20_ab_parity_swap.md](20_ab_parity_swap.md) | M1 종료 | A/B parity + ARM64 IL2CPP 성능 게이트 + RTT 리뷰 → 스왑(구현체 1곳) |

## M0 종료 판정 (2026-08-04)

- units 0~4를 각각 독립 커밋으로 구현했다: `8795ac3c` → `3e7b33f5` → `cc04bc19` → `11902d32` → `c0f7bd4f`.
- Unity 스크립트 컴파일 오류 0, 전체 EditMode **1,888건 중 실패 0**(1,886 통과, 기존 Ignore 2), `LegacyTrace` 집중 테스트 **5/5**, CardBuff PlayMode **1/1**을 확인했다.
- 7개 골든 시나리오를 각각 새 Play 세션에서 2회 실행해 JSON byte diff **0**을 확인했다.
- ECS 변경 리뷰는 Track A common과 Track B `$ecs-reviewer` 모두 **APPROVE**, 더 엄격한 최종 판정도 **APPROVE**다.
- M1은 이 기준선 위에서 unit 7부터 새로 분해한다. 상세 spec이 작성되기 전에는 adapter/sim-lib 이식을 시작하지 않는다.

### 선행 머지 + unit 12 이후 기준선 재확인 (2026-08-04 23:15)

M0 골든은 **12:44 에 녹음**됐고 그 뒤 M1 의 선행 머지 3건과 unit 12 가 들어왔다. 그중 sim 을
건드린 것은 **머지 2(`c0a361cb`, 18:22) 하나**뿐이라 골든이 그것을 담고 있지 않았다. HEAD
(`229ccd00`)에서 코퍼스를 재생성해 확인했다:

- 7 시나리오 × 2회 = 14 Play 세션, 전 시나리오 **two-run diff 0** → 승격 통과
  (승격은 `exitCode == 0` 에서만 일어나고 `PublishValidatedCorpus` 가 승격 직전 재검사한다).
- 재생성된 7개가 커밋본과 **byte 동일**(git clean + 백업 대비 `cmp` 7/7 IDENTICAL).
- ⇒ **머지 2 의 "행동 변화 0" 주장이 실측으로 증명**됐고, 머지 1·3·unit 12 도 중립임이 확인됐다.
  units 13~18 의 byte-diff 게이트는 이제 **초록 기준선 위에서** 의미를 갖는다.

> 골든은 뷰 상태를 기록하지 않는다 — 머지 1(뷰 상수 이관)의 **렌더 정합**은 이 초록이 답하지
> 않는다. 다만 14판이 Play 에서 신규 콘솔 에러 0 으로 완주했으므로 스모크 게이트의 "에러 0"
> 부분은 충족된다. 남은 것은 눈으로 보는 확인뿐이다.

## Feature-wide 계약

- **sim은 이식 가능한 순수 관리 C# 소스**(Burst-off)로 유지 — 특정 런타임 가정 금지. 클라는 Android IL2CPP, 검증 러너는 CoreCLR이므로 교차 실행이 전제. 교차 골든(Editor/IL2CPP/CoreCLR)은 M1 게이트.
- **정본 이원화**: 리플레이 정본 = 이벤트 스트림(AMR) — 클라 결정론 불요. 무결성 정본 = 커맨드로그 — M3 전까지 재시뮬 스팟체크는 advisory flag만(자동 판정 금지).
- **이벤트 3분리**: ① 내부 phase queue(같은 틱 소비 계약, 직렬화 안 함) ② authoritative semantic AMR ③ presentation projection. 27채널의 단일 스트림 붕괴 금지.
- **stable ID**: 매치 내 비재사용 `SimEntityId`(spawnOrdinal)가 타겟팅 동률·RNG seed·커맨드·이벤트·스냅샷·뷰 키의 유일 축. `Entity.Index/Version` 사용 금지(unit 1 이후).
- **parity 기준**: 커맨드 receipt·semantic 이벤트·틱별 read model·최종 상태+RNG 해시·점수(int)는 **exact**, 연속 물리값만 epsilon. 동률 지점 예외는 unit 4에 명문.
- **틱 페이즈 순서의 정본은 unit 0의 캡처 결과**다. 스케치·기억이 아니라 덤프가 이긴다 (예: CC 감쇠는 이동 **후** — 현행 `CcDecaySystem [UpdateAfter(MovementSystem)]`).
- **골든 오염 방어**: 하네스는 LoginAutoImport 차단 + configHash 동봉. 골든 diff 발생 시 "시트 드리프트 vs 코드 회귀"를 configHash로 먼저 가른다.
- **콘텐츠 동결 정책(기본값 채택)**: M1 이식 개시 후 신규 콘텐츠는 신 lib에만, 구 sim 조기 프리즈, parity 범위는 동결 시점 스냅샷 고정.

## CLAUDE.md 제약 이행 (2026-08-03 사용자 방향)

산출물은 서버권위(M3) **직전까지** — ECS 를 제거하고 sim lib + Mono 프레젠테이션으로 완전 플레이 가능한 클라 단독 상태가 이 spec 의 종착점이다. 그 과정에서 CLAUDE.md 의 ECS 시대 절대 제약은 "일괄 해제"가 아니라 **구간별 대체**다. 정식 CLAUDE.md 개정은 M1 진입 커밋에서 수행하며, 그 전까지는 아래 표가 효력 기준이다.

| CLAUDE.md 절대 제약 | M0 (현행 ECS 위) | M1+ (신 sim lib) |
|---|---|---|
| 1. BattleBridge 유일 창구 | **유지** | `IMatchSession` 파사드로 대체 → 최종 불변식은 **asmdef 의존 방향**(sim 의 UnityEngine 참조 = 컴파일 에러). Bridge 는 해체가 목적지(ADR D4) |
| 2. 맥락 쓰기 소유권(Units/Movement/Combat/Effects) | **유지** | sim lib 내부 모듈 경계로 승계 — 형태는 M1 백지 청사진이 결정 |
| 3. SubScene 금지 · ISystem 우선 | **유지** | ECS 소멸과 함께 자연 소멸 |
| 4. Authoring/Runtime 분리 | **유지** | 유지 — SO 저작 → `MatchConfig` 물질화로 오히려 강화 |
| 5~10. 매니저 절제 · 하드코딩 금지 · 상속 2단 · 인터페이스 절제 · 스코프 엄수 · 순수 함수 분리 | **유지** | **유지** — 아키텍처 중립 품질 규율. 특히 제약 10(순수 함수)은 sim lib 의 제1원칙으로 승격 |

- "모노에 맞는 설계" ≠ MonoBehaviour-per-unit 시뮬(ADR D1 기각 유지). Mono 관용구가 맞는 곳은 프레젠테이션 계층뿐이다.
- M0 는 라이브 경로 행동 변화 0 이 계약이므로, 위 제약이 전부 살아 있는 구간이다.

## 파이프라인 커버리지

**N/A** — M0 구간은 플레이 오브젝트 신설·생성→렌더 경로 변경이 없다(순서 핀·ID 컴포넌트 추가·하네스/기록 계층만). M1 스왑 unit에서 `docs/reference/object-pipeline-map.md` 대조를 재수행한다.

## 후속 후보

- **M1 units**: 백지 청사진 3장(세션 계약/데이터 대응표/틱 파이프라인) · salvage 판정표(모듈 단위 ~60건) · `LegacyMatchSessionAdapter`(유일 drain 소유자) · 소비자 82파일 재배선 · Bridge 상주 매치 규칙 적출(웨이브·승패·코스트·점수·드림캐쳐) · sim lib 이식(맥락 4 + `RequireForUpdate` 35개 이식 매트릭스) · 다단계 카드 트랜잭션의 원자 커맨드화 · pause/slow-mo gameplay 시계 정책 · Burst 상실 성능 게이트(ARM64 IL2CPP p95/p99) · A/B 스왑.
- **M2 units**: 헤드리스 dotnet 러너 CI · AMR 녹화 · ReplaySession(seek) · 커맨드로그 재시뮬 배치 잡(advisory) · 스키마 upcaster + 구버전 리플레이 코퍼스 CI · Entities 패키지 물리 제거.
- **`BattleBridge` 해체 (소유자 없음 — 등재 2026-08-05)**: ADR **D4** 가 *"해체가 목적지"* 를
  **채택**했는데(기각된 것은 "표면 동결 후 이면만 교체") **units 18~20 도 M2 백로그도 이것을
  소유하지 않는다.** D4 가 순서를 역전시켜(해체보다 seam 먼저) 뒤로 민 자리가 비어 있다.
  - 왜 자동으로 안 풀리나: 18~20 이 걷어가는 건 **sim 지분뿐**이다. 실측(10,049줄) —
    `_em.` 421 · `SystemAPI`/`World`/`EntityQuery` 59 는 사라지지만, `SerializeField` 89 ·
    `Presenter`/`View`/`Pool` 179 · `Debug.Log` 143 은 **잔존**한다. 파일은 얇아져도
    **겸직 구조는 그대로**이고, 그게 D4 가 경계한 "갓-매니저를 새 아키텍처에 계승" 이다.
  - 왜 지금 계획하지 않나: 무엇이 남을지 모르는 상태에서 경계를 그으면 D4 가 순서를 역전시킨
    이유와 같은 실수다. **M1 종료 후 별도 spec** 이 맞다.
  - 축은 코드가 이미 알려준다: 파셜이 `Dreamcatcher`/`LegacyTrace`/`BossLeap`/`Relocation`/
    `UltimateLeap`/`UnitStats` 로 갈려 있고, 남는 지분은 대부분 `Scripts/Presentation/` 성격이다.
- **M3 units**: RemoteSession · 서버 스택 결정(Unity headless vs 자체) · 재접속(스냅샷+백로그 exactly-once) · suspend/resume · 점수 발급 서버 이관.
- **미채택 보류**: lag compensation(RTT 매트릭스 리뷰에서 실패 스킬이 나오면 재론).
- **골든 코퍼스의 사각지대 2개** (코퍼스 변경 = unit 19 권한이라 미조치):
  ① 하네스가 Bridge API 를 직접 불러 **뷰→커맨드 경로를 우회**한다 → bundle C 의 검출기는 PlayMode 다.
  ② 코퍼스는 **draft 경로**를 녹음하고 거기서 `_skillLoadout` 은 null 이라 **로드아웃 결정론을
  증인할 수 없다**(회귀 방지는 `MatchSeedTests`·`SkillLoadoutControllerTests` 6건이 진다).
  ③ **배치 쿨타임도 증인이 없다** — 하네스는 유닛 타입마다 1회만 배치하고 쿨타임은 정규 상태
  라인에도 없다(그래서 `PlacementCooldownGateTests` 4건이 유일한 증인 — unit 15-A).
- **PlayMode 스위트 수리 (이 spec 범위 밖 · 별도 spec 후보)**: 2026-08-04 전체 실행 결과
  `passed=76 failed=15`. **이 spec 이 만든 파손이 아니다** — 골든 7종이 HEAD 에서 byte 동일하고,
  실패는 이 spec 이 건드리지 않은 경로에 있다. 진단된 것:
  - `AuthE2ETest` — 개발 DB 의 `uk_users_user_name` 중복(`user_name=e2e-test` 선점). 환경 상태.
  - ~~`DropDismountTest`~~ — **수리됨**(unit 13-C1, `18ed2315`). `fe53bd45`(drop-dismount unit 1)가
    `_defenderViewOverride` 값을 튜플로 바꿨는데 테스트의 리플렉션 헬퍼가 `float3` 캐스팅을 유지해
    `InvalidCastException` 이 났다. bundle C 의 **유일한 자동 검출기**라 C2 진행 전에 먼저 고쳤다.
  - ~~`NextWaveClearAttentionSmokeTest`~~ — **수리됨**(unit 14, `773e57b2`). 위 목록에 없었지만
    unit 13-A1 이후 줄곧 깨져 있었다: `NextWaveDock` 이 `bridge.X` 직독에서 세션 스냅샷 폴링으로
    옮겨갔는데 이 픽스처는 `BeginPlacement` 를 거치지 않아 세션이 무장되지 않고, 도크가 `IsActive`
    게이트에서 조기 return 해 **브리지 상태가 맞는데도 CTA 강조가 안 켜졌다**. A1 검증이
    EditMode+골든만 봤고 이 PlayMode 그룹을 돌리지 않아 놓쳤다 — **재배선 unit 은 그 뷰를 덮는
    PlayMode 군을 함께 돌려야 한다**는 교훈.
  - `BountyMarkTest` · `DreamstoneCarryInSmokeTest` ×2 — PrimeTween `EmergencyStop` 의
    "OnComplete ignored" 에러 로그를 `LogAssert.Expect` 하지 않음. 테스트 위생.
  - ~~`PlacementAuraTest` ×3~~ — **수리됨**(2026-08-05). 이분 탐색은 필요 없었다: 슬롯을 덤프하니
    범인이 이름을 댔다 — `origin=Tile stat=AttackSpeedMul op=Multiplicative mag=1.2`. **맵의
    EffectTile** 이다. `PlaceFirstValid` 가 `(-24,-24)` 부터 훑어 첫 배치 가능 칸에 놓는데 그게
    공속 버프 타일이라, 오라와 무관하게 ×1.2 가 곱해졌다(1.0→1.2 · 1.5→1.8).
    오라 자체는 내내 정상이었다(`origin=Dreamcatcher op=Additive mag=0.5`) — **제품 버그가 아니라
    테스트가 맵을 고려하지 않은 것**이고, 그래서 코드 무변에도 실패가 재현됐다.
    수리는 효과 타일 회피 배치. 교훈 2개:
    ① 절대 스탯을 단정하는 배치 테스트는 **효과 타일을 피해 놓아야 한다**(같은 스캔 패턴을
    쓰는 PlayMode 파일이 33개 — 스탯을 안 보는 테스트는 무해하다).
    ② 원인이 "코드가 안 변했는데 실패" 로 보이면 이분 탐색보다 **상태를 덤프**하는 것이 빠르다.
    부수 관찰: 매치 기믹은 `MatchSeed` 파생으로 **매 실행 랜덤 배정**된다(G1~G4). 이 테스트는
    `StartBattle` 을 안 해서 기믹 시스템이 틱하지 않아 무해했지만, 전투를 도는 PlayMode 테스트가
    스탯을 단정한다면 그 축을 고정해야 한다.
  - 나머지(`DragCancelZoneTest` · `DreamcatcherAttachRequirementE2ETest` ·
    `DreamcatcherCursedRelicTest` · `DreamCocoonTest` · `DreamcatcherDeckCarryInTest` ·
    `SceneTransitionSmokeTest` · `SquadCarryInSmokeTest`) — 미진단.
  - 주의: 전체 실행에서는 오라 수치에 ×1.012 가 **추가로** 붙었다(단독은 ×1.2 만).
    `TestModeContext.RuntimeImportsBlocked` 는 정적이고 PlayMode 는 테스트 간 도메인 리로드를
    하지 않으므로, 스위트에는 **테스트 간 상태 누출** 축이 하나 더 있다는 신호다.
