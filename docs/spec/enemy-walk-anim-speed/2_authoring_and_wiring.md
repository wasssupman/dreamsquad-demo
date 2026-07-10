# 2 — 에셋 생성 + BattleScene 배선 + Play 튜닝 검증

## 목적

`WalkAnimSpeedStyle` 에셋을 만들어 BattleBridge 에 배선하고, Play 로 실제 걷기 정합을 육안 검증하며 파라미터를 확정한다. unit-status-fx 등과 동일하게 씬 wiring 까지가 완료(사용자 수작업 미루지 않음).

## 변경 대상

- 신규 `Assets/_Project/Data/WalkAnimSpeedStyle.asset` (또는 프로젝트 SO 관례 경로)
- `Assets/_Project/Scenes/BattleScene.unity` — BattleBridge.walkAnimSpeedStyle 슬롯 할당

## 구현

1. `manage_scriptable_object` (또는 CreateAssetMenu) 로 `WalkAnimSpeedStyle.asset` 생성. 초기값은 SO 기본값.
2. BattleScene 의 BattleBridge 컴포넌트 `walkAnimSpeedStyle` 필드에 에셋 할당 (`manage_components`/reflection 배선). 씬 저장 시 사용자 in-memory WIP 오염 주의 — 기존 lesson 준수(스냅샷/delta 격리 필요 시).
3. Play 진입(에디터 포커스 필수 — MCP Play 는 비포커스 시 frame 정지) → 적 웨이브 이동 관찰:
   - 느린 적 / 빠른 적 걷기 사이클 차이 육안 확인.
   - standoff 정지 시 애니가 minTimeScale 로 잔잔한지(또는 프리즈 원하면 minTimeScale=0).
   - 포탈 통과 시 애니 튐 없음.
   - 슬로우모/정지(TimeManager) 시 애니 동기·프리즈 정상.
4. `referenceSpeed` 를 실제 적 평균속도 근처로 맞추고 min/max/smoothing 튜닝 후 확정. (스크린샷보다 **동영상/연속 관찰**이 정합 판단에 유효 — 걷기는 시간 축 현상.)

## 완료 기준

- BattleScene 에서 BattleBridge.walkAnimSpeedStyle 할당됨.
- Play 에서 발 미끄러짐이 육안으로 명확히 감소, 느림/빠름 적이 각각 느리게/빠르게 걷음.
- standoff·포탈·슬로우모/정지 회귀 없음, console 에러 0.
- 확정 파라미터를 SO 에 저장(커밋 포함).
- 사용자 통과 확인 후 README 상태 라인 "완료" + 각 unit 완료 기준에 확인 일자/커밋 해시 추가 + handoff (`3_handoff_summary.md`).
