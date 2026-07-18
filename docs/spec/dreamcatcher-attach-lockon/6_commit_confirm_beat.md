# 6 · Commit Confirm Beat (레이어 E)

## 목적

손 뗄 때 "이 유닛에 확실히 걸렸다"를 **손가락 밖에서** 보고·느끼게 한다(불편 ③). 리티클 수렴 + **콜아웃 "찰칵" 펀치**(주 초점) + 손끝 반경 초과 링 펄스 + (모바일)햅틱 → 기존 `FlyCardToUnit` 흡수. 실패/취소엔 비트 없음.

## 변경 대상

- `DreamcatcherFocusReticle.cs` — `PlayConfirm(rect)` 수렴/펄스/플래시
- `DreamcatcherFocusCallout.cs` — 확정 펀치(체크/스케일 팝)
- `DreamcatcherCardDragSlot.cs` — `CommitNow` 성공 경로 훅 + 햅틱

## 구현

- **콜아웃 펀치가 주 초점(UX H3)**: 확정 순간 손가락은 유닛을 가리므로, 확정 가시성의 주 신호를 **손끝 밖 콜아웃**에 둔다 — 체크마크/스케일 팝. 리티클 수렴은 보조.
- `DreamcatcherFocusReticle.PlayConfirm(Rect rect)`: 브래킷 안쪽 `confirmConvergeSec` 수렴 + 링 1회 펄스(`confirmPulseColor`/`Sec`). 펄스 확장 반경은 `confirmPulseMinRadius` 로 **손끝 반경 초과** 보장(계약 #7) + 짧은 플래시(`confirmFlashSec`). `unscaledDeltaTime`.
- `DreamcatcherCardDragSlot.CommitNow`(성공 시에만 소비/순환/흡수): `commit()` **성공 분기**에서 락온 렉트로 `callout.PlayConfirm` + `reticle.PlayConfirm` 호출 후 기존 `FlyCardToUnit` 흡수. 실패(대상 소멸/부착 상한/캐스트 거절)·취소 = `Release()`만, 비트 없음(부모 계약 #9 정합).
- **햅틱**: `focusConfig.enableHaptic` && 비에디터일 때 `Handheld.Vibrate()`(스택 유일 API — Android ~0.5s 고정 버즈, "경량" 아님을 인지하고 확정 순간 1회만). 에디터/마우스 무시.
- 확정 비트는 흡수 궤적과 시간 겹치지 않게(짧은 수렴 → 흡수). 기존 `FlyCardToUnit` 타이밍 존중, 되돌리지 않음.

## 완료 기준

- 유효 커밋 시 **콜아웃 펀치(손끝 밖)** + 리티클 수렴 + 링 펄스 재생 후 흡수. 무효/취소 시 비트 없음.
- 밀집·손가락 가림 상태에서도 "어느 유닛에 걸렸는지" 확정이 손가락 밖에서 체감.
- 기존 커밋 성공/실패 판정·소비·순환 회귀 없음. 콘솔 클린.
- 모바일 실기기 햅틱 1회(있으면), 에디터 무해.
