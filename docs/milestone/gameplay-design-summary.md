# Defense Tournament — 인게임 기획 현황 요약

> **역사 보존 배너 — 2026-05-08 snapshot.** 이 문서는 당시의 draft·flow·수치를 보존하며 현재 구현의 진실원이 아니다. 2026-07-29 기준 실제 흐름은 [`production-transition/demo-baseline.md`](../production-transition/demo-baseline.md)를 우선한다.
>
> 작성일: 2026-05-08  
> 기준: PRD + 전체 spec 문서 + 실제 코드/asset 직접 확인  
> 목적: 인게임 상세 기획서 작성의 토대  
> 기술 상세는 각 `docs/spec/{feature-slug}/README.md` 참조

---

## 1. 게임 정체성

**비동기 토너먼트 디펜스** — 같은 공격 패턴을 두고 여러 플레이어가 각자 드래프트한 유닛으로 방어하며 스코어를 겨루는 게임.

핵심 긴장감:
- **드래프트 판단**: "이 패턴에 무슨 유닛이 맞는가"를 반복 플레이로 학습
- **실시간 코스트 관리**: 유닛 배치 vs. 스킬 사용 사이의 자원 선택
- **패배 귀인**: 지고 나서 "내가 무엇을 잘못했는지"를 구체적으로 언어화 가능

---

## 2. 게임 페이즈 흐름

```
GamePhase.None
    │
    ▼
GamePhase.Draft  ← 게임 시작 직후 자동 진입
    │   드래프트 + 스킬 로드아웃 픽 + 웨이브 패턴 확인
    │   (배경에 맵이 프리빌드되어 표시됨)
    ▼
GamePhase.Placement  ← 드래프트 확정 후 자동 진입
    │   코스트 범위 내 유닛 배치 (30초 제한)
    │   SkillBar 표시 시작
    │   30초 경과 또는 START BATTLE 버튼
    ▼
GamePhase.Battle  ← Placement 종료(30초 경과 또는 START BATTLE) 후 진입
    │   3분 타이머, 웨이브 자동 등장
    │   실시간 유닛 추가 배치 / 스킬 사용
    ▼
GamePhase.Result  ← 종료 조건 달성 시
        VICTORY / DEFEAT + 리더보드 + RESTART / REDRAFT
```

---

## 3. 시스템별 기획 상세

### 3.1 맵 시스템

**20×N 그리드**, seed 기반 절차적 생성. 같은 seed = 항상 동일 맵.

**타일 종류 (4종)**

| 타일 | 역할 |
|---|---|
| Walk | 적 이동 경로. Flow Field로 경로 계산 |
| Place | 방어 유닛 배치 가능 |
| Env | 환경 지형 (장식, 이동 불가) |
| Deco | 순수 장식 타일 |

**경로 구조 (Branch → Trunk → Root)**
- 스폰 레인: 맵 높이에 따라 가변 (최소 2개)
- 각 레인은 독립 Branch에서 시작 → 공유 Trunk → 단일 Goal로 합류
- Goal에 적이 지정 횟수 도달하면 패배 (현재 기본 5회)

**맵 생성 설정 (MapGenerationOptions)**
- PathShape: 경로 형태 선택 (Straight / Free)
- GridSize: 맵 크기 (MapGenerationOptions 기본 20×10, MapGenerationSettings 기본 20×20)
- ObstacleDensity: 장애물 밀도 (Low / Medium / High)
- SpawnLaneCount: 스폰 레인 수 (최소 2, 맵 높이에 따라 상한 clamp)
→ 드래프트 화면에서 좌상단 MAP SETTINGS 토글로 실시간 변경 가능. 변경 즉시 맵 재생성.

**테마 (MapThemeData)**
- 현재 구현: forest 테마 (prop 14종 이상)
- prop 종류: boulder, bush, crates, crystal, dead_tree, fallen_log, mushroom, pine_tree, round_tree, ruin_wall, skull_sign, small_rock, stone_lantern, stone_shrine, tree_stump 등

---

### 3.2 웨이브 패턴 (공격 타임라인)

적 등장 순서표. **드래프트 화면에서 전체 공개**됨.

**생성 규칙**
- Seed 기반 deterministic 생성 (같은 seed = 동일 웨이브)
- 3분(180초) 기준 **10~15개 웨이브**
- 각 웨이브: **정확히 2종** 공격 유닛, **10~15마리**
- 스폰 순서: A, B, A, B… interleave
- 레인 배정: `localIndex % laneCount`

