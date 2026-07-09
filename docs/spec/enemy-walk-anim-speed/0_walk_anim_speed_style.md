# 0 — WalkAnimSpeedStyle SO + BattleBridge 정적 미러

## 목적

걷기 애니 속도 변조 파라미터를 하드코딩 대신 ScriptableObject 로 두고, `SpineUnitView` 가 읽을 수 있게 BattleBridge 정적 미러로 노출한다. SO 미할당 시 배율 1.0(현행 동작) 보장.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/WalkAnimSpeedStyle.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — SerializeField + 정적 미러 + 초기화 세팅

## 구현

### WalkAnimSpeedStyle (SO)

```csharp
[CreateAssetMenu(menuName = "Wassup/Presentation/Walk Anim Speed Style")]
public class WalkAnimSpeedStyle : ScriptableObject
{
    [Tooltip("walkFactor 1.0 이 되는 기준 이동속도(view units/sec, sim-time 기준).")]
    public float referenceSpeed = 2.5f;
    [Tooltip("애니 timeScale 배율 하한. 0=정지 시 완전 프리즈, >0=미세 idle.")]
    public float minTimeScale = 0.15f;
    [Tooltip("애니 timeScale 배율 상한(빠른 적 과속 방지).")]
    public float maxTimeScale = 2.0f;
    [Tooltip("측정 속도 지수 스무딩(0=고정, 1=즉시). 프레임 노이즈 억제.")]
    [Range(0f, 1f)] public float smoothing = 0.2f;
    [Tooltip("한 프레임 view 변위가 이 값을 넘으면 텔레포트로 보고 측정 스킵.")]
    public float teleportGuard = 1.5f;
}
```

기본값은 초안 추정치 — unit 2 Play 튜닝에서 확정한다.

### BattleBridge 정적 미러

기존 `CharacterVisualScale` / `BlobShadow*` 패턴과 동일:

- `[SerializeField] private WalkAnimSpeedStyle walkAnimSpeedStyle;`
- `public static float WalkAnimRefSpeed`, `WalkAnimMinTimeScale`, `WalkAnimMaxTimeScale`, `WalkAnimSmoothing`, `WalkAnimTeleportGuard` (get; private set;)
- `public static bool WalkAnimSpeedEnabled` — SO 할당 여부. false 면 뷰가 변조 안 함(1.0).
- 초기화(다른 미러를 세팅하는 지점과 동일 메서드)에서 SO→정적 복사. SO null 이면 `WalkAnimSpeedEnabled=false`.

## 완료 기준

- compile 성공, `read_console` 에러 0.
- `WalkAnimSpeedStyle` 이 CreateAssetMenu 로 생성 가능.
- BattleBridge 정적 미러가 SO 값으로 세팅되고, SO null 일 때 `WalkAnimSpeedEnabled=false`.
- 이 단위에서는 뷰 동작 변화 없음(미러만 준비).
