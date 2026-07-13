# card-crumple-unfold — 카드 꼬깃꼬깃 → 펴짐 (초안 rev1)

**상태: 계획 확정 2026-07-13** (plan critic 반영 · D1~D3 확정 · 구현 대기 — unit 0 부터)

## 목표

각성 손패 카드가 등장할 때 **구겨진 종이(꼬깃꼬깃)에서 쫙 펴지며 카드 면이 드러나는** 연출.
`dreamcatcher-hand-deal-in`(완료)의 후속(이관된 ③). 딜 등장(덱-드로우 안착)과 **동기**로 구김이 풀린다.

## 결정 (2026-07-13, 사용자)

- **렌더 경로 = UGUI 서브디바이드 + UGUI 버텍스 셰이더**(캔버스 내). RenderTexture/월드 쿼드 아님.
- **적용 = 덱-드로우 등장에 통합, 전체 카드**(5장 동시) → **실기(Android) 성능 선(先)검증 필수**.

## 실제 시작 상태 (코드 근거 — 초안 rev0 오류 정정)

`DreamcatcherHandView.EnsureSlots`/`BindCard` 기준, 카드 위젯의 실제 구성:

- **frame = 스프라이트 없는 단색 Image**(root GameObject 의 Image). `BindCard`/`BindEmpty`/`RefreshUsability`
  가 `slot.frame.color` 만 바꾼다. → "카드 페이스 텍스처"는 **존재하지 않는다**.
- **art = 별도 자식 Image**(진짜 스프라이트 `card.art`, `preserveAspect=true`, 6px inset). 없으면 skill uiTint 단색.
- **root GameObject 에 Image(=frame) + CanvasGroup(=group, 사용불가 dim 0.42 소유) + `DreamcatcherCardDragSlot`**
  가 함께 붙어 있다. NameTag(Image+TMP)·Cost(Image+TMP)는 자식.

따라서 "frame+art 를 한 스프라이트로 샘플"은 성립하지 않는다. **무엇을 서브디바이드/크럼플할지**가 unit 0 의
1순위 결정(아래 열린 결정 D1).

## 핵심 설계 난점

1. **복합 위젯 + TMP**: UI 버텍스 셰이더는 한 Graphic 메시만 변형. 이름(TMP)/코스트는 자체 메시라 크럼플 불가.
   → 구김은 **카드 면 그래픽에만**, 텍스트/배지는 **펴짐 완료 시 페이드-인**(연출과도 일치).
2. **Screen Space Overlay(확정, `UiCanvasSetup`)**: 원근 없음 → **Z 변위 무효**. 크럼플은 **XY 변위 + 가짜
   크리스 AO(버텍스 음영)만**. "진짜 3D 종이"가 아니라 **음영 있는 평면 구김**으로 읽힌다(기대치).
3. **UGUI 셰이더 계보**: 프로젝트 UI 셰이더 선례 `DraftCardFoil_UI.shader` 는 **Built-in UI/Default 계열 CG**
   (`UnityUI.cginc`·stencil·`_ClipRect`/`UnityGet2DClipping`·`ZTest [unity_GUIZTestMode]`). URP ShaderGraph "UI"가
   아니다 — 그 scaffold 를 복사해야 마스킹/클립이 정상. 버텍스 변위 선례는 repo 에 **없음**(vert 는 stock
   passthrough), `IMeshModifier`/`BaseMeshEffect`/`OnPopulateMesh` 도 **없음** → 서브디바이드 Graphic 은 전부 신작.
4. **`_Unfold` 전송**: UGUI `Graphic`/`CanvasRenderer` 엔 MaterialPropertyBlock 경로가 없다. per-instance 머티리얼
   (`.material` 클론)은 5카드 = 5머티리얼 → **UI 배치 깨짐 + `Destroy`-on-resize 카드의 머티리얼 leak**. →
   **공유 머티리얼 1개 + `_Unfold` 를 버텍스 스트림(UV1/UV2/color)으로** 실어 배치 유지가 기본 계약.

