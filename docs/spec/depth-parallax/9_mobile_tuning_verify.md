# 9 — 모바일 튜닝 + Play 검증

## 목적

실제 체감을 맞추고, 모바일 안전 계약(1 dependent read · ≤4% 진폭 · 공유 머티리얼 배칭)을 실측으로
확인한다. 이 unit 통과가 feature 종료 조건.

## 변경 대상

- Tune(asset): `DepthParallaxSettings.asset`(amplitude/depthCenter/persp/highlight/스프링), 유닛별 tiltGain
- (선택) `DragSwaySettings` 스와이프 정규화 상수

## 구현 / 검증 항목

- **틸트 체감**: 드래그 스와이프 방향으로 회전감이 나되 과하지 않게. `amplitude≤0.04`, `_Persp` 낮게,
  하이라이트 은은하게. 과구동 = "휘어진 텍스처"로 보임(ortho 라 과장 티남).
- **rest no-op 재확인**: 정지 시 원본과 동일(unit 3 하네스 재실행 또는 Play 육안).
- **스프링 수명**: 드래그 중 흔들다 손 떼기 → 틸트 0 복귀. **드래그를 컷신 재생 중간에 놓아도** 컷신은
  완주하며 틸트만 독립적으로 0 복귀(중단 없음). `CleanupSession` teardown 에서 예외/leak 없음.
- **모바일 안전 실측**:
  - dependent read 1회 유지(셰이더 최종본 리뷰).
  - **머티리얼/배칭**: 소비처별 per-instance 머티리얼, 프레임/틸트는 `SetTexture`/`SetVector` 만
    (런타임 머티리얼 스왑 없음). Frame Debugger 로 컷신 캔버스가 무관 UI 배치를 쪼개지 않는지 확인.
    per-instance 머티리얼/텍스처 `OnDestroy` Dispose 확인(leak).
  - Android 실기기 프레임/발열 이상 없음(줌 플립북 + 패럴랙스 동시).
- **뎁스 극성/품질**: 극성 뒤집힘은 `depthSign`. halo/smear 심하면 뎁스 blur↑ 또는 진폭↓(unit 8 재bake 전 먼저 시도).

## 완료 기준

- 사용자 Play 체감 승인(Editor + 가능하면 Android 실기기).
- rest no-op·스프링 복귀·중간 드롭 완주 3종 Play smoke 통과, 콘솔 클린.
- 모바일 안전 3계약(read 1회·진폭≤4%·공유 머티리얼) 코드/Frame Debugger 로 확인.
- 완료 후 README 상태 "완료 YYYY-MM-DD" + `10_handoff_summary.md` 작성. 파이프라인 맵 변경 없음 재확인.
