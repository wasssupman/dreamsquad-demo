# Session Handoff — Enemy Unit Development

**Status**: 구현 완료, PlayMode 밸런스/시각 확인은 후속 필요.  
**작성일**: 2026-04-30.  
**범위**: 신규 적 3종, imagegen 기반 적 스프라이트, 적 투사체 연결, 적 공격 중 이동 pause, 탱커 Spine 전환, Spine 유닛 비주얼 통합 리팩토링.

## 요약

이번 세션에서는 기존 적 3종(`Basic`, `Swift`, `Tanker`) 위에 신규 적 3종을 추가하고, 적도 방어 유닛과 같은 projectile/Spine 비주얼 인프라를 사용할 수 있게 확장했다.

핵심 결과:

- 신규 적 유닛 3종 추가:
  - `Rootcaster`: 장거리 투사체 공격형. 공격 후 1초 이동 정지.
  - `Needler`: 이동하면서 낮은 데미지 투사체를 빠른 주기로 발사.
  - `Runner`: 초고속 이동형. 공격 없음.
- imagegen 스킬로 cult/dark-cute casual cartoon 방향의 적 이미지 생성 후 크로마키 제거, 1024 PNG 텍스처로 정리.
- 적 전용 projectile data 2종 추가:
  - `Projectile_Enemy_RitualBolt`
  - `Projectile_Enemy_Needle`
- `Enemy_Tanker`는 `Assets/_Project/Characters/BellKnight.skel` 기반 Spine 비주얼로 전환.
- 방어/공격 유닛이 공통 Spine 렌더링 경로를 사용하도록 `SpineUnitView` / `SpineUnitPool`로 통합.

## 주요 코드 변경

### 적 데이터 확장

- `Assets/_Project/Scripts/Data/AttackUnitData.cs`
  - `ProjectileData projectile`
  - `float movePauseOnAttackSec`
  - Spine 표시 필드 추가:
    - `SkeletonDataAsset skeletonDataAsset`
    - `spineSkinName`
    - `idleAnimation`
    - `attackAnimation`
    - `deathAnimation`
    - `spineVisualScale`
  - `ISpineUnitVisualData` 구현.

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
  - 기존 Spine 필드를 `ISpineUnitVisualData` 구현으로 노출.

- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs`
  - 방어/공격 유닛이 공유하는 Spine 비주얼 계약.

### 적 공격 / 이동

- `Assets/_Project/Scripts/Battle/Combat/EnemyAttackMovePause.cs`
  - 적 공격 후 이동 정지 타이머 컴포넌트.

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
  - `ProjectileRef`를 가진 적도 방어 유닛처럼 `ProjectileSpawnRequest`를 생성.
  - `EnemyAttackMovePause`가 있는 적은 공격 시 `remaining`을 `duration`으로 갱신.

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
  - `EnemyAttackMovePause.remaining > 0`인 유닛은 이동하지 않고 타이머만 감소.

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - 적 스폰 시 `AttackUnitData.projectile`이 있으면 `ProjectileRef` 부착.
  - `movePauseOnAttackSec > 0`이면 `EnemyAttackMovePause` 부착.
  - 방어/공격 Spine 유닛 모두 `SpineUnitPool`을 통해 스폰/동기화.

### Spine 비주얼 통합

삭제됨:

- `Assets/_Project/Scripts/Presentation/SpineDefenderView.cs`
- `Assets/_Project/Scripts/Presentation/SpineDefenderPool.cs`
- `Assets/_Project/Scripts/Presentation/SpineAttackUnitView.cs`
- `Assets/_Project/Scripts/Presentation/SpineAttackUnitPool.cs`

추가됨:

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`
- `Assets/_Project/Scripts/Presentation/SpineUnitPool.cs`

`SpineUnitView` 기능:

- idle loop
- attack animation + idle 복귀
- deploy animation
- death animation
- 좌우 facing
- cast anchor resolve
- sorting order update

`SpineUnitPool` 기능:

- entity별 Spine view 관리
- `TrySpawn`
- `NotifyAttack`
- `NotifyDeath`
- `TryResolveAnchor`
- `Despawn`, `DespawnMissing`, `DisposeAll`

## 생성/수정된 에셋

