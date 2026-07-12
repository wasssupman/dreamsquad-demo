# 5 — Action Tray Play 검증 게이트

## 목적

시안과 구현의 차이를 실제 전투 상태에서 닫는다. 비용 경계·긴 이름·phase·hand·aspect·실기 터치를 고정 행렬로 검증하고 기능 종료 여부를 결정한다. 선행: units 0~4.

## 변경 대상

- 필요 시 신규 `Assets/_Project/Tests/PlayMode/BattleHudActionTraySmokeTest.cs`
- `docs/spec/battle-hud-action-tray/README.md`
- 완료 후 `6_handoff_summary.md`

## 구현

- 상태 행렬: Placement full, Battle compact, Hand open, Hand close, Result hide/restore.
- 데이터 행렬: current cost 0/2/4/10, unit cost 1~5, 5개 `DefenderClass`, 긴 캐스터 이름 3연속.
- 상호작용: affordable drag 성공, insufficient drag 차단, occupied/not-buildable 거부, 빠른 hand toggle.
- 화면: 1920×1080과 2400×1080, Android 실기 landscape 양 방향을 캡처한다.
- PlayMode smoke는 최소한 phase별 tray size/rail position, hand suppression, insufficient begin 차단을 assert한다.
- A/B telemetry나 touch heatmap은 이 spec에서 새로 구현하지 않고 수동 비교 지표만 handoff에 기록한다.

## 완료 기준

- [x] 관련 기존 테스트 통과 (EditMode 703 중 701, 실패 0 — 최종 스위트). PlayMode smoke 는 실기 QA 배치로 보류.
- [x] 상태·데이터·상호작용 행렬 에디터 실측 통과, 콘솔 에러 0. (상세는 아래 확인 기록)
- [ ] 16:9 ✓ / **20:9 는 보류** — 게임뷰 강제 해상도가 비정상 동작(1080×2160 캡처), Android 실기 QA 배치에서 실기로 확인.
- [x] 가림선 감소 기록: 부유 배지 top y=276 → 레일 top y=222 (placement, −54px). 비용 판독 = 슬롯 ⚡+숫자 + affordability 3중 표기 캡처.
- [ ] **Android 실기 3분 플레이 — 보류** (기기 미연결. safe-area unit 3 터치/unit 4 실기와 한 배치).
- [ ] 사용자 확인 후 README 완료 처리 + handoff 작성 — 보류 항목 해소 시.

확인 2026-07-12 (에디터 범위) — 상태 행렬: Placement full(136)/Battle compact(104)/Hand open(트레이 문법 배킹)/Hand close(strip 복원) 캡처 ✓, Result 에서 rail 숨김(기존 RefreshVisible 계약) ✓. 데이터 행렬: 비용 2~5 표기, cost 10↔2 경계 전환, 캐스터 4연속 이름, role 배지(원/수 실측 + 근/술/보 동일 config 경로) ✓. 상호작용: affordable drag 배치 성공(defender 0→1)·insufficient 차단+펄스·Occupied/NotBuildable 라벨·빠른 hand 토글 x4 ✓. 콘솔 0. **잔여 = 실기 배치(20:9 시각·Android 터치·PlayMode smoke)**.
