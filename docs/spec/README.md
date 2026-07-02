# Spec Documentation Structure

이 폴더는 프로토타이핑 이후의 feature 단위 구현 스펙을 보관한다. 새 기능은 `docs/spec/{feature-slug}/` 폴더 하나로 관리하고, 구현 단위는 번호가 붙은 작은 문서로 나눈다.

## 기본 구조

```text
docs/spec/{feature-slug}/
├── README.md
├── 0_{topic}.md
├── 1_{topic}.md
├── ...
├── N_{topic}.md
└── {N+1}_handoff_summary.md
```

## README.md

feature 의 입구 문서다.

- 현재 상태
- 목표
- 연결 문서
- 구현 문서 목록
- feature-wide 계약과 공통 원칙
- 비목표 또는 후속 후보

README 는 상세 구현서를 대신하지 않는다. 다음 작업자가 어디까지 완료됐고 어떤 번호 문서부터 읽어야 하는지 안내하는 인덱스다. 단, feature 전체에 영향을 주는 load-bearing 계약은 README 에 남긴다.

## 번호 문서

`0_{topic}.md` 부터 작업 순서대로 작성한다.

권장 섹션:

- 목적
- 변경 대상
- 구현
- 완료 기준

원칙:

- 1문서 = 1커밋에 가까운 작업 단위
- 1~3KB 정도의 작은 문서 유지
- 파일 경로를 명시
- 완료 기준은 compile/test/Play 확인 기준까지 포함
- 기존 번호를 재사용하지 않고 뒤에 추가
- 구현 완료 후에도 바뀌면 안 되는 계약만 갱신한다
- diff 설명이나 코드 흐름을 사후 문서화하지 않는다

## Handoff Summary

feature 구현이 끝났거나 세션 인계 가능성이 높으면 마지막 번호로 `{N+1}_handoff_summary.md` 를 작성한다.

예:

```text
docs/spec/map-system/20_claude_handoff_summary.md
docs/spec/wave-pattern/5_handoff_summary.md
```

필수 섹션:

- Commit
- Implemented
- Key Files
- Verified
- Notes
- Follow-up — 본 문서에 상세 항목을 적지 말고 본 README 하단 **Follow-up Backlog** 섹션으로 옮기고 한 줄 포인터만 남긴다

권장 길이:

- 30~80줄
- 핵심 파일 5~15개
- 완료 동작 5~10개

handoff 는 source of truth 가 아니다. 최신 상태와 계약은 README/번호 문서가 우선하고, 구현 상세는 코드와 커밋 히스토리가 우선한다. handoff 는 다음 에이전트가 무엇을 읽고 무엇을 건드리지 말아야 하는지 빠르게 파악하기 위한 지도다.

## Source Of Truth

```text
README.md                 최신 상태 + feature-wide 계약
{N}_{topic}.md            작업 단위 계약 + 완료 기준
{N+1}_handoff_summary.md  커밋 이후 인계 지도
code + git history        구현 상세
```

문서는 구현 상세를 전부 따라가지 않는다. 하지만 계약이 바뀌면 문서도 같이 바꾼다.

## Review 반영 기준

- 코드 버그를 유발하는 계약 공백: 코드 + 테스트 + 관련 spec 갱신
- 구현과 문서의 표현 불일치: 문서 갱신
- 단순 구현 설명 요구: handoff 에 짧게 쓰거나 생략
- 미래 확장/취향 제안: 후속 후보 또는 Follow-up 으로 이동

## 기존 예시

- `docs/spec/map-system/`
- `docs/spec/defender-drag-drop-deployment/`
- `docs/spec/defender-on-place-skills/`
- `docs/spec/wave-pattern/`

---

## Follow-up Backlog

종료된 spec 의 follow-up 후보를 한 곳에 모은다. 개별 spec 의 handoff 에는 한 줄 포인터만 남기고 항목은 여기로 이관한다.

### 사용 규칙

- 각 항목 **1~3줄 요약** (What · Why · Scope). 상세 설계는 새 spec 에서 다룬다.
- Scope: **S** = 단일 unit, **M** = 2~5 unit spec, **L** = 5+ unit spec.
- **같은 결의 작업은 테마 서브그룹** (`#### {테마명}`) 으로 묶는다. 예: "Modifier framework — Legacy migration".
- 출처 spec 이 여럿 섞이기 시작하면 항목 끝에 `(spec-slug)` 라벨로 표기.
- 새 spec 으로 승격되면 줄을 `→ docs/spec/{slug}/` 링크로 대체한다.
- 더 이상 유효하지 않으면 줄을 삭제하거나 한 줄 사유와 함께 `Promoted` 로 옮긴다.

### Active

> 출처 spec 이 섞여 있다. 그룹 헤더 또는 항목 끝의 `(spec-slug)` 라벨로 출처 표기.

