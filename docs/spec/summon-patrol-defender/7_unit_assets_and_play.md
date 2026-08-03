# unit 7 — 유닛 에셋 저작 + 카탈로그 등록 + Play 검증

## 목적

소환사와 순찰병을 실제 에셋으로 만들고 로스터에 올린다. 그리고 이 spec 의 검증 질문에 답한다.

## 변경 대상

- 신규 `Assets/_Project/Data/Defenders/Defender_Summoner.asset`
- 신규 `Assets/_Project/Data/Defenders/Defender_PatrolSoldier.asset`
- 신규 `Assets/_Project/Data/Abilities/SummonPatrol_Summoner.asset`
- `Assets/_Project/Data/DefenderCatalog.asset` — **소환사만** 등록

## 구현

**등록 규칙이 갈린다.** 소환사는 `DefenderCatalog` 에 등록한다(미등록 = 로스터 미노출). **순찰병은 등록하지 않는다** — 플레이어가 직접 배치하는 유닛이 아니다. 순찰병 에셋의 `cost`/`rarity`/`portrait`/`placementCooldown`/`deployCutscene*` 는 미사용으로 남는다(계약 2 가 수용한 비용).

**순찰병 필수 필드**: `health` · `attackRange`(근접 1) · `attackCooldown` · `outputs`(데미지) · `role`(→ `DefenderClassTag`) · Spine 블록 + **`SpineWalkAnimation`**(unit 5) · `awakeningReward` 0.

### 밸런스 출발점

현 로스터 기준선 — 가디언 HP 600 / cost 1 / 어그로 2, 이쒸시개 HP 350 / cost 2, 배스티온 HP 2070 / cost 5.

순찰병은 어그로가 없어도 적을 세운다 — 사거리에 들면 적이 `Engaging`+`Halt` 로 멈춘다. 그래서 **경로 위에 서면 적이 멈추고, 순찰병이 죽어야 다시 간다.** 재소환이 빠르면 영구 봉쇄가 성립한다.

봉쇄를 막는 knob 은 HP 가 아니라 **재소환 쿨다운**이다. 출발점: 순찰병 HP 250~400 · 소환사 `attackCooldown` 8초 내외 · 소환사 cost 3~5 · `leashTileRadius` 2. 여기서 Play 로 조정한다.

**시트**: 지금은 행을 만들지 않는다. `UnitStatApplier` 가 id 미매칭 행을 스킵하므로 안전하다(계약 2). 나중에 시트로 튜닝하고 싶으면 행을 추가하면 된다.

**`desc` 는 카탈로그 등록 유닛의 계약이다.** `UnitKitSummaryTests.CatalogDescriptions_UseThreeFixedSections` 가 카탈로그의 **모든** 유닛에 3줄 형식을 강제한다 — `기본 기능: ` / `배치 스킬: ` / `특수 효과: ` 접두, 줄당 28자 이내, 본문 비어 있으면 실패. 비워 두면 `UnitKitSummary.Build` 폴백이 1줄을 내놓아 **신규 유닛 등록만으로 기존 테스트가 빨개진다**(실제로 겪었다). 해당 섹션이 없으면 `배치 스킬: 없음`(BlockingCaster 선례).

**고유 스파인이 없으므로** 기존 `Casual Character` 파츠 조합으로 임시 외형을 입힌다. 교체는 unit 8.

## 완료 기준

- [ ] 소환사가 배치 로스터에 뜨고 배치된다
- [ ] 배치 → 쿨다운 → 순찰병 소환 → 적 마중 → 교전 → 사망 → 재소환 **전 순환이 Play 에서 돈다**
- [ ] 소환사 사망 시 순찰병 동시 소멸
- [ ] **효과 타일이 순찰병에 걸리는지 확인**(위치 기반이라 자동으로 걸릴 수 있음 — 계약 11). 걸리면 의도 여부를 판단해 기록
- [ ] 드림캐쳐 카드가 순찰병에 부착되지 않는다 (계약 11)
- [ ] 힐러가 순찰병을 치료한다 / 실드셔틀이 순찰병에 실드를 준다 (계약 1 의 `DefenderUnitTag` 귀결)
- [ ] 보스가 순찰병을 사냥 대상으로 삼는다 — 보스가 그 자리에 눌러앉지 않는지 관찰
- [ ] **영구 봉쇄가 성립하지 않는다** — 순찰병이 경로를 무한히 막지 않는다
- [ ] 각성치가 순찰병 사망으로 늘지 않는다
- [ ] 웨이브 3회 이상 연속 Play 에서 콘솔 에러 0 · 프레임 저하 없음
- [ ] **검증 질문**: "소환사를 뒤에 두고 순찰병을 앞세우는 것이, 타일에 유닛을 직접 놓는 것과 다른 배치 결정을 만드는가?" — 사용자 판정
