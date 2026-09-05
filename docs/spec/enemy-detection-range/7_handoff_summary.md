# 7 — 인계 요약

## Commit

- `4f27b542` — feat(enemy-detection-range): unit 0 — 감지 도입 전 기준선 계측기
- `032033f1` — feat(enemy-detection-range): units 1~6 — 적 감지 반경

두 커밋 모두 **미푸시**다(푸시는 사용자 승인제).

## Implemented

- **`detectionRange`(float) 저작 축 신설** — `0` 감지 안 함 / `>0` 반경(칸) / `<0` 무제한.
  구 `huntsDefenders`(bool)를 흡수했고 **`tier == Boss` 폴백을 없앴다**(보스도 명시 저작한다).
- **`DetectionSystem`**(Combat) — 감지 판정의 단일 권한. `AttackSystem` 후보 루프와 **같은 세 필터**
  (`targetMask`·통행층·`classMask`) + **같은 술어**(`AttackReach.InReach`)에 반경만 `detectionRange` 다.
  「같은 탐색, 반경만 넓힘」이 이 기능의 전부이고 **공격 엔드포인트는 한 줄도 안 바뀌었다.**
- **이동 게이트 교체** — `MovementSystem` 의 사냥 분기가 `DefenderHunterTag` 대신
  `DetectedTarget.hunting` 을 읽는다. 무제한 감지는 옛 4종과 같은 집합이라 무회귀다.
- **`leakProof` 분리** — 골 전환 면제는 **무제한 감지 전용**. 유한 반경 감지는 오늘처럼 공성한다.
- **관성 1초 · 히스테리시스 · 막힘 해제 · 억제** — `TargetPersistence.HysteresisTiles` 재사용.
- **30번째 채널 `DetectionEventsSingleton`** — `hunting` 0→1 전이 1회. 트레이스 먼저, 표식 나중.
- **저작** — `Vanguard`·`Tanker` = 3칸 · 보스 3종 + `DreamShard` = −1 · 나머지 18종 = 0.

## Key Files

- `Scripts/Battle/Combat/DetectionSystem.cs` — 판정·타이머 4종의 유일한 writer
- `Scripts/Battle/Combat/DetectedTarget.cs` · `DetectionRange.cs` · `DetectionEvents.cs`
- `Scripts/Battle/Movement/MovementSystem.cs` — `hunting` / `leakProof` 두 게이트
- `Scripts/Data/AttackUnitData.cs` — 저작 축 + `OnValidate` 경고 2종
- `Scripts/Bridge/BattleBridge.cs` — 베이크(`UsesDetection`) · 채널 수명 · `DrainDetectionEvents`
- `Editor/Battle/DetectionProbeMenu.cs` — 기준선 계측기(A/B 재실행용)
- `Tests/EditMode/DetectionSystemTests.cs`(17) · `DetectionLeakProofTests.cs`(9) ·
  `Tests/EditModeAssets/DetectionRangeAuthoringTests.cs`(4)

## Verified

- EditMode **2747건 중 실패 2건** — `boomerang`·`bomb_man` 문안 단언(**선행 실패**, 시트 문제).
- 감지 신규 테스트 **30건 전부 통과**.
- A/B 실측(8판 = 4맵 × 배치밀도 12/6): 정체 3틱→**0틱** · 총 스폰 386→384 · 웨이브 66→67 ·
  당김 30/136 동일 · 킬 266→266 · 골 도달 110→**102** · 사냥 필드 도달 틱 3295→**44815**.
  **사전 등록한 실패 판정선 전부 통과.**
- 투트랙 코드리뷰(code-reviewer / ecs-reviewer) — HIGH 5건 지적 전량 반영.

## Notes — 되돌리면 안 되는 것

- **`leakProof` 를 `hunting` 에 다시 묶지 말 것.** 감지 타이머가 꺼지는 틈에 무제한 사냥꾼이
  골을 유출한다(`DreamShard` 는 CC 면역이 없어 자장가 한 번이면 열린다). 유일한 패배 통로다.
- **막힘 해제에서 CC·도약을 빼는 `!lockedNow` 를 지우지 말 것.** `holdingGround` 는 「CC 잠금」도
  접는다 — 지우면 **플레이어가 CC 를 쓸수록 적이 사냥을 그만둔다.**
- **`MovementSystem` 의 `hunterLookup` 을 지우지 말 것.** 소비처 0 이어도 같은 `OnUpdate` 의
  lookup 호출을 지우면 Burst 가 조용히 깨진다(4회 재발).
- **`DetectedTarget.target` 을 이동·화면이 읽게 하지 말 것.** 공용 사냥판과 실측 **5.0%** 갈린다.
- **`Swift`·`Runner` 에 마음 비트(`DefenderCore`)를 켜지 말 것.** `canSiege` 가 켜져 돌격형의
  「한 방」(`stabilityDamage` 50)이 실행조차 안 되고 도발도 안 걸린다 — 실험했다가 되돌렸다(unit 6).

## Follow-up

- **「발견」 표식 프리팹 저작** — unit 5 는 채널·트레이스까지만 완료다. **화면에는 아직 안 뜬다**
  (재사용할 팝업이 숫자 전용, 경보 프리팹 0건). `unity-vfx-authoring` 필요.
- **Play 육안 4종** — ⑴ 감지 적이 옆길 유닛을 찾아가 싸우는가 ⑵ 대상 처치 후 관성
  ⑶ 도발이 감지를 이기는가(가디언을 3칸 이상 떼어 배치) ⑷ **미끼 배치** — 레인에서 2~3칸 떨어진
  칸에 놓으면 적이 오는가(「싸울 자리를 플레이어가 고른다」의 참/거짓).
- **골든 코퍼스** — 재베이크하지 않았다. 이 spec **이전부터 stale** 이라(마지막 동작이 「재생성」)
  지금 구우면 남의 세션 변화까지 기준선에 박힌다. 코퍼스 정리가 선행 조건이다.
- **payload 지표 교체** — 마칭을 「골 하강」과 「사냥판 하강」으로 쪼개야 한다. 지금 지표는
  감지가 잘 될수록 마칭이 **늘어나** 자기 효과를 못 잰다(unit 6 에 실측과 함께 기록).
- 나머지 후속 후보는 README 하단.
