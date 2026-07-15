# 0 — 모듈 스캐폴드 + 설정 스키마

## 목적

무의존 in-repo 모듈 경계를 세우고, 제네릭 튜너블만 담은 설정 SO 를 정의한다. 이후 모든 unit 이
이 위에서 컴파일된다.

## 변경 대상

- New: `Assets/_Project/Modules/DepthParallax/Runtime/Wassup.DepthParallax.asmdef`
- New: `Assets/_Project/Modules/DepthParallax/Runtime/DepthParallaxSettings.cs`

## 구현

- **asmdef**: `name: "Wassup.DepthParallax"`, `rootNamespace: "Wassup.DepthParallax"`,
  `references: []`(UnityEngine 암시), `autoReferenced: true`. **`Wassup.Runtime` 을 참조하지 않는다.**
  (Unity.Mathematics 가 실제로 필요해지면 그때만 추가 — 현재 `UnityEngine.Vector2` 로 충분.)
- **`DepthParallaxSettings : ScriptableObject`** — `[CreateAssetMenu(menuName="Wassup/Depth Parallax Settings")]`.
  제네릭 시각 튜너블 **만**:
  ```csharp
  [Header("Parallax")]
  public float amplitude = 0.02f;      // peak UV 오프셋(≤0.04). _Amplitude
  public float depthCenter = 0.5f;     // 힌지 평면(0..1). _DepthCenter
  public float depthSign = 1f;         // 극성(±1). _DepthSign
  [Header("Perspective / Highlight")]
  public float perspective = 0.05f;    // 클립공간 사다리꼴 세기. _Persp
  public float highlightStrength = 0.15f; // _HiStrength
  public float highlightWidth = 0.25f;    // _HiWidth
  [Header("Tilt Spring")]
  public float tiltSpring = 90f;
  public float tiltDamping = 2.2f;
  public float tiltMaxSpeed = 8f;      // 0=무제한
  public float tiltStalenessSeconds = 0.06f; // 무피드 → target 0
  public float tiltInputGain = 1f;     // 스와이프 속도→틸트 정규화 게인
  ```
- **소비처 타입 참조 절대 금지**. `DefenderUnitData`·컷신 플래그 등 없음.
- 파일 하나 = 순수 데이터 컨테이너. 로직 없음.

## 완료 기준

- 컴파일 통과(`read_console` clean). 모듈 asmdef 가 별도 어셈블리로 잡힘(Inspector 에서 확인).
- `Assets/Create > Wassup > Depth Parallax Settings` 로 `.asset` 생성 가능.
- `Wassup.DepthParallax.asmdef` 의 references 가 빈 배열임을 파일로 확인.
