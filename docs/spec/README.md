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

#### 보스 방어유닛 지향 이동 (헌터 재구현, 2026-07-11)

- **defender field dirty-skip 최적화** [S] · 방어유닛 셀 집합 불변 시 매 프레임 BFS 재빌드 skip. 현 그리드(20x10)에선 무의미 — 대형 그리드/프로파일 압박 시. (boss-defender-field, ecs-review M2)
- ~~**ecs-reviewer 채널 목록 stale**~~ — **완료 2026-07-11**. 재발 방지를 위해 목록 사본 자체를 제거 — 에이전트 정의가 CLAUDE.md § "ECS 맥락 분리"(source of truth) + 코드 실측 grep 을 가리키도록 변경. 코드 실측 18개 = CLAUDE.md 일치 확인. (boss-defender-field, ecs-review M1)

#### 유닛 상태 표현 / 인디케이터 (aggro-targeting 파생, 2026-07-09)

어그로 아이콘("!")을 만들며 드러난 일반화. **두 축으로 분리** — "느끼게 할 상태 연출" ↔ "훑어볼 정보 배지". 순차 진행 예정.

- ~~**상태별 프리팹 연출 인프라 (unit-status-fx)**~~ — **완료 2026-07-09** (`02a9db24`). `AggroIcon*` → `StatusFx*` 일반화: `StatusFxKind` + `StatusFxRegistry`(상태마다 프리팹) + `StatusFxSpawner`/`View`. 어그로 이관(현 "!" 폴백 유지). **잔여**: 실제 상태(스턴/빙결/독) registry 등록 + ECS 소스 훅, 어그로 전용 프리팹 연출(가디언 tether 등). (unit-status-fx)
- **모디파이어 인디케이터 스트립 (unit-modifier-indicators)** [M] · 버프/디버프(`ModifierStats` 델타·DoT 스택 Fire/Ice/Bleed/Poison)/드림캐쳐 부착(`DreamcatcherCard.art`)을 머리 위 아이콘 행으로. 스택/듀레이션 뱃지 + `+N` 오버플로. 상태 연출과 **다른 축**(정보 vs 느낌). 한 상태가 둘 다일 수 있음(예: 독=온-바디 VFX + 스택 아이콘). (aggro-targeting)

#### 곡사포 / 투사체 후속 (artillery-defender, projectile-trajectory-payload)

곡사포 유닛 완료(→ Promoted). 남은 후속:

- ~~**신규 유닛 프로필 reconcile**~~ [해결 2026-07-06] · 유닛을 프로필-소유(`ownedUnitIds`)에서 아예 제거하고 SquadBuilderView 가 `DefenderCatalog` 를 직독 → 모든 유닛 상시 오픈, 신규 유닛 자동 노출. 유닛 수집/가챠 도입 시 재검토(그때 소유 개념 부활). PlayerProfile.ownedUnitIds 삭제(JSON back-compat: 구 필드 무시).
- **slow-곡사포 / 임팩트 CC / arcHeight 거리비례 / 전용 Spine rig** [S/M] · artillery-defender 후속.
- ~~**Meteor→TileAoe 수렴 + GA 낙하 비주얼**~~ → `docs/spec/projectile-trajectory-payload/` units 7~9 **완료(2026-07-06)** — 레거시 3파일+큐 삭제(채널 15→14), Rock02 낙하+Hit_Rock03 파편, 스킬 aim/텔레그래프 격자 통일은 `placement-attack-range-preview/3_skill_aim_range.md`.
- **Bezier 궤적 / non-Damage payload / Homing+TileAoe** [S/M] · projectile-trajectory-payload 엔진 확장 후속.

#### 적 스폰/이동 비주얼 (enemy-spawn-positioning)

스폰 위치 개선(완료 2026-06-29, units 0~4) 후 남은 항목.

