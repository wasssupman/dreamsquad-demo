# PixPlays Ubershader 흡수 + `_SKELETON` 4종 리스킨 — 설계

**작성일**: 2026-04-20
**상태**: 설계 승인, 실행 플랜(`writing-plans`) 대기
**Phase 관계**: Phase 8 종료 후 / Phase 9 착수 전 "파이프라인 개선" 영역. Phase 9 의 Flow Field 길찾기 작업과 독립.
**선행 완료**: Phase 8 §13 (prefab-only VFX), §17 (Tornado pull field). 본 설계가 전제로 하는 VfxSpawner 계약은 불변.

---

## 1. 목적

1. 현재 `Assets/_Project/VFX/*_SKELETON.prefab` 4종(Placement / Tornado / Meteor Burst / Meteor Falling / Portal)의 placeholder 비주얼을 PixPlays 원소 VFX 재료로 내부 리스킨해 **판 위 비주얼 품질을 실전 수준으로 끌어올린다**.
2. `PixPlays/Components/Shaders/Ubershader.shadergraph` 를 **우리 VFX 셰이더 기반**으로 흡수해, 비어있는 `VFX_Dissolve.shadergraph` · `VFX_Glow.shadergraph` · `New Shader Graph.shadergraph` 등 레거시 빈 셰이더를 제거한다.
3. 이 모든 변경이 **Phase 8 §13 prefab-only 정책과 VfxSpawner 계약을 유지한 채** 이루어져, 리스킨이 본 게임 코드에 영향을 주지 않도록 한다.

## 2. 범위 (Q1~Q4 결정 반영)

| 결정 항목 | 값 |
|---|---|
| 리스킨 대상 (Q1) | `_SKELETON` 4종 전부 — Placement, Tornado, Meteor (Burst+Fall), Portal |
| 정리 시점 (Q2) | 각 카테고리 리스킨 완료 직후 해당 BuiltIn 폴더만 제거. 데모 씬/unitypackage 는 4종 완료 후 일괄 |
| 레거시 빈 셰이더 (Q3) | `VFX_Dissolve.shadergraph`, `VFX_Glow.shadergraph`, `New Shader Graph.shadergraph` 전부 제거. Material 은 Ubershader 로 재설정 |
| 모바일 예산 (Q4) | 원본 유지로 파일럿 측정 → 수치 기반으로 다운사이즈 티어 확정 → 나머지 3종에 적용 |
| 실행 순서 | 옵션 1: Tornado 파일럿 → Android 측정 → 나머지 3종 일괄 |

## 3. 비목표

- 새 스킬 / 새 VFX 타입 추가 금지. Beams / Shields / Auras 계열 활용은 Phase 9 이후 재검토.
- PixPlays `VfxSystem`(BaseVfx, ProjectileVfx 등) 의 런타임 채택 금지. 우리 VfxSpawner 가 유일한 창구.
- PixPlays 전체 제거는 범위 외. "BuiltIn 중복 제거 + 데모 자산 제거" 수준의 부분 정리만.
- Android 퍼포먼스 다운사이즈의 **선제 적용 금지**. 측정 없이 낮추지 않음.

## 4. 폴더·에셋 레이아웃

```
Assets/
├── PixPlays/                              # 외부 벤더, 읽기 전용
│   └── Components/Shaders/
│       └── Ubershader.shadergraph         # 우리 Material 이 참조
│
├── _Project/VFX/
│   ├── Materials/
│   │   ├── VFX_Uber_Wind.mat              # (신규) Tornado 전용
│   │   ├── VFX_Uber_Fire.mat              # (신규) Meteor 계열
│   │   ├── VFX_Uber_Earth.mat             # (신규) Placement
│   │   ├── VFX_Uber_Water.mat             # (신규) Portal
│   │   ├── VFX_Dissolve_Mat.mat           # (기존) Ubershader 기반 공용 헬퍼로 재설정
│   │   └── VFX_Glow_Mat.mat               # (기존) Ubershader 기반 공용 헬퍼로 재설정
│   │
│   ├── Shaders/                           # 빈 파일 제거, 폴더 유지
│   │   # (VFX_Dissolve.shadergraph 삭제)
│   │   # (VFX_Glow.shadergraph 삭제)
│   │
│   # (New Shader Graph.shadergraph 삭제)
│   │
│   ├── Placement_SKELETON.prefab          # guid 불변, 내부 hierarchy 리스킨
│   ├── Tornado_SKELETON.prefab            # guid 불변, 내부 hierarchy 리스킨 (파일럿 1번)
│   ├── Meteor_Burst_SKELETON.prefab       # guid 불변, 내부 hierarchy 리스킨
│   ├── Meteor_Falling_SKELETON.prefab     # guid 불변, 내부 hierarchy 리스킨
│   └── Portal_SKELETON.prefab             # guid 불변, 내부 hierarchy 리스킨
```

