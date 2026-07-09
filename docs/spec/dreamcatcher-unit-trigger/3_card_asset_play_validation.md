# 3 — 카드 에셋 + Play e2e 검증

## 목적

실제 카드 SO(가칭 "콕콕 바늘")를 만들어 검증 질문에 답한다: 5회째 타격마다 대상에게 투사체 → 20 데미지, 카드 2장 = 독립 카운터, 기존 경로 무회귀.

## 변경 대상

- 신규 에셋: `Assets/_Project/Data/Dreamcatcher/Card_PokeNeedle.asset` (기존 카드 에셋 폴더 위치 관례 확인 후 동일 폴더에)
- 코드 변경 없음 (unit 0~2 산출물 소비만)

## 구현

- `DreamcatcherCard`: id=`poke_needle`, displayName=`콕콕 바늘`(가칭), binding=`Unit`, category=`Unique`(임시), effects 비움, mechanics=[{ trigger:{AttackN, 5}, payload:{ProjectileToTarget, 20, projectile=기존 ProjectileData 재사용} }].
  - 투사체 뷰는 기존 원거리 유닛의 `ProjectileData` 하나를 임시 재사용 — 전용 뷰/카드 아트는 후속.
- 인게임 카드 선택 UI 편입은 스코프 밖 — 부착은 execute_code 로 `ApplyDreamcatcherCardToUnit` 직접 호출.

## Play 검증 절차 (UnityMCP)

lessons 참조: MCP Play 시뮬은 에디터 포커스 필요, execute_code 는 method body/타입 풀네임.

1. Play 진입 → 원거리 defender 1기 배치, 카드 부착 (AssetDatabase 로 카드 로드 → bridge reflection 호출).
2. 공격 5회 관찰: 5회째 타격 판정 직후 추가 투사체 발사 확인 (스크린샷 + 콘솔/로그).
3. 대상 적 HP 가 기본 데미지 외 −20 되는지 확인 (BattleLogger 또는 reflection 으로 Health 조회).
4. 같은 카드 1장 더 부착 → 슬롯 2개, 서로 다른 프레임에 획득했으면 발동 시점 독립 확인.
5. 발동 프레임 이후 `ProjectileRequestCarrier` 엔티티 카운트 == 0 확인 (캐리어가 drain 에서 파괴됨 — 누수 회귀 가드).
6. 카드 미부착 유닛/기존 스탯 카드 경로 무회귀 (일반 발사·히트 정상).

## 완료 기준

- [x] 5회째 타격마다 투사체 발사 + 20 데미지 (육안 + 로그 `Archer Damage 20.0`)
- [x] 카드 2장 독립 카운터 동작 (슬롯 2개, instanceId 0/1, 위상 독립)
- [x] 무회귀: 기존 투사체/스탯 카드/근접 유닛 정상, 콘솔 에러 0
- [x] 사용자 완료 확인 후 확인 일자 + 커밋 해시 기입

> 카드 실제 값: 투사체 = `Projectile_Shard02_GA`(화살과 구분되는 GA 샤드, speed 12→26 상향), magnitude 20, period 5, binding=Unit, category=Unique. 전용 카드 아트/발사 SFX 는 후속.

완료 확인: 2026-07-09 — 실전투 Play 육안 확인(사용자 승인). 5회 주기 발동·독립 카운터·flat 20(시너지 변조 무관) 로그 실증. 이 문서와 동일 커밋.
