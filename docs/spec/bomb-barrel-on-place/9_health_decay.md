# 9 — 판 위의 설치물은 스스로 닳는다

## 목적

「시간이 지나면 사라진다」를 **시한이 아니라 체력**으로 표현한다. unit 7 이 시한을 걷어낸
자리에 들어가되, 성격이 결정적으로 다르다.

| | 시한 (unit 1, 은퇴) | 노후화 (이 unit) |
|---|---|---|
| 죽음 경로 | **둘** (피해 / 만료) | **하나** (피해) |
| 폭발의 성격 | 적이 부순 사건 **또는** 시계 사건 | 언제나 부서진 사건 |
| 남은 시간 판독 | 전용 장치가 필요 (퓨즈 틴트) | **이미 있는 체력 바**(unit 8) |

노후화는 별도 규칙이 아니라 **설치물이 스스로를 때리는 것**이다. 그래서 계약 4(「폭발의
계기는 부서짐 하나」)를 깨지 않고 시간 축을 되돌려준다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardSO.cs` — `healthDecayPerSec`
- `Assets/_Project/Scripts/Battle/Effects/BlockingHazard.cs` — `decayPerSec` 런타임 사본
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` — bake
- `Assets/_Project/Scripts/Battle/Effects/ObstacleLifetimeSystem.cs` — 노후화 루프
- `Assets/_Project/Data/Hazards/Blocker_BombBarrel.asset` — `healthDecayPerSec: 10`

## 구현

- **신규 시스템 0.** 이미 매 프레임 길막 설치물을 걷는 `ObstacleLifetimeSystem` 에 루프 하나를
  더한다. 새 시스템을 세우면 M0 unit 0 이 얼린 `BattleSimGroup` 총순서에 행이 하나 생긴다.
- **피해는 `IncomingDamage` 로 흘린다** — TRD 2.5.2 의 정본 횡단 채널이고, Effects 가 여기에
  쓰는 것은 기존 관용구다(`DotApplySystem` 선례). `DotEffect` 를 재사용하지 **않는다**:
  그쪽은 화면에 원소 오라와 피해 숫자를 띄우는 어휘라 「낡아 간다」와 그림이 다르다.
- ⚠ **`source` 를 비워 둔다.** 환경 피해는 귀속이 없다는 뜻이고, 채우면 킬 귀속이 엉뚱한
  대상에게 간다(`IncomingDamage.source` 주석의 명시 계약).
- ⚠ **실행 순서가 이 설계를 지탱한다.** `ObstacleLifetimeSystem`(6) → `DamageApplicationSystem`
  (36) → `BarrelExplosionSystem`(41). 노후화 피해가 **같은 프레임**에 정산되고, 그 프레임에
  폭발이 스테이지된다. 노후화를 36 뒤로 옮기면 한 프레임씩 밀린다.
- **기본 0 = 안 닳음.** 기존 바위 길막은 키가 없어 0 으로 떨어진다(무회귀).
  `explodeDamage`·`overheadHeight` 와 같은 형태의 옵트인.

## 저작

`maxHp / healthDecayPerSec` = **아무도 안 때렸을 때의 수명(초)**.
배럴은 `200 / 16.67` = **12초**.

⚠ **이 knob 이 실제로 정하는 것은 「방치된 배럴이 판을 몇 초 차지하나」 하나뿐이다.**
설계 리뷰(2026-08-23)가 계산으로 뒤집은 지점이라 근거를 남긴다.

적 실측 DPS(`outputs[0].magnitude / attackCooldown`): Basic 40 · Skimmer 100 · Needler 57 ·
Tanker 33 · Vanguard 31 · Slime 16 · Runner/Swift **0**. 배럴 200 HP 기준 파괴까지:

| 상황 | 10/s (=20초) | 16.67/s (=12초) | 차 |
|---|---|---|---|
| Basic 1기 | 4.00초 | 3.66초 | 0.34초 |
| **Basic 3기 = 이 스킬이 노리는 그림** | 1.54초 | 1.46초 | **0.08초** |
| Basic 5기 | 0.95초 | 0.92초 | 0.03초 |
| **아무도 안 때림** | 20.0초 | 12.0초 | **8.0초** |

교전 구간에서 노후화가 총 DPS 에 기여하는 몫은 **7.7%** 이고 knob 감도는 **5% 미만**이다.
방치 구간 감도는 **67%**. 그러므로 「12초를 쓰면 교전 중 루프가 짧아진다」는 초판의 논증은
**틀렸다** — 그 축에서 이 값은 사실상 아무것도 안 한다. 이 값의 유일한 실질 효과는
「길 밖에 떨어진 배럴을 얼마나 빨리 치우나」이고, 그 축에서는 **짧을수록 좋다**.

⚠ 다음 사람에게: 이 숫자를 늘리려거든 **교전 감각이 아니라 「방치 배럴이 판에 남는 시간」**
으로 논증해라. 교전 축으로 정당화하면 위 표가 그 논증을 즉시 반증한다.

## 완료 기준

- [x] compile 0 에러.
- [x] `BarrelExplosionTests` 8건 — 노후화가 `IncomingDamage` 로 흐른다(10/s × 0.5s = 5,
      `source` 미설정) · 0 이면 한 항목도 안 쌓인다 · 노후화로 죽으면 `DeadTag` 가 붙고
      **같은 한 발**이 스테이지된다 · `maxHp/decay` 가 무방비 수명이다.
- [x] 전체 EditMode 2577건 중 실패 1건 = 사전 실패(말파이트 desc, 무관).
- [x] (Play) 방치한 배럴이 스스로 닳는다 — 스폰 `hp 200/200` → 9.5초 뒤 `hp 105.0`
      (10/s × 9.5s = 95, 오차 0) · 바 `ratio 0.525` 동행. **20.7초에 소멸**(예상 20초) ·
      고아 바 0 · 콘솔 에러/경고 0.
      ⚠ 이 실측은 `10/s` 시절 값이다. unit 10 에서 `16.67/s`(12초)로 재저작했고
      **관계식은 그대로**라 재측정하지 않았다(`maxHp/decay` 를 `DecayingBarrel_...` 가 고정).

확인 2026-08-23 · Play 실측(BattleScene).