#### 적 스폰/이동 비주얼 (enemy-spawn-positioning)

스폰 위치 개선(완료 2026-06-29, units 0~4) 후 남은 항목.

- **적 타일 이동 무결성** → `docs/spec/enemy-tile-movement-integrity/` (완료 2026-06-29 — `movement-lane-centering` 리프레임). 결함 3종: 코너 엣지-허깅 복원(target=0+deadband) · aggro 타일 제약 · 결정론 스폰. 레인 대형 시스템(II) · QuadUnit 뷰 누수는 후속 후보.
- **Quad 폴백 visualOffset 배선** [S] · Spine 없는 적의 `QuadUnitView` 경로에 `AttackUnitData.visualOffset` 전달. 현재 미배선(적=Spine 라 무영향).
- **유닛 간 separation/boid** [M] · 겹침 동적 해소(스폰 분산과 별개로 행진 중 밀집 완화).
- **블록 시 우회 재라우팅** [M] · 복도 차단 시 `BuildFlowField` rebuild 트리거(walk 마스크에 blockedCells 반영). flow field 유지 결론(유닛별 BFS 아님). 이동 아키텍처 별도 스펙.

#### Modifier framework — Producer 확장 (modifier-framework-and-healer)

framework 코어 변경 0. 새 producer 레이어 추가로 다양한 효과 적용 경로 확보. producer-agnostic 설계 검증 시점.

- **Aura defender** [M] · 지속 영역 효과 producer (`AuraOutput[]` + `AuraApplySystem`). 일정 반경 ally 에 매 프레임/N초마다 StatModifier 발화.

#### Modifier framework — 내부 보강 (modifier-framework-and-healer)

framework 코어/UX/테스트 보강. 콘텐츠 확장 전후 모두 가치 있음.

- **Modifier UI 시각화** [M] · defender HUD + 적 머리 위 활성 modifier 아이콘 표시. ModifierStats / Slot buffer read-only 구독. UI 리소스 의존.
- **Dispel/Cleanse 채널** [S] · ModifierBuffer 슬롯 제거 채널 (kind/source 기반). CombineOp 별 면역 정책. 콘텐츠 디자인 선행.
- **Testability — Stack threshold dispatch** [S] · `BattleBridge._stackThresholds` 에 test 주입 API 또는 `IStackThresholdRegistry` 인터페이스 도입. skipped Test 3 활성화 목적.
- **Testability — AttackSystem outputs dispatch helper 추출** [S] · `OnUpdate` 의 4-way 분기를 `static ProcessAttackOutputs(...)` 로 추출. skipped Test 4 활성화 목적.
- **추가 EditMode 회귀 테스트** [S] · Stack threshold edge (5→6→5 재발화) / Consume 모드 stack 차감 / IncomingHeal drain Clear / RegenPerSec 누적. Testability 보강과 합쳐 진행.

#### Enemy 콘텐츠 / 비주얼 (enemy-unit-development)

신규 적 3종 + Tanker Spine 전환 후 남은 검증/콘텐츠 작업.

- **PlayMode 밸런스/시각 검증** [S] · Rootcaster 공격 후 1초 pause, Needler 빠른 투사체 연사, Runner 과속 체감, Tanker BellKnight Spine 크기/정렬 — 실기 확인 후 SO 값 튜닝.
- **적 projectile VFX 분리** [S] · 현재 defender projectile prefab + tint/scale 재활용. enemy variant prefab 또는 material variant 분리. 적/방어 투사체 식별성 개선.
- **WavePatternGenerator unit weight 지원** [S] · 현재 균등 확률. `AttackDeck.attackUnitPool` 반복 참조 또는 weight 필드 도입. Runner/Needler 과다 출현 회피.
- **Enemy attack animation event 일반화** [S] · `DefenderAttackEvent` → `UnitAttackVisualEvent` 로 일반화. `SpineUnitPool.NotifyAttack(entity, target)` 으로 적 공격 애니메이션 트리거 연결.

#### Hazard caster — 확장 (hazard-caster-defenders)

hazard caster defender 4종 MVP 이후 남은 확장 후보.

- **footprint sampler** [S] · `SampleRect(center, width, height)` 구현. HazardCastSystem 의 width/height 고정을 제거하고 rect 범위 multi-cell spawn 지원. 콘텐츠 디자인 선행.
- **target priority 정책** [S] · 현재 nearest(world distancesq). first-path-progress / random policy 추가. `HazardCastState.targetPriority` 필드 도입.
- **cast warning VFX / tile preview** [S] · cooldown 직전 타겟 셀 하이라이트 또는 파티클. BattleBridge drain 에서 visual hint 생성.
- **same-frame hazard 효과** [M] · 현재 next Simulation tick 적용. ECS 내부 drain 으로 이동 시 같은 frame 적용 가능. `HazardLifetimeSystem` 순서 재편 필요.
- **DefenderCatalogSO** [S] · 씬 레벨 draft catalog 수동 배선 대신 공유 `DefenderCatalogSO`로 통합. roster 증가 시 씬 배선 brittle 해질 때.

