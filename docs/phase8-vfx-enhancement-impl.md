# Phase 8 §13 — VFX Enhancement 구현 문서

> Plan v0.1 (`phase8-vfx-enhancement-plan.md`) + Codex round 1/2 리뷰 전부 반영. 본 문서는 **실제 작업 지시** 수준의 구체 명세. Step 1~4 각각 커밋 단위. 판정: **Yellow** 항목 2건(BeamPulse 데드코드 / Tornado lifetime 명시)은 아래에서 해결됨.

## 0. 범위

- 4개 `_SKELETON.prefab` 생성: Placement / Meteor / Tornado / Portal
- VfxSpawner 에 prefab slot + early-return 폴백
- Portal 전용 `BeamPulse.cs` 경량 MB (MPB 제거)
- 씬 와이어링 + 단계별 커밋
- Shader Graph / 사운드 큐 / Meteor Warning Ring / HDR+bloom = **스코프 밖**

## 1. 최종 결정 통합 (Q&A + Codex 1차/2차)

| 항목 | 값 |
|---|---|
| 프리팹 작성 주체 | 에이전트 skeleton + 사용자 폴리시 (Q1=c) |
| Shader Graph | 템플릿 복사만, JSON 생성 X (Q2 변형) |
| ECS 브리지 | 시간 주도권 기준 (Q3=a) |
| VfxSpawner 폴백 | early-return 패턴 + `Debug.LogWarning` (R2 Q4) |
| HDR+bloom | **보류** — bloom-safe 일반색 먼저 (R1 High) |
| Beam pulse | `startColor/endColor` alpha Sin, **MPB 없음** (R2 Q2) |
| `_SKELETON` 접미사 | **영구 유지** (R2 Q3) |
| AutoDestroy | **불필요** (R2 Q1) |
| Destroy margin | 0.1s (R2 D) |
| 커밋 granularity | 이펙트별 1 커밋 (R2 C) |

## 2. 씬 배경 검증 항목 (Step 1 이전)

- Main Camera Background Color 확인
- Tile Material color 확인
- `#C8A882` (1순위) vs `#E8D4B0` (2순위) 중 GroundDust 색 결정

이 검증은 Step 1 시작 전 수행.

## 3. 프리팹 스펙 (계층 + 파라미터 + lifetime)

### 3.1 Tornado_SKELETON.prefab (Step 1)

```
Tornado_SKELETON [GO, Transform]
├── OuterDonut [ParticleSystem, ParticleSystemRenderer(sharedMaterial=Tornado_Mat)]
├── InnerSpiral [ParticleSystem, ParticleSystemRenderer(sharedMaterial=Tornado_Mat)]
└── GroundDust [ParticleSystem, ParticleSystemRenderer(sharedMaterial=Tornado_Mat)]
```

| PS | Shape | rate | lifetime | startSize | startColor | velocityOverLifetime | steady-state |
|---|---|---|---|---|---|---|---|
| OuterDonut | Circle(radius=0.9, radiusThickness=0) | 30 | 1.0 | 0.2 | `#99F2FF` α0.8 | orbitalY=6 | 30 |
| InnerSpiral | Circle(radius=0.5, radiusThickness=0) | 12 | 0.8 | 0.15 | `#CCF0FF` α0.6 | orbitalY=8 | 10 |
| GroundDust | Circle(radius=1.0, 위치 Y=-0.05) | 5 | 1.5 | 0.25 | `#C8A882` α0.5 | Y=0.2 | 8 |

- 합계 steady-state = 30+10+8 = 48 → MaxParticles=50 clamp 에 수용 ✓
- Loop=true, Duration=2.0s, playOnAwake=true (prefab 은 즉시 플레이)
- Simulation Space: Local

### 3.2 Meteor_Burst_SKELETON.prefab (Step 2)

```
Meteor_Burst_SKELETON [GO]
├── CoreFlash [ParticleSystem, ParticleSystemRenderer(Meteor_Mat)]
├── MainBurst [ParticleSystem, ParticleSystemRenderer(Meteor_Mat)]
└── Debris [ParticleSystem, ParticleSystemRenderer(Meteor_Mat)]
```

