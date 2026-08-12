# unit 10 — 유닛별 애니메이션 구조 (idle 변형 + 조건 루프 + 전이 원샷)

## 목적

요구사항 2 를 닫으면서, **특정 유닛이 자기만의 애니메이션 구조를 갖는 통로**를 연다. unit 8 이 "유닛마다 다른 스켈레톤"을 열었다면 이 unit 은 "유닛마다 다른 **재생 규칙**"을 연다.

지금 `SpineUnitView` 가 아는 상태는 **이동/정지 2개**뿐이고, 상태당 애니 이름도 **1개**뿐이다(`idle`/`walk`/`attack`/`death`). CH1(소환사)은 `idle`·`idle2`·`idle3`·`attack1`·`attack2`·`attack3`·`drop` 을 들고 왔는데 현재 구조로는 `idle` 과 `attack` 하나씩만 닿는다.

목표 동작(사용자 2026-08-12):

| # | 요구 | 이 unit 이 만드는 원시 동작 |
|---|---|---|
| 2-1 | idle 상태에서 `idle`/`idle2`/`idle3` 랜덤 재생 | **idle 변형 풀** |
| 2-2 | 적 감지 → `attack1` 원샷 + 소환물 스폰 | **없음 — 이미 된다** (`AttackState` 발화 → `PlayAttack`) |
| 2-2 | 소환물 생존 중 `attack2` 루프 | **조건부 루프 오버라이드** |
| 2-2 | 소환물 사망 시 `attack3` | **오버라이드 해제 시 원샷** |

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 공개 API 2개 + idle 변형 선택
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `idleVariants` (맨 뒤에 덧붙임)
- `Assets/_Project/Scripts/Data/Abilities/SummonPatrolAbility.cs` — `activeAnimation` · `lostAnimation`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 디펜더 뷰 프레임 루프(`:3074` 구간)에서 `SummonerState.current` 유효성 → 뷰 신호
- 신규 `Assets/_Project/Tests/EditMode/UnitAnimationChoiceTests.cs`
- `Defender_Summoner.asset` · `Ability_SummonPatrol_Summoner.asset` — 이름 저작

## 계약

1. **뷰는 규칙을 소유하지 않는다.** "소환물이 살아 있나"는 sim 사실(`SummonerState.current` + 계약 9 의 생존 술어)이고 ECS 를 읽는 건 `BattleBridge` 단독이다(절대 제약 1). 뷰가 받는 것은 **애니 이름 2개**뿐이고 `SummonerState` 라는 타입을 모른다.

2. **뷰 공개 API 는 2개.**
   - `SetLoopOverride(string loopAnim, string onClearOneShot)` — 로코모션 idle 자리를 `loopAnim` 으로 대체. **이미 그 루프가 track0 에서 돌고 있을 때만** 조기 반환한다. 그 외에는 매 프레임 재시도하고, `RefreshLocomotionIfLooping` 이 이름이 같으면 아무것도 하지 않으므로 애니는 재시작되지 않는다.
     - ⚠ **"값이 같으면 반환"으로 만들지 말 것.** 요청이 원샷(소환 애니) 도중에 오면 계약 4 때문에 그 프레임엔 적용할 수 없는데, 값만 보고 반환하면 저장만 되고 **영영 적용되지 않는다**. 그러면 소환사가 능력 루프에 못 들어가고 쿨다운마다 소환 애니만 반복한다 — 2026-08-12 사용자 제보로 드러난 실제 버그이고, 프레임 로그로 원인을 확정한 뒤 재시도 방식으로 고쳤다. 재현이 간헐적이었던 이유는 «브리지가 생존을 감지한 프레임이 원샷 구간과 겹치느냐»에 달렸기 때문이다.
     - 회귀 방지는 `PatrolDefenderPlayTest` 가 소환물 생존 중 소환사의 `CurrentAnimationName` 이 능력의 `activeAnimation` 이 되는지로 단언한다.
   - `ClearLoopOverride()` — 오버라이드 해제. **직전에 오버라이드가 걸려 있었을 때만** `onClearOneShot` 을 원샷으로 낸다. 이 엣지 조건이 "소환한 적 없는 소환사가 판 시작에 `attack3` 를 내는" 사고를 막는다.
   - 브리지는 매 프레임 상태를 그대로 밀어 넣고, 엣지 판정은 뷰가 한다 → 브리지에 "직전 프레임" 딕셔너리를 만들지 않는다.

