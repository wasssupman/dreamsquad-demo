# 5 — 드림스톤 개별 아이템화 + 등급 캐파 내 상중하 스탯 (rev 2026-07-06)

## 목적

스톤을 **전부 고유한 개별 아이템**으로 만든다: 기존 16종 각각에 복사본 3개를 더해 종류당 4개(총 64 에셋), **모든 스톤은 순차 부여된 고유 id** 를 가지며 **장착에 제한 규칙을 두지 않는다**(장착된 아이템이 딤드되는 것은 "하나의 아이템은 한 슬롯"이라는 물리적 사실일 뿐). 동시에 같은 종류 안에서 스탯을 **등급 캐파 내 소수 1자리 상/중/하**로 차등화한다. (사용자 결정 2026-07-06 — 이전 rev 의 "인스턴스/그룹핑" 프레임 폐기)

## 변경 대상

- `Assets/_Project/Data/Dreamstones/` — 전면 재작성: 64 에셋 (기존 16 리네임+수치 조정 + 신규 48), `DreamstoneCatalog.asset` 64종 등록
- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs` — 피커 평면 64 아이템(컴팩트 셀) + 장착 id 딤드·선택불가(유닛 규칙과 동일)
- `Assets/_Project/Tests/EditMode/DreamstoneCatalogTests.cs` — 64종/순차 id/캐파/티어 구성 검사
- `Assets/_Project/Tests/PlayMode/DreamstoneCarryInSmokeTest.cs` — e2e 의 스톤 id/기대값 갱신 (신 id 4개, 합 +24%)

## 구현

- **id 체계**: `stone_001` ~ `stone_064`, 카탈로그 순 = id 순. 종류 블록 단위 배치(예: 001~004 = Unique ATK 상/중/중/하). displayName 은 종류명 유지("Unique Attack Stone") — 티어는 요약 %로 구분.
- **스탯 티어** (rev 2026-07-06b — 소수점 1자리 허용. 캐파 = 등급캡 ÷ 4, 상=캐파·중=0.8캐파·하=0.6캐파, 종류당 [상,중,중,하]):
  - Unique(7.5): 7.5/6.0/6.0/4.5 · Epic(5): 5.0/4.0/4.0/3.0 · Rare(3): 3.0/2.4/2.4/1.8 · Common(2): 2.0/1.6/1.6/1.2
  - 파생: 종류 4개 합 = 3.2×캐파 (유니크 24%, 30% 캡 이내).
- **피커**: 평면 64 아이템. 셀 컴팩트화(≈110×54, spacing 8, 라벨 fontSize ≈11)로 그리드 밴드(스크롤 없이) 수용. 장착된 id 는 딤드 + `interactable=false`. 그 외 제한 없음. (그룹핑/카운트 표시 없음)
- **저장/반입 무변경**: id 기반 그대로. **구 id 저장분은 해석 시 skip → 슬롯 빈칸으로 로드** (기존 정책 그대로 — 1회 재장착 필요, 마이그레이션 없음).
- **validator**: 64종 · id 유일 + `stone_{NNN}` 순차 · percent 는 0.1 단위 & percent ≤ 등급캡÷4 · 종류(연속 4개 블록)별 [캐파, 0.8캐파, 0.8캐파, 0.6캐파] 정확 일치.

## 완료 기준

- EditMode: validator PASS (64종/순차/캐파/티어) + ProfileStore 회귀
- PlayMode: e2e (신 유니크 ATK 4개 = 7.5+6+6+4.5 → damageMul 1.24) + carry-in/재시작/누수 smoke 회귀 + 피커 진단(64 아이템, 장착 id 딤드, 서로 다른 id 4개 배정)
- 육안: 피커에서 같은 종류의 %차등(예: ATK +7/+6/+5) 확인, 장착분 딤드

> 완료 확인 2026-07-06 — 리그 게이트 PASS (main c2fe03d 기준): EditMode 13/13 (validator 64종·순차 id·0.1단위·티어블록 정합) + PlayMode 8/8 (e2e stone_001~004 = damageMul 1.24, 개별 아이템 딤드 계약 진단, carry-in/재시작/REDRAFT 누수/드래프트/아웃게임 회귀). 육안: 피커 64종 티어 %차등 확인 잔여.