#### CC / Obstacle 확장 (cc-pipeline-and-obstacle)

- **큐브 spawn 게임 통합** [S] · 디펜더 능력 / 스킬 카드에서 `BattleBridge.SpawnObstacle` 호출. 현재는 디버그 메뉴만 진입점.
- **Obstacle 시각 Presenter** [S] · `ObstaclePresenter` MonoBehaviour, mesh/particle. 현재 큐브는 시뮬만 있고 렌더 없음.
- **추가 CcKind** [S] · Stun/Root/Reverse/Pull/Push 등 enum + `MovementSystem` switch case 추가. 콘텐츠 디자인 선행.
- **멀티셀 큐브 / 적-적 분산** [S] · 현재 단일 셀, 단일 큐브.
- **CC merge helper 추출** [S] · 3번째 CC caller 등장 시 `EffectSpawner.ApplyCc` 와 `CcApplySystem.MergeOrAdd` 듀얼 구현 통합 (I1).
- **ObstacleLifetimeSystem Burst 분리** [S] · 큐브 16+ 시점 `OnUpdate` Burst 분리 + `blockedCells` incremental (I4).

#### 렌더 파이프라인 / 시각 (board-visualization, wrapped)

board-visualization spec 자체는 ROI 부족으로 wrap 종료. 진단/실험은 `docs/spec/board-visualization/29_final_handoff.md` 참조.

- **palette-and-overlay-fix** [M] · `forest.asset` red-tint 결정 실험 + Bug A (`_tileTextureMaterials` 캐시 키) / Bug B (Place edge mask 가 Env 이웃 한정) / Bug C (overlay alpha 0.25 너무 낮음). board-visualization wrap 의 root cause 후보. 새 spec 으로 시작 권장.
- **17r prop-distribution-retry** [S] · V-001 잔존. Poisson 정공법 재구현.
- **23 volcano-theme-fill** [M] · 두 번째 테마 자산 채움.
- **BattleBridge.StartBattle Persistent allocates 경고** [S] · 반복 시작 시 leak 추적. ECS 컨텍스트 정리 경로 점검.

#### Seasonal map backdrop — 후속 (seasonal-map-backdrop)

- **시즌별 차별화된 MapThemeData** [L] · 현재 4시즌 모두 forest 테마 공유. Lava/Lunar/Cosmic 전용 타일/장애물 정의. 별도 spec.
- **시각 검증 스크린샷 + tint/exposure 튜닝** [S] · `Assets/Screenshots/seasonal_backdrop_{season}_verify_*.png` 4장 캡처. 시즌별 backdropTint/skyboxExposure 미세 튜닝.
- **백드롭 미세 시차** [S] · camera 미세 이동에 skybox `_Rotation` 약간 따라가도록 BackdropMounter LateUpdate hook.
- **Backdrop ↔ MapTheme 라이팅·포그 매칭** [S] · 시즌별 ambient/fog color 자동 매칭 룩 패스.
- **토너먼트 메타 hook** [M] · 서버 응답 → `SeasonRuntime` active season swap API.
- **시즌 배지 UI** [S] · 매치 시작 시 활성 시즌 배지 노출.

#### 배경 프랍 영역 풀 (prop-area-pools)

근경/원경 풀 분리(완료 2026-07-02, units 0~3) 후 남은 확장 후보.

- **영역별 밀도/falloff 리스트 이관** [S] · 현재 `tilePropDensity`/`ringPropDensity` 는 테마 전역. WeightedProp 리스트 단위 또는 영역별 파라미터로 세분화.
- **원경 카테고리 회피** [S] · `sameCategoryMinDistanceCells` 를 원경 링에도 적용(현재 근경 전용). 원경 나무 군집 자연화.

#### 프랍 접지/프레임 (prop-upright-root 파생)

- **desert 테마 접지 fix** [S] · desert 풀의 `prop_style_*`·`prop_dummy_*` 가 아직 FullCamera + nonzero visualOffset(접지 fix `c6c77dc`/`f395afd` 의 desert 미적용분). forest 와 동일하게 Tilted + BottomCenter 재임포트 + offset 0. prop-upright-root unit0 audit 에서 발견, 프레임 문제와 성격 달라 분리.
- **ObstaclePlacer 테스트 기존 실패** [S] · `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio`(≥36 기대, 31). dea2733(phase10) 테스트, 맵 생성 결정론 실패. prop-upright-root 작업과 무관하게 HEAD 에서 이미 실패 — 회귀 아님. minPlaceableRatio/ObstaclePlacer 로직 별도 조사.

#### 기타

