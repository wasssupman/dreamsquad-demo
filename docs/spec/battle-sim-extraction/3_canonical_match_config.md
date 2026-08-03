# 3 — canonical MatchConfig blob + configHash

## 목적

골든의 "같은 조건" 보장은 스탯 SO 스냅샷만으로 부족하다 — 씬 상주 gameplay knob(스폰 spread, `enableAdjacencySynergy` 등)도 결과를 바꾼다. 한 판의 조건 전체(맵·웨이브플랜 **생성 결과**·덱·seed·유닛/스킬/투사체/해저드/기믹 스탯·점수 룰·씬 knob)를 **불변 blob으로 물질화**하고 canonical 직렬화의 `configHash`를 만든다. 골든 diff 발생 시 "시트 드리프트 vs 코드 회귀"를 해시로 먼저 가르는 1차 판독 장치이자, 이후 AMR·커맨드로그의 공통 필드다. 셋업 단계는 이미 결정적이다(2026-08-03 교차검증 정정 — 웨이브 생성 `WavePatternGenerator`·기믹 선택 `GimmickSelection` 은 `Unity.Mathematics.Random` + `MatchSeed.Derive*` 파생. `UnityEngine.Random` 은 매치 시드 1회 생성 진입점뿐); 어느 쪽이든 생성 **결과**가 blob에 실리므로 셋업 난수는 sim 상류로 격리된다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Core/MatchConfigSnapshot.cs` (또는 Battle 하위) — 수집·canonical 직렬화·SHA 해시
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `StartBattle` 직전 수집 지점 (씬 knob 필드 목록화 포함)
- `Assets/_Project/Scripts/UI/Outgame/LoginAutoImport.cs` — 테스트/하네스 모드 차단 가드 (시트 임포트가 SO를 덮어 골든을 오염시키는 기존 함정 방어. 임포트는 비동기·논블로킹이라 같은 빌드·같은 시트에서도 판 시작 시점에 따라 스탯이 갈릴 수 있고 — 주석이 명시한 트레이드오프 — 릴리즈 빌드는 구독 자체를 안 한다: `_devEnabled = Debug.isDebugBuild || Application.isEditor`)

## 구현

수집 범위는 "게임 결과에 영향을 주는가"로 판정 — 뷰 전용 값(비주얼 스케일·그림자 등)은 제외. canonical 직렬화는 필드 순서 고정·문화권 불변 포맷(invariant)·부동소수 R 포맷으로 재현성 확보. 씬 knob 전수는 Bridge SerializeField 87개를 gameplay/presentation으로 분류해 목록을 이 unit에 기록(M1 salvage 판정의 입력 재활용). 해시는 골든 덤프(unit 4) 헤더에 동봉.

## 완료 기준

- 같은 조건 2회 실행 → `configHash` 동일. 스탯 SO 값 1개 변경 → 해시 변경.
- 하네스 모드에서 LoginAutoImport 미실행 확인(로그).
- gameplay/presentation knob 분류표가 이 unit 문서에 기록됨.
