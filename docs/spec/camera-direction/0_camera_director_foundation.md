# 0 — CameraDirector 토대

## 목적

카메라 base 포즈의 런타임 단일 소유자를 세운다. 이후 유닛(비행/구두점/앰비언트)은 이 Director에 채널 값을 더하는 것으로 끝나야 한다. 기존 `CameraImpactKick`을 채널로 흡수해 "포즈 쓰기 주체 2개" 상태를 만들지 않는다.

## 변경 대상

- 신설 `Assets/_Project/Scripts/Presentation/CameraDirector.cs`
- 신설 `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` (순수 static)
- 신설 `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` (SO) + `Assets/_Project/Data/Camera/CameraDirectionConfig.asset`
- 수정 `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `EnsureCameraKick()` → Director API로 마이그레이션
- 삭제(은퇴) `Assets/_Project/Scripts/Presentation/CameraImpactKick.cs`
- 씬 `BattleScene` — Main Camera에 `CameraDirector` 부착 + config 배선 (씬 위생 절차 준수)

## 구현

- `CameraDirector : MonoBehaviour` (Main Camera 부착, 씬 배선):
  - `[DefaultExecutionOrder(-100)]` — **카메라 포즈 소비자(빌보드/데미지넘버/드래그 프리뷰 등 LateUpdate에서 카메라를 읽는 모든 컴포넌트)보다 먼저 실행**되는 것이 계약. 소비자들은 항상 이번 프레임 최종 포즈를 읽는다.
  - `Awake`에서 씬 authored 포즈(position/rotation/FOV)를 홈 포즈로 캡처. 캡처/config 준비 전에 들어온 `Kick()` 등 채널 호출은 안전 no-op.
  - `LateUpdate`에서 절대 합성: `최종 = 홈 ⊕ flightDelta ⊕ punctuationOffset ⊕ ambientOffset ⊕ kickOffset`. 이번 유닛에서는 flight/punctuation/ambient 채널은 항등값(구조만 존재), 킥 채널만 실동작.
  - 킥 채널: 기존 `CameraImpactKick`의 envelope(k², 하향 dir + roll)와 기본값(0.08/0.35°/0.16s)을 그대로 이식하되 수치는 config SO로 이동. `public void Kick(float strength = 1f)` 유지.
  - self-cancel 로직은 제거 — Director가 매 프레임 절대 포즈를 쓰므로 불필요(README 계약 참조).
- `CameraComposeMath` (순수 static, EditMode 테스트 대상):
  - `KickEnvelope(remaining, duration) → 0~1` — strength 는 호출부(Director)가 곱한다. duration ≤ 0 이면 0 (킥 비활성의 단일 소유 가드 — Director 쪽 최소치 클램프 없음, `kickDuration 0 = 킥 끔`).
  - `ComposePose(home, deltas...) → (pos, rot, fov)` — 델타는 **홈 회전 기준** 카메라 로컬 축 위치 오프셋 + pitch/roll 오프셋 + FOV 델타의 plain struct.
  - 아이들(전 채널 비활성) 프레임은 홈 포즈 1회 기입 후 no-op — 모바일에서 매 프레임 transform/FOV 재기입 낭비 방지. Director 유일 쓰기 주체라 소유 계약과 충돌 없음.
- `DreamcatcherHandView`: `EnsureCameraKick()`이 `CameraDirector`를 `GetComponent`로 찾도록 변경. **AddComponent fallback 금지** — Director는 config 배선이 필수라 런타임 생성이 성립하지 않는다. 없으면 1회 경고 로그 + 킥 skip(no-op).

## 완료 기준

- compile 클린, 기존 `CameraImpactKick` 참조 잔재 0.
- EditMode: `CameraComposeMathTests` — envelope 감쇠/합성 항등성(모든 델타 0 → 홈 포즈 그대로)/델타 합성 순서 검증.
- Play: 카드 흡수 임팩트 킥이 기존과 동일 체감으로 동작(스크립트 배틀 or 수동), 킥 종료 후 카메라가 홈 포즈 정확 복귀.
- 씬 diff가 Main Camera의 CameraDirector 추가 + config 참조만 포함.
