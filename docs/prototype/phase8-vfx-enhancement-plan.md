# Phase 8 §13 — VFX 프리팹 마이그레이션 + 비주얼 강화 (Plan v0.1)

> Superseded: 확정/구현 완료 내용은 `PHASE8.md`에 통합됨. 본 문서는 히스토리/리뷰 기록으로만 유지.

> 기존 4종 코드 기반 VFX(`VfxSpawner.cs`)를 새 VFX 파이프라인(prefab-first)으로 이전하면서 모바일 예산 내에서 화려함을 끌어올린다. Codex 1차 리뷰 반영 완료 상태. 이 문서는 round 2 리뷰 입력 + 구현 문서 산출을 목적으로 함.

## 1. 배경

- 현재: `VfxSpawner.cs` 의 4개 Spawn 메서드가 코드 프리셋으로 `ParticleSystem` 을 런타임 생성. prefab 0건.
- 보유 자산: `.claude/skills/unity-vfx-authoring` / `unity-vfx-integration` 스킬, 10개 VFX 카탈로그 draft (Whirlwind/Fireball/Meteor/Portal 등), Shader Graph placeholder 2종.
- 스킬 Iron Law: `_SKELETON.prefab` 접미사 필수 · 카탈로그 승인 게이트 · 모바일 예산 상한 (일반 50 / 임팩트 100 / 배경 200).

## 2. 목표 / 비목표

### 목표
- 4종 이펙트(Placement/Meteor/Tornado/Portal)를 `_SKELETON.prefab` 으로 이전.
- 기존 코드 프리셋 경로는 **fallback 으로 유지** (Q4=b 원칙).
- 화려함 향상: child ParticleSystem 2~3개 레이어링 + Gradient/Size 곡선 가중. Sub Emitter 불사용.
- Portal Link Beam: width pulse → **alpha pulse** 로 교체 (MeshRenderer rebuild 회피).
- `BeamPulse` 경량 MonoBehaviour 를 prefab 내부에 둔다.

### 비목표
- VFX Graph 도입 (모바일 제외).
- Shader Graph 신규 작성 (placeholder 는 placeholder 유지, 사용자 2차 제작 대기).
- Meteor warning Ring(현 BattleBridge.SpawnMeteorWarningVisual) 마이그레이션 — 이번 스코프 밖.
- 사운드 큐 연동 — 이번 스코프 밖.
- HDR 색 + bloom 2단계 업그레이드 — 씬 bloom 확인 후 별도 작업.

## 3. 확정 결정 (Q&A + Codex 1차 리뷰 반영)

1. **마이그레이션 방식 = prefab slot + 코드 폴백** — VfxSpawner 에 `SerializeField GameObject xxxPrefab` 4개 추가. null 이면 기존 코드 프리셋 경로.
2. **prefab 위치 = `Assets/_Project/VFX/`** (신규 폴더).
3. **prefab 구조 = 루트 GO + Child PS 2~3개** (Sub Emitter 아닌 독립 컴포넌트). 순수 시각, 루트에는 Portal 만 `BeamPulse` MB.
4. **Material 공유 = URP/Particles/Unlit** — prefab 당 1개 `.mat`.
5. **HDR/bloom 보류** — Meteor Core Flash 를 고채도 일반색(`#FFE566` alpha 1.0) 로 시작. bloom 확인 후 HDR 업그레이드 별도.
6. **Portal Beam pulse = startColor/endColor alpha Sin 기반** — width 고정, MaterialPropertyBlock 사용.
7. **Placement Ring 카탈로그 값 = Duration 0.35, MaxParticles 50** (운용 45 + 여유 5).
8. **카탈로그 승인** = 사용자 별도 응답 대기 중 (Placement Ring 신규 엔트리).

## 4. Per-Effect Spec (Codex 리뷰 반영 후)

### 4.1 Placement_SKELETON.prefab
- **Role**: defender 배치 확정 피드백
- **Children** (3 PS 레이어):
  - Ring: pale cyan 확산, radial 2.5 u/s, 30 particles burst, size 0.3→1 curve
  - CenterFlash: white 1-frame, 5 particles, 0.08s, size 0.5→0
  - RisingMotes: 소량 먼지, 10 particles, **velocity (0, 0.7, 0.3)** speed 0.3, lifetime 0.25s (Spine 캐릭터 충돌 완화)
- **4 필수 오버라이드**: Duration **0.35** / StartColor pale cyan `#4DCCE5` / MaxParticles **50 (운용 45)** / Loop **false**
- **모바일 예산**: 일반 상한 50 경계. Ring 30 + Flash 5 + Motes 10 = 45 ✓
- **Drop 후보** (오버슈팅 시): RisingMotes 우선 제거 → 35

