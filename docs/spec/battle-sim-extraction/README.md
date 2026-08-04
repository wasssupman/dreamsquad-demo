# battle-sim-extraction — 전투 시뮬의 엔진-프리 라이브러리화 (ECS 제거)

상태: **M0 units 0·1·2 구현·자동 검증 완료 (2026-08-04) · unit 3 착수 전 · 교차검증 4트랙(35건) 사실 정정 반영 2026-08-03**

설계 정본: [`docs/plans/2026-08-03-battle-sim-extraction-design.md`](../../plans/2026-08-03-battle-sim-extraction-design.md) (v6 — Claude critic 2트랙 + ECS 시맨틱 감사 6트랙 + Codex 적대 리뷰 수렴). 이 README는 그 계획의 실행 인덱스다. 근거·감사 상세는 설계 문서를 읽는다.

## 상위 목표

전투 ECS를 완전히 제거하되, 목적지는 "Mono 게임"이 아니라 **엔진-프리 순수 C# 시뮬 라이브러리 + Unity 프레젠테이션 클라이언트**다. **이 spec의 산출물(M0~M2)은 서버 없이 완전 구동되는 클라 단독 프로젝트**이며(sim lib을 `LocalSession`으로 인프로세스 내장), 서버권위는 이 spec이 열어두는 후속 옵션(M3)이다. "서버 가정"의 실체는 런타임이 아니라 설계 규율이다.

마일스톤 지도: **M0** 결정론 수복+골든 하네스(units 0~4, 이 문서) → **M1** seam 선행 적출(IMatchSession 파사드→소비자 재배선→sim lib 이식→스왑) → **M2** 스트림 정본화(헤드리스 러너·AMR·ReplaySession) → **M3** 토폴로지 전환(RemoteSession·서버). M1+ 작업 단위는 M0 완료 후 뒤 번호로 이어 쓴다.

## 작업 단위 목록 (M0)

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [0_system_order_capture.md](0_system_order_capture.md) | 순서 박제 | 유효 시스템 총순서 덤프 + 미선언 순서 어트리뷰트 핀 |
| [1_sim_entity_id.md](1_sim_entity_id.md) | stable ID | `SimEntityId` 도입, 타겟팅 동률·발사 RNG seed 축 교체 |
| [2_fixed_step_harness_driver.md](2_fixed_step_harness_driver.md) | 시간 결정론 | 하네스 모드 `StepOneTick` 드라이버 + 입력 sim-tick 스케줄 주입 |
| [3_canonical_match_config.md](3_canonical_match_config.md) | 조건 물질화 | MatchConfig blob + `configHash` + LoginAutoImport 차단 |
| [4_legacy_trace_golden.md](4_legacy_trace_golden.md) | 골든 하네스 | `LegacyTraceV0` 기록·직렬화 왕복·seed 코퍼스·parity 기준 확정 |
| [5_handoff_summary.md](5_handoff_summary.md) | 인계 문서 | 클론 세션용 헛발 방지 + 레포 함정 |
| [6_decision_record.md](6_decision_record.md) | 결정 기록 | 기각 대안·자기 철회·재론 조건 (ADR) — 재론 전 필독 |

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
- **M3 units**: RemoteSession · 서버 스택 결정(Unity headless vs 자체) · 재접속(스냅샷+백로그 exactly-once) · suspend/resume · 점수 발급 서버 이관.
- **미채택 보류**: lag compensation(RTT 매트릭스 리뷰에서 실패 스킬이 나오면 재론).
