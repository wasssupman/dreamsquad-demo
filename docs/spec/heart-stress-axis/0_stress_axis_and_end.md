# 0 — 스트레스 축과 3번째 종료 경로

## 목적

**마음 체력을 스트레스로 읽고, 그 값이 100 이 되면 판을 끝낸다.** 이 spec 의 토대이고,
「뒤집는 계약」 1·2 가 여기서 실행된다. 화면은 아직 안 바뀐다(unit 1·3) — 이 unit 은 **규칙**만 세운다.

곁들여 **마음이 1개임을 저작 단계에서 보장**한다. 명제 10 은 지금 데이터에서 참이지만
(`MapDocument` 15종 전부 `goals` 길이 1) 기계는 1~4 를 허용하므로, 가드가 없으면 누군가 2개를
찍는 순간 「첫 붕괴가 끝인가 마지막 붕괴가 끝인가」가 조용히 미정의가 된다.

## 변경 대상

- `Assets/_Project/Scripts/Core/StressMath.cs` **(신설)** — 순수 static
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncGoalStability` 붕괴 분기 · `EndMatch` 3번째 경로
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` (또는 `StructureAuthoringRules`) — 마음 1개 저작 가드
- `Assets/_Project/Tests/EditMode/StressMathTests.cs` **(신설)**
- **테스트 개정 3건** (계약이 뒤집혔으므로 단언도 뒤집는다):
  - `Tests/PlayMode/GoalStabilityTest.cs` — 「마음이 무너져도 Result 가 아니다」 5초 감시 → **반대로**
  - `Tests/EditMode/StructureSpawnAndBreachTests.cs` `OneTowerDestroyed_...` — `_resultShown == false` 단언
  - 같은 파일 `MirrorScalar_ZeroOnBreachFrame_ThenTracksSurvivingCore` — 붕괴 다음 프레임에 생존 골 추적

## 구현

**1. 산식은 순수 함수 하나.** `StressMath.FromHealth(float value, float max) → float 0..100`.
`= (1 − clamp01(value/max)) × 100`. 소비처가 3곳(unit 0 종료 판정 · unit 1 바 · unit 3 림)이라
제약 10 의 추출 조건 (b)「실제 재사용」을 만족한다. 자리는 `Scripts/Core/` — `MatchTally` 와 같은
「아키텍처 무참조 순수 값」 계층이다(UnityEngine/Entities 미참조).

**2. 종료는 붕괴 감지 지점에서.** `SyncGoalStability` 의 `newCoreBreach` 분기가 이미 「마음이
방금 부서졌다」를 정확히 안다. 거기서 미러를 0 으로 굳힌 **직후** `EndMatch("stress_full")` 을 부른다.

**3. ⚠ `OpenGoalCellAfterBreach` 를 타지 않는다.** 이게 명제 1(「누수가 없다」)의 코드적 실체다.
지금 흐름은 `미러 0 → OpenGoalCellAfterBreach(유출 전환)` 인데, 종료가 그 앞에 서면 유출 전환이
아예 실행되지 않는다. `_breachedCells` 가 영원히 비고 `_goalReachedCount` 증가 경로 2곳
(`DrainGoalEvents` 의 breached 분기 · `LeakSiegingEnemy`)이 **구조적으로** 도달 불가가 된다.
호출을 지우지 말고 **종료 뒤에 오게 두어** 되돌리기 쉽게 한다(계약: 휴면이지 삭제가 아니다).

**4. 재진입 방어.** `EndMatch` 는 `_resultShown` 을 세우고 `SyncGoalStability` 는 그걸 보고
early-return 한다 — 같은 프레임에 두 번 불릴 여지가 없다. `stress_full` 은 `CanSubmit` 게이트를
타지 않는다(시스템 종료이지 유저 제출이 아니다).

**5. 저작 가드.** 방어 진영 마음이 **정확히 1개**가 아니면 저작 에러. `battle-structures` 의
`StructureAuthoringRules.ValidateStructures` 가 이미 「적 마음 2+ = 에러」를 세운 선례라 같은 자리에
같은 문법으로 붙인다. 런타임 폴백은 두지 않는다 — **표현 불가능한 상태로 만드는 쪽**이 이 프로젝트의
관용구다(battle-structures 「모드 판정」).

## 완료 기준

- [ ] 컴파일 0 에러 · 콘솔 에러 0
- [ ] `StressMathTests` — 만피=0 · HP0=100 · 반=50 · `max<=0` 폴백 · 음수 클램프
- [ ] EditMode 전체 완주, **신규 실패 0건** (사전 실패 목록과 대조)
- [ ] 「붕괴 프레임에 `_goalReachedCount` 가 오르지 않는다」 EditMode 단언 신설
- [ ] 마음 2개 저작 시 검증 에러 1건 (`StructureAuthoringRules` 테스트)
- [ ] Play: 마음을 무방비로 내주면 **그 자리에서 결과 화면**이 뜬다(3분을 안 기다린다)
- [ ] Play: 결과 화면 총점 = 그때까지 처치 수
