# 0 — 스트레스 축과 게임 종료 조건

## 목적

**마음 체력을 스트레스로 읽고, 그 값이 100 이 되면 판을 끝낸다.** 그리고 **마음을 깎는 것은
적의 공격력 하나로 통일한다** — 돌격형의 자폭 피해 축을 끊는다. 이 spec 의 토대이고,
「뒤집는 계약」 1·2 가 여기서 실행된다. 화면은 아직 안 바뀐다(unit 1·3) — 이 unit 은 **규칙**만 세운다.

곁들여 **「첫 마음 파괴가 종료다」를 코드에 고정**한다. 명제 10(마음 1개)은 지금 데이터에서
참이지만 **코드 불변식이 아니다** — 그리고 마음이 1개인 동안은 「첫 붕괴」와 「마지막 붕괴」가
**관측 불가능하게 동일**해서, 어느 쪽으로 구현하든 테스트도 Play 도 통과한다. 「마지막」으로
구현되면 첫 붕괴가 유출 배수구를 열어 **명제 1 이 조용히 깨진다.**

## 변경 대상

- `Assets/_Project/Scripts/Core/StressMath.cs` **(신설)** — 순수 static
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncGoalStability` 붕괴 분기 · `EndMatch` 3번째 호출부 · `DrainGoalEvents` 자폭 피해 끊기 · 타워 복수 경고
- `Assets/_Project/Tests/EditMode/StressMathTests.cs` **(신설)**
- **테스트 개정 3건** (계약이 뒤집혔으므로 단언도 뒤집는다):
  - `Tests/PlayMode/GoalStabilityTest.cs` — 「마음이 무너져도 Result 가 아니다」 5초 감시 → **반대로**
  - `Tests/EditMode/StructureSpawnAndBreachTests.cs` `OneTowerDestroyed_...` — `_resultShown == false` 단언
  - 같은 파일 `MirrorScalar_ZeroOnBreachFrame_ThenTracksSurvivingCore` — 붕괴 다음 프레임에 생존 골 추적
  - **같은 파일에 단언 신설** — 그 픽스처는 이미 타워 2기((9,2)·(9,5))를 세운다(`:39`)

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

**5. ⚠ 저작 하드 에러는 두지 않는다 — 스코프 밖이다.** `goals > 1` 을 `OnValidate` 에서 막고
싶어지지만, `map-rework` 계약 3 이 **"멀티골 기계(goals[] 슬롯·소비처)는 건드리지 않는다"** 이므로
이 spec 이 그걸 뒤집으면 제약 9(스코프 엄수) 위반이다. 대신 셋으로 나눈다:

- **계약 문장** (README) — 「종료 = 첫 마음 파괴, `goals` 개수 무관」
- **런타임 경고 1줄** — `SpawnStructureEntities` 의 타워 루프 뒤, 개수가 1 을 넘으면
  `Debug.LogWarning`. 기계를 안 건드리면서 저작 사고를 표면화한다
  (`DrainGoalEvents` 의 「폴백 + 경고 1회」 관례와 같은 결)
- **2타워 단언** — `StructureSpawnAndBreachTests` 픽스처가 **이미 타워 2기를 세운다**(`:39`).
  어차피 이 파일을 개정하므로, 개정본에 「2기 중 1기 파괴 → **같은 프레임 종료** +
  `_breachedCells` 가 비어 있음」을 넣으면 「첫 붕괴」가 코드에 고정되고 회귀 불가가 된다

`goals > 1` 저작 가드 자체는 후속 후보로 `map-rework` 에 위임한다.

**6. 회복 대상 규칙 한 줄.** unit 2 가 쓸 계약 — 힐은 **살아있는 마음 전체**에 넣는다.
마음 1개에선 동치지만 「최근접 하나」로 두면 2골 사고 시 만피 마음이 흡수해 clamp 로 소멸시킨다.

**7. 자폭 피해 축을 끊는다 (명제 9).** `DrainGoalEvents` 의
`if (!breached) EnqueueGoalTowerDamage(stabilityDamage, evt.position);` 한 줄이 유일한 소비처다
(`EnqueueGoalTowerDamage` 호출부 1곳 · `stabilityDamage` 소비처 1곳 — 확인함). 이 호출을 끊으면
`Runner`·`Swift` 는 **마음 근처까지 이동한 뒤 아무 피해도 주지 않고 소멸**한다(도달 후 소멸 경로
자체는 피해와 독립이라 그대로 산다).
⇒ 이로써 **「마음은 적의 공격력만큼 피해를 입는다」가 문자 그대로 참**이 된다 — 공격력이 없는 적
(`attackMethod: None`)은 피해가 0 이다. `stabilityDamage` 필드와 `EnqueueGoalTowerDamage` 는
**지우지 않고 휴면**시킨다(다른 휴면 코드와 같은 규칙 — 되돌릴 때 1줄이다).

## 완료 기준

- [x] 컴파일 0 에러 · 콘솔 에러 0
- [x] `StressMathTests` — 만피=0 · HP0=100 · 반=50 · `max<=0` 폴백 · 음수 클램프
- [x] EditMode 전체 완주, **신규 실패 0건**
- [x] 「붕괴 프레임에 `_goalReachedCount` 가 오르지 않는다」 EditMode 단언 신설
- [x] **2타워 단언 신설** — 1기 파괴 → 같은 프레임 종료 + `_breachedCells` 비어 있음
      (「첫 붕괴 = 종료」를 코드에 고정. 이게 없으면 「마지막 붕괴」 구현이 조용히 통과한다)
- [x] 마음이 2개 스폰되면 런타임 경고 1회 (하드 에러 아님 — 스코프 밖)
- [x] Play: 마음을 무방비로 내주면 **그 자리에서 결과 화면**이 뜬다(3분을 안 기다린다)
- [x] Play: 결과 화면 총점 = 그때까지 처치 수
- ~~Play: `Runner`·`Swift` 가 마음에 닿아도 스트레스가 1도 오르지 않는다~~
- ~~EditMode: 자폭 경로가 `IncomingDamage` 를 넣지 않는다~~

⚠ **위 두 줄은 은퇴했다.** 사용자가 명제 9 를 뒤집어(2026-08-23 「누수로 이어지되 안정도
직격 피해」) 돌격형이 도달 시 `stabilityDamage` 를 꽂는다 — 자폭 경로가 **되살아났고**
`IncomingDamage` 를 넣는다. 이 문서의 rev 1 서술이고, 현행 계약은 unit 7 이 갖는다.

**확인 2026-08-24** — 커밋 `c961a41c`(+ `d91ff021` 리뷰 반영). 하네스 실측: 방어 0기 판이
**51초에 `stress_full` 로 종료**(3분을 안 기다린다) · 콘솔 `STRESS FULL` 로그 · EditMode 초록.
