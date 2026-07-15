# 3 — LobbyBackgroundParallax 드라이버

## 목적

앰비언트 자동 드리프트 + 로비 캐릭터 드래그를 합산해 틸트를 만들고, **앞(디졸브)·뒤(모듈) 두
머티리얼에 동일한 `_Tilt`/`_DepthTex`** 를 밀어넣는다.

## 변경 대상

- New: `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundParallax.cs`
- Modify: `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` (드래그 신호 노출)
- Modify: `Assets/_Project/Scripts/UI/Outgame/LobbyBackgroundDissolve.cs` (런타임 머티리얼 접근자)

## 구현

- **드래그 신호**: `LobbyKeyringDrag` 에 static 이벤트 추가 — 코드베이스의 기존
  `LobbyReactionLock.ReactionStarted` 패턴을 따른다:
  ```csharp
  public static event System.Action<Vector2> DragMoved;   // 손가락 로컬 델타
  ```
  `OnDrag` 에서 프레임 델타를 발행, `OnEndDrag` 에서는 발행 안 함(무피드 → staleness 로 0 복귀).
- **`LobbyBackgroundParallax : MonoBehaviour`** (namespace `Wassup.UI`, 모듈 소비처):
  - SerializeField: `DepthParallaxSettings settings`, `Texture2D depthMap`,
    `LobbyBackgroundDissolve dissolve`(앞), `Image underImage`(뒤),
    `float ambientAmplitude = 0.25f`, `float ambientSpeedA/B`(다주기 sin — `CameraDirector` 브리딩 참고),
    `float dragGain = 1f`.
  - 뒤 Image 에 모듈 머티리얼 주입: `Shader.Find("Wassup/UI/DepthParallax")` → per-instance Material
    (`hideFlags=HideAndDontSave`), `OnDestroy` Dispose. **`_Persp`=0, `_HiStrength`=0** 강제(README 계약).
  - 앞은 `dissolve` 의 런타임 머티리얼에 push — `LobbyBackgroundDissolve` 에
    `public void SetParallax(Vector2 tilt, Texture depth)` 같은 얇은 접근자 추가(런타임 mat 은 계속
    디졸브가 소유; 외부는 값만 밀어넣음).
  - `Update()`:
    ```
    ambient = new Vector2(sin(t*speedA), sin(t*speedB + phase)) * ambientAmplitude;  // 다주기 = 반복 티 안 남
    target  = ClampMagnitude(ambient + _dragTilt * dragGain, 1f);
    DepthParallaxMath.SpringStep(ref _tilt, ref _tiltVel, target, s.tiltSpring, s.tiltDamping, s.tiltMaxSpeed, dt);
    → 앞/뒤 둘 다 SetVector("_Tilt", _tilt) + SetTexture("_DepthTex", depthMap)
    ```
  - `_dragTilt` 는 `DragMoved` 로 갱신 + staleness watchdog(무피드 → 0)로 릴리즈 — 컷신과 동일 계약.
  - 시간은 `Time.unscaledDeltaTime`(로비는 timeScale 영향 없어야).
- **모듈 경계**: 모듈은 로비 타입 무참조. 이 드라이버가 소비처.

## 완료 기준

- 컴파일 클린. 로비 진입 시 배경이 미세하게 상시 드리프트(앰비언트).
- 캐릭터를 끌면 그 방향으로 배경이 밀리고, 놓으면 스프링으로 앰비언트만 남게 복귀.
- 앞/뒤 Image 의 `_Tilt` 가 **항상 동일**(디졸브 전환 중 어긋남 없음).
- 머티리얼 `OnDestroy` Dispose 확인(leak 없음). 콘솔 클린.