| PS | Shape | burst | lifetime | startSize (curve) | color (gradient) |
|---|---|---|---|---|---|
| CoreFlash | Sphere(radius=0.1) | 10 @ t=0 | 0.08 | 0.8 → 0 | `#FFE566` α1.0 (bloom-safe) |
| MainBurst | Hemisphere(radius=0.5) | 60 @ t=0 | 0.9 | curve 1→1.8@0.1s, sustain 0.25s | yellow(0)→orange(0.15)→dark-orange(0.22)→black-alpha0(0.3) |
| Debris | Hemisphere(radius=0.4) | 30 @ t=0 | 0.9 | 0.05~0.15 random | `#FFA050` α0.9 |

- 합계 burst = 10+60+30 = 100 → MaxParticles=100 clamp 경계 ✓
- Debris: gravity=0.6, startSpeed random 3.0~4.5
- Loop=false, Duration=0.3s
- **동시 발사 주의**: BattleBridge MeteorResolutionSystem 확인 필요 — 2발 동시면 200 particle 순간 공존 가능. 파동 설계 점검 Step 2 착수 전.

### 3.3 Portal_SKELETON.prefab (Step 3)

```
Portal_SKELETON [GO, BeamPulse MB]
├── Entry [GO]
│   ├── RimRing [ParticleSystem(Portal_Mat)]
│   └── InnerSpark [ParticleSystem(Portal_Mat)]
├── Exit [GO]
│   ├── RimRing [ParticleSystem(Portal_Mat)]
│   └── InnerSpark [ParticleSystem(Portal_Mat)]
└── LinkBeam [LineRenderer(sharedMaterial=Portal_BeamMat)]
```

| PS | Shape | rate | lifetime | startColor | velocity |
|---|---|---|---|---|---|
| Entry/RimRing | Donut(r=0.4, thickness=0.08) | 25 | 1.0 | `#C266FF` α0.9 | orbitalY=5 |
| Entry/InnerSpark | Sphere(r=0.15) | 10 | 0.6 | White α0.8 | 0 |
| Exit/RimRing | Donut(r=0.4, thickness=0.08) | 25 | 1.0 | `#8040FF` α0.9 | orbitalY=-5 |
| Exit/InnerSpark | Sphere(r=0.15) | 10 | 0.6 | `#40C0FF` α0.8 | 0 |

- 합계 steady-state ≈ 25+6+25+6 = 62 → MaxParticles=90 clamp ✓
- LinkBeam: `LineRenderer(positionCount=2, width 0.12 고정, useWorldSpace=true, startColor=#C266FF, endColor=#8040FF)`
- Loop=true, Duration=8.0s
- **BeamPulse MB 가 루트 GO 에 부착 — prefab 내 유일한 스크립트**
- Simulation Space: Local

### 3.4 Placement_SKELETON.prefab (Step 4, 카탈로그 승인 후)

```
Placement_SKELETON [GO]
├── Ring [ParticleSystem(Placement_Mat)]
├── CenterFlash [ParticleSystem(Placement_Mat)]
└── RisingMotes [ParticleSystem(Placement_Mat)]
```

| PS | Shape | burst | lifetime | startSize (curve) | velocity |
|---|---|---|---|---|---|
| Ring | Circle(radius=0.1, thickness=0) | 30 @ t=0 | 0.35 | 0.3 → 1.0 | radial 2.5 |
| CenterFlash | Sphere(r=0.1) | 5 @ t=0 | 0.08 | 0.5 → 0 | 0 |
| RisingMotes | Circle(radius=0.25) | 10 @ t=0 | 0.25 | 0.1 | (0, 0.7, 0.3) speed 0.3 |

- 합계 burst = 30+5+10 = 45 → MaxParticles=50 clamp ✓
- Loop=false, Duration=0.35s
- Ring startColor=`#4DCCE5` α0.9, CenterFlash=White α1.0, RisingMotes=`#B0D8E5` α0.6

## 4. Material 스펙

| Material | Shader | Surface | Blend | `_BaseColor` |
|---|---|---|---|---|
| Tornado_Mat | URP/Particles/Unlit | Transparent | Alpha | White (per-PS startColor 활용) |
| Meteor_Mat | URP/Particles/Unlit | Transparent | **Additive** | White |
| Portal_Mat | URP/Particles/Unlit | Transparent | Alpha | White |
| Portal_BeamMat | URP/Unlit | Transparent | Alpha | `#C266FF` |
| Placement_Mat | URP/Particles/Unlit | Transparent | Alpha | White |

위치: `Assets/_Project/VFX/Materials/`

## 5. VfxSpawner.cs 변경 사양

### 5.1 신규 SerializeField

