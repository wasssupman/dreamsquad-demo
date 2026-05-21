# 6. Play 검증 + 시각 확인

## 목적

BattleScene Play 진입 시 백드롭 + 8 EdgeProp 이 의도대로 보이는지 확인한다. 기존 매치 흐름 영향 여부도 점검.

## 변경 대상

문서/스크린샷만. 코드 변경 없음.

## 검증 절차

1. Unity Editor 에서 `BattleScene.unity` 열기.
2. `mcp__UnityMCP__read_console` 로 컴파일 에러/경고 0 확인.
3. Play 진입 (`mcp__UnityMCP__manage_editor` action `play`).
4. 매치 진입 후 첫 프레임 ~ 5초 사이 스크린샷 1장 캡처.
5. 점검:
   - [ ] 보드 뒤로 안개 낀 숲 일러스트 백드롭이 보인다 (하늘 ~ 산맥).
   - [ ] 보드 둘레 8 슬롯에 EdgeProp 인스턴스 분산 배치.
   - [ ] 보드 자체(타일 + 내부 prop) 가 가려지지 않는다.
   - [ ] EdgeProp 이 보드 안쪽으로 침범하지 않는다.
   - [ ] EdgeProp 이 카메라 추종 회전 없이 정적으로 배치 (PropBillboard 비활성 확인).
   - [ ] 캐릭터/적/이펙트 가독성 유지 (백드롭 톤이 너무 밝거나 채도가 강하지 않다).
   - [ ] 백드롭이 보드/캐릭터 위로 그려지지 않는다 (Background+10 RenderQueue 검증).
   - [ ] 콘솔 에러/예외 없음.
6. Play 종료 → `_Backdrop` GameObject 가 정리.
7. 한 번 더 Play 진입 → 중복 백드롭이 생기지 않음.
8. 추가: Draft 화면에서 "Redraft" 버튼 클릭 → `RebuildDraftMap` 경로로 백드롭이 한 번 destroy 후 재마운트.

## 미세 조정 (필요 시 SO 값만 수정)

- 백드롭이 너무 가까워 보이면 `backdropDistance` ↑ + `backdropHeightWorld` ↑.
- 백드롭이 시야를 막으면 distance 또는 height 줄임.
- EdgeProp 이 보드와 너무 붙으면 `edgePadding` ↑ (1.5 → 2.0).
- 특정 prop 이 보드와 겹치면 해당 entry 의 `worldOffset` 으로 보드 바깥으로 밀어냄.
- EdgeProp 이 너무 카메라를 향해 기울어 보이면 `yawDegrees` 조정.

## 완료 기준

- 위 8 항목 체크리스트 모두 OK.
- 스크린샷 1장 저장 (`Assets/Screenshots/seasonal_backdrop_forest_verify_2026-05-10.png`).
- `read_console` clean.

## 의존

- 선행: 1~5 모두.
- 후행: 7번 (handoff_summary).

## 커밋

검증 통과 후 본 spec 산출 전체를 단일 커밋 또는 단위별 커밋으로 마무리. 0번 단위에서 분리한 미커밋 자산(forest.asset 등) 은 본 spec 커밋에 포함시키지 않는다.

확인 일자: 2026-05-22 / verbal OK — Forest skybox + 8 EdgeProp + 콘솔 clean 확인. 스크린샷 캡처는 후속.
