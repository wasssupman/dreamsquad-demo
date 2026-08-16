# 3 — 도발 토대: 시한 어그로

## 목적

"N초간 도발"을 sim 에 만든다. 지금 어그로(`Aggroed`)는 **무기한**이고 해제 조건이 「가디언 사망」
하나뿐이며, 획득은 **가디언의 공격 명중**으로만 일어나 capacity 상한과 선점에 막힌다.
도발은 그중 **상한·선점·트리거**만 우회하는 어그로다.

`aggro-targeting` 후속 후보 「도발(에픽 가디언) — aggroCount 무한 해제 + 범위 일괄 어그로 +
최근 우선 중첩」의 첫 소비자다.

이 unit 만으로는 아무 도발도 일어나지 않는다(생산자는 unit 4).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Aggroed.cs` — `remainingTime` 필드 + 헤더 갱신
- `Assets/_Project/Scripts/Battle/Effects/AggroHitEvents.cs` — `kind`·`durationSec` + **파일/타입 rename**
- `Assets/_Project/Scripts/Battle/Effects/AggroStateSystem.cs` — 만료 해제 + 도발 arm
- `Assets/_Project/Scripts/Battle/Effects/FlowFieldRebuildSystem.cs` — 도발 분기
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — enqueue 지점 `kind` 명시
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐 타입명만 (lifecycle 3점 세트 **무증설**)
- `CLAUDE.md` — 채널 목록 이름 갱신
- `Assets/_Project/Tests/EditMode/AggroStateSystemTests.cs`

## 구현

### 채널은 늘리지 않는다 — 기존 것을 정직하게 만든다

29번째 큐를 만들지 않는다. 이벤트 **모양이 같기 때문**이다: 도발도 히트도 「이 가디언이 이 적을
어그로한다」는 (guardian, enemy) 쌍이다.

```
enum AggroAcquireKind : byte { Hit, Taunt }

