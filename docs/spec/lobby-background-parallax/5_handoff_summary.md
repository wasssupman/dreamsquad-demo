# 5 — Handoff Summary (lobby-background-parallax)

> **완료 (2026-07-15)** — u0~u4 구현·무회귀 검증·사용자 Play 체감 확인. **main 머지됨 `89815cf9`**
> (`feat/lobby-background-parallax` 에서 `--no-ff` 머지 — 문제 시 머지 커밋 1개 revert 로 통째 회수).

## Commit

- `8464e326` docs — spec 초안
- `216909a9` refactor(depth-parallax) — `DepthParallax.cginc` 추출 (unit 0)
- `7ab315c4` feat — BackgroundDissolve 에 Cue A (unit 1)
- `068fdc23` feat — 로비 뎁스맵 bake + `--flatten` 노브 (unit 2)
- `111fb8c4` feat — LobbyBackgroundParallax 드라이버 (unit 3)
- `858b8c2a` feat — 씬 배선 + 오버스캔 (unit 4)
- `83ea6e36` change — 틸트 구동 유닛드래그 → **포인터 위치**
- `66b67ca0` change — **키링 스와이프 중에만** + 진폭 0.015→0.04

## Implemented

- **뎁스 1장 공유**: 평탄화된 저주파 뎁스(640×360 R8)를 앞/뒤 Image 가 공유. 낮/밤 지오메트리 동일
  (상관 0.998)이라 시간대 스왑 없음.
- **디졸브 공존**: 모듈 `DepthParallax.cginc` 를 `BackgroundDissolve` 가 include 해 Cue A 만 사용.
  산식은 여전히 모듈 단일 소유. 앞=디졸브(+패럴랙스), 뒤=모듈 머티리얼, **같은 `_Tilt`/`_DepthTex`**.
- **구동**: 키링 스와이프 중에만, 포인터 화면 위치(중심=0, 가장자리=±1) → 스프링 → 두 머티리얼.
  평상시 정지(target=0). `LobbyKeyringDrag.AnyDragging` 이 게이트.
- **오버스캔 1.05**(앞/뒤 동일) — UV 시프트가 `[0,1]` 밖을 샘플해 가장자리가 늘어지는 것 방지.

## Key Files

- `Assets/_Project/Modules/DepthParallax/Shaders/DepthParallax.cginc` (산식 단일 소유)
- `Assets/_Project/Shaders/Background_Dissolve_UI.shader` (Cue A include)
- `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundParallax.cs` (드라이버)
- `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundDissolve.cs` (`SetParallaxParams/Tilt` 접근자)
- `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` (`AnyDragging` 게이트)
- `Assets/_Project/Art/Depth/lobby_bg_depth.png`, `Assets/_Project/Data/LobbyParallaxSettings.asset`
- `Assets/_Project/Modules/DepthParallax/Tools~/depth_bake.py` (`--flatten`, `--max-width`)

## Verified

- **무회귀 2건 (바이트 동일)** — 둘 다 변경 전 셰이더를 git 에서 임시 복원해 **같은 run A/B**:
  - 배틀 컷신(unit 0): 6개 틸트 상태 전부 `maxDiff=0, anyDiff=0/65536`
  - 로비 디졸브(unit 1): 실제 `dissolve_noise` 로 4모드 × 5진행도 전부 `maxDiff=0`
- **평탄화**: 뎁스 절벽(p99.5 grad) 70.3 → **4.1** (15배↓). 실제 로비 아트로 **출시값의 3.3배(amp 0.05)
  스트레스에서도 난간 살·가로등 늘어짐 없음** — 이 spec 의 존재 이유가 검증됨.
- EditMode `DepthParallaxMathTests` **6/6**. 전 단계 컴파일 클린.
- 씬 diff 는 delta 만(컴포넌트 1 + 필드 + 오버스캔 2줄).

## Notes (되돌리면 안 되는 의도)

- **Cue B(사다리꼴)/C(하이라이트)는 배경에 금지.** 전체화면은 여백이 없어 사다리꼴이 가장자리를
  안쪽으로 당기면 캔버스가 드러난다. 드라이버가 뒤 머티리얼에 `_Persp=0/_HiStrength=0` 을 SO 값과
  무관하게 강제하고, 디졸브 셰이더엔 아예 프로퍼티를 안 넣었다.
- **디졸브 UV 분리**: 이미지에 붙은 것(`_MainTex`·`_NoiseTex`)만 시프트. `_Center` 원형 확산·수평 스윕·
  `_ClipRect` 는 원본 uv — 확산 중심은 캐릭터(패럴랙스 안 함)에 앵커돼야 하고 마스킹은 논리 rect 기준.
- **앞/뒤 오버스캔·틸트가 다르면 전환 중 두 레이어가 갈라진다.** 항상 같은 값.
- **`--flatten` 은 배경 전용**(기본 off). 캐릭터 컷신 뎁스에 쓰면 실루엣이 뭉개진다.
- 프로젝트는 `activeInputHandler=Input System 전용` — 레거시 `Input.mousePosition` 사용 불가.
  `Pointer.current` 관례(cf. PlacementInput). 모바일은 hover 없음 → press 중에만 유효.
- **하네스 함정**: 오프스크린 렌더 비교는 **반드시 같은 execute_code run 안에서** 할 것.
  ScreenSpaceCamera 캔버스가 run 마다 다른 크기로 잡혀 cross-run 비교는 무효(29% 오탐 경험).

## Follow-up

- 더 강하게 원하면: amp 0.05+ 는 **오버스캔도 같이 올려야** 한다(현재 amp 0.04 에서 1.3배 헤드룸뿐).
  방향 반전은 `pointerGain` 음수.
- **B안(레이어 분리)**: 난간이 실제로 튀어나오는 진짜 깊이감 — A안 체감이 약하면 승격(인페인팅 필요).
- Android 실기기 확인.
