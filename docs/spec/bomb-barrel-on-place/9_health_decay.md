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
배럴은 `200 / 10` = **20초**.

⚠ **12초(구 시한)로 맞추지 않은 이유**: 이제 노후화와 적의 공격이 **더해진다**. 구 시한은
둘 중 먼저 오는 쪽이었지만 노후화는 같이 깎이므로, 같은 숫자를 쓰면 교전 중 배럴이 예전보다
빨리 터져 「몰려와 때리다 다 같이 터진다」는 이 스킬의 루프가 짧아진다. 20초는 **주 사망
원인을 여전히 「맞아서」로 두고**, 노후화는 길 밖에 떨어진 배럴을 치우는 뒷문으로 둔다.

## 완료 기준

- [x] compile 0 에러.
- [x] `BarrelExplosionTests` 8건 — 노후화가 `IncomingDamage` 로 흐른다(10/s × 0.5s = 5,
      `source` 미설정) · 0 이면 한 항목도 안 쌓인다 · 노후화로 죽으면 `DeadTag` 가 붙고
      **같은 한 발**이 스테이지된다 · `maxHp/decay` 가 무방비 수명이다.
- [x] 전체 EditMode 2577건 중 실패 1건 = 사전 실패(말파이트 desc, 무관).
- [x] (Play) 방치한 배럴이 스스로 닳는다 — 스폰 `hp 200/200` → 9.5초 뒤 `hp 105.0`
      (10/s × 9.5s = 95, 오차 0) · 바 `ratio 0.525` 동행. **20.7초에 소멸**(예상 20초) ·
      고아 바 0 · 콘솔 에러/경고 0.

확인 2026-08-23 · Play 실측(BattleScene).