## 구현 문서 목록 (예정 — 승인 후 파일 작성)

| # | 작업 구분 | 목적 |
|---|---|---|
| 0 | 서브디바이드 카드-페이스 Graphic + off 폴백 | 카드 면을 N×M 격자로 방출(UV/preserveAspect 보존). **frame.color setter·`raycastTarget`·CanvasGroup 계약 보존**. `_Unfold=1` rest=현재 픽셀 동일. **off 폴백=즉시 평면(현재 딜)** 을 처음부터. |
| 0.5 | 실기 perf spike | 5장 worst-case 해상도 Android 프로파일 → **서브디바이드 해상도 + `_Unfold` 전송(공유 머티리얼+버텍스 스트림)** 확정. unit 1 셰이더 착수 게이트. |
| 1 | 크럼플 UGUI 셰이더 | **UI/Default 계열 CG**(stencil/`_ClipRect`/`_GUIZTestMode` = `DraftCardFoil_UI.shader` 재사용). `_Unfold` 0→1: **XY** 노이즈 변위 + 가짜 크리스 AO. Z inert. |
| 2 | 딜 통합 + 라이프사이클 + 텍스트 alpha | `_Unfold` 를 `_dealSeq` 안 `Tween.Custom`(딜과 동일 stagger). 텍스트/배지 **별도 alpha 권한**으로 펴짐 끝 페이드-인. teardown/restore 강제 `_Unfold=1`, Refresh=평면 바인드(재구김 금지). |
| 3 | 스타일 확정 + 시각 폴리시 + 실기 재검증 | 크럼플 스타일(D2) 확정, 크리스 음영/타이밍 폴리시, Android 재프로파일. |
| 4 | handoff | 인계 요약. |

## feature-wide 계약 (초안 rev1)

- **rest 무회귀**: `_Unfold=1` = 현재 카드 면과 픽셀 동일. off 폴백 시 현재 딜과 완전 동일(escape hatch, unit 0 부터).
- **`_Unfold` 전송 = 공유 머티리얼 1개 + 버텍스 스트림**. per-instance 머티리얼은 배치/leak 근거로 금지(실기서
  버텍스 스트림이 불충분 판정될 때만 재고).
- **카드-페이스 그래픽은 기존 계약 보존**: `frame.color` setter(BindCard/dim), `raycastTarget=true`(드래그 픽킹은
  rect 기반이라 변위해도 히트테스트 무손상, 단 Graphic 상존·raycastable 유지), root `CanvasGroup`(dim 0.42) 호환.
  완료기준에 **drag/press-lift/dim 무회귀** 포함.
- **텍스트/배지 alpha 는 dim 과 별개 권한**: root CanvasGroup(=dim) 로 텍스트만 못 줄인다 → 자식 CanvasGroup 또는
  요소별 alpha 로 소유하고, 사용불가 dim(0.42)·press-lift 와의 합성 순서를 명시(unit 2).
- **`_Unfold` 라이프사이클**: `StopDeal`/`ForceClose`/`RestoreSlotHome`/`OnSinkComplete` 에서 **강제 1(평면)**.
  `Refresh`/`BindCard`(HandChanged Used/Recovered)는 이미 뽑힌 카드라 **평면으로 바인드·재구김 금지** — 크럼플은
  오직 `Open`→`StartDeal` 경로만. `_Unfold` 트윈은 `_dealSeq` 멤버라 `Stop()` 이 함께 정리.
- **크럼플 = XY 변위 + 가짜 AO**(Overlay Z inert). 무거운 per-pixel 금지, 실기 프로파일 게이트.
- **모션과 준(準)직교**: 구김은 카드 **내부 메시** 변형, 스프링/press/idle/딜의 RectTransform 모션과 별개 축.
  단 root Image 교체로 인한 계약 충돌(위)은 명시적으로 보존한다(완전 직교 아님).
- **②-A 흡수**: 서브디바이드 해상도가 매끈한 저주파 곡률을 낼 만큼이면 "살짝 휘어짐(②-A)"은 같은 메시의
  진폭/주파수 특수케이스 → 별도 작업 불필요(해상도는 unit 0.5 perf spike 와 결합해 결정).