### 신규 적 텍스처

- `Assets/_Project/Generated/Enemies/Textures/Enemy_Rootcaster_Generated.png`
- `Assets/_Project/Generated/Enemies/Textures/Enemy_Needler_Generated.png`
- `Assets/_Project/Generated/Enemies/Textures/Enemy_Runner_Generated.png`

원본 chroma-key 소스:

- `Assets/_Project/Generated/Enemies/ConceptSources/Enemy_Rootcaster_Source.png`
- `Assets/_Project/Generated/Enemies/ConceptSources/Enemy_Needler_Source.png`
- `Assets/_Project/Generated/Enemies/ConceptSources/Enemy_Runner_Source.png`

### 신규 적 데이터

- `Assets/_Project/Scripts/Data/Units/Enemy_Rootcaster.asset`
  - `health: 45`
  - `moveSpeed: 1.8`
  - `attackDamage: 14`
  - `attackRange: 5.5`
  - `attackCooldown: 2.2`
  - `projectile: Projectile_Enemy_RitualBolt`
  - `movePauseOnAttackSec: 1`

- `Assets/_Project/Scripts/Data/Units/Enemy_Needler.asset`
  - `health: 35`
  - `moveSpeed: 2.8`
  - `attackDamage: 3`
  - `attackRange: 4.2`
  - `attackCooldown: 0.35`
  - `projectile: Projectile_Enemy_Needle`
  - `movePauseOnAttackSec: 0`

- `Assets/_Project/Scripts/Data/Units/Enemy_Runner.asset`
  - `health: 20`
  - `moveSpeed: 7.2`
  - `attackDamage: 0`
  - 공격 없음.

### 신규 적 머티리얼

- `Assets/_Project/Scripts/Data/Units/Enemy_Rootcaster_Mat.mat`
- `Assets/_Project/Scripts/Data/Units/Enemy_Needler_Mat.mat`
- `Assets/_Project/Scripts/Data/Units/Enemy_Runner_Mat.mat`

### 적 투사체 데이터

- `Assets/_Project/Data/Projectiles/Projectile_Enemy_RitualBolt.asset`
  - `Projectile_Bolt` 프리팹 참조.
  - `speed: 13`
  - `visualScale: 2.7`
  - crimson tint.

- `Assets/_Project/Data/Projectiles/Projectile_Enemy_Needle.asset`
  - `Projectile_Arrow` 프리팹 참조.
  - `speed: 18`
  - `visualScale: 1.8`
  - cyan tint.

### BellKnight Spine 에셋

원본 `.skel/.atlas`는 Unity에서 `TextAsset`으로 바로 로드되지 않아 Spine 런타임용 복사본을 만들었다.

- `Assets/_Project/Characters/BellKnight.skel.bytes`
- `Assets/_Project/Characters/BellKnight.atlas.txt`
- `Assets/_Project/Characters/BellKnight_Material.mat`
- `Assets/_Project/Characters/BellKnight_Atlas.asset`
- `Assets/_Project/Characters/BellKnight_SkeletonData.asset`

`BellKnight_SkeletonData.asset` 확인 결과:

- animations:
  - `attack-1`
  - `attack-2`
  - `attack-2-recover`
  - `attack-3-end`
  - `attack-3-loop`
  - `attack-3-start`
  - `attack-4`
  - `attack-4-end`
  - `attack-4-loop`
  - `attack-4-start`
  - `idle`
  - `walk`
- skin:
  - `default`

`Enemy_Tanker.asset` Spine 연결:

- `skeletonDataAsset: BellKnight_SkeletonData`
- `spineSkinName: default`
- `idleAnimation: walk`
- `attackAnimation: attack-1`
- `deathAnimation: ""`
- `spineVisualScale: 1.05`

## Wave 연결

- `Assets/_Project/Scripts/Data/Decks/WaveA.asset`
  - generated `attackUnitPool`에 기존 3종 + 신규 3종 포함:
    - `Enemy_Basic`
    - `Enemy_Swift`
    - `Enemy_Tanker`
    - `Enemy_Rootcaster`
    - `Enemy_Needler`
    - `Enemy_Runner`

## Scene 변경

