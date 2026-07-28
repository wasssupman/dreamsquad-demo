# defender-drop-dismount

> 상태: **완료 2026-07-28** — unit 0~5 구현 + Play 육안 확인(사용자). 커밋 `ad886013`~`35bb5642`. 인계는 `6_handoff_summary.md`
> 선행 의존: 배치 셀 판정 손가락 기준 수정(`DefenderDragPlacementController._fingerBoardWorld`, 2026-07-28 작업, 커밋 대기 중)이 먼저 커밋되어야 한다. 이 spec 은 그 수정으로 생긴 "매달린 유닛 ↔ 판정 타일" 간격을 연출로 메운다.

## 목표

드래그 릴리스 시 매달려 있던 고스트 위치에서 확정 타일까지 **체조선수 하마(下馬) 모션**(반동→솟음→착지)으로 실제 유닛이 날아가 정착한다. 현재는 고스트가 즉시 파괴되고 실유닛이 타일에 팝업 — 2.2~3.1타일(현 튜닝) 순간이동으로 보인다.

검증 질문: **릴리스 순간부터 착지까지 유닛이 한 몸으로 이어져 보이는가? (팝 프레임 0)**

## 모션 스펙 (사용자 확정)

- 총 0.45s = 반동 0.12s + 솟음·착지 0.33s. 시계는 unscaled(배치 조작의 연장 — 전투 슬로우모 무관).
- 반동: 줄이 벙은 채 -camUp 으로 dip(잔여 스윙 속도를 Hermite 로 흡수 — 빠른 플릭일수록 반동 큼).
- 솟음: 분리 후 camUp 아치. apex 는 **절대 하한**(H_min ≈ 유닛 키 1.1~1.3배) — 거리비례만이면 짧은 드롭이 납작해짐.
- 착지: 끝접선 수직(-camUp 지배) = 스틱 착지. 착지 프레임에 타일 팝 + 스폰 연출.
- 고리·줄: 반동 동안 유닛과 연결 유지 → 분리 순간 스냅 → 놓은 자리에서 페이드아웃.

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 순수 수학 | `0_dismount_arc_math.md` | KeyringSim.DismountArc(Hermite 반동 + 수직착지 아치) + EditMode 테스트 |
| 1 | 계약 준비 | `1_view_override_and_knobs.md` | 뷰 오버라이드 이름 중립화 + DragSwaySettings ⑩ 드롭 노브 |
| 2 | 비행 구동 | `2_drop_dismount_flight.md` | 커밋 직후 실유닛 뷰 오버라이드 비행 + 핸드오프 + 안전망 |
| 3 | 연출 재배열 | `3_landing_presentation.md` | 스폰 연출(링·PlayDeploy·팝)을 착지 프레임으로, 활성화 시계는 commit 기준 유지 |
| 4 | 잔류 페이드 | `4_ring_cord_remnant.md` | 고리+줄 detach 잔류 → 분리 스냅 → 페이드 |
| 5 | 검증 | `5_playmode_verification.md` | 핸드오프 이격·활성화 타이밍·경로 게이트 PlayMode 테스트 |

## feature-wide 계약

1. **적용 범위 = 실드래그 릴리스만** (`EndDrag`→`CommitPlacementAt`, `!_simulatedDrag` 게이트). 탭 배치·armed 보드드래그(`RunSimulatedDrag`)·재배치는 무변경.
2. **커밋 타이밍 불변**: 점유·코스트·`PlacementCommitted`·튜토리얼/기믹 이벤트 전부 오늘과 같은 릴리스 프레임. dismount 는 순수 뷰.
3. **드롭 지속시간 ≤ `deploymentDuration` 런타임 클램프** (현재 전 유닛 0.45s). dismount 창 ⊆ pending 창 → 공중 유닛은 구조적으로 공격·피격·재배치 진입(`busy` 가드) 불가. 노브 우연에 기대지 않는 코드 계약.
4. **활성화 시계는 commit 기준 유지** (`commit + deploymentDuration + placementSkillDelay`). 밸런스 무변경. 착지 프레임 ≈ 활성화 프레임.
5. **핸드오프 연속성**: 변환·상수 없이 **양 끝점을 각 렌더러 실좌표로 캡처** — 시작 = 커밋 프레임 `_unitPosWorld` 그대로, 끝 = 정상 피드 공식 미러(`TryGetDefenderRestViewPos`). 이격 > 0.05 world 팝 금지(unit 5 단정). 시작 속도 = `_unitVelWorld` 캡처(반동 접선).
6. **튜닝 독립**: 시작점·거리·유닛키는 전부 라이브 캡처 — `ropeLength`/`spring`/유닛별 `unitHeight` 튜닝이 자동 반영된다. 결합은 하나: 시간이 pending 창에 캡이므로 **드롭 속도 ∝ totalDrop** (ropeLength ↑ = 빨라짐, 길어지지 않음).
7. **세션 독립**: dismount 는 `_session`/`_sessionGen` 과 분리(자체 gen + 시작 시 plain 값 캡처). 비행 중 새 드래그 시작해도 이전 dismount 지속. entity 별 오버라이드라 다중 비행 공존.
8. **방향 지정 유닛 병행** (사용자 결정 2026-07-28): `RequiresFacing` 도 dismount 발동, aim 페이즈와 병행(aim=셀 기준·Battle 슬로모, dismount=뷰·unscaled — 무충돌).
9. **중단 안전망**: 프레임별 binding check(`_defenderByTile` 셀↔entity) 실패 시 abandon(오버라이드 clear), 컨트롤러 OnDisable/OnDestroy 시 즉시 완결 — 재배치 `FinishFlightInstant` 패턴 미러.
10. 모든 수치는 `DragSwaySettings` ⑩ 그룹 SO 노브 (하드코딩 금지).

## 파이프라인 커버리지

새 아키타입 없음 — 기존 방어 유닛의 생성→렌더 정거장 구조 무변경. 해당 정거장 대조:

| 정거장 | 상태 |
|---|---|
| 스폰(TryBeginDefenderDeployment→Pending) | 무변경 (commit 프레임 그대로) |
| 뷰 위치 피드(SyncMonoUnitViews) | **기존 정거장 재사용** — 재배치가 만든 view override 경로에 두 번째 소비처(드롭) 추가. 구조 신설 아님 |
| 잔류 고리·줄 | N/A — 플레이 오브젝트 아님. 기존 드래그 프리뷰 서브트리의 detach 후 자멸하는 순수 프레젠테이션 오브젝트(수명 <1s) |

## 재사용 포인터

- 궤적 수학: `Assets/_Project/Scripts/UI/KeyringSim.cs` (`ThrowArcControls`/`CubicBezier` — unit 0 이 같은 파일에 추가)
- 뷰 오버라이드: `BattleBridge.Relocation.cs` `SetRelocationViewOverride`(unit 1 에서 중립화) + `SyncMonoUnitViews` 소비
- 즉시 완결 안전망: `DefenderRelocationController.FinishFlightInstant`
- 커밋 꼬리: `DefenderDragPlacementController.CommitPlacementAt` / `RunDeployment`

## 후속 후보 (현 스코프 밖)

- 착지 임팩트(스쿼시·먼지 퍼프)를 탭 배치·재배치 착지와 공유 모듈로 통일
- 착지 사운드 / 반동 시 줄 텐션 사운드
- 착지 스쿼시(y 0.9, 2~3프레임) — unit 3 완료 후 육안 판단
