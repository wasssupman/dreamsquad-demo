# 6 — DeployCutscenePlayer 통합 (플립북 소비처)

## 목적

배치 컷신 플레이어에 틸트 스프링 + 뎁스 프레임 lockstep 스왑 + 패럴랙스 머티리얼을 얹는다.
플레이어는 모듈의 셰이더/수학/SO 만 쓰고, `DefenderUnitData` 는 여전히 모름(컨트롤러가 번역).

## 변경 대상

- Modify: `Assets/_Project/Scripts/UI/DeployCutscenePlayer.cs`
- Modify: `Assets/_Project/Scripts/Wassup.Runtime.asmdef` — `references` 에 `"Wassup.DepthParallax"` 추가.
  **이 unit 이 모듈 타입(`DepthParallaxSettings`·`DepthParallaxMath`)을 처음 쓰는 Runtime 소비처라,
  참조를 여기서 배선해야 단독 커밋이 컴파일된다**(단방향 Runtime→모듈; 모듈은 여전히 Runtime 무참조).

## 구현

- **머티리얼 주입**: `EnsureCanvas()` 에서 `_image` 생성 직후(현 `:127`, `preserveAspect` `:129` 근처)
  `Shader.Find("Wassup/UI/DepthParallax")` → `new Material(sh){hideFlags=HideFlags.HideAndDontSave}`
  → `_image.material = _parallaxMat`. `OnDestroy`(`:145-148`)에서 Dispose. 셰이더 없으면(모듈 미존재)
  기본 머티리얼 유지 = 색만(그레이스풀).
- **틸트 스프링 소유**: 플레이어에 없던 `Update()` 추가. 필드 `Vector2 _tiltTarget,_tilt,_tiltVel;
  float _lastFeedUnscaled;`. `public void SetTilt(Vector2 t)` → target 갱신 + 타임스탬프.
  `Update()`: **맨 앞 `if(_parallaxMat==null) return;`** — 머티리얼은 첫 `Play` 의 `EnsureCanvas` 에서
  생성되므로 그 전(씬 로드~첫 드래그)엔 null. 가드 없으면 매 프레임 NRE(unit 9 "console clean" 위반)
  + 무의미한 idle 스프링 적분. 그 뒤 staleness(무피드 > `settings.tiltStalenessSeconds` → target 0) →
  `DepthParallaxMath.SpringStep` → `_parallaxMat.SetVector("_Tilt", _tilt)`. `Time.unscaledDeltaTime`
  (플레이어는 이미 unscaled). 드래그 종료·`CleanupSession` 은 플레이어를 안 건드리므로 자동 0 복귀.
- **뎁스 lockstep 스왑**: 확장 오버로드
  `Play(Sprite[] color, Texture2D[] depth, float fps, float unitScale, Vector2 unitOffset)`.
  기존 4-arg `Play` 는 `depth:null` 로 위임(하위호환). (틸트 게인은 플레이어가 아니라 컨트롤러가
  적용 — 아래 "설정 주입" + unit 7. `Play` 는 게인을 받지 않는다.) 첫 프레임 `_image.sprite=color[first]`
  (`:47`) 직후 + 루프 `idx!=shown` 블록(`:73` 이후) 같은 가드 안에서 인덱스 클램프로 스왑:
  ```csharp
  if (depth != null && depth.Length > 0) {
      int di = Mathf.Min(idx, depth.Length - 1);
      if (depth[di] != null) _parallaxMat.SetTexture("_DepthTex", depth[di]);
  }
  ```
  **뎁스 배열 길이 1 = 전 프레임 공유(정적 단일 뎁스, 기본 케이스), 길이 N = 프레임별.** 클램프라
  길이 1 이면 항상 frame 0 → 색만 애니되고 뎁스는 고정(줌이 미세한 컷신에 충분). 색/뎁스 desync 방지.
- **설정 주입**: 플레이어에 `DepthParallaxSettings` SerializeField(스프링/진폭/persp 등). 미할당이면
  클래스 기본 인스턴스 폴백(컨트롤러 SO 폴백 패턴과 동일). **유닛별 틸트 게인은 컨트롤러가 `SetTilt`
  전에 곱하므로 플레이어는 게인을 모른다**(단일 소유자 = 컨트롤러, unit 7).
- 모듈 타입만 import(`Wassup.DepthParallax`). `Wassup.Data`(DefenderUnitData) 는 import 하지 않음.

## 완료 기준

- 컴파일 클린. 기존 4-arg `Play` 호출부 무변경 동작(뎁스 null → 기존과 동일).
- 뎁스 배열을 넘긴 경우 프레임 진행에 맞춰 `_DepthTex` 가 색과 동기 스왑(코드 리뷰 + Play).
- `SetTilt` 무피드 시 스프링이 0 으로 복귀(Play 확인은 unit 9). 머티리얼 `OnDestroy` Dispose 확인.
