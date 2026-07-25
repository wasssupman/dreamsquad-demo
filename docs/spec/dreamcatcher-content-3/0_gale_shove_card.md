# 0 — 밀치기 (gale_shove): AttackN × Impulse 넉백

## 목적

N번째 공격에 맞은 적을 밀쳐내는 카드. Impulse CC 는 bake(`MapDcCc`)→arm(`AttackSystem` RESOLVE, phantom-impulse 가드 포함)→소비(`MovementSystem`)까지 전부 구현돼 있으나 카드는 빙결(Stun)만 쓴다. **코드 0줄, 카드 에셋 + 카탈로그 등록만.**

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_GaleShove.asset` (신규)
- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` (cards 배열 append)

## 구현

`Card_FrostArrow.asset` 을 원형으로 authoring:

- id `gale_shove` · displayName `밀치기` · axis All(3) · category Normal · type Unit(1)
- mechanics[0]: trigger `{ kind: AttackN(1), period: 4 }` / payload `{ kind: ApplyCcToTarget(10), ccKind: Impulse(1), magnitude: 6, duration: 0.35 }`
  - magnitude = 넉백 속도(발사 시점 공격자→적 방향 벡터에 곱). duration = 지속 초. **둘 다 Play 튜닝 대상 초안값.**
  - bake 가드: ApplyCcToTarget 은 duration ≤ 0 이면 skip — 0.35 로 충족.
- description: `4번째 공격마다 → 대상을 밀쳐냄` (CardText 가 Impulse 문안 지원 — "대상에게 넉백 속도 N · L초" — 자동 문안이 나오면 그대로 두고 description 은 미러만)
- art: null (category 색 폴백)

**사양으로 수용하는 것**: 넉백 방향은 공격자→적 방향이며 경로 역방향 보장 없음(코너에서 옆으로 밀림). 공격자·대상 동일 셀이면 기존 가드가 CC 자체를 생략.

## 완료 기준

- [ ] EditMode 전체 green (`DreamcatcherCatalogSyncTests` 가 신규 카드 등록 자동 검증)
- [ ] Play smoke: 카드 부착 유닛의 4번째 공격에서 적이 밀려나는 것 육안 확인, 콘솔에 bake skip 경고·unhandled payload 경고 없음
