# 2 — Play 시각·상호작용 검증

## 검증

- Battle 1920×1080에서 좌하단 도크와 중앙 트레이, 우하단 각성 버튼 간 겹침 확인.
- 정상 타이머와 30초 미만 경고색 상태 확인.
- `다음 웨이브 N`, `웨이브 없음`, hidden(`NextWaveAvailable=false`) 상태 확인.
- 실제 Button click이 `ForceNextWave()`를 한 번만 호출하는 기존 경로를 유지하는지 확인.
- Unity 컴파일 및 Console error 0 확인.

## 완료 기준

- 상단 시간/하단 웨이브 구조가 유지된다.
- 캐주얼 프레임·버튼과 눌림 반응이 적용된다.
- 기능 회귀와 레이아웃 충돌이 없다.

## 검증 결과 (2026-07-18)

- Unity 6000.4.3f1 Play 1920×1080: 좌하단 SafeArea 배치, 중앙 트레이 간격 확인.
- `남은 시간` 캡션과 `m:ss` 숫자, 경고색 tick 상태 렌더 확인.
- 직접 실행 맵의 `NextWaveAvailable=false`에서 하단 버튼 hidden 유지 확인.
- `다음 웨이브 N` / `웨이브 없음`, interactable 분기는 기존 bridge getter 코드 그대로 유지.
- Button listener는 기존 `OnWaveButtonClicked → bridge.ForceNextWave()` 1회 경로를 유지하고
  View 계층의 SFX/punch만 추가했다.
- Unity Console error 0.
