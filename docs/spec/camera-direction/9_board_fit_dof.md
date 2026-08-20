# 9. 보드 fit 에 연동한 DoF 거리

## 목적

피사계 심도(DepthOfField, Gaussian)의 `gaussianStart`/`gaussianEnd` 가 **월드 절대 거리**로 저작돼 있어, 튜닝한 화면비에서만 맞았다. 실기기(19.5:9)에서는 블러가 통째로 사라져 **포스트프로세싱이 아예 안 걸리는 것처럼** 보였다.

원인은 unit 8 의 fit 산식 자체다. `FitDistance` 는 `tanH = tanV·aspect` 로 거리를 정하므로 **가로가 넓을수록 카메라가 보드에 붙는다**. 임계값만 고정돼 있으면 화면 밖으로 밀려난다.

| | aspect | 카메라 깊이(fit+pullback) | 화면 최상단 지면까지 | 블러가 걸리는 영역 |
|---|---|---|---|---|
| 에디터 게임뷰 1920×1080 | 1.778 | 27.73 | 32.9 | 화면 상단 **25%** |
| 갤럭시 S23 2340×1080 | 2.167 | 25.28 | 29.7 | 화면 상단 **7%** (그마저 램프 초입) |

밴드가 `28.3~30` 으로 **1.7 유닛**뿐이라 2.45 유닛 이동만으로 사라진다. 태블릿(4:3)에서는 반대로 카메라가 더 물러나 **과하게** 걸린다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/CameraFramingMath.cs` — `DofRange` 순수 함수 추가
- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — `postVolume` 배선, `FrameBoard` 에서 적용, 화면비 변동 시 재프레이밍
- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — `dofBlurStartT` / `dofBlurEndT`
- `Assets/_Project/Scenes/BattleScene.unity` — Main Camera 의 `postVolume` → 글로벌 Post 볼륨
- `Assets/_Project/Tests/EditMode/CameraFramingMathTests.cs` — 회귀 테스트 4개

## 구현

**임계값을 보드 깊이로 정규화한다.** `DofRange(localCorners, camDistance, startT, endT)` 가 코너의 view 공간 깊이에서 `nearZ`/`farZ` 를 구하고, 그 사이를 `t` 로 보간한다(0 = 보드 앞단, 1 = 뒷단, 1 초과 = 보드 뒤).

보드의 깊이 **폭**은 맵이 정하고 화면비와 무관하다. 바뀌는 건 오프셋(`camDistance`)뿐이므로, 카메라가 당겨지면 임계값도 정확히 같은 양만큼 당겨진다 — "보드 뒤쪽부터 흐려진다"가 모든 화면비·모든 맵 크기에서 같은 그림으로 유지된다.

기본값은 **현재 저작된 그림을 그대로 재현**하도록 뽑았다. 실측 보드(nearZ 24.73 / farZ 30.73, span 6.0)에서 `28.3 → t=0.595`, `30.0 → t=0.879` 이므로 `dofBlurStartT = 0.6`, `dofBlurEndT = 0.88`.

**적용 지점**은 `FrameBoard` 끝. 거리를 확정한 바로 그 자리라 두 값이 갈릴 수 없다. 쓰기 대상은 `Volume.profile` — sharedProfile 의 **런타임 인스턴스**라 프로필 에셋은 건드리지 않는다(에디터 Play 에서도 디스크에 안 남는다).

**화면비 변동 재프레이밍**: `LateUpdate` 가 `_cam.aspect` 변화를 감지하면 같은 보드로 `FrameBoard` 를 다시 부른다. 기기에선 가로 고정이라 사실상 안 울리지만, **에디터 게임뷰를 실기기 해상도로 바꿨을 때 실제와 같은 그림이 나온다** — 이 결함이 에디터에서 안 보였던 이유가 바로 그 경로였다.

**경계**:
- `postVolume` 미배선 → DoF 연동만 조용히 skip. 프레이밍은 그대로.
- `mode != Gaussian` → skip. Bokeh 는 `focusDistance`/`aperture` 체계라 무관하다.
- 보드가 카메라 뒤(`nearZ ≤ 0`)면 `DofRange` 가 false — 호출부가 DoF 를 건드리지 않는다.
- `endT < startT` 로 저작해도 `end > start` 를 강제한다. URP 는 음수 폭 램프를 방어하지 않는다.

## 완료 기준

- [x] 컴파일 · EditMode 통과 (2026-08-20: 2332개 실패 0 · 스킵 3은 기존 ignore, 신규 `DofRange` 테스트 4개 포함)
- [x] 16:9 에서 산출값이 기존 수동 저작값과 일치 — `28.33 / 30.01` vs 손으로 잡았던 `28.3 / 30`
- [x] 19.5:9 로 바꾸면 `25.88 / 27.56` 으로 카메라 이동량(2.45)만큼 따라온다. 블러가 덮는 영역 **화면 상단 25% → 23%** (수정 전 7%)
- [x] 두 화면비의 원거리 나무 블러가 육안으로 동일 (Play 스크린샷 `Assets/Screenshots/fix_16x9.png`, `fix_device_aspect.png`)
- [x] 실기기 빌드에서 원거리 나무가 흐려진다 (2026-08-20 재빌드 후 사용자 확인)

확인: 2026-08-20 · 66d51ba9 · 에디터 Play · 실기기(SM-S911N, 19.5:9) 모두 사용자 확인 완료.

`_settled` 도 함께 고쳤다. 전 채널 비활성 상태에서는 `LateUpdate` 가 정착 포즈를 다시 쓰지 않아, `FrameBoard` 가 홈을 바꿔도 카메라가 그 자리에 머물렀다(재프레이밍이 무효). unit 8 부터 있던 잠복 결함으로, 맵 빌드 직후 페이즈 전환이 뒤따라서 가려져 있었다.

## 후속 후보

- Bloom `threshold` 등 다른 거리·밝기 의존 파라미터도 화면비 편차를 타는지 점검.
- unit 8 의 후속 후보였던 "aspect 별 margin 분리" 는 이 유닛으로 필요성이 줄었다 — 거리 변동을 소비처가 흡수하므로.