**타이밍 배정**
- Wave 1: 0초에 자동 호출
- 간격: `180초 / 웨이브 수` (균등 배분)
- 마지막 웨이브: 180초보다 앞에 예약
- **Next Wave 버튼**: 다음 웨이브 즉시 강제 호출 (연타 허용, 중복 없음)

**드래프트 화면 표시 (Wave Strip)**
- 상단에서 낙하 후 좌정렬
- 웨이브별 카드: 유닛 2종 + 각 수량 표시
- 우측 토글 버튼으로 펼치기/접기

**로그 기록 내용**
- wave seed, generatorVersion, waveIntervalSec, 각 wave 상세
- 이벤트: 자동 트리거 / 강제 트리거(Next Wave) 구분, 경과 시간

---

### 3.3 드래프트 시스템

한 판에 쓸 방어 유닛 7종을 결정하는 사전 의사결정.

#### 풀 구성 (10장 고정)

| 슬롯 | 장수 | 구성 방식 |
|---|---|---|
| Basic | 3 | basicDeck[] 고정 (항상 포함) |
| Meta | 2 | metaDeck[] 고정 (로테이션, 외부 교체) |
| Ego | 1 | egoUnit 고정 (현재: Bruiser) |
| Collection | 4 | collectionPool에서 seed 기반 랜덤 4종 |
| **합계** | **10** | |

#### 선택 방식 — "3장 폐기"
- 10장 fan 형태로 제시
- 플레이어는 **3장 폐기** → 남은 7장이 자동 픽
- 위 스와이프(delta.y ≥ 120px, 0.45초 내) 또는 클릭(드래그 < 30px)으로 폐기
- 3번째 폐기 완료 → 마지막 toss 애니메이션 후 자동 확정 (CONFIRM 버튼 없음)
- Restart: 드래프트 없이 같은 픽으로 재시작
- Redraft: 새 seed로 드래프트 재진행

#### 유닛 등급 시스템 (Rarity)

| 등급 | 해당 유닛 | 카드 테두리 |
|---|---|---|
| Common | Scout, Guardian, Cannon, Ranger, Piercer, Marksman | 회색 |
| Rare | Archer, Bastion, Healer, Sniper | 파랑 |
| Epic | FireCaster, IceCaster, PoisonCaster, BlockingCaster | 주황 |
| Ego | Bruiser | 보라 |

#### 카드 2-Layer 시각
- **테두리**: 등급 색상 (유닛 고유 속성)
- **상단 배너**: 슬롯 색상 (이 판에서 어느 슬롯으로 등장했는가)
  - Basic=파랑, Meta=골드, Collection=초록, Ego=보라

#### 카드 VFX 계층 (DraftCardVfxDriver)

| 등급 | VFX |
|---|---|
| Common | 테두리 pulse (3s) + foil overlay (0.08) |
| Rare | pulse (2s) + foil (0.22) |
| Epic | pulse + foil (0.48) + UI ember 8개 + Particle System |
| Ego | pulse + foil (0.72) + ember 15개 + Particle System + 배너 shimmer |

foil: `DraftCardFoil_UI.shader` — 홀로그래픽 레인보우, 마이크로 회절, 에지 림. 카드 틸트 반응.

#### 드래프트 화면 레이아웃 (1920×1080 기준)
- **배경 (전체)**: 이번 판 맵 풀스크린 프리빌드 (카드 뒤에 표시)
- **상단 (0~140px)**: Wave Strip (웨이브 요약 스크롤)
- **좌상단**: MAP SETTINGS 토글 (개발 옵션)
- **좌측 중앙**: Wave strip 펼치기/접기 버튼
- **우측 중앙**: 이번 판 스킬 슬롯 표시 (세로 배치)
- **하단 중앙**: 카드 Fan (10장)

#### 로그 기록
- pool 전체 10장 목록, picked 7장 목록, seed

---

### 3.4 스킬 로드아웃 시스템

드래프트와 동시에 결정되는 이번 판 스킬 구성.

**구조**: 6종 풀에서 seed 기반 Fisher-Yates shuffle로 **2종을 랜덤 픽**

**스킬 풀 (6종 전체)**

| 스킬 | EffectType | 타겟 방식 | 주요 효과 |
|---|---|---|---|
| SlowField | SlowField | TilePoint | 범위 내 적 이동속도 감소 |
| PowerSurge | PowerSurge | DefenderUnit | 대상 방어 유닛 공격력 증폭 |
| RapidFire | RapidFire | DefenderUnit | 대상 방어 유닛 공격속도 증폭 |
| Tornado | Tornado | TilePoint | 범위 내 적 풀링 + 이동 방해 |
| Meteor | Meteor | TilePoint | 경고 딜레이 후 범위 광역 데미지 |
| Portal | Portal | TilePoint×2 | 입구→출구 적 강제 이동 (2-tap) |

