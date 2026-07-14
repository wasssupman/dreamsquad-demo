# camera-direction — 연출 카메라 시스템

상태: 완료 2026-07-14 (unit 0~3: d769bac3/c86eff33/fb078a3f/2fe2e000, unit 5 rev3: 71e2e8d7; 사용자 Play 확인 완료)

## 목표

지금까지 카메라 이동이 배제된 채(씬 authored 정적 포즈 + `CameraImpactKick` 킥만) 모든 컨텐츠를 만들어왔다. 이 spec은 **카메라 포즈의 런타임 단일 소유자(`CameraDirector`)를 세우고**, 그 위에 페이즈 전환 비행 · 배틀 이벤트 구두점 · 앰비언트 브리딩을 레이어로 얹는다. 순수 연출 카메라다 — 플레이어 조작(핀치 줌/팬) 없음.

## 배경 (현재 상태)

- `BattleBridge.ApplyTilemapCameraPreset()` 호출은 주석 처리됨(BattleBridge.cs:781) — **씬에 수동 배치한 Main Camera 포즈가 유일한 진실**. 퍼스펙티브(FOV 40, pitch 55°).
- 유일한 동적 레이어 = `CameraImpactKick`(self-cancel additive, `DreamcatcherHandView.EnsureCameraKick()`이 런타임 AddComponent, 호출처 1곳).
- 빌보드(유닛/프랍)는 라이브 카메라 pitch에서 자기보정 — 카메라 이동 내성 이미 있음.
- `GameManager.PhaseChanged` 이벤트 존재 (`GamePhase`: None/Draft/Gift/Placement/Battle/Result).

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토대 | `0_camera_director_foundation.md` | `CameraDirector` 신설 — 홈 포즈 캡처, 레이어 합성, 킥 채널 흡수 |
| 1 | 연출 | `1_phase_flight.md` | 페이즈 전환 시 포즈 보간 비행 (SO 데이터 구동) |
| 2 | 연출 | `2_battle_punctuation.md` | 배틀 이벤트 구두점 — 줌 펄스 + 킬 스트릭 셰이크 (additive만) |
| 3 | 연출 | `3_ambient_breathing.md` | 앰비언트 브리딩 (저진폭 상시 무빙, 비행 중 감쇠) |
| 5 | 연출 | `5_drag_focus.md` | 드래그 포커스 — 스와이프 중 유닛 줌인 + 방향 lookat 리드 |

## Feature-wide 계약

- **`CameraDirector`가 카메라 base 포즈의 유일한 쓰기 주체**다. 매 LateUpdate에 `최종 포즈 = 홈 포즈 ⊕ 페이즈 비행 ⊕ 드래그 포커스 ⊕ 구두점 ⊕ 앰비언트 ⊕ 킥`을 절대값으로 합성한다. 다른 컴포넌트의 카메라 transform 쓰기 금지.
- **실행 순서 계약**: Director는 `[DefaultExecutionOrder(-90)]` — GameManager(-100)보다 뒤(시작 페이즈 스냅 결정성), LateUpdate에서 카메라를 읽는 소비자(빌보드/데미지넘버/드래그 프리뷰, order 0)보다 항상 앞. 소비자 → Director 방향의 데이터(예: 킬 스트릭 heat)는 지연 ≤1프레임을 허용 계약으로 명시.
- **페이즈 포즈는 등록제, 미등록 = hold**: 등록된 페이즈로의 전환만 카메라를 움직이고, 미등록 페이즈 진입은 현재 델타를 유지한다(홈 복귀 아님 — Gift 등 범위 밖 페이즈에 의도치 않은 연출이 새는 것 방지).
- **최종 FOV는 config 범위로 클램프**: 페이즈 델타 + 펄스 합성 결과가 SO 튜닝만으로 위험 FOV가 되지 않도록 코드 계약으로 차단.
- **홈 포즈 = 씬 authored 포즈를 시작 시 캡처**. 씬에서 직접 카메라를 튜닝하는 기존 워크플로우가 그대로 유지된다. 페이즈별 포즈는 홈 대비 **델타**(pitch/dolly/FOV 오프셋)로 SO에 정의한다.
- **기존 `CameraImpactKick`의 self-cancel 패턴은 Director와 양립 불가** (Director가 매 프레임 절대 포즈를 쓰면 킥의 revert가 이중 차감). unit 0에서 킥을 Director의 additive 채널로 흡수하고 컴포넌트는 은퇴시킨다. `Kick(strength)` 의미는 보존.
- **카메라 탈취(지시적 이동)는 페이즈 전환 비행과 드래그 포커스(unit 5)에만 허용**. 배틀 중 구두점은 현재 프레이밍 위 additive 오프셋만 — 카메라가 이벤트 지점으로 날아가지 않는다 (보스 스폰 push-in은 브레인스토밍에서 명시적으로 제거 결정). 우선순위: 비행 > 드래그 포커스 > 구두점/브리딩.
- **시간은 unscaledDeltaTime** (기존 킥과 동일). `TimeManager`/timeScale 비의존 — 히트스톱·슬로우 중에도 카메라 연출은 실시간 진행.
- **모든 수치는 SO에서** (제약 6): 페이즈 포즈 델타, 비행 시간/커브, 펄스/셰이크/브리딩 진폭 전부 `CameraDirectionConfig` (+ 페이즈 포즈 테이블).
- **아키텍처 중립 계산은 순수 함수로** (제약 10): envelope 평가, 포즈 델타 합성, 이징 보간은 plain in/out static 함수 + EditMode 테스트.
- **스크린→월드 의존 코드는 이미 라이브 카메라 기준** (`TryScreenToCell` 등) — 카메라 이동과 자동 정합. 단 페이즈 비행 중 배치 입력은 짧은 전환 구간이라 별도 잠금을 두지 않는다(문제 관측 시 후속).

## 검증 방향

- 순수 합성/envelope 함수 EditMode 단위 테스트.
- Play smoke: UnityMCP 스크립트 배틀 e2e(TestModeContext)로 페이즈 전환 비행/구두점 발동을 조건 기반 스크린샷으로 확인.
- 최종 체감(속도감/멀미)은 사용자 실기·에디터 Play 확인.

## 후속 후보 (이번 범위 밖)

- 응축된 일격 등 **단일 타격 헤비 히트 줌 펄스** — 히트 이벤트에 배율(heavy) 정보가 없어 이벤트 데이터 확장 필요 (unit 2 는 TileAoe 광역 착탄만, faction-blind)
- 마지막 웨이브/매치포인트 지속 긴장 줌 (구두점 채널 재사용으로 쌈)
- Gift 페이즈 진입/이탈 카메라 연출 (현 Gift 연출은 UI 레이어 중심이라 카메라 관여 재검토 필요)
- ~~드래그 배치 중 미세 pitch 반응~~ → unit 5 드래그 포커스로 승격 (2026-07-14 사용자 요청)
- 전역 셰이크 서비스 고도화 (킥 컴포넌트 주석의 원래 후속 후보 — unit 2 셰이크 채널이 사실상 승계)
- **명시 제외 결정**: 플레이어 핀치 줌/팬 (조작 기능 — 하지 않기로 결정), 보스 스폰 push-in (제거 결정)
