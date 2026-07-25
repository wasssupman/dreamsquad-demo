# 5 — handoff summary

## Commit

- `60e1f8d9` unit 0 밀치기(AttackN×Impulse) + spec · `bc27201e` unit 1 동상(Ice 스택+씬 배선) · `79d9f844` unit 2 자장가(Sleep 개통) · `9a465578` unit 3 시체폭발(OnKill×킬셀 AoE) · `9186ab85` unit 4 진동갑주(HealthThreshold×self AoE)
- `a8b2d381` 진동갑주 재설계(HP 30% 이하 1회 — 사용자 결정) · `f6c8e45d`/`ac421b2d`/`cb36c074` docs(e2e 기록·리뷰 반영)

## Implemented

- 액션형 유닛 카드 5장: 밀치기·동상·자장가·시체폭발·진동갑주 (전부 category Unique·type Unit·axis All·art=null 폴백)
- `DcCcKind.Sleep` 개통(append+MapDcCc), `StackModifier_Ice`(3스택 슬로우/5스택 Consume 동결) + BattleScene `stackModifierAuthoring` 배선
- `EnemyKilledEvent` 위드닝(hasKillBurst/killer — DefenderDeathEvent 선례), HealthThreshold bake 호이스팅(payload-불문)
- 폭발 킬 귀속: 시체폭발=killer·진동갑주=self (이후 `378c792a` 로 전 폭발 카드 통일 — trigger-gates 참조)

## Key Files

- `Data/Dreamcatcher/Card_{GaleShove,Frostbite,LullabyDart,CorpseBurst,TremorPlate}.asset` + `StackModifier_Ice.asset`
- `Battle/Units/{EnemyKilledEvent,DamageApplicationSystem}.cs` · `Battle/Combat/HealthThresholdSystem.cs` · `Bridge/BattleBridge{,.Dreamcatcher}.cs` · `UI/Dreamcatcher/DreamcatcherCardText.cs`

## Verified

- EditMode 전체 green(카드 검증 3종 자동 포함) · PlayMode 킬임계/온히트/전투데미지 green
- 라이브 e2e: 넉백/수면/슬로우/동결 실전 웨이브 관찰 + 시체폭발 킬+0.01s −25 · 진동갑주 주입+0.03s −15 (더미 계측)
- 투트랙 리뷰 APPROVE (2026-07-25, CRITICAL/HIGH 0)

## Notes (되돌리면 안 되는 것)

- 진동갑주 fraction 0.7 = "HP 30% 이하 1회" (0.1 반복형은 사용자 결정으로 폐기)
- 시체폭발 연쇄(폭발 킬→OnKill 재발동)는 사양. OnKill 은 막타 귀속 — 근접 유닛 킬 스틸 시 미발동이 맞는 동작
- 카드 추가 절차 = 이름 맵 + structuredCount + 축약 매핑 표 3종 동시 갱신, description 은 CardText formatter 정확 미러
- HealthThreshold 는 슬롯당 발동(중복 부착 = 2회 — 빈사폭주 선례. "첫 슬롯만"은 OnKill/OnDeath 의 event-stamp 구조 한정)

## Follow-up

- 사용자 Play 체감 확인 (수치·연출 — 전부 SO 값)
- 시체폭발·진동갑주 PlayMode e2e 핀 (리뷰 B-M2, DreamcatcherGateE2ETest 패턴)
- 실아트 5장(guid 유지 교체) · Fire/Poison 스택 카드 · 동상 오버헤드 아이콘 · 시트 push 1회
