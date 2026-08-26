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


---

## 진행 (2026-08-26)

| 조각 | 내용 | 상태 |
|---|---|---|
| **5a** | **캐스트 seam 개통** + 하자드 4에셋(얼음·불·독·길막) | 완료 |
| **5b** | 실드 셔틀 1에셋 | 미결 — 아래 |
| **5c** | 볼리 2에셋 | **범위 밖 확정** (2026-08-26 사용자 결정) |
| **5d** | 폭탄 1에셋 | |

## ⚠ 이 문서의 전제 하나가 stale 이었다

「**그물이 없다** — 캐스트 8에셋은 특성화 테스트가 확인되지 않았다」는 거짓이다. 넷 다 있다:

| 가족 | 그물 |
|---|---|
| 하자드 | `HazardCasterTests` (EditMode 8) |
| 실드 | `AbilityAreaShieldTest` |
| 볼리 | `DirectionalVolleyIntegrationTests`(8) · `VolleyMachineGunnerTest`(2) |
| 폭탄 | `AttackSystemUnifiedLoopTests` 의 BombThrower 3종 |

unit 2 의 「3/9 nets」와 같은 부류다 — **spec 의 재고는 세는 시점의 사진**이고, 착수 전에
다시 세는 것이 규칙이어야 한다.

## 5a 에서 나온 것

**일곱 번째 seam 이 필요했고, 이유는 순서 계약이다.** 캐스트 성사 = 그 host 의 공격
사건이고 `AttackSystem` 이 **같은 프레임**에 소비해야 한다(`HazardCastSystem` 이
`[UpdateBefore(AttackSystem)]` 을 명시한 이유). 주기 seam 은 `AttackSystem` 과 순서 계약이
없어서 거기로 옮기면 정렬기 tie-break 에 따라 한 프레임 밀린다.
⚠ **주기 seam 에 그 제약을 거는 것도 안 된다** — 모든 주기 스킬의 순서가 같이 움직이고
emitter 를 뒤로 미는 전이 간선이 생긴다(ECS 리뷰 H-1 이 같은 이유로 경계 seam 의 emitter
제약을 뺐다). seam 하나가 그 전부보다 싸다.

**「언제·어디에」는 감지자에 남겼다.** 대상 선정이 캐스터의 공격 사양(사거리·대상 마스크·
통행 층·동률 축 = 낮은 `SimEntityId`)과 얽혀 있고 그 값들은 캐스트 상태가 갖고 있다.
스킬이 다시 고르면 그 사양을 복제한다. 깔린 다음의 일은 전부 해저드 저작 소유이므로
concrete 가 하는 일은 **「저 칸에 이 에셋을」** 하나이고, 그래서 4에셋이 concrete 하나를 쓴다.

⚠ **종류(장판/길막)는 접으면 안 된다** — 둘이 **다른 등록부**에서 에셋을 찾는다.
어댑터가 `Selector` 로 가르고, 0(`None`)은 존으로 읽는다(죽음 자리 장판이 그 경우다).

**그물이 초록인데 스킬 레이어를 안 지나가고 있었다.** `skillId` 기본값이 0(legacy)이라
하네스가 여전히 arm 을 탔다. 디스패처를 끼우니 세 가지가 드러났다:
- 테스트 적 더미에 **`AttackUnitTag` 가 없어** 어댑터 후보 풀 밖이었다(라이브 악몽은 갖는다).
- 캐스터·대상에 **`SimEntityId` 가 없었다**(이 spec 에서 네 번째다).
- `HazardSpawnRequest.width/height` 가 죽음 자리 경로에서 **0** 이었다. 드레인이 안 읽는
  필드지만 「한 칸에」의 정직한 인코딩은 1 이라 어댑터에서 채운다.

## 5b·5c 미결 — **축을 세고 나서 합친다**

unit 4d 에서 오라가 정확히 이 실수로 두 unit 을 지나갔다(축 넷이 같아 보여 합쳤는데
다섯 번째가 숨어 있었다). 그래서 여기서는 세어 두고 멈춘다.

**실드 셔틀 → `GrantShieldSkill`** 은 축 **셋**이 다르다:
| 축 | `GrantShieldSkill`(악몽의 가호) | `ShieldCastSystem`(셔틀) |
|---|---|---|
| 자기 포함 | **제외가 계약**(병합 키 붕괴 방지) | **포함**(계약 6) |
| 대상 수 | 반경 내 전부 | `targetCount` 명 |
| 우선순위 | 없음 | `ShieldTargeting.Select`(실효 HP 낮은 순 등) |

셋 다 파생 축으로 올리면 합칠 수 있다 — 오라와 같은 처방이다. 다만 「host 제외가 계약」의
근거(같은 host 의 두 능력이 한 슬롯을 공유해 상시 실드로 붕괴)가 **살아 있는 경고**라,
셔틀에 포함을 열 때 그 조합이 저작 가능해지는지 bake 가 막아야 한다.

**실드 셔틀은 위 축 셋을 파생으로 올려 합친다** — 오라와 같은 처방이고 이전 작업이다.

### 볼리 2에셋 — 범위 밖 (2026-08-26 사용자 결정)

> **「단순히 투사체 모음을 트리거하는 건 기본공격이고 스킬일 필요 없음」**

`DirectionalVolleyAbility` 는 「이 유닛이 **어떻게 공격하나**」를 말한다. 트리거가 AttackN
카운터가 아니라 **평타 그 자체**이고, 이미 `PatternSlot` 0 번에 구워져 emitter 가 전개한다.
옮기면 유닛의 공격 모델이 스킬 디스패치 뒤로 가는데, **없어지는 arm 사본이 0**이다.

이 판정은 payload 하나가 아니라 **경계선**이다 — 「발사 명세를 트리거한다」는 것만으로는
스킬이 아니다. 스킬이 되는 것은 그 위에 **조건**(N회째·처치 시·경계 돌파)이 얹힐 때다.
그래서 머신거너의 10연발은 평타이고, 같은 패턴을 `AttackN × EmitProjectilePattern` 으로
저작한 카드는 스킬이다(그쪽은 이미 이전됐다).

⚠ 이 문서 상단의 「캐스트 계열 8에셋」 재고에서 볼리 2는 빠진다 — 남은 이전 대상은 **6**이다.