**스킬 공통 속성 (SkillData)**
- `range`: 효과 범위 (타일 단위)
- `magnitude`: 효과 강도 (속도배율 / 데미지 / 증폭량)
- `durationSec`: 효과 지속 시간
- `cooldownSec`: 재사용 대기시간
- `cost`: 코스트 소모량
- `warningSec`: 경고 시간 (Meteor용, 0=즉시)
- `uiTint`: SkillBar 슬롯 색상

**Restart vs. Redraft**
- Restart: 같은 스킬 로드아웃 유지 (동일 조건 재도전)
- Redraft: 새 seed로 스킬 다시 롤 (새 판)

**SkillBar UI (우측 중앙 세로 배치)**
- 드래프트 확정 후 표시, 드래프트 시작 시 숨김
- 각 슬롯: 스킬 이름 + 쿨다운 카운트다운 또는 코스트 표시
- 상태: 준비(uiTint) / 쿨다운 또는 코스트 부족(어두움) / aim 중(밝게)
- 슬롯 클릭 → aim 모드 진입 → 맵 탭 → 발동
- ESC 또는 다른 슬롯 선택 시 aim 취소
- Portal: 2-tap (입구 탭 → 출구 탭)
- 배치 선택 ↔ 스킬 aim 상호 배타 (마지막 선택이 우선)

**로그 기록**
- 로드아웃 2종 ID 목록, 풀 6종 전체 ID, seed
- 각 사용: skill_id, 발동 시간, 타겟 타일, 영향 받은 유닛 수, 코스트 소모

---

### 3.5 방어 유닛 (Defenders) — 15종

모든 유닛은 `DefenderUnitData` ScriptableObject로 정의.

#### 기본 스탯 구조
- health, attackRange, attackDamage, attackCooldown
- attackTargetCount (melee AoE 최대 타겟 수, 기본 1)
- cost (배치 코스트)
- rarity (등급)

#### 공격 방식 (outputs[])
- `AttackOutput[]`: kind(Damage/Heal/ApplyStat/ApplyStack), magnitude, duration, stat/op/stackKind
- 투사체 없음(null) → 근접 즉시 데미지
- ProjectileData 있음 → 투사체 발사

#### 배치 On-Place 스킬 (OnPlaceEffectType — 8종)

| 효과 | 설명 |
|---|---|
| None | 없음 |
| SlowPulse | 주변 적에게 즉시 Slow |
| BoostNearbyDefenders | 인접 아군 공격력 증가 |
| BindNearby | 주변 적 이동 방해 |
| MeleeBurst | 주변 적에게 즉시 데미지 |
| ForwardProjectile | 전방으로 투사체 발사 |
| GainCost | 코스트 즉시 획득 |
| ReduceSkillCooldown | 스킬 쿨다운 단축 |

#### CC/Knockback 설정 (유닛별)
- knockbackDistance / knockbackDuration: 공격 시 넉백
- onPlacePushDistance / onPlacePushDuration / onPlacePushRadius: 배치 시 주변 적 push

#### 유닛 목록

**기본 전투형 (Common)**

| 유닛 | 특성 |
|---|---|
| Scout | 짧은 사거리, 빠른 공격속도 |
| Guardian | 높은 체력, 느린 공격 |
| Cannon | 광역 투사체 (CannonBall) |
| Ranger | 중거리 원거리 딜러 |
| Piercer | 관통 투사체 (Bolt) |
| Marksman | 장거리 단일 고데미지 |

**강화형 (Rare)**

| 유닛 | 특성 |
|---|---|
| Archer | 중거리 빠른 연사 (Arrow), 물 결속 배치 VFX |
| Bastion | AoE 근접 다중 타겟 (attackTargetCount > 1) |
| Healer | 주변 3타일 아군 HP 회복 (targetAllies=true, 출력: Heal) |
| Sniper | 최장거리 단발 초고데미지 (Crimson 투사체) |

**Hazard Caster형 (Epic)**

| 유닛 | hazardCastKind | 생성 hazard |
|---|---|---|
| FireCaster | Zone | 화염 zone 1×1 |
| IceCaster | Zone | 얼음 zone 1×1 |
| PoisonCaster | Zone | 독 zone 1×1 |
| BlockingCaster | Blocking | 차단형 장애물 1×1 |

