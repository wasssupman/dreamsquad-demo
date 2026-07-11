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

- [ ] PlayMode smoke 및 관련 기존 테스트 통과.
- [ ] 위 상태·데이터·상호작용 행렬 전부 통과, 콘솔 에러 0.
- [ ] 16:9/20:9에서 safe edge 침범·이름 겹침·rail 분리 없음.
- [ ] 현행 대비 하단 최고 가림선 감소와 비용 판독 개선을 전/후 캡처로 확인.
- [ ] Android에서 7슬롯 미스그랩/중앙 reach 문제를 3분 플레이로 확인.
- [ ] 사용자 확인 후 README 완료 처리, 커밋 해시와 검증 결과를 handoff에 기록.
