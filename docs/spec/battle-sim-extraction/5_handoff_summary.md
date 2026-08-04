# 5 — Handoff: M0 완료 → M1 설계 세션

> 2026-08-04 M0 종료 스냅샷이다. M1+ 작업 단위는 7번부터 이어 쓴다.

## 상태

- **M0 units 0~4 구현·검증·리뷰 완료.** 현행 ECS sim 위에 결정론 기준선과 `LegacyTraceV0` 골든이 고정됐다.
- 다음 단계는 구현이 아니라 **M1 상세 spec 분해**다. `IMatchSession` adapter나 순수 C# sim lib 이식을 unit 문서 없이 시작하지 않는다.
- 읽는 순서: ① `README.md` ② `docs/plans/2026-08-03-battle-sim-extraction-design.md`의 M1 절 ③ `6_decision_record.md` ④ units 0~4와 `order-capture.md`.

## 완료 커밋

| 범위 | 커밋 |
|---|---|
| 설계 정정 문서 | `a8c8a862` |
| unit 0 — 시스템 순서 캡처·핀 | `8795ac3c` |
| unit 1 — `SimEntityId` | `3e7b33f5` |
| unit 2 — `StepOneTick` 하네스 | `cc04bc19` |
| unit 3 — canonical `MatchConfig` | `11902d32` |
| unit 4 — `LegacyTraceV0` 골든 | `c0f7bd4f` |

## 구현된 기준선

- `BattleSimGroup` 44개 시스템의 유효 총순서를 캡처하고 미선언 순서 13건을 현행 순서 그대로 핀했다.
- 매치 내 비재사용 `SimEntityId`를 스폰 7경로에 부착하고 타겟팅 동률·발사 RNG·ThreatTable의 결정 축을 교체했다.
- `StepOneTick`이 입력, 배틀 시계, Bridge drain, ECS sim을 고정 tick으로 자가 구동한다. 라이브 PlayerLoop 경로와 하네스 경로는 상호 배타다.
- canonical `MatchConfig`와 `configHash`가 생성 맵·웨이브·덱·스탯·gameplay knob를 고정하고, 하네스 중 `LoginAutoImport` 오염을 차단한다.
- `LegacyTraceV0`가 command receipt, tick read model, Bridge 출력 이벤트, 점수와 최종 상태 해시를 직렬화 왕복 후 기록한다.
- 운영 27채널 중 Bridge 출력 18개만 trace event stream에 넣고, 같은 틱 내부 전달용 9개는 `internalPhaseChannels`로 명시 제외했다.
- 7개 시나리오 골든은 `Assets/_Project/Tests/Golden/LegacyTraceV0/`에 추적된다.

## 검증 증거

- Unity 스크립트 컴파일 오류 **0**.
- 전체 EditMode **1,888건**: 1,886 통과, 실패 0, 기존 Ignore 2.
- 집중 `LegacyTrace` EditMode **5/5**, CardBuff PlayMode **1/1**.
- 7개 시나리오를 각각 새 Play 세션에서 2회 실행해 JSON byte diff **0**.
- 종료 로그의 `NullReferenceException`, Persistent allocator/Native Collection leak **0**.
- Track A common review **APPROVE**, Track B `$ecs-reviewer` **APPROVE**, 최종 **APPROVE**.

## M1에서 보존할 계약

1. 목적지는 MonoBehaviour-per-unit이 아니라 **엔진-프리 순수 C# tick sim + Unity 프레젠테이션**이다.
2. tick phase의 정본은 `order-capture.md`이며, 기억이나 설계 스케치로 순서를 재구성하지 않는다.
3. `SimEntityId`가 커맨드·이벤트·스냅샷·뷰 키의 유일 축이다. `Entity.Index/Version`을 새 sim 계약에 노출하지 않는다.
4. 내부 phase queue, authoritative semantic event, presentation projection의 3분리를 유지한다.
5. parity는 receipt·semantic event·read model·점수·최종 상태/RNG hash를 exact로 비교한다. epsilon은 연속값에만 적용한다.
6. `LegacyMatchSessionAdapter`가 ECS 채널의 유일 drain 소유자가 되어야 한다. 관찰용 소비자 추가로 라이브 순서를 바꾸지 않는다.
7. Android IL2CPP와 CoreCLR 교차 실행을 전제로 순수 관리 C#을 유지한다.

## 다음 작업

- 설계 정본 M1 절을 unit 7+로 분해한다: 세션 계약, 데이터 대응표, tick 파이프라인, adapter/소비자 재배선, sim 이식, A/B 스왑과 성능 게이트.
- M1 A/B runner에 연속값 epsilon 비교기와 동률 예외 전용 로그를 추가한다.
- 스왑 전 Android ARM64 IL2CPP 피크 웨이브 soak에서 tick p95/p99와 steady-state GC를 측정한다.
- M1 구조 변경 시 `docs/reference/object-pipeline-map.md`를 다시 대조한다. M0는 플레이 오브젝트 생성·렌더 경로 변경이 없어 N/A였다.

## 잔여 위험

- unit 4 종료 검증은 집중 PlayMode와 7개 골든 러너까지다. 전체 PlayMode suite와 Player build는 이번 unit에서 실행하지 않았다.
- M0 결정론은 하네스 모드 계약이다. 라이브 fixed tick 상시화, pause/slow-mo 정책, Burst 제거 성능은 M1 책임이다.
- 콘텐츠 동결과 lag compensation 미채택은 현 기본값이다. 재론 조건은 `6_decision_record.md`를 따른다.
