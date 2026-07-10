# 1 — AttackSystem · MovementSystem action-lock 게이트

## 목적
Sleep/Stun 보유 시 공격 START 와 flow 이동을 정지. (Stun 이 이 단위에서 실제로 유효해짐.)

## 변경 대상
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## 구현
**읽기전용 CcEffect lookup** 추가(Combat/Movement 가 Effects 를 읽기만 — 계약 1).

AttackSystem: START 조건에 `!locked` 추가. (cooldown 틱은 유지, START 만 차단.)
```csharp
var ccLookup = SystemAPI.GetBufferLookup<CcEffect>(isReadOnly: true); // OnUpdate
...
bool locked = ccLookup.HasBuffer(attackerEntity) && CcActionLock.IsLocked(ccLookup[attackerEntity]);
// START 분기: else if (!locked && bestTarget != Entity.Null && cooldownRemaining <= 0f) { ... }
// (진행 중 hitDelay RESOLVE 는 그대로 — 이미 시작된 타격은 완료)
```
MovementSystem (critic MED1 — **lock 을 조기 계산 + 자기주도 이동 전 분기 게이트**):
`locked` 을 flow-step 자리(175)가 아니라 **`AiState ai` 읽은 직후(line 64)** 계산해야 한다. 여러 로코모션 분기가
flow-step 전에 `continue` 하기 때문(Chasing 85 / goal 108 / tornado 130). 그렇지 않으면 aggro-chase 적이
잠들어도 계속 걸어감(테스트 5 는 Marching 이라 우연히 통과 → 숨은 구멍).
```csharp
bool locked = ccLookup.HasBuffer(entity) && CcActionLock.IsLocked(ccLookup[entity]); // line 64 직후
```
게이트 원칙 = **자기주도 이동만 정지, 외력은 유지**:
- **정지 대상(자기주도)**: Chasing self-walk(68-86, `locked` 면 위치 write 82-83 스킵 — 제자리) · Engaging
  Advance/Pulse(134+, `locked` 면 Halt 취급) · 일반 flow-step(175-190, `flowStep=0` + LateralRecenter 스킵).
- **유지 대상(외력)**: Portal 텔레포트(88-101) · Tornado pull(111-130) · Impulse 넉백(flow-step 경로의 impulse 합산).
  → 잠/스턴 중에도 밀리고 빨려가고 포탈 탄다.
- Chasing 분기는 현재도 impulse/tornado 미적용(override 후 continue)이라, 잠긴 chasing 은 "제자리"만 — 회귀 없음.

## 완료 기준
- [ ] 컴파일 클린. Burst 유지(순수 헬퍼·lookup만).
- [ ] Sleep 또는 Stun 보유 유닛: 공격 START 안 함 + flow 이동 정지, impulse(넉백)는 적용.
- [ ] 미보유 유닛 회귀 없음.