- `Assets/_Project/Scenes/BattleScene.unity`
  - 기존 `SpineDefenderPool` missing script 오브젝트를 `SpineUnitPool`로 교체.
  - `BattleBridge.spineUnitPool`에 해당 씬 오브젝트 할당.

주의:

- 씬에는 아직 `DraftView` missing script 1개가 남아 있다.
- 이번 작업과 무관한 기존 잔여 상태라 건드리지 않았다.

## 검증 결과

수행한 검증:

- Unity compile: 성공.
- `BellKnight_SkeletonDataAsset` 로드 확인.
- `SkeletonAnimation` 초기화 확인.
- `walk` animation 존재 확인.
- EditMode 전체 테스트:
  - `170/170` 통과.
- 최종 Unity console:
  - error/warning 0개.

테스트 실행 중 일시적으로 나온 로그:

- `Executing IPrebuildSetup...`
- `Executing IPostBuildCleanup...`
- `Saving results to...`
- `[BattleMapBuilder] BuildFromManual spawn[0] int2(9, 2) outside gridSize int2(5, 5).`

위 로그는 기존 테스트 흐름에서 발생했고, 최종 콘솔은 clear 후 0개 확인.

## 남은 작업 / 리스크

1. PlayMode에서 실제 웨이브 스폰 확인 필요.
   - `Rootcaster`: 공격 시 1초 정지 후 쿨타임 동안 이동하는지.
   - `Needler`: 이동하면서 빠르게 투사체를 쏘는지.
   - `Runner`: 너무 빠르거나 불쾌하지 않은지.
   - `Tanker`: BellKnight Spine 크기/정렬/정렬 순서가 보드 위에서 자연스러운지.

2. 적 projectile VFX/타격감 조정 필요.
   - 현재는 방어 유닛 projectile prefab을 재사용하고 tint/scale만 조정했다.
   - 실제 플레이에서 적/방어 투사체 구분이 약하면 prefab 또는 material variant를 별도로 분리할 것.

3. `WavePatternGenerator`는 unit weight를 지원하지 않는다.
   - 현재 신규 3종은 기존 3종과 동일 확률로 선택된다.
   - `Runner`/`Needler`가 과도하게 나오면 `AttackDeck.attackUnitPool` 반복 참조 또는 후속 weight 필드가 필요하다.

4. 적 Spine attack animation 트리거는 아직 연결하지 않았다.
   - 공통 `SpineUnitPool.NotifyAttack`은 존재하지만, 현재 `DefenderAttackEvent`만 drain한다.
   - 탱커가 공격 애니메이션을 명확히 보여야 하면 enemy attack event channel을 추가하거나 AttackSystem에서 공통 attack visual event를 내도록 확장해야 한다.

5. 기존 `Enemy_Basic_Mat`, `Enemy_Swift_Mat`, `Enemy_Tanker_Mat`는 앞선 이미지 개선 작업으로 수정된 상태다.
   - 본 handoff 범위에는 포함되지만, 이전 변경과 섞여 있으니 revert 금지.

## 다음 권장 순서

1. PlayMode에서 `WaveA` 시작 후 신규 적 3종과 탱커 Spine 스폰 확인.
2. 탱커 크기 조정:
   - `Enemy_Tanker.asset.spineVisualScale`
   - 필요 시 `spineDefenderYOffset`과 별도 enemy y offset 분리 검토.
3. Needler projectile 주기/데미지 조정.
4. Rootcaster pause가 게임적으로 명확한지 확인.
5. Enemy attack animation event channel 설계.
   - 가능하면 `DefenderAttackEvent`를 `UnitAttackVisualEvent`로 일반화.
   - 방어/공격 모두 `SpineUnitPool.NotifyAttack(entity, targetWorld)`로 연결.

## 건드리지 말 것

- `SpineUnitView` / `SpineUnitPool`를 다시 defender/enemy 전용으로 분리하지 말 것.
- `AttackUnitData.projectile` 경로는 방어 유닛의 `DefenderUnitData.projectile`과 같은 `ProjectileRef` 인프라를 공유한다.
- `EnemyAttackMovePause`는 공격형 적의 이동 제어만 담당한다. 일반 CC와 섞지 말 것.
- `DraftView` missing script는 이번 작업 범위가 아니다.