```csharp
[Header("Phase 8 §13 — Prefab slots (null → code fallback)")]
[SerializeField] private GameObject placementRingPrefab;
[SerializeField] private GameObject meteorBurstPrefab;
[SerializeField] private GameObject tornadoPrefab;
[SerializeField] private GameObject portalPrefab;
```

### 5.2 TrySpawnPrefab 래퍼 (private)

```csharp
private bool TrySpawnPrefab(GameObject prefab, Vector3 pos, out GameObject go)
{
    if (prefab == null) { go = null; return false; }
    go = Instantiate(prefab, pos, Quaternion.identity, transform);
    return true;
}
```

### 5.3 각 Spawn 메서드 early-return 패턴

```csharp
public void SpawnTornado(Vector3 centerWorld, float radiusWorld, float durationSec)
{
    var pos = new Vector3(centerWorld.x, centerWorld.y + 0.05f, centerWorld.z);
    if (TrySpawnPrefab(tornadoPrefab, pos, out var go))
    {
        go.transform.localScale = Vector3.one * Mathf.Max(0.1f, radiusWorld); // optional scale sync
        Destroy(go, durationSec + 0.1f);
        return;
    }
    Debug.LogWarning("[VfxSpawner] tornadoPrefab 미할당 — 코드 폴백 사용");
    // ... 기존 코드 프리셋 (그대로 유지, 폴백)
}
```

Placement/Meteor/Portal 도 동일 패턴.

### 5.4 Portal 특수 처리

Portal prefab 은 2점(entry/exit) + LinkBeam 이라 위치 조정이 단순 `pos` 아님:

```csharp
public void SpawnPortal(Vector3 entryWorld, Vector3 exitWorld, float durationSec)
{
    if (TrySpawnPrefab(portalPrefab, Vector3.zero, out var root))
    {
        var entry = root.transform.Find("Entry");
        var exit = root.transform.Find("Exit");
        if (entry != null) entry.position = new Vector3(entryWorld.x, entryWorld.y + 0.05f, entryWorld.z);
        if (exit != null) exit.position = new Vector3(exitWorld.x, exitWorld.y + 0.05f, exitWorld.z);
        var line = root.transform.Find("LinkBeam")?.GetComponent<LineRenderer>();
        if (line != null)
        {
            line.SetPosition(0, new Vector3(entryWorld.x, entryWorld.y + 0.15f, entryWorld.z));
            line.SetPosition(1, new Vector3(exitWorld.x, exitWorld.y + 0.15f, exitWorld.z));
        }
        Destroy(root, durationSec + 0.1f);
        return;
    }
    Debug.LogWarning("[VfxSpawner] portalPrefab 미할당 — 코드 폴백 사용");
    // ... 기존 코드 폴백
}
```

## 6. BeamPulse.cs (최종, MPB 제거)

```csharp
using UnityEngine;

namespace Wassup.Presentation
{
    // Phase 8 §13 — Portal link beam alpha pulse.
    // LineRenderer.startColor/endColor 직접 대입은 내부적으로 dirty flag 를
    // partial update 경로로 처리하므로 vertex count 2 수준에서 비용 무시 가능.
    // MaterialPropertyBlock 경로는 LineRenderer vertex color 가 _BaseColor 를
    // 곱하기 구조라 alpha 전달이 불확실 — 직접 대입이 안전.
    [DisallowMultipleComponent]
    public class BeamPulse : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private float frequency = 2.5f;
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.4f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 1.0f;

        private void Awake()
        {
            if (line == null) line = GetComponent<LineRenderer>();
        }

        private void Update()
        {
            if (line == null) return;
            float t = (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(alphaMin, alphaMax, t);
            var sc = line.startColor; sc.a = alpha; line.startColor = sc;
            var ec = line.endColor;   ec.a = alpha; line.endColor = ec;
        }
    }
}
```

위치: `Assets/_Project/Scripts/Presentation/BeamPulse.cs`

## 7. 씬 와이어링 체크리스트

- [ ] `Assets/_Project/VFX/Materials/*.mat` 5개 생성
- [ ] `Assets/_Project/VFX/Tornado_SKELETON.prefab` 생성
- [ ] `VfxSpawner.tornadoPrefab` slot 할당 (UnityMCP execute_code + reflection + SaveScene)
- [ ] 이하 Meteor/Portal/Placement 동일
- [ ] 각 slot 할당 후 `grep` 으로 BattleScene.unity 에 `fileID` 확정 확인
- [ ] `unity-feature-wiring` 스킬 checklist 준수

## 8. 단계별 실행 (각 1 커밋)

