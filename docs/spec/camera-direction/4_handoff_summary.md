# 4 — Handoff Summary

## Commit

- `9ddd4829` docs: spec (README + unit 0~3, Codex 설계 리뷰 8건 반영)
- `d769bac3` unit 0: CameraDirector 토대 — 포즈 단일 소유 + 킥 채널 흡수
- `c86eff33` unit 1: 페이즈 전환 비행 (SO 포즈 델타 + 보간)
- `fb078a3f` unit 2: 배틀 구두점 (줌 펄스 + 킬 스트릭 셰이크)
- `2fe2e000` unit 3: 앰비언트 브리딩
- `aaa13668` unit 5: 드래그 포커스 (스와이프 줌인 + 방향 lookat 리드)
- `b2d9ac57` unit 5 rev1: 룩앳 타겟 포인터화 + 줌 강도 하향
- 이번 마무리 커밋: unit 5 rev3 — 스크린 입력 기반 포커스와 사용자 Play 확인

## Implemented

- `CameraDirector`(Main Camera, order **-90**)가 매 LateUpdate 절대 합성: 홈(씬 authored, Awake 캡처) ⊕ 페이즈 비행 ⊕ 구두점 ⊕ 브리딩 ⊕ 킥. 아이들 프레임은 settle-once no-op.
- `CameraImpactKick` 은퇴 — 킥은 Director 채널(`Kick()`), 호출처 `DreamcatcherHandView` 마이그레이션(AddComponent fallback 금지, miss 캐시).
- 페이즈 비행: 등록 페이즈만 이동, 미등록=hold, 최초 적용 스냅은 Start 에서 소진. 포즈: Draft(pitch-8/z-1.5) Placement(-5/-0.8) Battle(홈) Result(z+1/fov-2), 커브 폴백 smoothstep.
- 구두점: TileAoe 착탄(faction-blind) → `ZoomPulse`(max-hold, 타이머는 비행 중에도 실시간 감쇠), `ScoreHudView` 킬 heat → 결정론 sin 셰이크. 비행 중 가중치 0. 최종 FOV [30,60] 클램프.
- 브리딩: 파동 3개(7.3/9.7/12.1s) 위상 누적기, Draft/Placement/Battle 만, 크로스페이드 1.5s.
- 모든 수치 `CameraDirectionConfig.asset` (Play 중 실시간 튜닝 가능).
- 드래그 포커스 rev3: 월드 유닛/고리 대신 포인터 스크린 좌표를 Director에 공급한다. Director 내부 NDC 스프링 추종의 속도로 스와이프 리드를 만들므로, 카메라 포즈가 월드 타겟 재계산에 되먹임되지 않는다.

## Key Files

- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — 채널 상태·합성 루프
- `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — 순수 함수(EditMode 테스트 대상)
- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` + `Assets/_Project/Data/Camera/CameraDirectionConfig.asset`
- 배선: `BattleBridge.DrainProjectileHitEvents`(펄스), `ScoreHudView.PushShakeHeat`(셰이크), `DreamcatcherHandView`(킥)

## Verified

- EditMode 780개 통과(신규 CameraComposeMathTests 포함), 컴파일/콘솔 클린.
- Play(execute_code 상태 관측): Draft 스냅 포즈 정확, 비행 중간값 smoothstep 일치, 비행 중 재전환 연속, Result 착지 정확·브리딩 off·완전 정지(settle), 킥/펄스/셰이크 발동·감쇠·홈 복귀 정확, 비행 중 펄스 실시간 감쇠.
- 사용자 Play 확인(2026-07-14): 드래그 포커스의 줌인·방향 리드·복귀와 호버/드롭 좌표감 통과.

## Notes (되돌리면 안 되는 의도)

- `ApplyTilemapCameraPreset` 재활성 금지(양쪽에 은퇴 마커) — Director 가 포즈 유일 소유.
- 킥/구두점 축은 **홈 기준** — 페이즈 pitch 크게 튜닝 시 체감 확인(1_phase_flight.md 주의 항목).
- 펄스 타이머는 비행 가중치와 무관하게 실시간 감쇠(지연 펄스 금지). 아이들 진입 시 구두점 가중치 목표 스냅.
- heat 소유는 ScoreHudView(산정·감쇠·panel off 시 0 푸시), Director 는 미러 소비(지연 ≤1프레임 계약).
- Play 중 SO에 브리딩 파동 **추가**는 무시됨(위상 누적기 Awake 고정) — 값 수정/축소만 라이브.
- **드래그 포커스는 스크린 좌표 입력만 허용** — 월드 유닛/고리 입력은 카메라 포즈에 의존해 되먹임을 만든다. `lookWeight` 0.5 캡은 풀 lookat으로 배치 좌표감이 훼손되는 것을 막는다.
- 드래그 포커스는 명시 해제 없음 — 컨트롤러 피드가 끊기면 staleness(2프레임)로 자동 페이드아웃.

## Follow-up

- 후속 후보(README): 단일 타격 헤비 히트 펄스(이벤트 데이터 필요), 매치포인트 긴장 줌, Gift 카메라, 드래그 중 pitch 반응.
