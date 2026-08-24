# 5 — 캐스트 계열 8에셋

## 목적

「능력마다 전용 ECS 상태 + 전용 시스템」으로 자란 가족을 규칙으로 접는다.
`on-place-skill-rework` 계약 2 가 **가지 않기로 결정한 방향**의 현물이다 —
*"새 스킬마다 시스템이 하나씩 는다."*

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/HazardCastSystem.cs` (하자드 4에셋)
- `Assets/_Project/Scripts/Battle/Effects/ShieldCastSystem.cs` · `ShieldCastState.cs` (실드 1)
- 볼리 2에셋 · `Assets/_Project/Scripts/Battle/Combat/BombLauncherState.cs` (폭탄 1)
- `Assets/_Project/Scripts/Battle/Combat/CastEvents.cs`

## 구현

1. **`CastEventsSingleton` 의 의미를 보존한다.** 이 채널은 「해저드 캐스트 성사 = 그 host 의
   공격 사건」을 나른다 — 캐스터는 `attackRange` 0 이라 RESOLVE 에 못 간다.
   `HazardCastSystem [UpdateBefore(AttackSystem)]` 이 같은 프레임 소비를 보장한다.
   이전 후에도 이 관계가 유지돼야 한다.
2. **진행형 상태는 남긴다**(토대 계약 5). `ShieldCastState` · `BombLauncherState` 는
   컴포넌트+시스템 소유다. 스킬은 **개시와 수치**까지다.
3. **캐스터가 CC 를 안 본다** — `HazardCastSystem`·`ShieldCastSystem` 이 CC 를 무시하는 것은
   `shield-guardian-defender` 계약 7 의 **의도**인데 사용자는 이를 버그로 읽었다(2026-08-11 Play).
   ⚠ **이 spec 에서 고치지 않는다.** 고치면 가디언·해저드 캐스터 **전원**의 동작이 바뀐다.
   별 spec 이며 백로그에 「⚠ 자는 캐스터가 계속 시전한다 — 사용자 판정 대기」로 있다.
   이전은 **동작 무변경**이어야 한다.
4. **그물이 없다** — 캐스트 8에셋은 특성화 테스트가 확인되지 않았다. 가족 선행으로 깐다.

## 완료 기준

- [ ] 8에셋이 concrete + 저작 SO 로 존재하고 전용 arm 이 죽었다
- [ ] `CastEventsSingleton` 의 같은 프레임 소비가 유지된다 (`[UpdateBefore]` 관계 보존)
- [ ] 진행형 상태 컴포넌트/시스템이 남아 있다
- [ ] **자는 캐스터 동작이 바뀌지 않았다** (이 spec 은 그 결함을 고치지 않는다)
- [ ] 그물 초록 + Play 육안
