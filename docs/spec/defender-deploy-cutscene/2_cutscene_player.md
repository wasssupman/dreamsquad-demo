# 2 — DeployCutscenePlayer (플립북 재생기)

> **rev 2026-07-18 — 아래 본문의 다음 기술은 초기 계약이며 폐지됐다:**
> - 앵커 "좌상단 / top-left (0,1)" → 실제 **좌하단** `anchorMin/Max=(0,0)`, `pivot=(0,0)`
> - 수명 → **플립북 완주 + 최종 프레임 0.5초 + 자동 퇴장**. 단 배치 성공은 즉시 강제 초기화(unit 8).
> - SerializeField 기본값도 튜닝으로 변경됨: `holdSecondsAfter` 1f → **0.5f**,
>   `displayScale` 1f → **1.2f**, `cornerMarginPx` (24,24) → **(-100, 24)**
>
> 최신 계약은 README 공통 원칙, 구현 상세는 코드가 source of truth.

## 목적

프레임 배열을 좌하단 오버레이에 원샷 플립북으로 재생한다. 화면 왼쪽 바깥에서
빠르게 슬라이드-인하며 동시에 플립북을 재생 → 최종 프레임 0.5초 hold → 왼쪽으로
슬라이드-아웃 후 소멸한다. 배치 성공 시에는 즉시 강제 초기화한다.

## 변경 대상

- New: `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs` (namespace `Wassup.UI`)

## 구현

- MonoBehaviour. 자체 ScreenSpaceOverlay 캔버스 + UI `Image` 1장을 런타임 생성
  (DefenderDragPlacementController 의 reject 라벨 캔버스 패턴 참고). raycastTarget=false.
- SerializeField 튜닝(하드코딩 금지):
  - `float holdSecondsAfter = 1f;` — 애니메이션 종료 후 유지 시간
  - `float displayScale = 1f;` — 스프라이트 네이티브(640×360) 대비 배율
  - `Vector2 cornerMarginPx = new(24, 24);` — 좌상단 목표 여백(px)
  - `float slideInSeconds = 0.18f;` / `float slideOutSeconds = 0.18f;` — 진입/퇴장 속도
  - `float offscreenMarginPx = 48f;` — 화면 밖 여분(완전히 가려지도록)
  - `int sortingOrder = 20050;` — 드래그 프리뷰/거부 라벨 위
- 앵커/피벗 top-left(0,1). Image `SetNativeSize()` × displayScale 로 첫 프레임 기준 크기 확정.
  화면 밖 시작 x = `-(width + offscreenMarginPx)`.
- `public void Play(Sprite[] frames, float fps)`:
  - frames null/빈 배열 또는 fps<=0 이면 no-op.
  - 진행 중 재트리거 시 현재 코루틴 중단 후 재시작(계약: 재시작).
  - 코루틴(`Time.unscaledDeltaTime` 기준, 슬로우모/일시정지 영향 배제):
    - Phase A: `totalAnim = frames.Length/fps` 동안 프레임 진행 + 왼쪽 밖→목표 슬라이드-인
      (EaseOut) 동시. 슬라이드는 `slideInSeconds` 에 완료(애니보다 빠름).
    - Phase B: 마지막 non-null 컬러/뎁스를 명시 적용하고 `holdSecondsAfter`(0.5초) 유지.
    - Phase C: 목표→왼쪽 밖 슬라이드-아웃(EaseIn) → Image 숨김
      (canvas GO 는 재사용 위해 SetActive(false), Destroy 안 함).
  - 배치 성공 시 `ForceStopAndReset()`으로 어느 Phase 에서든 코루틴을 즉시 중단하고
    Canvas·틸트 상태를 초기화한다.
- OnDisable/OnDestroy 에서 코루틴 정지 + 캔버스 정리.

## 완료 기준

- 컴파일 통과.
- (수동/에디터) `Play(ranger frames, 24)` 호출 시 왼쪽 밖에서 슬라이드-인하며 33프레임
  재생 → 1초 유지 → 왼쪽으로 슬라이드-아웃 후 사라짐. 세로는 좌상단.
- 재생 도중 다시 `Play` 호출하면 처음부터(화면 밖) 재시작.
- 빈 배열 `Play` 는 아무 것도 표시하지 않음.

_확인: 2026-07-14 — 사용자 Play 반복 피드백으로 왼쪽 슬라이드 인/아웃 + 유닛별 크기·도착 튜닝 완료._