struct AggroAcquireEvent          // 구 AggroHitEvent
{
    public Entity guardian;
    public Entity enemy;
    public AggroAcquireKind kind;
    public float durationSec;     // Taunt 전용. Hit 은 무시(무기한)
}
```

rename 실측 범위: Assets 10파일 23줄 + 주석 인용 4곳 + `CLAUDE.md` 1줄(2회).
**파일명 `AggroHitEvents.cs` 도 같이 바꾼다.** `durationSec > 0` 매직 플래그를 안 쓰는 이유:
우회 대상(상한·선점)이 지속시간과 논리적으로 무관하다 — 지속 0인 도발도 표현 가능해야 한다.

> 덤: `docs/spec/aggro-targeting/11_combat-hit-arm.md` 가 이 파일 경로를 `Battle/Combat/` 으로
> 잘못 적어 뒀다(실제 `Battle/Effects/`). 같이 고친다.

### `Aggroed.remainingTime` (Effects 소유)

```
public struct Aggroed : IComponentData
{
    public Entity guardian;
    public float  remainingTime;   // 0 이하 = 무기한(기존 히트 획득). >0 = 도발 잔여
}
```

별도 `Taunted` 를 두지 않는 이유(README 계약 7): `Aggroed` 소비처가 6곳이라 상위 레이어를
만들면 그 6곳이 전부 "둘 중 어느 쪽이냐"를 물어야 한다.

- ⚠ **`0 = 무기한` sentinel 이 기존 픽스처 8곳을 보호한다** — `AggroAoeWidthTests`(3),
  `GoalTauntGrantTests`(3), `EnemyAiStateSystemTests`(2)가 `new Aggroed { guardian = … }` 로
  만들어 `remainingTime` 을 0 으로 받는다. 이 규약을 뒤집지 말 것.
- ⚠ Pass 1 이 `RefRO<Aggroed>` → `RefRW` 가 된다. 쓰기는 여전히 Effects 단독.
- ⚠ `AggroStateSystem` 헤더의 "`Aggroed`/`AggroCapacity` 는 이 시스템만 쓴다"는 **이미 stale** —
  `FlowFieldRebuildSystem` 도 뗀다(remover 2곳). 타이머가 실리므로 "누가 지울 수 있나"를 다시 적는다.

### `AggroStateSystem` — Pass 1 (해제)

`remainingTime > 0` 이면 `SystemAPI.Time.DeltaTime` 만큼 감소(`BattleSimGroup` =
TimeManager Battle 도메인 시계. `CcDecaySystem` 과 같은 시계라 슬로모에서 CC 와 도발이 갈리지
않는다). 0 이하 → **가디언 생존과 무관하게 해제**, 기존 해제 경로 그대로(`Aggroed` +
`AggroChaseCell` 동시 제거). 가디언 사망 판정을 **먼저** 하므로 도발 중 배스티온이 죽어도 즉시 풀린다.

### `AggroStateSystem` — Pass 3 (획득)

같은 드레인 루프에서 `kind` 로 갈린다. **게이트 코드는 공유**한다(README 계약 8).
`Taunt` 도 통과해야 하는 것: 요청자 `AggroCapacity` 보유 · `enemy` 존재 · `DeadTag` 아님 ·
보스 면역 · `EnemyTargetFilter` · 공격 수단(`ResolveTileRange != NoAttack`) · **도달 가능**.

우회하는 것은 **둘뿐**:

- **capacity 상한** — `AggroPolicy.CanAcquire` 를 건너뛴다. Pass 2 가 매 틱 full recompute 이므로
  `held > max` 가 그대로 표현되고, 그 상태에선 평시 히트 획득이 자연히 막힌다.
  `AggroTargeting`·`AggroPolicy` 둘 다 `held < capacity` 라 타겟팅은 무해(실측 확인).
- **선점** — 이미 다른 가디언에 묶인 적도 가져온다. 만료 시 이전 가디언으로 **복귀하지 않고**
  완전 해제(다음 히트에 재획득). 복귀는 README 후속 후보.

**⚠ 반드시 고칠 것 넷:**

1. **`ecb.AddComponent` 를 쓴다 — `SetComponent` 아니다.** ECB 의 `SetComponent` 는 컴포넌트
   존재를 단언한다(없으면 checks 빌드 예외, 릴리즈 UB). **`AreaTaunt` 의 정상 대상은 «아직
   어그로 안 된 적»** 이므로 예외 상황의 API 를 일반 경로에 쓰면 안 된다. `AddComponent` 는
   add-or-set 이라 신규·override 를 한 경로로 덮는다.
2. **Hit/Taunt 게이트를 분리한다.** 현행은 단일 공유 게이트다:
   `if (claimed.Contains(ev.enemy) || aggroedLookup.HasComponent(ev.enemy)) continue;`
   Hit 이 먼저 dequeue 되어 적을 `claimed` 에 넣으면 **뒤이은 Taunt 가 같은 줄에 걸려 탈락한다.**
   브리지(Mono Update)와 `AttackSystem`(sim)이 같은 큐를 쓰므로 혼재는 예외가 아니라 평상이다.
   → Taunt 는 이 두 조건을 건너뛰고, 대신 **이미 도발 중이면 갱신**(`remainingTime = max(기존, 요청)`
   — CC 갱신 관례. 겹친 배치가 시간을 깎지 않게). 같은 틱 Taunt 중복은 별도 집합으로 막는다.
   `runningHeld` 이중 계상도 함께 막는다.
3. **chase 를 다시 못 구우면 도발을 거절한다.** override 시 `ecb.AddBuffer<AggroChaseCell>` 는
   기존 버퍼를 덮으므로 정상 경로는 stale 이 아니다. 진짜 구멍은 flow field/transform 이 없어
   `attachField` 를 못 세우는 경우 — `Aggroed.guardian` 만 바뀌고 **옛 가디언 기준 필드**가 남아
   적이 엉뚱한 쪽으로 걸어간다. → 그 경우 도발을 **붙이지 않는다.**
4. **`runningHeld` 를 도발도 올리는지 정한다.** 어느 쪽도 방어 가능하지만 테스트가 하나를
   인코딩하므로 계약으로 적는다. **올린다**(같은 틱에 히트가 남은 자리를 다시 세지 않도록).
   ⚠ `AggroPolicy.CanAcquire` 의 3번째 인자는 프로덕션 dead(`false` 고정) — "겸사겸사" 고치지 말 것.

### ⚠ 성능 — 집단 도발은 히트 구동과 부하 규모가 다르다

통과 이벤트마다 `AggroChaseMath.BuildChaseField`(그리드 전체 BFS)를 돌고
`ecb.AddBuffer<AggroChaseCell>` 에 **그리드 셀 수만큼 int** 를 채운다. 히트 구동은 틱당 몇 건이지만
**반경 2 도발은 한 틱에 최대 25기 분량**을 한 가디언에서 밀어 넣는다. 가디언 셀이 같으니 BFS 는
`(적 tileRange, traversalLayers)` 로만 갈리는데 매번 처음부터 굽는다. **Android 실기기 타겟이다.**

→ 드레인 안에서 `(tileRange, layerMask)` 키로 **필드를 재사용**하고, 필요하면 후보 상한을 둔다
(`AoeTargetCap` 선례). 완료 기준에 부하 측정을 넣는다.

### `FlowFieldRebuildSystem` — 도발 분기

장애물 signature 가 바뀌면 `InvalidateChaseFields` 가 **모든** 어그로를 `Aggroed` 째로 뗀다.
논거는 "다음 히트에 재획득"인데 **1회성 배치 도발은 재획득 경로가 없다** — 도발 중 무효화가
일어나면 통째로 풀리고 사용자는 "가끔 안 걸린다"로 겪는다.

현재는 `Obstacle` 생산자가 디버그 메뉴뿐이라 **휴면**이지만, 해저드가 장애물이 되는 순간
라이브가 된다. → `remainingTime > 0` 인 `Aggroed` 는 **떼지 말고 `AggroChaseCell` 만 재구축**한다.

### `AggroPolicy` (정의 계층)

**새 함수를 만들지 않는다.** rev2 초안이 `ShouldReleaseTimed(float)` 를 두려 했는데 호출처 1곳·
본문 한 줄·분기 없음이라 제약 10 의 추출 3조건 어느 것도 미충족이고, 바로 다음 줄에서 "제약 8"을
인용하며 자기모순이었다. **인라인**한다. 기존 `CanAcquire`/`ShouldRelease` 는 그대로 —
히트 획득 규칙은 안 바뀐다.

⚠ `AggroStateSystem` 은 `[BurstCompile]` 이다 — arm 안에서 `Debug.LogWarning` 불가.
저작 실수의 loud warn 은 브리지/bake 쪽에서만 낸다.

## 완료 기준

- [ ] compile 0 error · `grep AggroHitEvent` 잔여 0 (파일명 포함)
- [ ] **NativeQueue 채널 수 불변(28개)**. CLAUDE.md 는 이름만 갱신
- [ ] 이 unit 만으로는 도발이 일어나지 않는다. 기존 어그로 동작 무변경
- [ ] EditMode `AggroStateSystemTests` 추가
  - 도발 → capacity(2) 초과 5마리 전원 `Aggroed`
  - `remainingTime` 경과 → 전원 해제 · `AggroChaseCell` 도 제거
  - 도발 중 가디언 사망 → 즉시 해제
  - **같은 틱에 Hit 이 먼저 온 적에게 Taunt 가 걸린다** (게이트 분리 핀 — rev2 가 정반대로 서술했다)
  - **타 가디언에서 가져온 적의 `AggroChaseCell` 이 새 가디언 기준**이다 → 이동 방향 단언
  - chase 를 못 굽는 상황(flow field 부재) → **도발이 붙지 않는다**
  - 보스 · 유닛 미조준 적 · 공격 수단 없는 적 · 도달 불가 적 → 도발 안 걸림
  - 무기한 어그로(`remainingTime == 0`)는 시간이 지나도 해제되지 않음 (**기존 픽스처 8곳 보호 핀**)
  - 도발 중 장애물 signature 변경 → **도발 유지**(필드만 재구축)
- [ ] **부하**: 반경 2 · 적 15기 도발 1회의 드레인 프레임 시간 측정. 필드 재사용 전/후 비교
- [ ] 기존 EditMode/PlayMode 무회귀 — 특히 히트 획득 상한/선점 테스트
