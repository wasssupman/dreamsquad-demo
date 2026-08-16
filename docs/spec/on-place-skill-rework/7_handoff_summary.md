# 7. Handoff — 배치 스킬 재설계 (units 0~6)

## Commit

| unit | 해시 | |
|---|---|---|
| 0 | `02eecb37` | `OnPlace` 트리거 · `UnitSkillAbility` · 진영 중립 bake · `JustDeployed` |
| 1 | `94198452` | 패턴 `scopeTileRange` + `fanOutToAllCandidates` · `PatternScope` |
| 2 | `819fa9ec` | 캐논 1:1 융단폭격 — **에셋만(C# 0줄)** |
| 3 | `6aeeb898` | 시한 어그로(도발) · `AggroHitEvent` → `AggroAcquireEvent` |
| 4 | `8738da0e` | 배스티온 집단 도발 · `AreaTaunt` |
| 5 | `363193a4` | 말파이트 반경 2 · 3초 정지 · 띄움 길이 분리 |
| 6 | `fb387632` | 문안 + handoff |
| 리뷰 | `020f301d` | 투트랙 코드리뷰 반영 — 아래 「리뷰가 잡은 것」 |
| 연타 | (이 커밋) | 캐논 낙하 시차 `fanOutStaggerSec`(사용자 요청) |

⚠ unit 0 의 `BattleBridge.cs` 변경분(`BakeUnitMechanics` 본문 · `MarkJustDeployedForRules` ·
방어유닛 bake 호출)은 **병행 세션이 같은 파일을 통째로 스테이징하면서 `2b6362e9` 에 딸려
들어갔다.** 코드는 동일하고 히스토리는 재작성하지 않았다.

## Implemented

- **배치 스킬이 적/보스와 같은 배관 위에 앉았다.** `DcTriggerKind.OnPlace`(9) 신설 +
  `UnitSkillAbility`(방어유닛 자기 규칙의 집). 유닛은 `abilities` 하나만 보고,
  `DefenderUnitData` flat 필드는 하나도 안 늘었다.
- **발화는 태그로.** 브리지는 `JustDeployed` 만 붙이고 슬롯 소비는 `BossPeriodicTriggerSystem`
  이 한다. 배치 확정 지점이 **셋**(D&D · 탭 · 재배치)이라 실행부를 브리지에 두면 하나만 놓쳐도
  그 경로에서만 스킬이 안 나간 채 테스트가 초록이 된다.
- **캐논** — 반경 2 안 **적이 있는 칸마다** 낙하 미사일 1발이 0.08초 간격으로 줄지어 꽂힌다.
  피해 총량은 기존과 동일(적당 80). 바뀐 것은 예고 0.4초와 하늘에서 내려오는 연타 그림.
- **배스티온** — 반경 2 안 적 **전원**을 5초 도발(상한 2 무시, 선점 가져옴). 밀쳐냄 제거.
- **말파이트** — 반경 2 · 3초 정지. 체공은 0.8초로 짧게(스턴 길이와 분리).
- 신규 payload 는 `AreaTaunt` **하나뿐**. 캐논은 기존 `EmitProjectilePattern` 재사용.
- 신규 NativeQueue 채널 **0**(28개 불변) · 신규 ECS 시스템 **0**.

## Key Files

- `Scripts/Data/Dreamcatcher/DcMechanic.cs` — `OnPlace`(9) · `AreaTaunt`(23)
- `Scripts/Data/Abilities/UnitSkillAbility.cs` · `Scripts/Battle/Units/JustDeployed.cs`
- `Scripts/Battle/Combat/DcTrigger.cs` — `DefenderTriggerArmed`(적 목록과 **분리**)
- `Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — `OnPlace` 게이트 + `AreaTaunt` arm
- `Scripts/Battle/Combat/Projectile/Emission/PatternScope.cs` + `ProjectileEmitterSystem.cs`
- `Scripts/Battle/Effects/AggroAcquireEvents.cs` · `Aggroed.cs` · `AggroStateSystem.cs`
- `Scripts/Bridge/BattleBridge.cs` — `BakeUnitMechanics` · `MarkJustDeployedForRules`
- `Data/Abilities/Ability_{SkyStrike_Cannon,Taunt_Bastion}.asset` ·
  `Data/Projectiles/{Projectile_CannonStrike,Pattern_Cannon_Strike}.asset`

## Verified

- EditMode 2443 중 실패 5 = **전부 기존 실패**(맵 폭1 협곡 4 · Whirlpot 스켈레톤 1)
- 신규 EditMode: `DcTriggerArmedTests` 5 · `PatternScopeTests` 9 · `AggroStateSystemTests` +8 ·
  `UnitKitSummaryTests` +2
- 신규 PlayMode: `OnPlaceRuleTriggerTest` 6 · `OnPlaceSkyStrikeTest` 4 ·
  `OnPlaceTauntNearbyTest` 4 · `OnPlaceStunNearbyTest` 3
- 회귀 PlayMode: on-place 4종 · 재배치 · 보스 자장가/실드 · 드림캐쳐 4종 · 투사체 스택 · 평타 넉업

## Notes (되돌리지 말 것)

- **`EnemyTriggerArmed` 는 한 글자도 안 건드렸다.** 그 목록은 보스의 자기진영 타격을 막는
  문이다(`DcTrigger.cs` 주석). 방어유닛 트리거는 `DefenderTriggerArmed` 로 **분리**한다.
- **bake 호출 순서**: `BakeUnitMechanics` 는 `BakeDefenderDirectionalPattern` **뒤**다.
  `AttackSystem` 이 `PatternSlot[0]` 하나만 읽고 index 0 은 호출 순서로만 정해진다 —
  앞으로 옮기면 **머신거너가 배치 스킬 패턴을 쏜다**(EditMode 가 고정).
- **`hostIsEnemy` 를 bake 시그니처에서 빼지 말 것.** `targetFaction` 이 여기서 파생된다 —
  빠뜨리면 **캐논 미사일이 아군을 때린다**.
- **칸 접기는 emitter(셀 바인딩 한정)가 한다.** `PatternScope` 는 반경 필터일 뿐이다.
  셀을 겨누는 낙하탄은 반경 0 이어도 그 칸 전원을 때리므로 **칸당 1발**이어야 «적당 80» 이
  성립한다 — 안 접으면 같은 칸 2기가 각자 160(리뷰가 잡고 실측으로 확정).
- **연타의 낙하 순서는 row-major 셀 rank 로 고정한다.** 청크 순서에 맡기면 「누가 먼저 맞나」가
  프레임마다 달라지고, 시차가 있으면 그게 **결과를 바꾼다**(늦게 맞는 적은 피할 시간이 더 있다).
- **scope 필터의 반환은 항상 원본 풀 index.** 잠금 경로가 원본 index 를 만들어 쓰므로
  두 index 공간이 섞이면 엉뚱한 칸을 때린다.
- **`Aggroed.remainingTime` 의 `0 = 무기한` sentinel.** 기존 픽스처 8곳과 히트 어그로 계약을
  이것이 보호한다.
- **히트/도발 게이트를 한 줄로 합치지 말 것.** 히트가 먼저 dequeue 되면 도발이 조용히 탈락한다.
- **도발 override 는 `AddComponent`.** `SetComponent` 는 미존재 시 playback 예외이고,
  도발의 정상 대상은 「아직 어그로 안 된 적」이다.
- **`FlowFieldRebuildSystem` 은 도발된 적의 `Aggroed` 를 떼지 않는다**(1회성이라 재획득
  경로가 없다). 대신 필드만 떼고 `AggroStateSystem` Pass 4 가 다시 굽는다 — **필드만 떼고
  재굽기를 지우면 적이 얼어붙는다.**

## 실측으로 드러난 것 (다음 사람이 시간 아끼도록)

- **전투 시작 전 배치는 캐논 미사일이 한 발도 안 뜬다.** 브리지의 `DrainProjectileSpawnRequests`
  가 `Update` 의 `if (!_running) return;` 아래라, 트리거·스코프·fan-out 이 다 돌아 **캐리어까지
  만들어지고도**(실측 `maxCarrier=3`) 투사체가 0이다. 「낭비」는 기존 사양대로 두되 **뒤늦게
  터지는 것**은 리뷰 반영으로 막았다 — `StartBattle()` 이 잔류 캐리어를 버린다.
- **테스트 더미는 실제 적의 부품 셋을 갖춰야 한다.** 하나만 빠져도 sim 게이트가 조기 통과하거나
  거절해 제품이 멀쩡해도 결과가 뒤집힌다:
  `PathFollowState` 부재 → 통행 층 0 = 무제한(게이트가 죽어도 초록) ·
  Walk 아닌 칸 → 도달 가능 게이트 거절 ·
  `EnemyAiState` 부재 → `Marching` 으로 떨어져 골로 걸어감.
  적을 놓을 칸은 `FlowFieldSingleton.walkMask` 로 실제 Walk 칸을 찾는다.

## 리뷰가 잡은 것 (투트랙 코드리뷰 2026-08-16, 양측 REQUEST CHANGES)

두 리뷰어가 **독립적으로 같은 최상위 결함**을 지목했고 실측으로 확정했다.

| 지적 | 조치 |
|---|---|
| **같은 칸 적이 각자 N배 피해**(실측 160) — spec 의 「적당 정확히 80」이 거짓이었다 | 셀 바인딩 fan-out 에서 **칸당 1발**로 접음. 테스트를 `> 0` → **저작값 exact** 로 강화(그 느슨한 단언이 결함을 정확히 비껴갔다) |
| **계약 9 위반** — `AreaTaunt` 후보가 `DeadTag`·`UltimateLeapState` 를 안 뺐다(README 가 예언해 둔 자리) | arm 에서 lookup 으로 제외 |
| **캐논이 하늘의 적을 폭격** — 레거시 `MeleeBurst` 는 걸렀는데 규칙 경로로 옮기며 층 게이트를 잃었다 | `BuildPatternTemplate` 이 `targetTraversalLayers` 를 싣는다. `CanTarget` 은 양쪽 중 하나가 0 이면 통과라 **보스 융단폭격 무영향** |
| **잔류 캐리어 지연 폭발** | `StartBattle()` 이 캐리어를 버린다 |
| **fan-out + scope 0 무경고** = 맵 전체 동시 발사 | bake loud 거절 |
| 문안 루프가 첫 미배선 payload 에서 조기 반환 | `return` → `continue` |
| PlayMode 어셈블리의 `[Test]` 가 테스트 순서 의존 | EditMode 로 이관(`MalphiteKnockupAuthoringTests`) |
| 문서 거짓 서술(캐스케이드 0.06초 · selection 사용 · reselect 폴백 · dedupe 근거 등) | 정정 |

**옳다고 확인된 것**: 맥락 경계 · ECB 사용 · fan-out index 환원(전 경로) · 시스템 순서 무순환 ·
teardown · 계약 ②③④⑤⑩ · 제약 6·10 · GUID 배선 4건 · 테스트가 tautology 아님.

**미조치(후속)**: Pass 3/4 chase 굽기 40줄 중복 · `AggroChaseCell` 버퍼 복사(그리드 전체 × 적 수) ·
`slots[0]` 을 순서가 아닌 불변식으로 · `BossPeriodicTriggerSystem` 의 방어유닛 스냅샷 lazy 화 ·
도발이 이전 가디언 `runningHeld` 미차감(1틱) · Pass 4/장애물 분기 테스트 부재 · 손YAML 정규화.

## Follow-up

- **사용자 육안 Play 확인 미완** — 6_text_and_validation.md 의 검증 질문 ② 및 밸런스 시나리오
  (배스티온이 도발 중 죽으면 뭉친 적이 그대로 진격 / 세 유닛 반경이 전부 2 라 겹치는 콤보).
- **시트 `desc` 3줄 갱신 미완** — `desc` 는 시트가 정본이라 코드/에셋에서 못 고친다.
  문안 초안은 6_text_and_validation.md 표 참조.
- README 「후속 후보」 참조. 우선순위: 레거시 배치 효과 전량 이관(계약 2 의 **만료 조건**) →
  배치 페이즈 발동 정책(위 실측 포함) → 전방 관통 4종 재설계.
