# 3 — Handoff Summary

## Commit

- `27224339` — Restyle next wave dock with casual jelly UI
- 사용자 마감 확인: 2026-07-18

## Implemented

- Battle 좌하단 NextWaveDock의 상단 시간/하단 웨이브 구조를 유지했다.
- 넓은 네이비 도크와 보라·청록·노랑 젤리 프레임을 적용했다.
- 다음 웨이브 버튼에 통통한 cyan face와 두꺼운 외곽선을 적용했다.
- 작은 `남은 시간` 캡션과 큰 `m:ss` 숫자로 시간 위계를 명확히 했다.
- pointer down squash와 release overshoot로 버튼 반응을 보강했다.
- 경고색, 초 tick punch, disabled 및 hidden 상태 의미를 유지했다.
- 클릭은 기존 `OnWaveButtonClicked → ForceNextWave()` 1회 경로를 유지한다.
- 생성 Sprite가 없어도 기존 색상 기반 UI로 동작하는 폴백을 유지한다.

## Key Files

- `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- `Assets/_Project/Art/UI/NextWaveDockFrame.png`
- `Assets/_Project/Art/UI/NextWaveButtonFace.png`
- `Assets/_Project/Scenes/BattleScene.unity`
- `docs/spec/nextwave-dock-casual-style/README.md`

## Verified

- Unity 6000.4.3f1 Play, 1920×1080 좌하단 배치와 중앙 트레이 간격을 확인했다.
- 타이머 캡션·숫자·경고색 렌더를 확인했다.
- `NextWaveAvailable=false`에서 버튼 hidden 상태를 확인했다.
- 기존 label/interactable 분기와 ForceNextWave 호출 경로가 보존됨을 확인했다.
- Unity 컴파일 및 Console error 0을 확인했다.
- 2026-07-18 사용자 마감 확인을 받았다.

## Notes

- 웨이브 데이터, 타이머 계산, 조기 호출 조건은 이 spec의 변경 대상이 아니다.
- 꿈·별·밤 등 드림 테마 상징은 에셋에 사용하지 않는다.
- 구조 변경이 없어 `docs/reference/object-pipeline-map.md` 갱신 대상이 아니다.
- BattleScene에는 두 Sprite 참조만 이 기능 변경으로 포함됐다.

## Follow-up

- Android 실제 해상도와 cutout 영역 확인은 공통 모바일 UI QA에서 수행한다.
- 웨이브 진행 로직 또는 보상 변경은 별도 spec으로 다룬다.