hazardCastRange, hazardCastCooldown으로 동작. 사거리 내 적 위치에 hazard 생성.

**Ego (1종)**

| 유닛 | 특성 |
|---|---|
| Bruiser | 근접 고체력 고데미지, 다중 타겟 (Ego 슬롯 전용) |

#### Spine 비주얼 (구현된 유닛)
- SpineUnitView / SpineUnitPool로 방어/공격 유닛 통합
- SO 필드: skeletonDataAsset, spineSkinName, idle/attack/death/drag/deploy animation
- spineVisualScale: SO에서 직접 크기 조절
- castAnchorBone: 투사체 발사 anchor (머즐 플래시 위치)
- Fallback: skeletonDataAsset=null이면 billboard quad 렌더링

#### 배치 VFX
- placementVfxPrefab: 배치 시 재생
- attackVfxPrefab: 공격 시 재생
- deploymentDuration: 배치 연출 시간 (기본 0.45초)
- placementSkillDelay: on-place 스킬 발동 딜레이

---

### 3.6 공격 유닛 (Enemies) — 6종

`AttackUnitData` SO로 정의. AI는 Flow Field 경로 따라가기만.

| 유닛 | 역할 | 특성 |
|---|---|---|
| Basic | 표준 보병 | 중간 체력·속도 |
| Swift | 고속 돌파 | 빠른 이동속도, 낮은 체력 |
| Tanker | 고체력 선봉 | 높은 체력, 느린 속도. BellKnight Spine |
| Rootcaster | 장거리 공격 | 투사체(RitualBolt) 발사, 공격 후 1초 이동 정지 |
| Needler | 이동 중 연사 | 이동하며 빠른 투사체(Needle) 연사 |
| Runner | 초고속 통과 | 최고 이동속도, 공격 없음 |

적 전용 투사체:
- `Projectile_Enemy_RitualBolt`: Rootcaster용
- `Projectile_Enemy_Needle`: Needler용

**웨이브 구성 예시**:
```
Wave 1  - Basic 8마리 + Swift 5마리
Wave 5  - Tanker 4마리 + Needler 8마리
Wave 10 - Rootcaster 6마리 + Runner 10마리
```

---

### 3.7 배치 시스템

**게임 페이즈**: Placement (30초 제한, CostConfig.placementPhaseDuration)

**흐름**:
1. 드래프트 확정 → Placement 페이즈 진입 → 코스트 리셋 → SkillBar 표시
2. 인벤토리(드래프트 7종)에서 유닛 카드 드래그
3. Place 타일 위 hover highlight
4. Ghost preview (silhouette) 표시
5. Drop 성공 → 타일 점유 → 코스트 차감 → 배치 VFX → On-Place 스킬 발동 → 전투 활성화
6. 배치 후 위치 고정 (이동 불가)
7. 30초가 끝나거나 START BATTLE 버튼을 누르면 Battle 페이즈 진입 + 코스트 자동 충전 시작

**배치 제약**:
- Place 타일에만 배치 가능
- 타일당 1종
- 코스트 부족 시 배치 불가 (TrySpend 실패)
- 코스트 차감 실패 시 자동 환불

**인접 시너지**:
- 배치/제거 시 주변 8방향 재계산
- StatModifierApplyEvents 채널을 통해 DamageMul 적용

**로그 기록**: unit_type, tile, time, cost_spent

---

### 3.8 코스트 시스템

**현재 DefaultCostConfig 수치 (SO)**:
- startingCost: **10**
- maxCost: **15**
- regenPerSec: **1.0/초**
- placementPhaseDuration: **30초**

**타임라인**:
- Placement 진입: 코스트 = startingCost(10)으로 리셋
- Battle 진입: 자동 충전 시작
- Result 또는 종료: 충전 정지

**소모처 (현재 정의된 코스트 범위)**:
- 유닛 배치: 유닛 SO의 cost 필드
- 스킬 사용: SkillData의 cost 필드 (cooldownSec 후 재사용 가능)

**보너스**: 코드 확인 - 특수 유닛 처치 보너스 미구현 (PRD 원안 항목)

---

### 3.9 실시간 방어 (Battle 페이즈)

**타이머**: 3:00 시작 (180초)

**플레이어 행동**:
1. 코스트 범위 내 추가 유닛 배치 (Placement과 동일 D&D 방식)
2. SkillBar에서 스킬 선택 → 타겟 탭 → 발동