### 4.2 Meteor_Burst_SKELETON.prefab
- **Role**: 경고 링 종료 직후 폭발 연출
- **Children** (3 PS 레이어):
  - CoreFlash: `#FFE566` alpha 1.0, 10 particles, size 0.8→0 curve, lifetime 0.08s (bloom-safe)
  - MainBurst: hemisphere, 60 particles, gradient **yellow(0) → orange(0.15) → dark-orange(0.22) → black-alpha0(0.3)**, size curve 1→1.8 at 0.1s → 1.8 sustain to 0.25s
  - Debris: 방사형 파편, 30 particles, gravity 0.6, sprite sparse
- **4 필수 오버라이드**: Duration **0.3** / StartColor orange `#FF7319` / MaxParticles **100** / Loop **false**
- **모바일 예산**: 임팩트 상한 100 경계 = CoreFlash 10 + Main 60 + Debris 30
- **Drop 후보**: Debris 먼저 (Core+Main 만으로도 임팩트 전달)

### 4.3 Tornado_SKELETON.prefab
- **Role**: 2초 지속 회오리, 적 인력 영역 시각화
- **Children** (3 PS 레이어):
  - OuterDonut: radius×0.9, orbitalY=6, **rate 30**, 하늘색 `#99F2FF` alpha 0.8
  - InnerSpiral: radius×0.5, orbitalY=8, **rate 12** (15→12 조정), 연한 하늘 `#CCF0FF` alpha 0.6, start size **0.15** (Outer 절반)
  - GroundDust: Ring 주변 바닥, rate 5, 회갈색 `#80705C` alpha 0.5
- **4 필수 오버라이드**: Duration **2** / StartColor cyan `#99F2FF` / MaxParticles **50 명시 clamp** / Loop **true**
- **모바일 예산**: 일반 상한 50 = 30 + 12 + 5 = 47 + 여유
- **Drop 후보**: GroundDust (저밀도 기여 낮음)

### 4.4 Portal_SKELETON.prefab
- **Role**: 8초 지속 2점 링크 이동
- **Children** (4 PS + 1 LineRenderer):
  - RimRing_Entry: donut, **rate 25**, 보라 `#C266FF`, 입구 위치
  - RimRing_Exit: donut, **rate 25**, 보라-청 `#8040FF`, 출구 위치 (색 분기)
  - InnerSpark_Entry: 중심 반짝, rate 10, white
  - InnerSpark_Exit: 중심 반짝, rate 10, cyan `#40C0FF`
  - LinkBeam: LineRenderer, width 0.12 고정, **alpha Sin pulse (BeamPulse MB)**, gradient `#C266FF` → `#8040FF`
- **4 필수 오버라이드**: Duration **8** / StartColor purple `#C266FF` / MaxParticles **90** / Loop **true**
- **모바일 예산**: 임팩트 상한 100 = 25×2 + 10×2 + LineRenderer 0 = 70 + 여유
- **Drop 후보**: InnerSpark 먼저 → 50

## 5. 구조 변경

### VfxSpawner.cs
```csharp
[SerializeField] private GameObject placementRingPrefab;
[SerializeField] private GameObject meteorBurstPrefab;
[SerializeField] private GameObject tornadoPrefab;
[SerializeField] private GameObject portalPrefab;

public void SpawnPlacementRing(Vector3 worldPos)
{
    if (TrySpawnPrefab(placementRingPrefab, worldPos, out var go))
    {
        Destroy(go, 1.2f); // Duration + lifetime + margin
        return;
    }
    // ... 기존 코드 프리셋 (fallback)
}
```
- 4개 메서드 모두 동일 패턴.
- `TrySpawnPrefab` 은 `Instantiate(prefab, pos, Quaternion.identity, transform)` 래퍼.
- **폴백 진입 시 Debug.LogWarning** (skill 규칙 준수).

### BeamPulse.cs (신규, prefab 내부)
```csharp
using UnityEngine;

// Phase 8 §13 — Portal link beam 의 alpha pulse. LineRenderer 를
// MaterialPropertyBlock 으로 제어해 매 프레임 MeshRenderer rebuild 회피.
public class BeamPulse : MonoBehaviour
{
    [SerializeField] private LineRenderer line;
    [SerializeField] private float frequency = 2.5f;
    [SerializeField] private float alphaMin = 0.4f;
    [SerializeField] private float alphaMax = 1.0f;

    private MaterialPropertyBlock _mpb;
    private int _colorId;

    private void Awake()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        _mpb = new MaterialPropertyBlock();
        _colorId = Shader.PropertyToID("_BaseColor");
    }

    private void Update()
    {
        if (line == null) return;
        float t = (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(alphaMin, alphaMax, t);
        // LineRenderer startColor/endColor 는 per-vertex. alpha 만 변조.
        var sc = line.startColor; sc.a = alpha; line.startColor = sc;
        var ec = line.endColor;   ec.a = alpha; line.endColor = ec;
    }
}
```
- LineRenderer `startColor/endColor` alpha 만 Sin 기반 변조.
- width 고정 → MeshRenderer rebuild 없음.

### 씬 와이어링
- `VfxSpawner` 4개 prefab slot 에 `_SKELETON.prefab` 할당.
- UnityMCP `execute_code` + reflection 으로 SerializeField 세팅 후 SaveScene.
- `unity-feature-wiring` 스킬 체크리스트 적용.

