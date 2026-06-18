# Unit 3 — AttackSystem 거동 소비

## 목적

`targetMode`(FocusUntilDead)와 `aimMode`(정지 게이팅)를 AttackSystem 이 소비. 어그로 override 는 최상위 유지.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`

## 구현

추가 lookup: `behaviorLookup`(RO `EnemyBehavior`), `focusLookup`(RW `FocusTarget`). (healthLookup/deadLookup 는 기존 활용.)

### 타게팅 우선순위 — 블록 순서 명시 (Critic M1)
선정 순서: **① nearest+filter 계산(기존) → ② FocusUntilDead override → ③ Aggroed override(최상위)**.

② FocusUntilDead 블록은 **기존 nearest+filter 계산 직후, aggro override 직전**에 둔다:
```csharp
if (behaviorLookup.HasComponent(attackerEntity)
    && behaviorLookup[attackerEntity].targetMode == EnemyTargetMode.FocusUntilDead
    && focusLookup.HasComponent(attackerEntity))
{
    Entity cur = focusLookup[attackerEntity].current;
    // 유효성: 룩업만 사용(em.Exists 불필요 — 디스폰 시 Health 없음). Critic C2
    bool curValid = cur != Entity.Null
        && healthLookup.HasComponent(cur) && healthLookup[cur].value > 0f
        && !deadLookup.HasComponent(cur);
    if (curValid)
    {
        // 사거리 안일 때만 발사 — fire 경로엔 range 검사가 없으므로 여기서 검사. Critic M2
        int2 cCell = GridMath.WorldToCell(curPos, tileSize, gridSize, origin: ffOrigin);
        int cDist = math.max(math.abs(cCell.x - atkCell.x), math.abs(cCell.y - atkCell.y));
        bestTarget = (cDist <= tileRange) ? cur : Entity.Null; // 밖이면 발사 보류(lock 유지)
        if (bestTarget != Entity.Null) bestTargetPos = curPos;
    }
    else
    {
        // 무효 → ① 에서 이미 계산된 nearest+filter bestTarget 을 새 lock 으로 채택(중복 루프 X)
    }
    focusLookup[attackerEntity] = new FocusTarget { current = curValid ? cur : bestTarget }; // Null 가능
}
```
- `curPos` 는 `aggroTransformLookup`(또는 LocalTransform RO lookup)로 조회.
- lock 은 **죽을 때까지 유지**(사거리 밖이어도). 사거리 밖이면 `bestTarget=Null`(발사 보류), lock 보존.
- 무효 시 새 lock = ①의 nearest 결과(Null 일 수 있음 → 다음 틱 재선정).

### Aggroed override (기존, 최상위)
어그로면 FocusUntilDead 무시하고 가디언 고정(기존 코드). `FocusTarget.current` 는 건드리지 않음 — 해제 후 ② 가 유효성 재검(죽었으면 재선정). (gap 노트)

### AoE × Focus (Critic M3)
outputs melee 경로 `desiredCount` 는 어그로면 1(기존). **FocusUntilDead 는 primary 만 고정**하고 AoE 보조타겟(`attackTargetCount>1`)은 lock 대상 기준으로 기존대로 확장. (현 6종은 전부 1이라 비활성)

### aimMode 정지 게이팅 (발사 후)
```csharp
bool stop = behaviorLookup.HasComponent(attackerEntity)
    ? behaviorLookup[attackerEntity].aimMode == EnemyAimMode.StopToAttack : true;
if (isEnemy && hasMovementPauseQ && stop && movePauseOnAttackSec > 0f) enqueue pause;
```
- MoveAndShoot → 정지 요청 안 함. (StopToAttack 도 movePause==0 이면 정지 안 함 — 기존 동작.)

## 완료 기준

- [x] 컴파일 + Burst 호환(룩업만; em.Exists 미사용).
- [x] FocusUntilDead: 잠금 유지(더 가까운 타겟 등장해도 불변), 사망 시 재선정. (Play 검증)
- [x] MoveAndShoot 정지 안 함 / StopToAttack(movePause>0) 정지. (Play: S→EnemyAttackMovePause, M→없음)
- [x] 어그로 override 가 focus 위(코드 순서: focus → aggro).
- [x] Nearest 적 회귀 없음(EditMode 전체 Unit 4 에서 확인).

> Play 검증: focus lock/유지/재선정 통과, aimMode 정지 게이팅 통과(pause drain 은 다음 틱 — 시스템 순서상 정상). 콘솔 에러 0.

완료: 2026-06-18 / 커밋 해시 `45b0390`