**종료 조건**:
- 적 전멸: 즉시 VICTORY
- 시간 초과까지 버팀: VICTORY_TIMEOUT (ResultScreen에는 VICTORY로 표시)
- 적 5마리 Goal 도달: DEFEAT (AttackDeck.defeatGoalReachedCount 기본 5)

**현재 임시 스코어 공식**
```
score = max(0, floor(elapsedBattleSeconds * 10 - enemiesReachedGoal * 50))
```

처치 점수, 남은 시간 보너스, 남은 적 체력 기반 점수는 아직 기획 미확정.

---

### 3.10 스킬 효과 상세

#### Tornado (토네이도)
- EffectType: `Tornado`
- TargetType: TilePoint
- 효과: N초간 범위 내 적을 중심으로 풀링 (centerWorld 방향 끌어당김)
- 범위: tileRange (Chebyshev 정사각형)
- ECS 컴포넌트: `TornadoField` (centerWorld, tileRange, pullSpeed, remaining)
- VFX: SpawnTornado (범위 링 + 파티클)

#### Portal (포탈)
- EffectType: `Portal`
- TargetType: TilePoint (2-tap)
- 효과: 적이 입구 반경 진입 시 출구로 강제 이동
- ECS 컴포넌트: `PortalLink` (entry/exit 위치)
- UI: 1탭 = 입구 설정, 2탭 = 출구 설정 + 발동

#### Meteor (메테오)
- EffectType: `Meteor`
- TargetType: TilePoint
- 효과: `warningSec` 경고 후 범위 내 모든 적에게 `magnitude` 데미지
- 범위: tileRange (Chebyshev 정사각형)
- ECS 컴포넌트: `MeteorPending` (centerWorld, tileRange, damage, warningRemaining)
- VFX: 경고 링 → `MeteorFall.cs` → 버스트 VFX (`MeteorBurstEventsSingleton` drain)
- 시스템: `MeteorResolutionSystem` (warningRemaining 소진 → 범위 내 IncomingDamage 적용)

#### SlowField (슬로우 필드)
- EffectType: `SlowField`
- TargetType: TilePoint
- 효과: 범위 내 적 이동속도 감소 (magnitude = 속도 배율)
- CC 파이프라인: `EnemyCcEvents` → `CcApplySystem` → `CcEffect.Slow` → `ModifierStats.moveSpeedMul`

#### PowerSurge (파워서지)
- EffectType: `PowerSurge`
- TargetType: DefenderUnit
- 효과: 대상 방어 유닛 공격력(DamageMul) 증폭
- Modifier 채널: `StatModifierApplyEvents` → `ModifierApplySystem` → `ModifierStats.damageMul`

#### RapidFire (래피드파이어)
- EffectType: `RapidFire`
- TargetType: DefenderUnit
- 효과: 대상 방어 유닛 공격속도(AttackSpeedMul) 증폭
- Modifier 채널: `StatModifierApplyEvents` → `ModifierApplySystem` → `ModifierStats.attackSpeedMul`

---

### 3.11 전투 메커닉 심화

#### Zone Hazard (통과형 위험 지대)
이동 경로 위 통과 가능 + 효과 발동 구역. 현재 3종 × 2 크기 = 6 asset.

| 종류 | 효과 | asset |
|---|---|---|
| Poison zone | DoT (초당 데미지) | Hazard_Poison_1x1, Hazard_Poison_3x3 |
| Ice zone | Slow (이동속도 감소) | Hazard_Ice_1x1, Hazard_Ice_3x3 |
| Fire zone | 강한 DoT | Hazard_Fire_1x1, Hazard_Fire_3x3 |

- 적이 zone cell에 있는 매 프레임 CC enqueue → 자연 감쇠
- Visual ⊥ Effects: HazardPresenter(MonoBehaviour) / ECS CC pipeline 독립
- `HazardSO`: visual prefab + HazardEffect[] + lifetime + shape

#### Blocking Hazard (차단형 HP 장애물)
경로를 물리적으로 차단. 적이 공격해서 파괴.

| asset | 특성 |
|---|---|
| Hazard_Rock_1x1 | 1×1 바위 차단 |
| Hazard_Rock_3x3 | 3×3 바위 차단 |

- HP 보유 → 적이 공격 → HP 0 → DeadTag → 파괴 → 경로 재개방
- Destruction VFX: `HazardDestroyedEventsSingleton` drain → BlockingHazardPresenter
- HP bar: HealthBarState 부착

#### CC 효과 (이동 제한)