- **Healer 전용 Spine asset** [S] · 현재 Archer Spine reuse. 전용 rig + idle/heal-cast/death 애니메이션. 시각 식별성, 기능 영향 없음. (modifier-framework-and-healer)
- **Spec 5~10 backfill** [S] · hybrid 진행 시 누락된 단위 spec 파일 작성. commit/handoff 가 임시 대체 중. 필수는 아님. (modifier-framework-and-healer)
- **VFX magic number 정리** [S] · `VfxSpawner` 의 y-offset / lifetime 하드코딩 → SerializedField 또는 ParticleSystem main.duration + startLifetime 으로 동기화. heal/placement/meteor 일괄 대상. (heal-vfx)
- **Heal VFX amount scaling** [S] · `HealAppliedEvent.amount` 를 `VfxSpawner.SpawnHealApplied` 에서 ParticleSystem main.startSize/startColor 에 매핑. 큰 힐 = 큰 펄스. 시그니처는 이미 amount 파라미터 확보됨. (heal-vfx)

#### Outgame / squad / dreamcatcher — 후속 (outgame-scene-and-flow, squad-loadout, ingame-dreamcatcher)

- **드림캐쳐 카드 보유/콘텐츠 확장** [L] · ownedCardIds + 가챠/꿈런 파밍, 카드 콘텐츠 확장(기획 일반10+고유3+무의식2, 신규 메커닉 채널), 다중 덱 수집/전환·이름 편집·무의식 편입. (D 후속)
- **드림캐쳐 복합 효과** [L] · row-only/crit/pierce/splash/lowcost-summon/guardian-taunt/match-start-cost + 무의식 2장. 신규 메커닉/채널 필요.
- **진짜 MaxHealthMul 채널** [M] · 현재 HP 카드는 DmgTakenMul 프록시. 정확한 max-HP 증가 채널(Health/Units 맥락).
- **스쿼드 class/특성** [L] · class 라벨(완료, C unit0)을 이용한 슬롯 조건 + 타입별 특성(스탯 합산, 하드캡 15%). 다중 스쿼드 수집/전환, 가챠/꿈런 파밍/교환/리롤/등급.
- **한글 TMP 폰트** [S] · 현재 LiberationSans only → UI 라벨 영문. 로컬라이즈 패스에서 한글 폰트 에셋 도입.
- **반복 씬 로드 ECS leak 점검** [M] · 2-씬 전환으로 BattleScene 반복 로드 → 기존 **BattleBridge.StartBattle Persistent allocates 경고** 백로그가 더 중요. 재진입 시 ECS World/Persistent 정리 경로 검증.

### Promoted / Closed

- **Prop upright root** → `docs/spec/prop-upright-root/` (completed 2026-07-03, units 0~1 — 프랍을 90° 타일맵 루트에서 떼어 upright 저작 프레임(+Y=위). 루트 flip + 블롭 마이그레이션 + EditMode 테스트. desert 접지는 follow-up)

- **Prop area pools** → `docs/spec/prop-area-pools/` (completed 2026-07-02, units 0~3 — 근경 playAreaProps / 원경 distantRingProps 독립 WeightedProp[] 풀 분리 + 인스펙터 영역별 weight. tileProps/placementWeight 등 retire)

- **Dreamcatcher deck builder** → `docs/spec/dreamcatcher-deck-builder/` (completed 2026-06-03, 10장 빌더+저장+인게임 반입 MVP. 10·고유≤2)

- **Ingame dreamcatcher** → `docs/spec/ingame-dreamcatcher/` (completed 2026-06-02, 인게임 카드 선택+효과 MVP. 드래프트 prep 단계 대체. modifier-framework 버그 수정 동반)
- **Squad loadout** → `docs/spec/squad-loadout/` (completed 2026-06-02, 편성+반입 MVP. 드래프트 유닛선택 대체)
- **Outgame scene & flow** → `docs/spec/outgame-scene-and-flow/` (completed 2026-06-02, 2-씬 분리 + 프로필 영속 기반. B/C/D 의 토대)
- **Seasonal map backdrop** → `docs/spec/seasonal-map-backdrop/` (completed 2026-05-22, 4시즌 + Skybox 전환)
- **Modifier framework — Legacy migration** → `docs/spec/modifier-legacy-migration/` (completed 2026-05-01)
- **Modifier framework & Healer** → `docs/spec/modifier-framework-and-healer/` (completed 2026-05)
- **CC pipeline & Obstacle** → `docs/spec/cc-pipeline-and-obstacle/` (completed 2026-04-29)
- **Enemy unit development** → `docs/spec/enemy-unit-development/` (completed 2026-04-30, PlayMode 검증 후속)
- **Board visualization** → `docs/spec/board-visualization/` (wrap 2026-04-27 — ROI 부족 중단, palette-and-overlay-fix 로 후속)