- **적 타일 이동 무결성** → `docs/spec/enemy-tile-movement-integrity/` (완료 2026-06-29 — `movement-lane-centering` 리프레임). 결함 3종: 코너 엣지-허깅 복원(target=0+deadband) · aggro 타일 제약 · 결정론 스폰. 레인 대형 시스템(II) · QuadUnit 뷰 누수는 후속 후보.
- **Quad 폴백 visualOffset 배선** [S] · Spine 없는 적의 `QuadUnitView` 경로에 `AttackUnitData.visualOffset` 전달. 현재 미배선(적=Spine 라 무영향).
- **유닛 간 separation/boid** [M] · 겹침 동적 해소(스폰 분산과 별개로 행진 중 밀집 완화).
- **블록 시 우회 재라우팅** [M] · 복도 차단 시 `BuildFlowField` rebuild 트리거(walk 마스크에 blockedCells 반영). flow field 유지 결론(유닛별 BFS 아님). 이동 아키텍처 별도 스펙.

#### 점수 HUD 타격감 (score-hud-impact-upgrade)

점수 HUD 임팩트 업그레이드 **완료(2026-07-07, units 0~4)** — 탄성 슬램/골드 아이덴티티/Kanit 폰트·골드 스파클 버스트·발광+샤인·패널 킥/마일스톤 플래시(Play 통과) + SoundManager 처치 틱(ElevenLabs `ScoreTick`, 피치 상승). 상세: `docs/spec/score-hud-impact-upgrade/`.

- **연속처치 heat · 킬 위치 "+N" 플로팅 · 콤보 배수 스코어링 · 적별 차등 점수 · 진짜 URP Bloom · SFX 다양화(마일스톤 팡파레)** [S~M] · 상세는 spec README "후속 후보".

#### 체력 표기 (unit-health-display)

적/방어유닛 체력 표기(완료 2026-07-04, units 0~3 — 적 피격 마이크로바 + 저체력 틴트, 방어유닛 타일 테두리 게이지, 투트랙 리뷰 반영). 상세 후속: `docs/spec/unit-health-display/README.md`.

- **킬 포어캐스트 마크** [M] · `IncomingDamage` + 비행 투사체 예약 데미지 ≥ 잔여 HP 인 적에 스컬 마크. 바가 못 주는 의사결정 정보. 투사체 데미지 귀속 필요.
- **체력 표기 poll 효율화** [S] · `SyncMonoUnitViews` 가 매 프레임 적/방어유닛 `Health` 조회 + 뷰 write(틴트/게이지). entity/cell→last-ratio 캐시로 skip. 비블로커(유닛 수 그리드 상한).
- **타일 게이지 시각 폴리시** [S] · fill inset `pad=0.18` SO화, 코너 조인트 갭 보정, 4-edge 계단식 → 연속 SDF 셰이더 교체.
- **hazard 체력 표시 / 상태이상 틴트 합성 / 웨이브 압력 게이지 / 보스 상시 바** — unit-health-display README 후속 후보.

#### 배치 프리뷰 / 범위 (placement-attack-range-preview, placement-drag-preview-polish, keyring-cord-preview)

드래그 배치 UX(공격범위 격자 표시 + 프리뷰 sway → **키링화** 완료 2026-07-05) 후 남은 항목.

- **배치 스킬 범위 표시** [M] · `onPlaceRange`/`hazardCastRange` 를 다른 색 채널로. 웜(공격)/쿨(스킬) 색코드 + 채널별 펄스 위상차, 필요 시 border 타일. 2번째 색 채널 시점에 `EnsureRangeTilemap`/펄스 로직 파라미터 추출. (range-preview)
- **Guardian 어그로 반경 시각화** [S] · `aggroRange` 를 또 다른 표기로(공격 범위와 별개 성격). (range-preview)
- **이미 배치된 유닛 선택/탭 시 범위 표시** [S] · 현재는 드래그 중만. (range-preview)
- **키링 중력 드롭 방식** [S] · 움직일 땐 유닛이 손가락에 붙고, 멈추면 중력으로 툭 떨어져 매달리는 물리감(사용자 제안, 현 스프링 follow 의 대안). (keyring)
- **키링 고리/줄 실제 아트** [S] · 현재 절차적 원 링 + 단색 LineRenderer. 금속 링/체인 스프라이트로 스왑. (keyring)
- **줄 sag 곡선** [S] · 현재 2점 직선. 정적 catenary 곡선으로 끈 느낌. (keyring)
- **배치 유닛 idle sway** [S] · 현재 sway 는 드래그 프리뷰 전용. 배치된 유닛의 상시 미세 흔들림. (drag-preview)
- **드롭 bounce / 세로·전후 흔들림** [S] · 드롭 착지 반동·다축 진자. (drag-preview)
- **fallback capsule 프리뷰 sway** [S] · Spine 없는 유닛 경로(현재 스킵, 키링 미적용). (keyring/drag-preview)

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