| CC | 발생 원인 | 효과 |
|---|---|---|
| Slow | Zone Ice, SlowField 스킬, BoostNearby OnPlace 등 | MoveSpeedMul 감소 |
| Impulse | 유닛 knockback (SO: knockbackDistance), 배치 push | 순간 방향 충격 |
| DoT | Zone Poison/Fire | 지속 데미지 (DotApplySystem) |

#### Modifier 시스템 (StatModifier)

| StatKind | 의미 | 적용처 |
|---|---|---|
| DamageMul | 공격 데미지 배율 | AttackSystem (attacker side) |
| AttackSpeedMul | 공격 속도 배율 | AttackSystem (cooldown) |
| DmgTakenMul | 받는 데미지 배율 | DamageApplicationSystem (target side) |
| RegenPerSec | 초당 HP 회복 | DamageApplicationSystem (매 프레임) |
| MoveSpeedMul | 이동 속도 배율 | MovementSystem |

StackModifier: 스택 누적 → 임계값 도달 시 파생 효과 발동 (1프레임 지연)

---

### 3.12 투사체 시스템

**구현 투사체 (ProjectileData)**

| asset | 사용 유닛 | 비주얼 키트 |
|---|---|---|
| Projectile_Arrow | Archer | WindBullet |
| Projectile_Bolt | Piercer | Stonebullet |
| Projectile_CannonBall | Cannon | Fireball |
| Projectile_Sniper_Crimson | Sniper | (별도) |
| Projectile_Enemy_RitualBolt | Rootcaster | (tint/scale) |
| Projectile_Enemy_Needle | Needler | (tint/scale) |

**배리에이션 시스템**:
- 결정적(per-asset): tintColor, emissionMultiplier, facing(AlongVelocity/FixedUp/SpinAroundUp), spinSpeed, textureVariants
- per-shot 랜덤: scaleJitter, hueJitter, rotationJitter (시뮬레이션 결정성 분리)

**Hit VFX**: 타격 시 hit prefab 1회 재생 후 풀 반환  
**Cast VFX**: 발사 순간 머즐 플래시 (castAnchorBone 위치)

---

### 3.13 결과 화면 (ResultScreen)

**구현 완료** (ResultScreen.cs).

