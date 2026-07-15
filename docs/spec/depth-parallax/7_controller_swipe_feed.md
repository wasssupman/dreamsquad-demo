# 7 — 컨트롤러 스와이프→틸트 피드 + 데이터 배선

## 목적

드래그 스와이프 속도를 정규화된 틸트 벡터로 만들어 매 프레임 컷신 플레이어에 피드한다. 유닛별
뎁스 프레임을 데이터에서 읽어 `Play` 로 넘기고, 유닛별 틸트 게인은 컨트롤러가 적용한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- Modify: `Assets/_Project/Scripts/Data/DefenderUnitData.cs`

## 구현

- **asmdef 배선**: `Wassup.Runtime → Wassup.DepthParallax` 참조는 **unit 6 에서 이미 추가됨**(단방향;
  모듈은 Runtime 무참조). 이 unit 은 그 참조 위에서 컴파일.
- **데이터 필드**: `DefenderUnitData` 의 `deployCutsceneFrames`(`:119`) 옆에
  `public Texture2D[] deployCutsceneDepth;` + `public float deployCutsceneTiltGain = 1f;`.
  **길이 1 = 정적 단일 뎁스(전 프레임 공유, 기본), 길이 == 색 프레임 수 = 프레임별.** 비어 있으면
  뎁스 없음(색만) — 기존 유닛 불변.
- **스와이프→틸트**: `DefenderDragPlacementController.Update()`(`:106`)에서 `_session.active` 게이트로,
  보드 early-return(`:108-109`) **위**에 블록(컷신은 보드 독립). **블록 자체 로컬 dt 선언**
  (`float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);` — 기존 `:111` dt 는 early-return 아래라 스코프
  밖). 필드 `Vector2 _prevScreenPos, _swipeVelSmoothed; float _tiltGain;`. raw 속도
  `(_lastScreenPos - _prevScreenPos)/dt` → exp-lerp 스무딩 → `[-1,1]²` 정규화·클램프 → **×`_tiltGain`
  (유닛별 게인의 유일 소유자 = 컨트롤러)** → `_cutscenePlayer?.SetTilt(tilt)`. 블록 끝 `_prevScreenPos =
  _lastScreenPos`(`:171` 갱신). `SetDragFocus` 피드(`:122`) 미러.
- **마셜링**: `BeginDrag`(`:96-99`) 에서 데이터의 `deployCutsceneDepth` 를 확장
  `Play(color, depth, fps, scale, offset)` 로 전달. `deployCutsceneTiltGain` 은 `Play` 로 넘기지 않고
  `_tiltGain` 에 저장 → 위 스와이프 블록에서 곱함(게인은 컨트롤러가 단독 소유). 컨트롤러가 유일한
  `DefenderUnitData`→모듈 번역기.
- 스와이프 정규화 상수(속도→[-1,1] 스케일, 스무딩 계수)는 `DragSwaySettings` 에 추가하거나 컨트롤러
  SerializeField. (튜닝 허브 일관성 위해 `DragSwaySettings` 선호 — 단 제네릭 모듈 SO 아님에 주의.)

## 완료 기준

- 컴파일 클린. asmdef 순환참조 없음(모듈→Runtime 참조 0 유지).
- 드래그 중 손가락을 좌우로 흔들면 `SetTilt` 가 방향성 있는 값으로 호출됨(로그/Play).
- 뎁스 미할당 유닛은 `SetTilt` 가 색 렌더에 영향 없음(패럴랙스 0).