- ~~**palette-and-overlay-fix**~~ [무효화 2026-07-03] · 대상(Legacy 렌더 텍스처/오버레이/tint 경로)이 legacy-render-removal 로 통삭제됨.
- **BattleScene MapView 잔재 씬 청소** [S] · 구 MapView GameObject(missing-script) + `BattleBridge.mapView` stale serialized 참조 제거. 씬 dirty WIP 정리 후 SaveScene 격리 절차로. (legacy-render-removal handoff)
- **17r prop-distribution-retry** [S] · V-001 잔존. Poisson 정공법 재구현.
- **23 volcano-theme-fill** [M] · 두 번째 테마 자산 채움.
- **BattleBridge.StartBattle Persistent allocates 경고** [S] · 반복 시작 시 leak 추적. ECS 컨텍스트 정리 경로 점검.

#### Seasonal — 후속 (seasonal-map-backdrop)

> 백드롭 서브시스템(BackdropMounter/SeasonBackdropData)은 Legacy3D 전용이라 legacy-render-removal unit 2 에서 통삭제(사용자 결정 2026-07-03). backdrop 의존 항목 3개(tint/exposure 튜닝·미세 시차·라이팅 매칭) 무효화로 제거. 시즌 시스템(SeasonRuntime/mapTheme)은 ACTIVE.

- **시즌별 차별화된 MapThemeData** [L] · 현재 4시즌 모두 forest 테마 공유. Lava/Lunar/Cosmic 전용 타일/장애물 정의. 별도 spec.
- **토너먼트 메타 hook** [M] · 서버 응답 → `SeasonRuntime` active season swap API.
- **시즌 배지 UI** [S] · 매치 시작 시 활성 시즌 배지 노출.

#### 배경 프랍 영역 풀 (prop-area-pools)

근경/원경 풀 분리(완료 2026-07-02, units 0~3) 후 남은 확장 후보.

- **영역별 밀도/falloff 리스트 이관** [S] · 현재 `tilePropDensity`/`ringPropDensity` 는 테마 전역. WeightedProp 리스트 단위 또는 영역별 파라미터로 세분화.
- **원경 카테고리 회피** [S] · `sameCategoryMinDistanceCells` 를 원경 링에도 적용(현재 근경 전용). 원경 나무 군집 자연화.

#### 프랍 접지/프레임 (prop-upright-root 파생)

- ~~**desert 테마 접지 fix**~~ [완료 2026-07-03] · desert prop_style_*/prop_dummy_* + 공유 forest dummy PropData 를 Tilted + offset 0 + 텍스처 BottomCenter 로 정합. 실제 렌더 sink 는 dummy 2종뿐(prop_style_* 는 공유 forest 프리팹의 baked data 로 이미 정상)이었고 나머지는 데이터 hygiene. Play 검증(`desert_dummy_grounding_verify.png`).
- **ObstaclePlacer 테스트 기존 실패** [S] · `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio`(≥36 기대, 31). dea2733(phase10) 테스트, 맵 생성 결정론 실패. prop-upright-root 작업과 무관하게 HEAD 에서 이미 실패 — 회귀 아님. minPlaceableRatio/ObstaclePlacer 로직 별도 조사.

#### 모바일 디스플레이·Battle HUD 대응

모바일 aspect/framerate 수정(`GameManager.Awake` — 세로 1080 캡 + 기기 aspect 로 가로만 확장, `targetFrameRate=60` + `vSyncCount=0`) 후 남은 UI 후속.

