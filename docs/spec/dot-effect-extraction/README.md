# dot-effect-extraction — 지속 피해를 CC 버퍼에서 떼어낸다

> 상태: **작성 중 (2026-07-29)** — 사용자 승인 대기

## 목표

`CcEffect` 안에 세들어 살던 **DoT(지속 피해)를 자기 버퍼(`DotEffect`)로 분리**한다. 그 결과
지속 피해와 행동 제약이 서로를 모르는 별개 파이프라인이 되고, 중첩 정책도 각자 갖는다.

검증 질문: **"지속 피해와 행동 제약이 서로의 슬롯·병합 규칙을 건드리지 않고 각자 굴러가는가?"**

## 왜 — DoT 는 CC 가 아니다

`CcKind` 5개의 성격이 이미 갈라져 있다:

| 성격 | 항목 | 병합이 어때야 하나 |
|---|---|---|
| 행동 제약 | `Stun` · `Sleep` | **덮어쓰기 + `remainingTime = max` 가 정답.** 이진 효과이고 소비처가 any-match (`CcActionLock` · `DreamCocoonSystem`) |
| 물리 충격 | `Impulse` | `MovementSystem.cs:210` 이 슬롯을 **합산** |
| **지속 피해** | `DoT` | **출처별 공존이 필요.** 양적이고 자기 틱 누적기를 갖는다 |
| 이미 이사 감 | `Slow` | `CcEffect` 버퍼에 **안 들어온다** — `ZoneApplySystem.cs:45` 가 `StatModifier` 로 우회 |

5개 중 실제 CC 는 2~3개고, 하나는 이미 다른 집으로 갔고, `DoT` 는 성격이 다른데 남아 있다.

**증상이 이미 나와 있었다.** 직전 설계 초안은 "병합 키를 **DoT 에 한해** flavor 로 넓힌다"였는데,
공용 병합 함수 안에 `if (kind == DoT)` 분기를 넣는다는 것 자체가 **두 도메인이 한 함수를 공유하고
있다**는 신호다. 이름도 마찬가지였다 — 출처 태그를 `CcFlavor` 라 불렀지만 지속 피해의 속성은
CC 와 아무 상관이 없다.

## 지금 터지고 있는 결함 (실측)

**CRITICAL — 도트 과피해.** `CcEffectMerge.cs:32-40` 이 `remainingTime` 은 `max` 로 합치고
`scalar`·`tickInterval` 은 incoming 으로 덮는다. 성격이 다른 세 producer 가 한 슬롯을 공유한다:

| producer | 위치 | 값 |
|---|---|---|
| 스택 임계 파생 | `StackModifierTickSystem.cs:99` | 출혈 5 / 0.5s / 4.85s |
| 해저드 장판 | `ZoneApplySystem.cs:62` | 10~20 / 0.25s(3x3 은 연속) / 0.2s |
| 배치 스킬 `DotNearby` | `BattleBridge.cs:3894` (버스터즈) | 7 / 0.2s / 2s |

출혈(rem 4.85) 중인 적이 화염 장판(10 / 0.25s)을 밟으면 **40 DPS 가 4.85초** 붙어 총 ~194
(의도 50)가 되고, **장판을 나가도 계속 탄다**. 난도질꾼·FireCaster·PoisonCaster 전부 카탈로그에
있어 배포 콘텐츠 조합으로 도달한다.

