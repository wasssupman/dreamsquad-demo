# Spec — Depth-Map Parallax Module

> 상태: 완료 (2026-07-15) — u0~u9 구현·검증·사용자 Play 확인. 3 컷신 유닛(Guardian/Ranger/Archer) 뎁스 적용.
> 인계 지도는 `10_handoff_summary.md`.

## 상위 목표

2D 아트(컷신 프레임·카드 일러스트)에서 추출한 **뎁스맵**을 이용해, 스와이프/틸트 방향에 따라
아트가 **3D 공간에서 살짝 회전하는 듯한** 착시(패럴랙스)를 준다. Marvel Snap 카드류의 "2.5D"
기법이다. 실제 지오메트리 회전이 아니라 **뎁스 기반 UV 패럴랙스 + 클립공간 사다리꼴 + 하이라이트
스윕** 3중 큐를 얹은 UGUI 프래그먼트 셰이더 트릭이라 모바일 부담이 적다.

이 기능을 **전용 in-repo asmdef 모듈** `Wassup.DepthParallax` 로 캡슐화한다. 모듈은 게임 코드에
전혀 의존하지 않고(`references: []`), 폴더 복사만으로 다른 프로젝트/컨텐츠에 이식 가능하다. 첫
소비처는 **배치 컷신**(49프레임 줌 플립북)이고, **드림캐쳐 손패 카드**를 두 번째 소비처로 상정해
API 를 일반화한다.

## 검증 질문

- Defender_Ranger 를 드래그로 집어 컷신이 뜬 상태에서, 스와이프 방향으로 손가락을 움직이면
  컷신 아트가 그 방향으로 3D 회전하듯 기울고, 손을 떼면(또는 드래그가 끝나도) 틸트가 부드럽게
  0 으로 복귀하는가?