- **UI CanvasScaler Height + Safe Area 통일** → `docs/spec/mobile-ui-safe-area/` [M, 설계 완료·승인 대기]. Battle/Outgame 전체를 full-bleed/safe root로 분리하고 16:9~20:9 + Android cutout/gesture를 검증한다.
- **Battle HUD Safe Action Tray** → `docs/spec/battle-hud-action-tray/` [L, 선행 spec 대기]. 비용·role·affordability 슬롯 정보, compact energy rail, tray↔hand 시각 정합, 배치 거부 원인 피드백.
- **남은 허용 유출 HUD** [S/M] · `defeatGoalReachedCount` 대비 현재 유출/잔여 허용치를 전투 중 상시 표시해 패배 원인 예측성을 높인다. Action Tray와 다른 상단 생존 정보 scope로 별도 승격.

#### 기타

- **Healer 전용 Spine asset** [S] · 현재 Archer Spine reuse. 전용 rig + idle/heal-cast/death 애니메이션. 시각 식별성, 기능 영향 없음. (modifier-framework-and-healer)
- **Spec 5~10 backfill** [S] · hybrid 진행 시 누락된 단위 spec 파일 작성. commit/handoff 가 임시 대체 중. 필수는 아님. (modifier-framework-and-healer)
- **VFX magic number 정리** [S] · `VfxSpawner` 의 y-offset / lifetime 하드코딩 → SerializedField 또는 ParticleSystem main.duration + startLifetime 으로 동기화. heal/placement/meteor 일괄 대상. (heal-vfx)
- **Heal VFX amount scaling** [S] · `HealAppliedEvent.amount` 를 `VfxSpawner.SpawnHealApplied` 에서 ParticleSystem main.startSize/startColor 에 매핑. 큰 힐 = 큰 펄스. 시그니처는 이미 amount 파라미터 확보됨. (heal-vfx)
- **GA 투사체 최종화** [S] · 디펜더별 최종 변종 선택(50종 중) + 스케일/높이 취향 미세조정 + 안 쓰는 변종 SO/프리팹 정리. (projectile-ga-reskin)
- **GA 투사체 모바일 최적화** [M] · 라이트/트레일 감축 · soft particle 토글 · 실기기 프로파일. tint 데이터-드리븐 recolor 는 별도(preserveVfxColors 우회 필요). (projectile-ga-reskin)

#### 파이프라인 커버리지 — 후속 (object-pipeline-map)

- **spec 파일 트리거 훅** [S] · `docs/spec/**/README.md` Write/Edit 시 파이프라인 커버리지 섹션 리마인더 주입(PostToolUse 훅). 템플릿 규칙 정착 후 잔여 누락 케이스 확인되면.
- **리뷰 게이트** [S] · two-track-review/critic 체크리스트에 "파이프라인 정거장 누락" 항목 추가.

#### 워크플로우 재현성 — 후속 (workflow-reproducibility)

- **문서 수명주기 정리** [S] · PRD/TRD 는 폐기된 "프로토타입/Phase" 프레임을 현재형으로 서술하는 legacy — staleness 배너 + supersession 포인터(현재 진실원=CLAUDE.md+spec)로 정직하게 동결.
- **ADR 로그** [M] · 횡단 결정(TimeManager·구조적 결정론·ECS 맥락 규칙)을 `docs/decisions/` 에 동결·번호·supersede 규칙으로. `docs/reference/lessons/` 와 합류 가능.
- **deepinit ↔ AGENTS symlink 충돌 정책** [S] · deepinit 재실행 시 AGENTS.md 를 실제 파일로 재생성해 symlink 이 풀림 — 재적용 자동화 또는 deepinit 출력 위치 변경.
- **첫 실전 클론 체크리스트 완주 확인** [S] · 새 머신/팀원 첫 클론에서 루트 README 부트스트랩 체크리스트(훅 승인·Unity 첫 Play) 실전 검증.
- **thick 하네스 표준화** [S] · OMC/superpowers 를 `enabledPlugins`+`extraKnownMarketplaces` 로 커밋해 팀 동일 오케스트레이션(사용자 결정 시).