## 6. 마이그레이션 단계

### Step 1: Tornado 먼저 (리스크 최저)
1. `Assets/_Project/VFX/Materials/Tornado_Mat.mat` 생성 (URP/Particles/Unlit)
2. `Assets/_Project/VFX/Tornado_SKELETON.prefab` 생성 (3 child PS)
3. VfxSpawner.cs 에 `tornadoPrefab` SerializeField + Instantiate 분기 + 폴백 Warning 로그 추가
4. 씬에서 slot 할당 + SaveScene
5. Play 모드: 기존 코드 경로와 prefab 경로 교차 테스트

### Step 2: Meteor (bloom-safe fallback)
1. Materials/Meteor_Mat 생성
2. Meteor_Burst_SKELETON.prefab (3 child PS, CoreFlash 일반색)
3. VfxSpawner 분기 추가
4. 검증

### Step 3: Portal (BeamPulse MB)
1. Materials/Portal_Mat (Particles/Unlit) + Portal_BeamMat (URP/Unlit)
2. Scripts/Presentation/BeamPulse.cs 신규
3. Portal_SKELETON.prefab (4 PS + LineRenderer + BeamPulse)
4. VfxSpawner 분기 추가
5. 검증

### Step 4: Placement Ring (카탈로그 승인 선행)
1. 카탈로그 승인 완료 대기
2. Placement_Mat 생성
3. Placement_SKELETON.prefab (3 child PS)
4. VfxSpawner 분기 추가
5. 검증

### Step 5: 커밋
단계별 또는 일괄 (사용자 선호). 권장: 이펙트 별 커밋 (fix 격리 용이).

## 7. 성능 / 모바일 예산

| 이펙트 | 운용 입자 수 | 상한 | 여유 |
|---|---|---|---|
| Placement | 45 | 50 (일반) | 5 |
| Meteor | 100 | 100 (임팩트) | 0 (경계) |
| Tornado | 47 | 50 (일반) | 3 |
| Portal | 70 | 100 (임팩트) | 30 |

- **Overdraw 경고**: 큰 투명 쿼드 다중 중첩 없음. 확인됨.
- **Sub Emitter**: 0건.
- **Texture Sheet Animation**: 0건.
- **Material 공유**: 이펙트별 1개 `.mat`, prefab 내 child 들이 같은 material 공유.

## 8. Open Questions (Codex round 2 리뷰용)

1. **prefab 루트 GO 에 AutoDestroy 스크립트?** — 현재 `VfxSpawner.Destroy(go, duration+margin)` 로 외부 관리. prefab 자율적 lifetime 원하면 `AutoDestroy : MonoBehaviour` 필요. 장단점?
2. **BeamPulse 의 `_BaseColor` property ID** — URP/Unlit shader 의 프로퍼티명 확인 필요. `_BaseColor` 가 URP 표준이지만 LineRenderer 가 sharedMaterial 을 쓸 때 실제 반영되나? LineRenderer color 는 vertex color 로 들어가는데 _BaseColor 변경해도 안 보일 수 있음.
3. **prefab `_SKELETON` 접미사 제거 시점** — 사용자가 Inspector 폴리시 완료 후 수동 rename 하는 건 번거로움. 에디터 스크립트로 "skeleton → final" 버튼 제공할지?
4. **VfxSpawner 분기 순서** — `if (prefab != null) Instantiate; else 코드 프리셋` vs `if (prefab == null) { LogWarning; 코드 프리셋; return; } Instantiate` — 가독성 우위?
5. **Tornado GroundDust 색 `#80705C`** — Whirlwind 카탈로그 "gray-brown" 과 일치하나 타일 바닥색과 겹칠 가능성. 씬 배경색 확인 필요.
6. **Meteor Debris sprite shape** — 현재 코드는 Hemisphere. 향상안에서 별도 Debris 레이어는 어떤 shape? Cone 위쪽? Sphere radial? 구체화 필요.
7. **Portal 입구/출구 색 분기 — RimRing 색만 다르고 나머지는 공통** — 동일 `_Mat` 인스턴스 공유시 색 분기는 per-PS `main.startColor` 에만 의존. 충분한가?

## 9. 검증

- **EditMode**: VfxSpawner 에 prefab null 분기 vs 비 null 분기 각각 커버. 현재 테스트 없으므로 추가 고려.
- **PlayMode**: Play 모드에서 4개 이펙트 visual 확인 (사용자 작업).
- **모바일 실기**: Android 저사양 기기(가능하면 Adreno 3xx 또는 유사) 프레임 드롭 측정. 이번 스코프 밖이지만 추후 P8-NN 으로 별도.

---

**상태**: Codex round 2 입력 대기. Open Questions 답변 + 추가 리뷰 후 **구현 문서** (docs/phase8-vfx-enhancement-impl.md) 로 좁혀 실제 작업 지시 생성.