- 틸트가 0 일 때 출력이 **원본 스프라이트와 픽셀 단위로 동일**한가? (rest = 완전 no-op)
- 뎁스맵이 없는 유닛/카드는 아무 패럴랙스 없이 기존과 동일하게 렌더되는가?
- 모듈을 `DepthParallaxView` 컴포넌트로 정적 카드에 붙였을 때, 컷신 코드 없이 단독으로 동작하는가?
- 모바일 실기기에서 dependent texture read 가 1회로 유지되고, 공유 머티리얼 배칭이 깨지지 않는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_module_scaffold_and_settings.md` | 모듈 asmdef + `DepthParallaxSettings` SO | 무의존 경계 + 제네릭 튜너블 스키마 |
| 1 | `1_parallax_math.md` | `DepthParallaxMath` 순수 함수 + EditMode 테스트 | uv 오프셋·스프링 스텝 결정 로직 |
| 2 | `2_ugui_shader.md` | `DepthParallax_UI.shader` + 기본 머티리얼 | 3중 큐(패럴랙스·사다리꼴·하이라이트) |
| 3 | `3_rest_state_verify.md` | tilt=0 오프스크린 diff 하네스 | rest no-op 불변식 게이트 |
| 4 | `4_depth_baker_editor.md` | `DepthMapBaker`(Editor) + 오프라인 bake 문서 | PNG→R8 뎁스 에셋 임포트 파이프라인 |
| 5 | `5_runtime_view_component.md` | `DepthParallaxView` MonoBehaviour | 정적 컨텐츠용 제네릭 소비 컴포넌트 |
| 6 | `6_cutscene_player_extension.md` | `DeployCutscenePlayer` 틸트 스프링 + 뎁스 스왑 + asmdef 배선 | 플립북 소비처 통합 |
| 7 | `7_controller_swipe_feed.md` | 컨트롤러 스와이프→틸트 피드 + SO 필드 | 드래그 입력 연결 |
| 8 | `8_defender_depth_assets.md` | Ranger/Archer 뎁스 프레임 bake·핸드터치·임포트 | 실제 컷신 뎁스 자산 |
| 9 | `9_mobile_tuning_verify.md` | 실기기 튜닝 + Play 검증 | 진폭·배칭·스프링 최종 확인 |

의존 순서: 계약/수학/셰이더/no-op(0–3) → Editor baker(4) → 런타임 컴포넌트(5) →
컷신·컨트롤러 통합(6–7) → 자산(8) → 튜닝/검증(9).

## 공통 원칙 / Feature-wide 계약

- **모듈 경계 (하드)**: `Assets/_Project/Modules/DepthParallax/` 아래 자체 asmdef. Runtime asmdef
  는 `references: []`(UnityEngine 암시). **절대 `Wassup.Runtime` 을 참조하지 않는다.** 소비 방향은
  단방향: `Wassup.Runtime` 이 `Wassup.DepthParallax` 를 참조한다(한 줄 추가). Editor baker asmdef
  는 `includePlatforms:["Editor"]` 라 플레이어 빌드에서 자동 제외(`Wassup.Editor.UnitStatImport` 와 동일).
- **콘텐츠 무지 (하드)**: 모듈 타입은 `DefenderUnitData`·컷신·카드 등 **소비처 타입을 절대 import 하지
  않는다.** 공개 API 는 plain 값만 받는다 — `SetTilt(Vector2)`, `Texture depthMap`, `Color tint`,
  `DepthParallaxSettings`. 소비처(컨트롤러)가 유일한 번역기다. (`DragSwaySettings.enableDeployCutscene`
  처럼 제네릭 SO 에 컨텐츠 플래그를 새는 것은 **금지** — 이번에 피하는 안티패턴이다.)
- **설정 소유 분리**: 모듈은 *스키마 + 기본값 + 수학* 을 소유. 소비처는 *인스턴스 + 오브젝트별 값* 을
  소유. `DepthParallaxSettings.asset` 인스턴스는 소비처의 `Assets/_Project/Data/` 에 둔다. 유닛별
  프레임/뎁스/틸트는 런타임에 API 로 주입, 모듈 SO 에 굽지 않는다. (제약 10 의 shape — 값은
  아키텍처를 모른 채 흐른다.)
- **rest = no-op 불변식 (하드)**: `_Tilt==(0,0)` 이면 출력이 원본 스프라이트와 픽셀 동일. 모든 큐 항은
  `_Tilt`(또는 `length(_Tilt)`)에 곱해져 상수 bias 가 0. `_Time` sheen 금지, 최종 UV 클램프 금지(오프셋만
  클램프). unit 3 오프스크린 diff 로 게이트.
- **스프링 수학 중복 (의도된 tradeoff)**: 모듈은 무의존 경계라 `Wassup.Runtime` 의 `KeyringSim.SpringStep`
  을 참조할 수 없다. 따라서 `DepthParallaxMath.SpringStep` 은 검증된 임계감쇠 스프링의 **모듈 로컬 포트**
  다(≈10줄). 세 번째 소비처가 생기면 공유 math 모듈 추출을 검토(→ 후속 후보). 지금은 경계 유지가 우선.
- **모바일 안전 (하드)**: dependent texture read **정확히 1회**(총 2 샘플). POM·동적 루프 금지. 진폭(peak UV
  오프셋) **≤4%**(중심 피벗이 peak 를 `±0.5·_Amplitude` 로 절반화). 뎁스 = R8·half-res·linear·mip off·무압축.
  머티리얼은 **소비처별 per-instance 인스턴스**(진짜 per-instance 선례 = `UiCardFaceMesh`; `GiftCardWidget`
  은 런타임 생성+Dispose 패턴 선례이나 foil 을 *공유*하므로 per-object 뎁스에는 복제 금지 — UGUI Image 는
  MPB 경로가 제한적이라 인스턴스가 표준). 프레임/틸트 변화는 **`SetTexture`/`SetVector`/`SetFloat` 만**으로 적용,
  **런타임 머티리얼 스왑 금지**(스왑=캔버스 리빌드). per-instance 머티리얼·절차 텍스처는 `OnDestroy` Dispose.
  패럴랙스 콘텐츠는 자체 캔버스/연속 draw order 에 두어 무관 UI 배치를 쪼개지 않게(per-instance 라도 per-object
  텍스처라 어차피 cross-batch 안 됨).
- **뎁스 소스**: Depth Anything V2 **Small**(Apache-2.0, 상용 안전)로 오프라인 bake. **기본은 단일 정적
  뎁스 1장을 전 프레임 공유**(줌이 미세하고 진폭 ≤4% 라 sub-perceptual; `deployCutsceneDepth` 길이 1).
  실루엣이 실제로 움직이는 유닛만 프레임별로 에스컬레이션하되 **프레임별 독립 추출 금지**(flicker) —
  대표 1장 측정 정렬 워프 또는 프레임별 추론+글로벌 정규화. (이 art 는 프로그램적 줌이 아니라 리프로젝션
  변환이 ground-truth 로 주어지지 않음 — "알려진 줌 배율" 가정 금지.) 정규화는 전 프레임 **글로벌
  퍼센타일**(2/98). 흰색=near(`_DepthSign` 극성 반전). **CC-BY-NC 모델(DA-V2 Base/Large/Giant, Depth Pro)
  금지** — 상용 자산 파이프라인 위반.
- **셰이더 스캐폴드**: `DraftCardFoil_UI`/`CardCrumple_UI` 의 UGUI 스캐폴드(Stencil·`_ClipRect`·
  `unity_GUIZTestMode`·`[PerRendererData] _MainTex`·Transparent)를 **그대로** 복제. ScreenSpaceOverlay 는
  URP 이후 렌더라 `_CameraDepthTexture` 없음 → 셰이더가 **자기 뎁스**를 샘플. 사다리꼴 코너 부호는
  **UV0** 에서 유도(per-vertex 채널 금지 → `additionalShaderChannels` 스트립 함정 회피).
- **틸트 독립 수명**: 틸트 스프링은 소비처(컷신 플레이어/View)가 소유. 소비처는 매 프레임 `SetTilt` 를
  피드하고, **staleness watchdog**(~0.06s 무피드 → target=0)로 릴리즈. 드래그가 컷신 중간에 끝나도
  플레이어는 드래그 수명을 몰라도 자동으로 0 복귀. 시간은 `Time.unscaledDeltaTime`.

## Two-surface (플레이어 vs View)

이 모듈은 두 소비 표면을 가진다. 둘 다 **같은 셰이더 + `DepthParallaxMath` + `DepthParallaxSettings`**
를 공유하지만 호스트가 다르다:

- **플립북(컷신)**: 실제 소비처는 기존 `DeployCutscenePlayer` 다(unit 6, in-place 확장). 자체 프레임
  루프로 색·뎁스를 lockstep 스왑하고 틸트 스프링을 직접 소유하므로, `DepthParallaxView` MonoBehaviour 를
  호스트하지 **않는다**.
- **정적 콘텐츠(카드·로비 캐릭터)**: `DepthParallaxView`(unit 5)가 단일 스프라이트+뎁스에 틸트 패럴랙스를
  주는 재사용 컴포넌트. 붙이고 `SetTilt` 만 먹이면 된다.

즉 **셰이더와 순수 수학이 진짜 공유 코어**이고, `DepthParallaxView` 는 비플립북 케이스용 편의 래퍼다.

## 파이프라인 커버리지

N/A — 전투 플레이 오브젝트(유닛/적/투사체/해저드)가 아닌 UI 오버레이/카드 프레젠테이션 연출.
스폰→렌더 파이프라인(`docs/reference/object-pipeline-map.md`) 대상이 아니다.
(cf. `defender-deploy-cutscene`·`outgame-lobby-characters` 도 같은 사유로 N/A.)

## 후속 후보 (이번 스코프 밖)

- **드림캐쳐 손패 카드 적용**: `DepthParallaxView` 를 카드 위젯에 배선 + 카드 일러스트 뎁스 bake.
  (이번엔 API 가 카드를 수용하도록 설계만; 실제 배선은 별도 작업.)
- **로비 캐릭터 정적 패럴랙스**: outgame 로비 스프라이트에 마우스/자이로 틸트.
- **하이라이트 고급 변형**: `ddx/ddy` 노멀 기반 foil-grade 트윙클(히어로 카드용, blurred depth 필요).
- **공유 math 모듈 추출**: 세 번째 SpringStep 소비처가 생기면 `Wassup.Math` micro-asmdef 로 승격.
- **UPM 패키지화**: 외부 배포가 필요해지면 GUID 보존 이동으로 `Packages/com.wassup.depth-parallax` 승격
  (지금은 in-repo asmdef 가 상위집합의 부분집합이라 기계적 이관 가능).
- **뎁스 alpha-packing**: 정적 단일 스프라이트는 색 알파에 height 패킹(제로 추가 메모리) 옵션.