#### Outgame / squad / dreamcatcher — 후속 (outgame-scene-and-flow, squad-loadout, ingame-dreamcatcher)

- **드림캐쳐 카드 보유/콘텐츠 확장** [L] · ownedCardIds + 가챠/꿈런 파밍, 카드 콘텐츠 확장(기획 일반10+고유3+무의식2, 신규 메커닉 채널), 다중 덱 수집/전환·이름 편집·무의식 편입. (D 후속)
- **드림캐쳐 복합 효과** [L] · row-only/crit/pierce/splash/lowcost-summon/guardian-taunt/match-start-cost + 무의식 2장. 신규 메커닉/채널 필요. 트리거형 메커닉(개별유닛 바인딩 + N회 공격 발동) 토대는 → `docs/spec/dreamcatcher-unit-trigger/` 로 부분 승격 (2026-07-08).
- **진짜 MaxHealthMul 채널** [M] · 현재 HP 카드는 DmgTakenMul 프록시. 정확한 max-HP 증가 채널(Health/Units 맥락).
- **스쿼드 class/특성** [L] · class 라벨(완료, C unit0)을 이용한 슬롯 조건 + 타입별 특성(스탯 합산, 하드캡 15%). 다중 스쿼드 수집/전환, 가챠/꿈런 파밍/교환/리롤/등급.
- **한글 TMP 폰트** [S] · 현재 LiberationSans only → UI 라벨 영문. 로컬라이즈 패스에서 한글 폰트 에셋 도입.
- **반복 씬 로드 ECS leak 점검** [M] · 2-씬 전환으로 BattleScene 반복 로드 → 기존 **BattleBridge.StartBattle Persistent allocates 경고** 백로그가 더 중요. 재진입 시 ECS World/Persistent 정리 경로 검증.

### Promoted / Closed

- **보스 방어유닛 지향 이동** → `docs/spec/boss-defender-field/` (완료 2026-07-11, units 0~3, `dc298ceb` — 방어유닛 walkable 이웃 multi-source BFS "defender field"(Effects 싱글톤+매 프레임 재빌드) 를 보스(`BossTag`)가 Marching 에서 flow-follow. 지나친/뒤 배치 방어유닛에 역주행 재교전, 전멸까지 사냥(leak-proof), 0마리면 goal 마칭(무상태 fallback). FSM/채널 변경 0, 비-보스 무회귀 라이브 확인. 폐기된 enemy-hunter-targeting 의 직선추격/wall-slide 는 재도입 금지 계약)