### Step 1: Tornado
1. Tornado_Mat, Tornado_SKELETON.prefab 생성 (UnityMCP)
2. VfxSpawner.cs 수정 (tornadoPrefab slot + TrySpawnPrefab + early-return)
3. 씬 slot 할당 + SaveScene
4. Unity 컴파일 확인 (0 에러)
5. Play 모드 테스트: Tornado 캐스트 → prefab 경로 확인, slot 제거해 폴백 확인, 경고 로그 확인
6. commit: `feat(phase8): Tornado_SKELETON prefab + VfxSpawner 슬롯 + 폴백 경고`

### Step 2: Meteor
- BattleBridge MeteorResolutionSystem 동시 발사 가능성 점검
- Meteor_Mat, Meteor_Burst_SKELETON.prefab 생성
- VfxSpawner.cs meteorBurstPrefab slot 추가
- 씬 slot 할당
- 검증 + commit: `feat(phase8): Meteor_Burst_SKELETON prefab + VfxSpawner 슬롯`

### Step 3: Portal
1. Portal_Mat, Portal_BeamMat 생성
2. BeamPulse.cs 작성
3. Portal_SKELETON.prefab 생성 (Entry/Exit/LinkBeam 계층 + BeamPulse MB 부착)
4. VfxSpawner.cs portalPrefab slot + SpawnPortal 특수 로직
5. 씬 slot 할당
6. 검증 + commit: `feat(phase8): Portal_SKELETON prefab + BeamPulse MB + VfxSpawner 슬롯`

### Step 4: Placement (카탈로그 승인 후)
- Placement Ring 카탈로그 entry 사용자 승인 확인
- Placement_Mat, Placement_SKELETON.prefab 생성
- VfxSpawner.cs placementRingPrefab slot 추가
- 씬 slot 할당
- 검증 + commit: `feat(phase8): Placement_SKELETON prefab + VfxSpawner 슬롯`

## 9. 예산 검증 (rate × lifetime)

| 이펙트 | 계산 | steady-state | MaxParticles | 여유 |
|---|---|---|---|---|
| Tornado | 30×1.0 + 12×0.8 + 5×1.5 = 30+9.6+7.5 | 48 | 50 | 2 |
| Meteor (burst) | 10 + 60 + 30 (burst 1회) | 100 | 100 | 0 (경계) |
| Portal | 25×1.0 + 10×0.6 + 25×1.0 + 10×0.6 = 25+6+25+6 | 62 | 90 | 28 |
| Placement (burst) | 30 + 5 + 10 (burst 1회) | 45 | 50 | 5 |

Meteor 경계는 Debris Drop 으로 대응 가능 (100→70).

## 10. 검증 프로토콜

### 10.1 컴파일
- 각 Step 후 UnityMCP `refresh_unity` + `read_console` → 에러 0 확인

### 10.2 폴백 경로 검증
- slot 비우고 Play 모드 → `[VfxSpawner] xxxPrefab 미할당 — 코드 폴백 사용` 경고 확인
- 이펙트는 기존 코드 프리셋으로 정상 출력

### 10.3 Prefab 경로 검증 (사용자)
- slot 할당 후 Play → prefab 기반 이펙트 확인
- Placement/Meteor/Tornado/Portal 각각 시각 폴리시 필요 여부 판단
- MaxParticles 초과 잘림 없는지 Scene view 에서 particle count 관찰

### 10.4 모바일 회귀
- Android 실기 테스트는 **이번 스코프 밖**, 후속 P8-NN 으로 별도

## 11. 오픈 이슈 / 후속

- **Placement Ring 카탈로그 승인** — 사용자 대기 중
- **씬 bloom Volume 유무** — Step 2 진입 전 사용자 확인 or UnityMCP 로 직접 조회
- **Meteor 동시 발사** — BattleBridge MeteorResolutionSystem 확인 (Step 2 착수 전)
- **Tile/배경 색 충돌** — Tornado GroundDust 색 결정 전 Main Camera Background + Tile material 확인
- **HDR + bloom 2단계 업그레이드** — 별도 Phase 8 §14 로 분리 가능
- **Shader Graph 템플릿 실제 제작** — 사용자 별도 작업
- **Meteor Warning Ring 프리팹화** — BattleBridge 쪽 procedural Quad, 이번 스코프 밖 후속 고려

---

**문서 버전**: impl v1.0 (구현 즉시 실행 가능)
**판정**: Yellow → 사용자 확인 2건(bloom Volume / Meteor 동시 발사) 해결 후 Step 1 진행