3. **오버라이드는 로코모션 루프의 idle 자리만 바꾼다.** `ResolveLocomotionAnimation`(`:758`)의 walk 우선 → idle 폴백 체인 구조를 유지하고, 오버라이드가 있으면 **idle 후보의 맨 앞**에 들어간다. 소환사는 `walkAnimation` 이 비어 있어 실질적으로 항상 오버라이드 트랙이다.

4. **원샷 중에는 건드리지 않는다.** `UpdateLocomotionAnimation`(`:768`)이 이미 `!current.Loop` 면 조기 반환하는 규칙을 그대로 쓴다 — 소환 순간의 `drop`(attack) 이 잘리면 안 된다. 오버라이드 진입도 같은 게이트를 지난다.

5. **idle 변형 재추첨은 루프 끝에서 한다.** `TrackEntry.Complete` 는 looping 엔트리에서도 **사이클마다** 발화한다(`AnimationState.cs:551` — `cycles > trackLast/duration`). 그 콜백에서 다음 변형을 뽑아 `SetAnimation(..., loop: true)` 한다. **`loop:false` 로 이어붙이지 않는다** — `IsLocomotionLoopPlaying`(`:132`)과 계약 4 가 둘 다 `Loop` 를 "로코모션이냐 원샷이냐"의 판정 기준으로 쓰고 있어서, 루프 idle 을 원샷으로 만들면 걷기 배율과 오버라이드 게이트가 동시에 오작동한다.

6. **이 난수는 sim 에 닿지 않는다.** 변형 선택은 순수 프레젠테이션이고 `UnityEngine.Random` 을 쓴다. 맵/웨이브 결정론(`waveSeed`)과 **같은 난수원을 공유하지 않는다** — 뷰가 심 난수를 소비하면 같은 시드가 프레임률에 따라 갈린다.

7. **미설정이면 무동작.** `idleVariants` 가 비면 현행 단일 `idleAnimation`, 오버라이드 이름이 비면 오버라이드 없음. 나머지 **42유닛**(unit 8 이후 파츠형 잔존 실측)은 전부 빈 값이라 **애니 거동 변화 0** 이다 — `walkAnimation` 이 쓴 게이트와 같은 형태.

8. **애니 이름의 소유자는 "그 상태를 아는 쪽"이다.** `idleVariants` 는 어떤 유닛이든 가질 수 있으므로 **공용 `ISpineUnitVisualData`** 에 둔다(구현 중 변경 — 초안은 `DefenderUnitData` 였다).
   - 바꾼 이유: 디펜더 전용 `IDefenderSpineExtras` 에 넣어 적을 배제하는 방식은 **무기 궤적에서 한 번 막다른 길이었다**. 그 인터페이스 주석이 직접 기록한다 — *"적 제외가 코드 분기 없이 성립"을 노려 여기 뒀지만, 그 이점이 보스/구조물을 넣을 길을 막는 제약이 됐다.* 같은 함정을 반복하지 않는다.
   - 대가: 인터페이스가 11 → 12 멤버가 됐다(README 계약 2 가 경고한 그 축). `AttackUnitData` 는 저작 슬롯만 열고 값을 채운 적 에셋은 없다 — 코드 리뷰가 «미사용 슬롯 + 스펙이 범위 밖이라 한 확장»으로 지적했고, **의도된 트레이드오프로 수용**했다. 다음 확장 때는 계약 2 의 경고를 다시 읽을 것. `activeAnimation`/`lostAnimation` 은 **소환 능력이 만든 상태**의 이름이므로 `SummonPatrolAbility` 가 갖는다. `ISpineUnitVisualData` 에 넣지 않는다 — 계약 2 가 "멤버 11개이고 네 번 커졌다"고 경고한 그 인터페이스에, 한 유닛만 쓰는 필드를 얹지 않기 위함이다. 다른 능력이 자기 상태 애니를 원하면 그 능력 SO 에 같은 모양으로 얹는다. **이것이 "확장을 연다"의 실체다.**

