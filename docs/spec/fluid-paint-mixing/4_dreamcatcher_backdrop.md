# Unit 4 — Dreamcatcher 배경 어댑터

## 목적

검증된 유체 솔버를 **진짜 표면(Dreamcatcher 핸드 배경)**에 붙인다. 핸드가 열리는 "꿈에 들어가는" 순간
카드 뒤로 물감 유체가 피어오른다. 핸드 오픈 = 이미 배틀 슬로모(GPU 여유) → perf 를 그 구간으로 한정.

## 설계 결정

- **핸드-게이트(기본)**: `DreamcatcherHandView.State == Hand` 일 때만 sim 구동·표시. 닫히면 sim 비활성(step 정지)
  + CanvasGroup 페이드아웃. 앞서 우려한 "배틀 상시 렌더" 부하가 이 게이팅으로 사라진다.
- **큰 공유 파일 무수정**: `DreamcatcherHandView.cs`(1000줄, 멀티세션 편집)를 건드리지 않고 공개 `State` 만 폴링하는
  독립 컴포넌트. backdrop GO 는 Dreamcatcher 캔버스 아래, 핸드 패널 **뒤** sibling.
- 표시 바인딩은 unit 3 에서 검증된 경로 그대로(RawImage.texture = sim.DyeTexture).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFluidBackdrop.cs` (신규)
- (배선) BattleScene: Dreamcatcher 캔버스 아래 backdrop GO(RawImage+FluidPaintSim+DreamcatcherFluidBackdrop)

## 구현

`DreamcatcherFluidBackdrop : MonoBehaviour` (Wassup.UI, `[RequireComponent(CanvasGroup)]`):
- SerializeField: `handView`, `sim`(FluidPaintSim), `image`(RawImage), `mode`(HandGated/AlwaysOn), `maxAlpha`, `fadeSpeed`
- Update: `open = mode==AlwaysOn || handView.State==Hand` → sim.enabled=open(게이트), image.texture=sim.DyeTexture,
  CanvasGroup.alpha 를 open?maxAlpha:0 으로 unscaled 페이드
- 색·세기는 `FluidSimConfig.asset`(팔레트/앰비언트)에서. backdrop 은 어둡고 은은하게(maxAlpha≈0.7).

## 완료 기준

- [x] `DreamcatcherFluidBackdrop` 컴파일 클린 (EditMode 8/8)
- [x] Play 검증: AlwaysOn 프리뷰(목업 카드 핸드 + backdrop)에서 페이드-인 + 카드 뒤 유체 표시 확인 (스크린샷)
- [ ] BattleScene 배선(Dreamcatcher 캔버스 아래, 핸드 뒤) + 핸드 오픈 시 실제 카드 뒤 유체 + 실기 perf — **공유 씬 아트 배치라 사용자 라이브 검증 대기**

**완료 확인 2026-07-23**: AlwaysOn 프리뷰 씬(in-memory) Play → 하단 목업 카드 5장 아치 뒤로 물감 유체가 은은히
피어오르는 구도 스크린샷 확인. 컴포넌트 게이트/페이드/바인딩 동작 검증. 실제 BattleScene 배치는 다음 단계.

## 후속

- BattleScene 실배선 + 실기 perf 프로파일. sort order/알파/색은 실제 카드 위에서 조정.
