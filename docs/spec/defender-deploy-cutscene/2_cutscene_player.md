# 2 — DeployCutscenePlayer (좌상단 플립북 재생기)

## 목적

프레임 배열을 좌상단 오버레이에 원샷 플립북으로 재생한다. 화면 왼쪽 '바깥'에서
빠르게 슬라이드-인하며 동시에 플립북을 재생 → 1초 hold → 왼쪽으로 슬라이드-아웃 후
소멸. 세로는 좌상단 고정. 드래그 세션과 독립.

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
    - Phase B: `holdSecondsAfter`(사실상 무한) 유지 — 단 `_endRequested`(스와이프 종료, EndCutscene) 시 즉시 탈출.
    - Phase C: 목표→왼쪽 밖 슬라이드-아웃(EaseIn) → Image 숨김
      (canvas GO 는 재사용 위해 SetActive(false), Destroy 안 함).
- OnDisable/OnDestroy 에서 코루틴 정지 + 캔버스 정리.

## 완료 기준

- 컴파일 통과.
- (수동/에디터) `Play(ranger frames, 24)` 호출 시 왼쪽 밖에서 슬라이드-인하며 33프레임
  재생 → 1초 유지 → 왼쪽으로 슬라이드-아웃 후 사라짐. 세로는 좌상단.
- 재생 도중 다시 `Play` 호출하면 처음부터(화면 밖) 재시작.
- 빈 배열 `Play` 는 아무 것도 표시하지 않음.

_확인: 2026-07-14 — 사용자 Play 반복 피드백으로 왼쪽 슬라이드 인/아웃 + 유닛별 크기·도착 튜닝 완료._