## 구현 메모

- 브리지 훅 위치는 디펜더 뷰 프레임 루프(`BattleBridge.cs:3074` 구간). 바인딩이 `data`(DefenderUnitData)를 들고 있으므로 `data.abilities` 에서 `SummonPatrolAbility` 를 찾아 이름 2개를 읽는다 — **새 레지스트리도 새 이벤트 채널도 만들지 않는다.** 순서는 `HasComponent<SummonerState>` 게이트 **먼저**, 통과한 것만 능력 목록을 훑는다(디펜더 전원에 대해 매 프레임 `abilities` 를 도는 낭비 방지).
- **생존 판정은 3중이어야 한다** — `Exists && !DeadTag && HP>0` (계약 9). **공유 헬퍼는 없다**(리뷰 확인): `PatrolLifecycleSystem` 은 ECS 쪽에서 Burst 로 직접 쓰고, `AggroStateSystem` 의 링크 가디언 판정이 그 원형이다. 브리지 쪽에 **명시적으로 새로 쓴다.**
  - ⚠ `BattleBridge.Relocation.cs:130` 의 기존 검사(`Entity.Null || !Exists`)를 복사하지 말 것 — **2중이다.** DeadTag 가 붙고 실제 파괴되기까지의 프레임 동안 순찰병이 `Exists` 로 살아 보여서 `attack3` 가 늦게 나간다. 재배치는 그 지연이 무해했지만 연출 전이는 아니다.
- 변형 선택은 **순수 함수**로 뺀다(제약 10 판정 기준 (c) — 회귀 테스트 가치): `ChooseNext(count, currentIndex, roll)` — 직전과 같은 것을 연속으로 뽑지 않는다(변형이 2개 이상일 때). 아키텍처 타입을 안 쓰므로 EditMode 대상.
- 저작 목표값: `idleVariants = [idle, idle2, idle3]` · `activeAnimation = attack2` · `lostAnimation = attack3` · `attackAnimation` 은 unit 8 이 넣은 `drop` 유지(= 소환 동작).

## 완료 기준

- [ ] 소환사가 대기 중 `idle`/`idle2`/`idle3` 를 랜덤으로 이어 재생한다 (같은 것이 연속으로 두 번 나오지 않는다)
- [ ] 소환 순간 `drop` 이 잘리지 않고 끝까지 재생된 뒤 `attack2` 루프로 들어간다
- [ ] 순찰병 생존 중 `attack2` 가 유지된다
- [ ] 순찰병이 죽으면 `attack3` 가 **한 번** 나가고 idle 변형 루프로 복귀한다
- [ ] 재소환하면 다시 `attack2` 로 들어간다 (순환이 반복 가능하다)
- [ ] 판 시작 직후(소환한 적 없음)에 `attack3` 가 나가지 않는다 (계약 2 의 엣지 조건)
- [ ] 소환사가 죽어도 콘솔 에러 0 (`TrackEntry.Complete` 구독 해제)
- [ ] **파츠형 42유닛 무회귀** — 필드 미설정 유닛의 애니 거동 변화 0
- [ ] 순찰병 사망 프레임에 `attack3` 가 나간다 — `Exists` 만 보는 2중 판정이면 늦는다(구현 메모 ⚠)
- [ ] 슬로우모/정지에서 오버라이드 루프가 `timeScale` 을 따른다 (`ApplyTimeScale` 경유, 별도 시계 신설 금지)
- [ ] EditMode: `ChooseNext` 단위 테스트 (변형 1개 · 2개 · 3개 · 연속 회피)
- [ ] EditMode/PlayMode 기존 테스트 무회귀

## 범위 밖

- **`attack1` 을 소환 전용 원샷으로 따로 두기.** 현재 `attackAnimation` 하나가 소환 동작을 겸한다(`drop`). 소환사가 소환 외의 공격을 갖게 되면 그때 나눈다.
- **적/보스로의 확대.** `AttackUnitData` 에도 같은 필드를 열 수 있으나 요구가 없다(제약 8).
- **일반 트리거 시스템.** 조건→애니를 데이터로 기술하는 범용 기구는 만들지 않는다. 조건은 코드가 알고(브리지), 이름만 데이터가 준다.
