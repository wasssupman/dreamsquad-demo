# 8 — 최종 프레임 유지 · 자동 퇴장 · 배치 성공 강제 초기화

## 목적

배치 컷씬 플립북이 전 프레임을 재생한 뒤 첫 프레임으로 돌아가 보이는 현상을 제거한다.
마지막 유효 프레임을 0.5초 보여준 뒤 자동 퇴장하되, 실제 배치 성공은 연출보다 절대 우선한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`

## 구현

- `PlayRoutine` 시작 시 컬러 배열의 마지막 non-null 인덱스를 찾는다.
- Phase A 플립북 루프가 끝난 직후 해당 컬러 Sprite를 `_image.sprite`에 다시 지정한다.
- 뎁스 배열이 있으면 같은 컬러 인덱스를 기존 clamp 규칙으로 매핑해 `_DepthTex`도 함께 고정한다.
- Phase B는 이 최종 컬러/뎁스 조합을 unscaled time 기준 0.5초 유지한다.
- Phase C는 기존처럼 왼쪽으로 0.18초 slide-out한 뒤 Canvas를 숨긴다.
- 마지막 배열 원소가 null이어도 마지막 non-null 프레임을 사용한다.
- 프레임 전체가 null인 기존 입력은 첫 프레임 탐색 가드에서 Hide 후 종료한다.
- 드래그 실패·취소는 자동 종료 흐름을 중단하지 않는다.
- 새 배치 세션 시작 시 직전 컷씬을 먼저 강제 초기화한다. 다음 유닛에 프레임이 없어도
  이전 유닛 컷씬이 남지 않는다.
- `TryBeginDefenderDeployment` 성공 직후 `ForceStopAndReset()`을 호출한다. 재생·hold·slide-out
  어느 단계든 즉시 코루틴을 중단하고 Canvas를 숨기며 틸트 target/current/velocity를 0으로 원복한다.
- 재트리거는 기존처럼 현재 코루틴을 중단하고 첫 프레임부터 다시 시작한다.

## 완료 기준

- Ranger처럼 첫/마지막 포즈 차이가 큰 배열에서 완주 후 마지막 포즈가 유지된다.
- 마지막 원소가 null인 배열에서도 마지막 유효 포즈가 유지된다.
- 정적 뎁스 1장과 프레임별 뎁스 N장 모두 최종 컬러 프레임과 lockstep이다.
- 완주 후 마지막 포즈가 0.5초 유지된 다음 왼쪽으로 slide-out 후 숨는다.
- 드래그를 일찍 취소해도 일반 자동 종료 흐름을 완료한다.
- 배치 성공 시 컷씬 단계와 관계없이 같은 프레임에 숨고 다음 재생에 틸트가 남지 않는다.
- Unity 컴파일 및 Console error 0.

_구현 확인 2026-07-18: 열린 Unity 6000.4.3f1 Editor에서 스크립트 리컴파일 완료, Console error 0._