**MAJOR — 오라 stale 비트.** `BattleBridge.cs:2444` 의 OR 누적 마스크가 켠 비트를 못 내려,
동상이 한 번만 때려도 얼음 오라가 매치 끝까지 남는다. 뿌리는 같다 — **도트가 자기 출처를 모른다.**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_dot_effect_buffer.md` | `DotEffect` 버퍼 + `DotFlavor` + 전용 채널. producer 3 · 소비 2 이관, flavor 별 병합 |
| 1 | code | `1_aura_from_dot_flavor.md` | 오라를 `DotEffect.flavor` 로 구동 → bridge 래치 4종 삭제 |
| 2 | docs | `2_handoff_summary.md` | 인계 요약 |

unit 0 은 크지만 **쪼갤 수 없다** — 버퍼를 옮기는 도중에 "쓰는 곳과 읽는 곳이 다른" 중간 상태가
생기면 게임이 깨진다. 이동은 원자적이어야 한다.

## Feature-wide 계약

1. **`DotEffect` 는 자기 버퍼다.** `CcEffect` 에서 `DoT` 는 **런타임 값으로 사라진다**
   (`CcKind.DoT` enum 멤버는 저작 호환을 위해 남는다 — 아래 4번).
2. **`DotFlavor` 는 "무엇이 만들었나"이지 "누가"가 아니다.** 축을 `source`(Entity)로 잡으면 안 된다 —
   난도질꾼 2기는 source 가 둘인데 둘 다 출혈이라 식별에 기여가 없고, `ZoneApplySystem` 은 내부
   루프에 해저드 엔티티가 없어 **source 를 만들 수조차 없다**. 선례는 `StatModifierSlot` 의
   `ModifierOrigin`. append-only, `None = 0` = 미분류(서로 병합).
3. **병합은 flavor 별로 분리한다.** 새 파이프라인이므로 처음부터 올바르게 쓴다 — 옛 덮어쓰기를
   재현했다가 다음 단위에서 고치지 않는다. `None` 끼리는 계속 병합(현행 동작 유지).
4. **`Stun`·`Sleep`·`Impulse` 는 손대지 않는다.** `CcEffectMerge` 의 남은 규칙은 그대로가 정답이고,
   `CcActionLock`·`CcClearSystem`·`DreamCocoonSystem`·`MovementSystem` 은 무영향이어야 한다.
   특히 wake-on-hit 를 source 별로 좁히면 호접몽 파탄 판정이 무력화되는 함정이 있다 — 건드리지 말 것.
5. **`HazardEffect.kind` 의 `CcKind.DoT` 는 저작 토큰으로 남는다.** `ZoneApplySystem` 이 그 값을
   보고 새 파이프라인으로 라우팅한다 — **`Slow` 가 이미 정확히 같은 형태**다(`:44` 의 "SO 호환용
   잔존" 주석). 에셋 마이그레이션 0.
6. **`tickTimer` 는 슬롯 지속 상태다.** 매 프레임 존 refresh 에도 리셋 금지, 주기 변경 시 진행률
   비례 환산 — 기존 `CcEffectMerge` 의 규약을 그대로 가져온다. add 경로 첫 틱 즉발도 유지.
7. 전 수치는 SO — 하드코딩 금지. 해저드 flavor 는 `HazardEffect` 저작 필드.

## 신규 이벤트 채널 (25번째)

`DotApplyEventsSingleton` — Effects·Combat·Bridge → Effects 의 도트 부여 seam. 기존
`EnemyCcEventsSingleton` 을 재사용하면 페이로드가 두 도메인을 섞게 되므로 분리한다(이 spec 의
목적 자체가 그 혼합을 없애는 것). `CLAUDE.md` 의 채널 목록을 같이 갱신한다.

## 파이프라인 커버리지

**N/A — 신규 플레이 오브젝트 없음, 생성→렌더 경로 불변.** 오라 VFX 스폰 경로
(`StatusFxSpawner` → `StatusFxView`)는 그대로이고 unit 1 이 바꾸는 것은 **점등 판단 소스**뿐이다.

## 알려진 결정 — 얼음 오라는 뜨지 않게 된다

unit 1 이후 점등 조건은 "그 flavor 의 도트가 도는 중"이다. 그런데 `StackModifier_Ice.asset` 의
임계는 `ApplyStat`(감속)·`ApplyStun` 뿐 **`ApplyDot` 이 없다** — 얼음은 도트를 만들지 않는다.
지금 얼음 오라가 보이는 유일한 경로는 **오귀속**이므로, unit 1 은 기능을 없애는 게 아니라 **거짓
점등을 없앤다.** 반대로 화염·독 오라는 해저드 도트에 물려 **처음으로 정상 동작한다**(현재는 스택
슬롯을 보는데 화염·독은 스택 producer 가 0이라 영영 안 뜬다).

## 후속 후보

- **얼음 오라 소스** [S] · 도트가 아닌 상태(감속·스턴)를 오라 소스로 삼을지 결정.
- **`HazardEffect` 저작 enum 분리** [S] · `HazardEffectKind` 를 따로 두면 `CcKind` 잔존 토큰이
  사라진다. 값을 같은 순서로 두면 에셋 마이그레이션이 공짜다.
- **`Impulse` 슬롯 합산** [M] · 분리하면 의미는 맞아지지만 바스티온·샷건너·`Card_GaleShove` 변위가
  전부 재튜닝 대상.
- **다중 공격자 출혈 합산** [M] · flavor 는 "무엇"이라 난도질꾼 2기는 여전히 한 슬롯. 도트 전용
  가산 병합이 별도로 필요(`bleed-fighter-defender` 에서 이관).
- **`DotNearby` 의 flavor 저작** [S] · 지금은 None(버스터즈가 유일 producer 라 충돌 없음).
- **`maxStack` 권위 이중화** [S] · 유닛 SO `stackMaxStack` 과 `StackModifierSO.maxStack` 두 곳이
  권위이고 `ModifierApplySystem.cs:148` 이 기존 슬롯 값을 유지해 "먼저 도달한 producer" 가 이긴다.
- **`DisposeCachedQueries` 조기 리턴 플래그 리셋** [S] · `BattleBridge.cs:662` 가 일부
  `*QueryCreated` 만 리셋한다. 선행 결함.
