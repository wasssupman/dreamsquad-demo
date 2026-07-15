# 4 — 전 유닛 트리거 + 선택 유닛 줌인

## 목적

두 가지 사용자 결정(2026-07-15)을 반영한다:

1. **트리거 범위 확대**: 부착 0장 유닛도 탭하면 트리거된다. 기존 "0장 = 무반응"(계약 8) 을 뒤집는다.
2. **줌인 추가**: 선택된 유닛 쪽으로 카메라를 당겨온다. 배치/덱 오픈의 슬로우와 같은 결의 "들여다보기" 연출.

두 결정은 묶여 있다 — 줌이 생기면 부착 0장 유닛을 탭해도 **줌 자체가 피드백**이 되므로 무반응일 이유가 사라진다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — 인스펙트 포커스 채널 신설
- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — 튜닝 필드 (append-only)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 트리거 범위 + 매 프레임 타겟 피드
- `Assets/_Project/Data/**/CameraDirectionConfig.asset` — 기본값

## 구현

### 1. 트리거 범위 (계약 8 개정)

`Select(entity)` 는 **부착 유무와 무관하게** 선택을 확정한다:

- `_selected = entity` · 슬로우 lease 획득 · 줌 타겟 설정 — 항상.
- 패널은 `_cards.Count > 0` 일 때만 `Show`, 0장이면 `panel.Hide()`. **빈 상태 UI 는 만들지 않는다** — 보여줄 게 없을 때 패널이 없는 게 정직하고, 줌+슬로우가 이미 "이 유닛을 보고 있다"를 전달한다.
- 빈 보드 탭 / 재탭(토글) 은 계약 8 그대로 닫는다.

### 2. 인스펙트 포커스 채널 (`CameraDirector`)

**신규 수학 없음** — 기존 `CameraComposeMath.FocusDelta` 를 그대로 재사용한다. 그 함수는 NDC + 홈 FOV/aspect 로 홈-로컬 ray 를 복원해 `dolly + 부분 lookat + fovDelta` 를 만든다.

**핵심: NDC 를 홈 포즈 기준으로 산출한다.** 현재 포즈로 계산하면 카메라가 다가갈수록 NDC 가 0 으로 줄어 오프셋이 사라지고 → 다시 벌어지는 **진동**이 된다. 홈 포즈는 고정이므로 되먹임이 없다 (`FocusDelta` 주석의 "월드/카메라 포즈 비의존" 계약과 같은 근거).

```csharp
// worldPos → 홈-로컬 NDC (FocusDelta 의 dirLocal 복원식의 역변환)
var local = Quaternion.Inverse(_homeRot) * (worldPos - _homePos);
if (local.z <= 0.001f) return false;              // 홈 카메라 뒤 — 스킵
float tanV = Mathf.Tan(_homeFov * 0.5f * Mathf.Deg2Rad);
float tanH = tanV * Mathf.Max(0.01f, _cam.aspect);
ndc = new Vector2(local.x / (local.z * tanH), local.y / (local.z * tanV));
```

채널 형태는 **드래그 포커스 채널을 그대로 미러링**한다:

- `public void SetInspectFocus(Vector3 worldPos)` — 컨트롤러가 **매 프레임** 피드. `_inspectFedFrame = Time.frameCount`.
- **staleness 자동 해제**(2프레임) — 명시 Clear 불필요. 컨트롤러 파괴/정리 누락에도 줌이 붙박이가 되지 않는다(드래그 채널 선례의 근거 그대로).
- 가중치 페이드 인/아웃 (`inspectFadeInSec`/`OutSec`), 해제는 `EaseOutCubic01`.
- NDC 자체는 스프링 불필요 — 타겟이 고정 월드 좌표라 스텝 변화가 없다. 페이드가 부드러움을 담당한다. (드래그는 손가락이 계속 움직여 스프링이 필요했다.)
- `FocusDelta(ndc, Vector2.zero, _homeFov, aspect, weight, inspectDolly, inspectFovDelta, inspectLookWeight, 0f, 0f)` — 리드/린은 0(스와이프가 아니다).
- 합성은 기존 `CameraComposeMath.Add` 로 다른 채널과 가산. FOV 는 `Compose` 가 `[fovMin, fovMax]` 로 클램프한다(기존 계약).
- **`config.enableNonDragEffects` 로 게이팅하지 않는다.** 그 토글은 앰비언트 연출(킥/펄스/브리딩/비행) 억제용이고 현재 에셋에서 `0`(꺼짐)이다. 인스펙트 줌은 사용자가 요청한 명시적 기능이라 그 토글에 묶으면 조용히 죽는다.
- `anyActive` 판정에 `inspectActive` 를 포함시켜야 한다 — 빠뜨리면 idle 최적화(`_settled`)가 줌을 한 프레임 만에 덮어쓴다.

### 3. 튜닝 필드 (append-only, `CameraDirectionConfig` 말미)

`inspectDolly`(유닛 방향 전진) · `inspectFovDelta`(음수=줌인) · `inspectLookWeight`(0~0.5, `FocusDelta` 가 클램프) · `inspectFadeInSec` · `inspectFadeOutSec`.

드래그 포커스(dolly 1 / fov -1)보다 **강해야** 한다 — 그건 스와이프 중 미묘한 리드고 이건 의도적 들여다보기다. 초기값은 Play 튜닝 대상.

> `focusLookWeight` 툴팁의 "피드백 루프 수축 계수 / 1.0 이면 발산" 서술은 **rev 2 시절 stale** 이다(rev 3 에서 NDC 입력으로 되먹임 제거, `FocusDelta` 주석 참조). 현재 `FocusDelta` 의 `Clamp(0, 0.5)` 는 안정성이 아니라 **취향 상한**("풀 lookat 은 배치 좌표감 파괴"). 인스펙트는 고정 월드 타겟이라 어느 쪽이든 되먹임이 없다.

## 완료 기준

- compile 클린 · 콘솔 에러 0.
- 부착 0장 유닛 탭 → 선택 + 줌 + 슬로우, 패널 없음. 재탭 → 원복.
- 부착 유닛 탭 → 줌 + 슬로우 + 패널. 패널이 줌 중에도 유닛에 붙어 있다(`LateUpdate` 추종 — 계약 6).
- 빈 보드 / 페이즈 이탈 / 손패 오픈 → 줌 원복 + lease 해제.
- **컨트롤러를 강제 비활성화해도 줌이 2프레임 내 자동 해제**(staleness).
- 카메라 최종 FOV 가 `[fovMin, fovMax]` 안에 유지.
- 다른 카메라 채널(드래그 포커스)과 동시 발생해도 발산/진동 없음.
- **`CameraDirector` 가 카메라 포즈의 유일한 쓰기 주체라는 계약 불변** — 컨트롤러는 카메라를 직접 만지지 않는다.