**핵심 규칙**:
- 프리팹 **파일 경로·이름·guid 불변** → VfxSpawner 의 SerializeField 와 BattleBridge 경로 불변.
- 리스킨 = **내부 hierarchy 교체**. 기존 `.prefab` 을 삭제 후 재생성하지 않는다.
- PixPlays 프리팹 · MonoBehaviour · VfxData 계열은 **직접 참조하지 않음**. 파티클 계층 + Material 만 복사 이식.
- 공용 텍스처(`PixPlays/Components/Textures/`) 는 이번 범위에서 복사하지 않고 Material 이 원경로 참조. 추후 PixPlays 제거 결정 시 별도 작업으로 이관.

## 5. Material 프리셋 매핑

### 5.1 원소 프리셋 4종

| Material | 기반 | 용도 (`_SKELETON`) | 핵심 파라미터 |
|---|---|---|---|
| `VFX_Uber_Wind.mat` | WindAOE 메인 파티클 Material | Tornado (파일럿) | 청록 틴트, vertical UV scroll, mid opacity, soft edge |
| `VFX_Uber_Fire.mat` | FireAOE 폭발 코어 | Meteor Burst / Falling | 주·적색 emission, Dissolve on, 거친 noise |
| `VFX_Uber_Earth.mat` | EarthAOE ring 평면 | Placement | 다크 브라운 rim + soft alpha pulse, 지면 평면 |
| `VFX_Uber_Water.mat` | WaterBeam shaft | Portal (entry/exit + LinkBeam) | 청색 반투명, vertex distortion 소량, rim glow |

### 5.2 공용 헬퍼 2종 (기존 빈 Material 재활용)

| Material | 용도 | 파라미터 |
|---|---|---|
| `VFX_Dissolve_Mat.mat` | shard / 파편 공용 | Dissolve on, Emission low, Noise tile mid |
| `VFX_Glow_Mat.mat` | 링 · 빔 하이라이트 공용 | Additive blend, Emission high, Soft particles on |

## 6. Tornado 파일럿 절차 (옵션 1 실행 순서)

### 6.1 전제

- 파일럿 시작 전 VfxSpawner.SpawnTornado 호출 경로가 살아있는지 Play 확인.
- 파일럿 직전 상태를 커밋으로 고정(시각 회귀 비교용).

### 6.2 단계

1. `Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat` 생성, Shader 를 PixPlays Ubershader 로 지정, WindAOE Material 파라미터 값 수동 복사.
2. PixPlays `WindAOE/Version_URP/WindAoeVFX.prefab` 임시 Instantiate → Unpack Completely → 필요 파티클 노드만 `Tornado_SKELETON` 로 이식.
3. 기존 placeholder 자식 삭제, 루트 Transform 유지, Material 을 `VFX_Uber_Wind.mat` 로 swap.
4. `Main → Scaling Mode = Hierarchy`, `Looping = true` 유지 (`VfxSpawner` 가 Destroy 로 끊는 현행 패턴 보존).
5. 파티클 비주얼 기준 스케일을 `scale = 1.0 → 반지름 1m` 에 맞춤.

### 6.3 검증

- Play Mode 에서 Tornado 스킬 발동 → 회오리 회전 지속 → Destroy 타이밍 종료.
- `UnityMCP.read_console` 신규 에러 / 워닝 0.
- Phase 8 §17 pull 동작 시각 회귀 없음.

### 6.4 측정 게이트 (Q4)

Android 실기 + Unity Profiler, Tornado 3회 동시 발화 시나리오.

| 항목 | 임계선 (초안) | 초과 시 조치 |
|---|---|---|
| CPU main ms | < 8ms | 파티클 다운사이즈 |
| GPU ms | < 12ms | 파티클 다운사이즈 |
| SetPass delta | +5 이하 | 키워드 strip, Material 공유 확대 |
| Particle peak | < 800 | Emission rate ↓ |
| APK size delta | +2MB 이하 | Ubershader variant-strip 버전 |

측정 결과로 "원본 유지 / 기본 다운 / 공격 다운" 중 한 티어가 나머지 3종의 기본값이 된다.

## 7. 일괄 적용 단계 (나머지 3종)

### 7.1 Placement — Earth 베이스

- 원본: EarthAOE. Shard Rigidbody 투척/폭발 모두 버림. 지면 크랙 glow ring + dust puff 만 이식.
- `SpawnPlacementRing` 은 `Destroy(go, 0.6f)` 이므로 `Duration < 0.5s`, `Looping = off`.

### 7.2 Meteor — Fire 베이스 (Burst + Falling)

- Burst: FireAOE 의 center explosion + shockwave ring + spark burst 3노드. 지면 smolder 제외.
- Falling: Fireball 의 flight trail + core flame 2노드만. PixPlays MonoBehaviour 는 하나도 남기지 않음. `MeteorFall` 컴포넌트는 우리 것이며 루트에서 그대로 유지.

### 7.3 Portal — Water 베이스

- Entry / Exit / LinkBeam 자식 이름 **반드시 유지** (VfxSpawner 가 `Transform.Find` 로 참조).
- `LinkBeam` 의 `LineRenderer` 보존 (`SetPosition(0/1, ...)` 호출 경로 보존). WaterBeam shaft 는 시각 오버레이로만 얹음.
- LinkBeam 구조 유지가 불가하면 Portal 만 예외로 VfxSpawner 1곳 수정 제안을 재상정.

