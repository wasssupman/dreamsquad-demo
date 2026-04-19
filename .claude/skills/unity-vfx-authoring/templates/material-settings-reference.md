# Material 설정 레퍼런스 (Shader Graph 대체)

`.shadergraph` 템플릿을 아직 만들지 않은 상태에서도 dissolve / glow 느낌을 빠르게 시작할 수 있도록, Unity 에디터에서 바로 만들 수 있는 **Material 기본 프리셋 2종** 의 설정값을 문서로 정리한다. `.mat` 파일을 하드코딩된 GUID 로 직접 drop 하면 URP 셰이더 GUID 가 프로젝트마다 달라 충돌 위험이 있으므로, **사용자가 에디터에서 수작업 생성**하는 가이드를 따르는 것이 안전하다.

---

## 1. Dissolve-lite (Shader Graph 없이)

**목적**: 사망/해제 연출용 "투명해지며 사라지는" 느낌. 진짜 dissolve 는 Shader Graph 의 Noise 샘플 + Step 노드가 필요하지만, 이 프리셋은 **alpha cutout + 점진 fade** 조합으로 근사한다.

### 생성 절차

1. `Assets/_Project/VFX/Materials/` 에서 `Create → Material`. 이름 `Dissolve_Lite_Mat`
2. Shader: `Universal Render Pipeline/Lit`
3. Inspector 설정:
   - **Surface Type**: Transparent
   - **Blending Mode**: Alpha
   - **Alpha Clipping**: **ON**, Threshold 0.5
   - **Base Map**: 노이즈 텍스처 할당 (없으면 Unity 기본 `Default-Particle` 도 근사)
   - **Base Color**: 효과 톤 (예: defender 색)
   - **Emission**: ON, `Color` HDR, intensity 1.5~3.0 → 해체 중 밝게

### 런타임 구동

- Script 에서 `material.SetFloat("_Cutoff", t)` 를 `t: 0 → 1` 로 Lerp (1초 전후) → alpha cutout 이 점진 확대되며 "흩어지는" 느낌
- Emission 은 동일한 t 에 비례해 peak 후 감소시키면 "타오르다 사라지는" 효과

### 한계

- 진짜 Shader Graph dissolve (Noise → Step → edge glow) 의 **edge burn** 효과는 없음
- 텍스처 노이즈를 스크롤하는 재료 기반 연출 불가
- **Phase 9+** 에서 사용자가 정식 `.shadergraph` 제작 후 대체 권장

---

## 2. Glow aura (emission 기반)

**목적**: 아군 buff aura, synergy 활성화, 중요 오브젝트 강조 등 "밝게 빛나는" 느낌.

### 생성 절차

1. `Create → Material`. 이름 `Glow_Mat`
2. Shader: `Universal Render Pipeline/Unlit` (또는 Lit)
3. Inspector 설정:
   - **Surface Type**: Transparent
   - **Blending Mode**: **Additive** (블룸 없이도 밝게 보임)
   - **Base Map**: 소프트 원형 텍스처 (Default-Particle)
   - **Base Color**: 원하는 색 (예: cyan `#40C0FF`)
   - **Emission**: ON, Emission Color HDR, **intensity 2~4**
     - URP Bloom 이 있으면 자연스러운 halo 생성
     - Bloom 없으면 그냥 밝은 additive 로 보임 — bloom-safe fallback

### 런타임 구동

- `material.SetColor("_EmissionColor", baseColor * intensity)` 로 런타임 제어
- 맥박 효과: `intensity` 를 `Mathf.Lerp(2, 4, (Sin(t*2π)+1)*0.5)` 로 Sin 변조

### 한계

- Shader Graph 의 Fresnel 기반 rim-light 효과는 없음 (Lit 의 Specular 로 근사 가능)
- Emission 이 표면 전체에 평평하게 작용 — edge glow / 내부 gradient 효과 없음

---

## 3. 사용 시점 가이드

| 효과 | 먼저 시도 |
|---|---|
| 사망 dissolve | `Dissolve_Lite_Mat` → 부족하면 Shader Graph 제작 |
| buff/heal aura | `Glow_Mat` → 충분히 효과적인 경우 많음 |
| synergy 활성화 표시 | `Glow_Mat` emission intensity 상향 (3~5) |
| projectile hit flash | `Glow_Mat` + 짧은 수명 particle |
| portal rim 강조 | `Glow_Mat` 을 LineRenderer / MeshRenderer 에 추가 |

**Shader Graph 필요 판단 기준**:
- 노이즈 텍스처를 스크롤시키며 표면에 dynamic pattern 필요 → Shader Graph
- Edge burn / Rim-only glow / 복잡한 시간 기반 변조 → Shader Graph
- 그 외 정적/단순 애니메이션 → 위 두 Material 로 충분

---

## 4. `.shadergraph` 템플릿 제작 완료 시 교체 전략

사용자가 `templates/dissolve_template.shadergraph` / `glow_template.shadergraph` 를 Unity 에디터에서 정식으로 제작하고 나면:

1. 해당 `.shadergraph` 자산을 프로젝트 `Assets/_Project/VFX/Shaders/` 로 복사
2. `Dissolve_Lite_Mat` / `Glow_Mat` 의 Shader 필드를 새 shader graph 로 교체
3. 노출된 properties (Dissolve Amount, Emission Intensity 등) 를 런타임에서 동일한 API로 제어 가능
4. 본 문서에 기록된 한계들이 해소됨

---

**주의**: 본 문서는 skill 참고용 가이드. 에이전트가 자동으로 이 프리셋 `.mat` 을 생성하는 것은 GUID 충돌 위험 때문에 비권장. 사용자 에디터 수작업 경로만 신뢰.