- **Enemy walk anim speed match** → `docs/spec/enemy-walk-anim-speed/` (완료 2026-07-10, units 0~2 — 적 Spine 걷기 애니를 실제 view 변위 기반 재생속도로 변조해 발 미끄러짐(문워크) 제거. `skeleton.timeScale = battleScale × walkFactor`(sim-time 정규화 속도/refSpeed, min/max/스무딩/텔레포트가드 = `WalkAnimSpeedStyle` SO + BattleBridge 미러). 포탈 텔레포트 무시·standoff 바닥. **회귀 수정**: timeScale 트랙 전역이라 걷기 배율이 공격/사망/배치까지 늦추던 것 → 로코모션 루프(Loop==true)에만 적용. 튜닝 확정 refSpeed 1.2/max 3.0. 순수 프레젠테이션, ECS 변경 0. 후속: 코너 접지 스냅·Android 프로파일)
- **Attack anim speed match** → `docs/spec/attack-anim-speed-match/` (완료 2026-07-10, units 0~1 — 공격 Spine 애니를 실제 발사 주기에 compress-to-fit → 공속이 "빠른 스윙"으로 체감. `TrackEntry.TimeScale = max(1, animDuration / max(cooldownDuration/attackSpeedMul, hitDelaySec))`. **별도 튜닝 SO 없이 공격속도 필드(SO attackCooldown+버프+hitDelay)에서 직접 파생**(SoT 불변, 사용자 결정). 하한 1.0=구조 상수(느린 공격 자연+대기), 상한 없음(attackSpeedMul [0.2,5] 캡+authoring 규율). 산식 critic 1회 준수 판정+MEDIUM/LOW 반영. 시뮬 rate/데미지 불변. 후속: hit 프레임 정렬)
- **Result screen visual upgrade** → `docs/spec/result-screen-visual-upgrade/` (완료 2026-07-08, units 0~3 — 결과 팝업 리더보드를 인게임 HUD 언어(네이비/골드 홀로그램)로 리스킨: `UiRoundedSprite` 공용 절차 스프라이트 + 행별 플레이트·순위 배지(금/은/동)·본인 골드 강조·WAITING 회색 + **RESTART 하단 고정 3영역 앵커 레이아웃**(단일 VerticalLayoutGroup 겹침 결함 제거). 순수 `BuildRows` + EditMode 6. tournament-play-report 배선 불변, 순수 프레젠테이션. 배경은 시즌 아트 시도 → 인게임에서 풀스크린이 보드 덮어 폐기, `UiOverlay.Dim` 유지. 직렬화 필드 0(씬 diff 0). 후속: 등장 애니메이션·ScrollRect·한글 폰트)
- **Damage number visual upgrade** → `docs/spec/damage-number-visual-upgrade/` (완료 2026-07-07, units 0~3 + Play 튜닝 다회 — 순수 프레젠테이션(ECS 변경 0). 머리위 앵커(sim-Y drop 회피)·카메라축 겹침방지 격자·청록→골드→오렌지 팔레트·정점 그라데이션·TimeManager 델타 교정·index 결정론 셰이크/회전 + 하프톤/글로우/흰아웃라인/드롭섀도 머티리얼(비-모바일 Distance Field 변종) + 클러스터 스파크. 스파크는 별→**GA Circle18 라운드 도트 버스트 + 폰트색 틴트 emissive + 임팩트 플래시** 로 재작업(GA 텍스처만 재활용, 단일 경량 PS). 2트랙 critic BLOCKER 2건 반영. 후속: unit 2 Android 실기 프로파일 게이트·유닛별 정밀 앵커·진짜 emissive(URP Bloom))

- **Placement enemy see-through** → `docs/spec/placement-enemy-see-through/` (completed 2026-07-06, units 0~6, `9941f27` — 드래그 배치 중 적 유닛(Spine·Quad 혼합)을 반투명화해 가려진 뒤 보드 타일 노출. cutout↔transparent 런타임 전환(Quad)·PMA skeleton.A(Spine)·그림자 페이드·health tint 합성·매프레임 재적용. 프리뷰 불투명/최상단(unit 5) + 배치 하이라이트 적 위로(unit 6). 순수 프레젠테이션, ECS 변경 0·채널 14개 불변. two-track APPROVE(0~4)+M1 반영. 스텐실/후처리 리빌·블로킹 하자드 반투명은 후속)

- **Portal VFX upgrade** → `docs/spec/portal-vfx-upgrade/` (completed 2026-07-06, unit 0 — 물빔(WaterBeam 어거지) 제거 + 스월 지속화(loop+사이클 오버라이드, LocationVfx 가 duration 무시하는 원인 해소). 룬 게이트 실험은 사용자 반려·롤백. 입구/출구 시각 구분은 후속 후보)
- **Object pipeline map** → `docs/spec/object-pipeline-map/` (completed 2026-07-06, unit 0, 커밋 `aeccbc3a` — 플레이 오브젝트 생성→렌더 정거장 체크표 `docs/reference/object-pipeline-map.md`(아키타입 10종, `.cs` 앵커 57건 실측) + CLAUDE.md 파이프라인 커버리지 필수 섹션 규칙(N/A+이유 강제). artillery-defender 사후 대조로 카탈로그 등록 확인 포인트 승격. 훅/리뷰 게이트는 후속)