**표시 내용**:
- 결과 텍스트: "VICTORY" / "DEFEAT"
- 리더보드 (monospace 표 형식):
  - Bot 5명 더미 스코어 + 플레이어 스코어 합산
  - 스코어 내림차순 정렬
  - 플레이어 행은 금색(#FFD54A)으로 강조
  - RANK / NAME / SCORE 컬럼

**버튼**:
- **RESTART** (파랑): 같은 맵+드래프트+스킬 로드아웃으로 재시작. 드래프트 없이 즉시 배치 페이즈.
- **REDRAFT** (주황): 새 seed로 드래프트 재진행. 맵/스킬 새로 롤.

---

### 3.14 로컬 로깅 시스템

**구현 완료** (BattleLogger.cs + BattleLogSchema.cs).

**저장 위치**:
- Unity Editor: `<프로젝트 루트>/GameLogs/session-YYYYMMDD-HHmmss-{uuid8}.json`
- Android 빌드: `Application.persistentDataPath/GameLogs/`

**기록 데이터 전체 목록**:

```json
{
  "session_id": "...",
  "phase": "phase8",
  "timestamp_start": "...", "timestamp_end": "...",
  "attack_deck_id": "...",
  "map": { "seed", "generatorVersion", "gridWidth", "gridHeight", "spawnCount", "pathShape" },
  "wavePattern": {
    "seed", "generatorVersion", "waveIntervalSec", "waveCount",
    "waves": [{ "waveIndex", "triggerTimeSec", "unitA", "countA", "unitB", "countB", "totalCount" }],
    "events": [{ "eventType", "waveIndex", "elapsedSec", "forced" }]
  },
  "draft": { "pool": [...10장], "picked": [...7장], "seed" },
  "skill": {
    "loadout": [...2종 id],
    "pool": [...6종 id],
    "seed": ...,
    "usages": [{ "skill_id", "time", "target_tile", "target_tile_b", "affected_count", "cost_spent" }]
  },
  "hazards": [{ "event_type", "hazard_id", "kind", "tile", "time", "scalar", "amount", "target_index" }],
  "synergy": { "activations", "peakCount" },
  "on_place_usages": [{ "unit_type", "effect", "tile", "time", "affected_count" }],
  "placements": [{ "unit_type", "tile", "time", "cost_spent" }],
  "attack_outputs": [{ "source_unit", "kind", "magnitude", "detail", "duration", "source_tile", "target_tile", "time" }],
  "result": { "outcome", "duration_sec", "enemies_reached_goal", "score" }
}
```

---

## 4. 구현 현황 요약

### 완료된 시스템 (코드 확인 기준)

| 시스템 | 확인 기준 |
|---|---|
| 맵 시스템 | ProceduralMapGenerator.cs, MapView.cs, BattleMapBuilder.cs |
| 웨이브 패턴 | WavePatternGenerator.cs, GeneratedWavePlan.cs |
| 드래프트 + 슬롯 기반 풀 | DraftController.cs (Basic/Meta/Ego/Collection 슬롯 구현) |
| 유닛 등급 + 카드 VFX | DefenderRarity.cs, DraftCardVfxDriver.cs, DraftCardFoil_UI.shader |
| Drag & Drop 배치 | DefenderDragPlacementController.cs, DefenderDragSlot.cs |
| 스킬 로드아웃 (6종 풀, 2종 픽) | SkillLoadoutController.cs, SkillData.cs (6종 SO 확인) |
| 스킬 발동 UI | SkillBar.cs (aim 모드, Portal 2-tap, 쿨다운 표시) |
| 코스트 시스템 | CostRuntime.cs, CostConfig.cs, DefaultCostConfig.asset |
| On-Place 스킬 (8종) | DefenderUnitData.OnPlaceEffectType (8종 enum 확인) |
| Modifier Framework | ModifierApplySystem.cs, StatModifierSlot.cs, StackModifierSlot.cs |
| Healer 유닛 | Defender_Healer.asset, targetAllies=true |
| CC 파이프라인 | CcApplySystem.cs, CcDecaySystem.cs, CcEffect.cs |
| Path Zone Hazard (3종) | Hazard_Poison/Ice/Fire 1x1+3x3 asset 확인 |
| Blocking Hazard | Hazard_Rock 1x1+3x3, BlockingHazardPresenter.cs |
| 공격 유닛 6종 | Enemy_Basic/Swift/Tanker/Needler/Rootcaster/Runner.asset |
| Hazard Caster 4종 | Defender_FireCaster/IceCaster/PoisonCaster/BlockingCaster.asset |
| 투사체 비주얼 | ProjectileViewPool.cs, ProjectileData 6종 asset |
| Tornado/Portal/Meteor 스킬 | TornadoField.cs, PortalLink.cs, MeteorPending.cs, MeteorFall.cs |
| 드래프트 맵 프리빌드 | GameManager.Start → battleBridge.PrepareDraftMap() |
| AttackSystem 단일 루프 | Faction + targetMask 기반 통합 쿼리 |
| 결과 화면 | ResultScreen.cs (VICTORY/DEFEAT + 리더보드 + RESTART/REDRAFT) |
| 로컬 로깅 | BattleLogger.cs + BattleLogSchema.cs (JSON 파일 기록) |

### 미완/튜닝 필요

| 항목 | 상태 |
|---|---|
| Tile Range 통일 (Chebyshev) | 코드 반영 완료. GridMath.RangeToTiles/ChebyshevDistance, AttackSystem, BattleBridge 스킬/OnPlace, Tornado, Meteor 확인 |
| 패배 조건 N (Goal 도달 횟수) | 구현값 있음: AttackDeck.defeatGoalReachedCount 기본 5. 기획 튜닝 필요 |
| 스코어 계산식 | 임시 공식 구현됨: elapsedBattleSeconds*10 - enemiesReachedGoal*50. 상세 기획 확정 필요 |
| 코스트 수치 튜닝 | DefaultCostConfig 값(10/15/1.0) 확정 필요 |
| 특수 유닛 처치 코스트 보너스 | PRD 원안 미구현 |
| 각 유닛 스탯 수치 | SO는 있으나 밸런스 튜닝 미완 |
| SlowField/PowerSurge/RapidFire 수치 | SO는 있으나 수치 미확인 |

---

## 5. 콘텐츠 현황 (asset 직접 확인 기준)

### 방어 유닛 SO (15종)
```
Common (6): Defender_Scout, Defender_Guardian, Defender_Cannon,
            Defender_Ranger, Defender_Piercer, Defender_Marksman
Rare   (4): Defender_Archer, Defender_Bastion, Defender_Healer, Defender_Sniper
Epic   (4): Defender_FireCaster, Defender_IceCaster,
            Defender_PoisonCaster, Defender_BlockingCaster
Ego    (1): Defender_Bruiser
```

### 공격 유닛 SO (6종)
```
기존 3종: Enemy_Basic, Enemy_Swift, Enemy_Tanker
신규 3종: Enemy_Rootcaster, Enemy_Needler, Enemy_Runner
```

### 스킬 SO (6종)
```
Skill_SlowField, Skill_PowerSurge, Skill_RapidFire
Skill_Tornado, Skill_Meteor, Skill_Portal
```

### Hazard SO (8종)
```
Zone:     Hazard_Poison_1x1, Hazard_Poison_3x3
          Hazard_Ice_1x1, Hazard_Ice_3x3
          Hazard_Fire_1x1, Hazard_Fire_3x3
Blocking: Hazard_Rock_1x1, Hazard_Rock_3x3
```

### 투사체 SO (6종)
```
방어 유닛용: Projectile_Arrow, Projectile_Bolt, Projectile_CannonBall,
             Projectile_Sniper_Crimson
공격 유닛용: Projectile_Enemy_RitualBolt, Projectile_Enemy_Needle
```

### 맵 테마 — forest (prop 14종 이상)
```
boulder_cluster, bush, crates_barrel, crystal_cluster, dead_tree,
fallen_log, mushroom, pine_tree, round_tree, ruin_wall, skull_sign,
small_rock, stone_lantern, stone_shrine, tree_stump
```

### Spine 캐릭터
```
BellKnight_SkeletonData  (Enemy_Tanker)
player-main_SkeletonData (Defender 일부)
```

---

## 6. 기획 미결 사항 (상세 기획서 작성 전 결정 필요)

### 게임플레이 규칙
- [ ] 패배 조건 튜닝: 현재 기본 5마리 Goal 도달 시 패배
- [ ] 스코어 계산식 확정: 현재 임시 공식은 생존 시간 보상 - Goal 도달 페널티. 처치 점수/시간 보너스/남은 적 체력 반영 여부 결정 필요
- [ ] 특수 유닛 처치 코스트 보너스 (PRD 원안, 미구현)
- [ ] 시너지 조건 상세 (인접 동종 몇 마리 이상?)

### 수치 튜닝 (현재 DefaultCostConfig 수치는 초기값 — 플레이테스트 전)
- [ ] startingCost (현재 10), maxCost (현재 15), regenPerSec (현재 1.0)
- [ ] placementPhaseDuration (현재 30초)
- [ ] 각 유닛 배치 cost, 스킬 cost
- [ ] 각 스킬 range/magnitude/durationSec/cooldownSec
- [ ] 각 유닛 스탯 (health/attackDamage/attackCooldown/attackRange)
- [ ] Hazard lifetime, 효과 강도

### 스킬 로드아웃 UX
- [ ] 드래프트 화면에서 2종 픽을 플레이어가 보는 방식 (우측 패널 표시 현재 구현됨)
- [x] 픽된 스킬 2종 공개 시점: 드래프트 시작 시 Roll 후 우측 패널에 표시
- [ ] 스킬 사용 횟수 제한 유무 (현재: 쿨다운만, 무제한 횟수)

### UX 미결
- [ ] 배치 페이즈 남은 시간 표시 방식 (TimerDisplay.cs 있음)
- [ ] 결과 화면 "결정적 순간" 표시 (패배 귀인 지원)
- [ ] 인벤토리 UI (드래프트한 7종 카드 + 배치 현황 표시)

---

## 7. 검증 가설 대비 현황

| 가설 | 필요 시스템 | 구현 상태 |
|---|---|---|
| **H1**: 반복 플레이로 드래프트 판단 개선 | 로컬 로그 + 결과 화면 다시 시작 | **로그 구현 완료** / 결과 화면 구현 완료 / 임시 스코어 공식 있음, 상세 공식 미확정 |
| **H2**: 코스트 제약의 실시간 긴장감 | 코스트 시스템 + 스킬 + 실시간 배치 | **구현 완료**, 수치 튜닝 필요 |
| **H3**: 패배 원인 귀인 가능성 | 결과 화면 + 배치 요약 + 핵심 순간 표시 | 결과 화면 기본 구현 / 배치 요약 미구현 / "결정적 순간" 미구현 |
| **H4**: 아키텍처 타당성 | ECS + BattleBridge | 지속 검증 중 (긍정적) |

**다음 우선순위**: H1/H3 완성을 위한 스코어 계산식 확정 + 배치 요약 표시 + 패배 귀인 힌트 구현.
