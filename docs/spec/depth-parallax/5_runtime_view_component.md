# 5 — 콘텐츠 무지 런타임 컴포넌트

## 목적

정적 컨텐츠(드림캐쳐 카드·로비 캐릭터)가 붙여 쓰는 제네릭 소비 컴포넌트. 플립북이 아닌
단일 스프라이트+뎁스에 틸트 패럴랙스를 준다. 컷신 플레이어와 별개 경로(플레이어는 자체 플립북
루프라 이 컴포넌트를 호스트하지 않음 — README "Two-surface" 참조).

## 변경 대상

- New: `Assets/_Project/Modules/DepthParallax/Runtime/DepthParallaxView.cs`

## 구현

- **`DepthParallaxView : MonoBehaviour`** (namespace `Wassup.DepthParallax`, `Billboard.cs` 주입 shape):
  - SerializeField: `DepthParallaxSettings settings`, `Texture depthMap`, `Color tint = white`.
    (대상 `Image`/`RawImage` 는 같은 GO 에서 `GetComponent`, 또는 SerializeField 로 지정.)
  - `public void SetTilt(Vector2 tilt)` → `_tiltTarget=tilt; _lastFeedUnscaled=Time.unscaledTime;`.
  - 자체 `Update()`: staleness watchdog(무피드 > `settings.tiltStalenessSeconds` → target 0) →
    `DepthParallaxMath.SpringStep(...)` → 머티리얼에 `_Tilt`/`_DepthTex`/제네릭 파라미터 push.
  - **머티리얼(per-instance)**: `Shader.Find("Wassup/UI/DepthParallax")` → `new Material(sh){hideFlags=
    HideAndDontSave}`(per-instance 선례 `UiCardFaceMesh.cs`; `GiftCardWidget` 은 생성/Dispose 패턴만 —
    foil 을 공유하므로 복제 모델은 아님). 프레임/틸트/파라미터는 인스턴스에 `SetVector`/`SetTexture`/
    `SetFloat` 만. **런타임 머티리얼 스왑 금지**. `OnDestroy` Dispose(`UiCardFaceMesh.cs:182-188` 선례).
    (per-instance 라도 per-object 텍스처라 cross-batch 손해 없음.)
  - 뎁스 없으면(`depthMap==null`) 패럴랙스 skip(색만).
- **소비처 타입 무지**: 카드/컷신/디펜더 어떤 타입도 import 하지 않음. 입력은 plain 값뿐.
- 이 unit 은 모듈 단독 검증까지만. 실제 카드 배선은 후속 후보(스코프 밖).

## 완료 기준

- 컴파일 클린.
- 빈 씬에 UI Image + `DepthParallaxView`(임의 스프라이트/뎁스/SO) 배치 → `SetTilt` 호출 시
  패럴랙스, 무피드 0.06s 후 스프링 0 복귀(오프스크린/Play 확인).
- `Wassup.DepthParallax` 어셈블리가 소비처 어셈블리를 여전히 참조 안 함(경계 유지).