- **Workflow reproducibility** → `docs/spec/workflow-reproducibility/` (completed 2026-07-06, units 0~3 — fresh clone 워크플로우 재현: `.claude` 표준 추적+settings 분할(훅·read-only 권한 커밋) + auto-memory 27건 → `docs/reference/lessons/` 승격 + AGENTS=CLAUDE symlink + 루트 README 부트스트랩. critic APPROVE-WITH-CHANGES 반영, fresh clone 실측 검증. MCP/LFS 는 범위 밖)

- **Artillery defender** → `docs/spec/artillery-defender/` (completed 2026-07-06 — 곡사포 유닛: `Projectile_ArtilleryShell`(Rock ballistic) + `Defender_Artillery`(range7/cd3.5/dmg60, Cannon Spine 재사용) + DefenderCatalog 등록. projectile-trajectory-payload 엔진의 첫 Play 실증. 신규유닛 프로필 reconcile 은 후속)

- **Projectile trajectory × payload** → `docs/spec/projectile-trajectory-payload/` (엔진 완료 2026-07-06, units 0~5 — 투사체를 궤적(Homing/BallisticArc)×페이로드(SingleSplash/TileAoe) 직교 2축으로 분해. 홈잉 무회귀 이관 + BallisticArc 궤적 + TileAoe 반경 AOE + 곡사 발사 배선. 커밋 `e5836bc`~`27a452a`, 양트랙 리뷰 3게이트, EditMode 498/499. Play e2e 는 artillery-defender 로 이관. 신규 시스템/큐/맥락 0)

- **Placement keyring cord preview** → `docs/spec/keyring-cord-preview/` (completed 2026-07-05, squash 머지 `d197bc7` — 드래그 프리뷰 키링화: 고리=손가락(공중)·유닛=보드 스프링 follow(무게추 흔들림)·**하이라이트는 마우스 고정**(스윙 유닛 아님). 이전 drag-preview sway 완전 교체(SO 스키마도). camUp 수직분리·워밍업 금지 등 되돌리면 안 되는 설계는 handoff 참조. 탐색 이력 16커밋은 `feature/keyring-cord` 브랜치. 중력 드롭·아트 스왑은 후속)
- **Placement attack-range preview** → `docs/spec/placement-attack-range-preview/` (completed 2026-07-04, units 0~2 — 드래그 배치 중 공격범위를 노란 격자 outline 로 동기 펄스 표시. `Tilemap.color` tint + 전용 `_rangeTilemap`(sorting -12) + Chebyshev `RangeToTiles`. e2e 드래그 추종 Play 검증)
- **Placement drag-preview polish** → `docs/spec/placement-drag-preview-polish/` (completed 2026-07-04, units 0~1 + rev — 프리뷰 빌보드 각도 정합 + 매달린 키링 velocity-lean sway(SO 튜닝) + 프랍 위 정렬. Play(MCP) 검증)
- **Dreamstone loadout** → `docs/spec/dreamstone-loadout/` (completed 2026-07-06, units 0~7 — 스쿼드 4슬롯 장착 + set-then-apply 반입 + 개별 아이템 64종(순차 id·캐파 내 티어 스탯) + 코스트 생산속도 매치 배선 + 아이콘 스크롤 피커. 리뷰 4단 + 실측 검증. 획득/인벤토리는 후속)

- **Legacy render removal** → `docs/spec/legacy-render-removal/` (completed 2026-07-03, units 0~4 — Legacy MapView 렌더/Legacy3D 모드/시즌 백드롭/테마 LEGACY 43필드 완전 삭제, ~6,300줄 순삭. Tilemap 경로 무회귀. 씬 MapView 잔재 청소는 follow-up)

- **Projectile GA reskin** → `docs/spec/projectile-ga-reskin/` (completed 2026-07-03, units 0~6 — GabrielAguiar UniqueProjectiles Vol4 50종 라이브러리 + 스트립/스왑 툴 + ViewPool as-is 가드(streak/preserveVfxColors) + 높이오프셋 + ProjectileOffset sorting + muzzle-hit. 실게임 검증 PASS. 최종 변종선택/스케일/미사용정리는 사용자 취향 후속)

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
