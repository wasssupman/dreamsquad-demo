# 1 — Card_SlowAwakening → Unit 전환

## 목적

느린 각성을 Squad(axis 전체 버프)에서 Unit(개별 부착 + SelfWarmupBuff)로 전환한다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_SlowAwakening.asset`

## 구현 (필드 변경)

| 필드 | 이전 | 이후 |
|---|---|---|
| `type` | Squad(0) | **Unit(1)** |
| `binding` | Axis(0) | **Unit(1)** |
| `effects` | [{AttackSpeed, 50}] | **[]** (mechanics 로 이관) |
| `placementWarmupSec` | 2 | **0** (mechanics duration 으로 이관) |
| `mechanics` | [] | **[{ trigger:{kind:None, period:0}, payload:{kind:SelfWarmupBuff(5), magnitude:50, projectile:null, tileRange:0, duration:2} }]** |
| `category` | Subconscious(2) | 유지 |
| `axis` | All(3) | 유지(Unit 경로에선 inert) |
| `description` | "배치 후 2초간 잠들어…" | **"부착한 유닛이 2초간 잠들었다가 깨어나 공속 +50% (사망 전까지 지속)"** |

- 유닛 0 의 enum(SelfWarmupBuff=5) 컴파일 이후에 기입.

## 완료 기준

- [ ] `DreamcatcherCatalogSyncTests` 그린(카탈로그 등록 유지).
- [ ] Play: 손패에서 느린 각성을 한 유닛에 드래그·부착 → 그 유닛만 2초 idle 후 공속 ×1.5,
      다른 유닛 무영향. 콘솔 에러 0.
- [ ] 덱빌더 팝업: 헤더 `UNIT`(축 칩 없음) + 설명 문장.
