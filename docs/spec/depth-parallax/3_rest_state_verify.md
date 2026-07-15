# 3 — rest-state no-op 검증 하네스

## 목적

`_Tilt=(0,0)` 에서 셰이더 출력이 원본 스프라이트와 **픽셀 동일**함을 자동 증명한다. 이 게이트를
통과해야 통합(unit 6~) 으로 진행한다. rest 회귀는 이 기능 최상위 리스크다.

## 변경 대상

- New: 오프스크린 diff 검증 스크립트 (scratchpad 의 `execute_code` 또는 일회용 EditMode 유틸)
- 문서화: 본 파일에 검증 절차 + 기대 결과 기록

## 구현

- **오프스크린 렌더 diff** (memory `offscreen_render_vfx_verify` 기법):
  1. 임의 테스트 스프라이트 + 임의 뎁스 텍스처를 far 좌표에 UI Image 로 Instantiate.
  2. (a) `_image.material = DepthParallax_UI`(`_Tilt=0`) 로 RenderTexture A 렌더.
  3. (b) 같은 Image 를 `Sprites/Default`(또는 UI/Default) 로 RenderTexture B 렌더.
  4. A/B 픽셀 diff → **max 차이 0**(또는 8bit 양자화 노이즈 이내 ≤1) 확인.
- **부가 확인**: `_Tilt=(0.5,0)` 로 렌더 시 A 와 **달라짐**(효과가 실제로 동작함을 반증 방지).
- 이 하네스는 CI 상시 실행 대상은 아니지만, 셰이더 수정 시 재실행하는 회귀 절차로 본 문서에 남긴다.
  (프로젝트 EditMode 에 무거운 GPU 리드백 테스트를 상시 넣지는 않는다 — 실행 비용/플랫폼 편차.)

## 완료 기준

- tilt=0 diff = 0(또는 ≤1/255) 스크린샷/로그 증거 확보.
- tilt≠0 diff > 0 확인(효과 활성 반증).
- 절차가 재현 가능하게 본 파일에 기록됨(다음 셰이더 수정자가 그대로 재실행).