### 7.4 일괄 종료 기준

- [ ] 4개 `_SKELETON` 리스킨, 경로·guid 불변
- [ ] 4개 원소 Material 프리셋 생성
- [ ] VfxSpawner 시그니처 변경 없음 (Portal 예외 발생 시 별도 합의)
- [ ] Play Mode 5개 VFX 호출 전부 정상, 콘솔 0 에러
- [ ] 동시 발화 시나리오(Tornado 2 + Meteor 1 + Placement 1) 임계선 내

## 8. 정리 스코프

### 8.1 단계별 정리 (Q2 C안)

| 단계 | 제거 | 유지 |
|---|---|---|
| Tornado 파일럿 종료 | `PixPlays/ElementalAOE/WindAOE/Version_BuiltIn/` | URP, README, Material |
| Placement 종료 | `PixPlays/ElementalAOE/EarthAOE/Version_BuiltIn/` | URP |
| Meteor 종료 | `PixPlays/ElementalAOE/FireAOE/Version_BuiltIn/`, `PixPlays/ElementalProjectiles/Fireball/Version_BuiltIn/` | URP |
| Portal 종료 | `PixPlays/ElementalBeams/WaterBeam/Version_BuiltIn/`, `PixPlays/ElementalShields/WaterShield/Version_BuiltIn/` | URP |
| 최종 감사 | `Demo*Scene*_{BuiltIn,URP}.unity`, `*_URP.unitypackage` 중복, `VFXTester.cs`, `Character.cs`, `PixPlays/Components/Components_BuiltIn/` | `Ubershader.shadergraph`, `Components/Textures/`, `Components_URP/SharedMaterials/`·`SharedVFXComponents/`, 미사용 원소 URP 프리팹(보관) |

### 8.2 보관 정책

우리가 안 쓰는 나머지 원소 URP 프리팹(StoneBullet, WindShield, FireBeam 등)은 이번 범위에서 삭제하지 않음. 미래 스킬·유닛 작업 시 재활용 가능성이 높고, BuiltIn 제거로 용량은 이미 상쇄.

## 9. CLAUDE.md 절대 제약 준수 확인

- **ECS 경계 엄수**: 본 작업은 Presentation 계층 한정. EntityManager · SystemAPI 직접 호출 없음. VfxSpawner 계약 불변이 이 실천.
- **Manager 싱글톤 금지**: PixPlays VfxSystem 디스패처 미도입. VfxSpawner 유지.
- **하드코딩 금지**: Material 파라미터는 Inspector. 코드에서 shader property 설정 금지.
- **상속 2단계 제약**: PixPlays 의 BaseVfx → ProjectileVfx → Fireball 상속 체인 prefab 에 남기지 않음.
- **Phase 범위 엄수**: 새 스킬 / VFX 타입 확장 금지.
- **Unity 씬 wiring 자동화**: VfxSpawner SerializeField 는 이미 할당 상태, 경로·guid 불변으로 재할당 불필요. 프리팹 내부 hierarchy 변경은 UnityMCP 로 수행 가능.

## 10. 리스크 · 완화책

| 리스크 | 영향 | 완화책 |
|---|---|---|
| Portal LinkBeam 의 LineRenderer 가 WaterBeam 과 충돌 | SpawnPortal 깨짐 | LinkBeam 자식에 LineRenderer 별도 유지, WaterBeam shaft 는 시각 오버레이로 얹음. 불가 시 VfxSpawner 1곳 수정을 별도 합의. |
| Android APK 크기 증가 (Ubershader 키워드) | 빌드 용량·첫 진입 시 로드 | 측정 후 variant-strip 버전 준비. |
| 동시 발화 시 파티클 누적 프레임 드랍 | 실기기 UX | Q4 측정 게이트로 early detect, 다운사이즈 티어 적용. |
| PixPlays 텍스처 원경로 참조 끊김 (Phase 9 후 제거 시) | Material pink shader | 별도 작업으로 `_Project/VFX/Textures/` 이관 + guid fix-up 큐잉. |
| Ubershader 업데이트·벤더 변경 | 깨짐 | Ubershader 를 우리 쪽으로 복제하는 옵션을 Phase 9 이후 재검토. |

## 11. 종료 기준 (Definition of Done)

- [ ] Section 6 파일럿 전 항목 통과 + 측정 수치 기록
- [ ] Section 7.4 일괄 종료 기준 전 항목 통과
- [ ] Section 8 정리 스코프 전 항목 수행
- [ ] `docs/residual-issues.md` 에 "파이프라인 개선 작업" 항목 추가 or 종결 표시
- [ ] Design doc 과 실행 플랜(writing-plans 산출물) 이 커밋 히스토리에 남아있음

## 12. 다음 단계

본 설계 승인 직후 `superpowers:writing-plans` 스킬로 전이해 실행 가능한 단계별 구현 플랜을 작성한다. 이번 design doc 은 "무엇을 · 왜", writing-plans 산출물은 "어떻게 · 무슨 순서로".
