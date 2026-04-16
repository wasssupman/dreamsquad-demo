# Phase 5 — 마무리 (측정 프로토콜 + 비주얼 업그레이드)

> 본 문서는 `PRD.md`, `TRD.md`, `PHASE0~4.md`를 전제로 작성되었다. Phase 0~4에서 확립된 아키텍처 경계는 Phase 5에서도 유지된다.

---

## 0. Phase 5의 존재 이유

Phase 5는 **H1/H2/H3 전면 검증**을 위한 마무리 단계다. 측정 프로토콜(3분 타이머 / 더미 봇 스코어 / 헤드리스 시뮬레이션)을 가동하고, 외부 플레이어 세션에서 쓸 만한 수준의 시각 품질을 확보한다.

### Phase 5가 하는 것 / 안 하는 것

**하는 것:**
- §1 **유닛 비주얼 업그레이드** — Cube → Billboard Quad → (향후) Sprite/Spine 파이프라인의 1단계.
- §2 **3분 타이머** — 판 시간 경과에 따른 자동 종료 + Victory/Defeat 판정 규칙 확장.
- §3 **더미 봇 스코어 비교 UI** — 결과 화면에 봇 5개 스코어와 비교.
- §4 **H1/H2/H3 측정 프로토콜 연동** — 로그 수집 경로 + 분석 스크립트 스텁.
- §5 **헤드리스 시뮬레이션 하네스** — Unity Editor Batch 모드에서 N판 자동 실행.

**안 하는 것:**
- Sprite / Spine 실 애니메이션 도입 (§1은 Billboard Quad까지만).
- 신규 유닛 / 신규 스킬 / 신규 효과.
- 네트워크 / 세션 공유.

---

## 1. 유닛 비주얼 업그레이드 (Billboard Quad)

Phase 4까지 유닛은 Cube mesh + Unlit material로 렌더링. 외부 플레이어 세션을 위한 최소 품질 확보 + 향후 Sprite/Spine 파이프라인 기반을 동시에 마련한다.

### 1.1 검증 결과 반영 (Spine↔ECS 조사 완료)

- Spine Runtime(2025)은 SkeletonAnimation MonoBehaviour + MeshRenderer 기반. **Native ECS 통합 없음**.
- ECS와 Spine을 섞으려면 Unity Entities Graphics 1.4.x의 **Companion GameObject 패턴** 필요 — Entity ↔ 숨김 GameObject 단방향 Transform 동기화. Burst/Jobs 이점은 GameObject 경로에서 소멸.
- **결정**: Phase 5는 Spine을 도입하지 **않는다**. Billboard Quad + 향후 Sprite Animation까지 **순수 ECS 경로**로 진행. Spine 필요성은 Phase 6 이후 실측 기반 재평가.

### 1.2 Billboard 구현 (Phase 5a, 이번 착수)

**설계:**
- **회전 축**: Y축 빌보드 (스프라이트가 수직으로 서 있고 좌우로만 카메라 따라 회전). 탑다운 카메라에서 "수직 스프라이트" 연출과 정합.
- **방식**: Vertex shader 기반 빌보드 — GPU 처리, ECS 성능 영향 없음. Billboard System 같은 CPU 경로 도입하지 않음.
- **셰이더**: `Shaders/Billboard_Unlit.shader` (URP Unlit 기반 HLSL 또는 Shader Graph).
  - Property: `_BaseColor` (기존 유닛 색상 유지). `_BaseMap` (향후 sprite 대비, 지금은 null 허용).
  - Property(예약, Phase 5b/5c에서 사용): `_FrameIndex`, `_SpriteSheetDims`, `_PixelsPerUnit`.
- **Mesh**: `Resources.GetBuiltinResource<Mesh>("Quad.fbx")` (Cube 대체).

**영향 범위:**

| 파일 | 변경 |
|---|---|
| `Shaders/Billboard_Unlit.shader` (신규) | Y축 빌보드 + _BaseColor + _BaseMap + 예약 properties |
| `Assets/_Project/Data/Materials/*_Mat.mat` | 기존 Defender/Attack 머티리얼 셰이더 `URP/Unlit` → `Wassup/Billboard_Unlit`로 교체 (BaseColor 값 유지) |
| `Scripts/Bridge/BattleBridge.cs` | SpawnUnit/PlaceDefender/SpawnProjectile에서 Cube mesh → Quad mesh. `GetOrCreate...RenderMeshArray` 캐시는 mesh 참조 단일 갱신 |
| `Scripts/Battle/Units/HealthBar/*` | HealthBar도 Quad 기반으로 교체 (현재 Cube). 전용 material 변경 |
| `Scripts/Battle/Combat/Projectile/*` | 투사체도 Quad 기반 빌보드 |
| `Scripts/Battle/Units/HitFlashSystem.cs` | 스케일 펀치 로직 그대로 유지 (빌보드 Quad도 Scale 반응 동일) |

**자율 결정 영역:**
- Billboard Shader 구현: HLSL 직접 vs Shader Graph. 둘 다 OK, 단순한 쪽.
- 체력바/투사체 너비/두께 튜닝 (Cube → Quad 전환 시 체감 크기 다름).
- 모든 머티리얼 일괄 교체 vs 점진적 교체 (권장: 일괄, 스크립트로 자동).

### 1.3 Done Criteria (Phase 5a)

- [ ] `Shaders/Billboard_Unlit.shader` 생성. Y축 빌보드 동작 (카메라 각도 변경 시 Quad가 카메라 수평 회전 따라감).
- [ ] 기존 Defender/Attack 머티리얼 전부 새 셰이더로 교체. 색상 유지.
- [ ] BattleBridge가 Cube mesh 참조를 Quad mesh로 교체. Spawn/Place/Projectile 전부 Quad.
- [ ] HealthBar + HitFlash 정상 동작 (Quad 기반으로 재현).
- [ ] 기존 26 EditMode 테스트 전부 pass (변경이 비주얼/shader면 로직 테스트 무회귀).
- [ ] Play 수동 확인: 유닛이 카메라 방향으로 서 있는 평면으로 보임. 체력바/피격 플래시/투사체 비주얼 모두 작동.

### 1.4 Phase 5b~5d (후속 단계, 이번 Phase에서 구현 X — 구조만 열어둠)

- **Phase 5b Sprite 텍스처**: 머티리얼 `_BaseMap`에 유닛별 텍스처 연결. UnitData SO에 `visualTexture` 필드 추가 검토.
- **Phase 5c Sprite animation**: 셰이더 `_FrameIndex` 업데이트를 위한 `SpriteAnimationSystem` (ECS, Burst). FPS/시작 프레임 등은 UnitData SO 필드.
- **Phase 5d Spine 재평가 (옵션)**: 유닛 수가 Hybrid Companion 오버헤드를 감내할 수준이면 도입. 아니면 5c에서 멈춤.

---

## 2. 3분 타이머

> 상세는 Phase 5a 완료 후 확정.

---

## 3. 더미 봇 스코어

> 상세는 Phase 5a 완료 후 확정.

---

## 4. H1/H2/H3 측정 프로토콜 연동

> 상세는 Phase 5a 완료 후 확정.

---

## 5. 헤드리스 시뮬레이션 하네스

> 상세는 Phase 5a 완료 후 확정.

---

**문서 버전**: v0.1 (Phase 5a 초안, §2~5는 stub)
**다음 업데이트**: Phase 5a 완료 후 §2~5 상세 작성