- **순수 프레젠테이션. ECS 변경 0, 채널 변경 0.** (critic: 경계/맥락 우려 없음 — 리스크는 전적으로 UGUI 메시/셰이더 배관.)

## 파이프라인 커버리지

N/A — 플레이 오브젝트(유닛/적/투사체/해저드/VFX) 신설·생성→렌더 경로 변경 없음. 대상은 손패 카드 위젯의
**카드-페이스 메시 + UGUI 셰이더** 뿐(런타임 빌드 UGUI). `object-pipeline-map` 무관.

## 확정된 결정 (2026-07-13)

- **D1 — 크럼플 대상 = art 자식만**(저위험 a 확정). art 자식(진짜 스프라이트)만 서브디바이드/크럼플, frame 은
  평면 유지. frame+art 합성(b)은 preserveAspect letterbox 재현 비용으로 보류.
  **에스컬레이션 트리거**: unit 0.5/3 실기 검증에서 "리지드 프레임 안 art 만 구겨짐"이 어색하면 → 같은 변위 필드를
  공유하는 두-그래픽(frame+art)으로 승격(b-lite). 저위험부터 가되 미학 리스크는 검증 게이트에서 판정.
- **D2 — 크럼플 스타일 = 종이-볼 구김**(방사/노이즈 크리스). 접힌-편지는 대안 후속.
- **D3 — `_Unfold` 타이밍 = 안착보다 살짝 늦게 끝**(펴지고 자리잡음). unit 2 에서 미세 튜닝.

## 비목표 / 후속 후보

- **카드 premium 이펙트(foil/holo/빛반사) 축** [별도 spec] · 크럼플(베이스 메시 지오메트리)과 **직교하는 오버레이
  머티리얼 축** — 서로 안 건드려 나중에 공존. **대부분 이미 존재**: `Assets/_Project/Scripts/UI/Draft/DraftCardVfxDriver.cs`
  + `DraftCardFoil_UI.shader`(등급별 foil `_Intensity`/`_HueShift`/shimmer/ember, **`_Tilt`=카드 각도로 페이크한 light**,
  하스스톤/포켓몬TCG 방식). 드림캐쳐 손패 적용 = **재사용/배선** + fractal/노이즈 iridescence 셰이더 확장 +
  **카드 등급/premium 매핑(제품 결정)**. crumple 과 별도로 진행.
- **실 라이팅/노멀 기반 3D 종이**(RenderTexture+월드 쿼드) — 이번 경로에서 명시 배제(모바일 비용·Overlay). 별도 spec.
- **카드별 다른 구김 시드**(index 결정론) — 초안은 동일 필드+위상차. 다양화는 후속.
- **사용/버림 시 되구김**(펴짐 역재생 → 침강) — 퇴장 침강(deal-in unit 4)에 얹는 후속.
- **procedural Texture leak 주의**: 크럼플 그래픽이 `EnsureSlots` 재빌드마다 Texture2D 를 굽지 않도록(UiRoundedSprite/
  score-hud lesson). 굽는다면 캐시/해제 경로 명시.

## 연결 문서

- 선행(완료): `docs/spec/dreamcatcher-hand-deal-in/`(아치 부채+스프링+덱-드로우 딜+press-lift+idle). 이 spec 은 그
  딜 등장에 카드-면 크럼플을 얹는다.
- UGUI 셰이더 scaffold 재사용원: `DraftCardFoil_UI.shader`(stencil/`_ClipRect`/`_GUIZTestMode`).
- 대상 코드(예정): 신규 `Assets/_Project/Scripts/UI/Dreamcatcher/UiCardFaceMesh.cs`(가칭) + UGUI 셰이더,
  `DreamcatcherHandView.StartDeal`/teardown 훅.
- 저작 스킬: `.claude/skills/unity-vfx-authoring/`(셰이더/머티리얼 준비 관례 참고).
