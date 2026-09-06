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
- ~~**`DetectedTarget.target` 을 이동·화면이 읽게 하지 말 것.**~~ → **unit 8 에서 유한 반경 한정으로
  풀렸다**(그 대상까지 구운 추격판을 따라가므로 일치한다). **무제한은 여전히 갈린다** — 공용
  사냥판을 타기 때문이고, 화면이 대상을 가리키려면 감지 종류로 갈라야 한다.
- **`Swift`·`Runner` 에 마음 비트(`DefenderCore`)를 켜지 말 것.** `canSiege` 가 켜져 돌격형의
  「한 방」(`stabilityDamage` 50)이 실행조차 안 되고 도발도 안 걸린다 — 실험했다가 되돌렸다(unit 6).

## Follow-up

- **「발견」 표식 프리팹 저작** — unit 5 는 채널·트레이스까지만 완료다. **화면에는 아직 안 뜬다**
  (재사용할 팝업이 숫자 전용, 경보 프리팹 0건). `unity-vfx-authoring` 필요.
- **Play 육안 4종** — ⑴ 감지 적이 옆길 유닛을 찾아가 싸우는가 ⑵ 대상 처치 후 관성
  ⑶ 도발이 감지를 이기는가(가디언을 3칸 이상 떼어 배치) ⑷ **미끼 배치** — 레인에서 2~3칸 떨어진
  칸에 놓으면 적이 오는가(「싸울 자리를 플레이어가 고른다」의 참/거짓).
- **골든 코퍼스** — ⚠ **정정 2026-09-06.** 당시 「이 spec **이전부터** stale」이라 적었는데
  **오진이다.** 코퍼스 마지막 베이크(`39020371`) 이후 적/방어유닛/웨이브 저작을 바꾼 커밋은
  **이 spec 의 커밋 둘뿐**(`032033f1`·`02298532`)이었다 — `huntsDefenders` → `detectionRange`
  **필드 rename**(적 24종)이 `configHash` 를 흔든 것이다(해시는 「이번 판에 등장하는 적 SO 필드」를 담는다).
  당시 실험이 `DetectionRange` **attach 줄만** 껐고 **에셋 필드는 그대로** 뒀기 때문에 해시 축을
  아예 안 건드렸고, 「이벤트·킬 동일」을 「내 변경 무관」의 근거로 쓴 것이 오진이었다.
  → `fcce6bb5` 에서 재베이크하며 이 spec 이 코퍼스에 **처음 반영**됐다.
- **payload 지표 교체** — 마칭을 「골 하강」과 「사냥판 하강」으로 쪼개야 한다. 지금 지표는
  감지가 잘 될수록 마칭이 **늘어나** 자기 효과를 못 잰다(unit 6 에 실측과 함께 기록).
- **`DetectionSystem` 위생 2건**(코드 리뷰 비블로커, 손 안 댐) — ⑴ 막힘 해제가 `Clear` 와 같은
  초기화를 인라인으로 복제해 **정의가 두 벌**이다(필드가 늘면 조용히 갈린다). ⑵ `Clear` 주석이
  「무엇을 남기고 무엇을 지우나」의 규칙을 반만 적었다(`markCooldown` 누락).
  고치려다 **되돌렸다** — Unity 연결이 끊겨 컴파일 검증이 불가했고, 순수 위생 변경을 미검증으로
  올리거나 공유 워크트리에 미커밋으로 두는 것보다 검증된 상태를 유지하는 편이 낫다고 판단했다.
- 나머지 후속 후보는 README 하단.

---

## unit 8 — 대상 지향 추격 (2026-09-06)

**Implemented**

- 규칙을 문장 그대로 만들었다: 「내 감지 반경 안에 적이 있고 **그 적을 향해 갈 수 있는 이동
  경로가 있으면** 그쪽으로, 없으면 원래 가던 길로」. units 1~6 은 1·3단계만 구현하고 2단계를
  공용 사냥판에 위임했는데, 그 필드는 **다른 질문**에 답했다(「아무 방어유닛이나 · 지상 통행으로」).
- **`DetectionChaseDist`/`DetectionChaseFlow`**(Combat) 신설 — 감지한 «그» 대상까지, «내» 통행
  층으로 구운 dist/flow. 어그로 추격판과 같은 기계(`AggroChaseMath.BuildChaseField`)를 쓴다.
- `DetectionSystem` 후보 선정에 **경로 질의**를 넣었다. 최근접이 못 가면 다음 후보(최대 3),
  다 못 가면 `hunting = 0`. ECB 는 **자기 `OnUpdate` 끝에서 재생**해 같은 프레임에 이동이 본다.
- `MovementSystem` 사냥 레인이 둘로 갈렸다 — **무제한 = 공용 사냥판**(무회귀) / **유한 = 추격판**.
  추격판이 `flow` 도 들고 있어 하강·평활화·접근 보정 코드는 **한 줄도 안 바뀌었다**.
- **비행 편입** — `skimmer`·`dragon` = 3칸. **코드에 비행 분기 0.** `waypoint_air` 는 계속 0
  (경로 저작이 정체성 — 규칙에 의한 배제).
- 무효화는 **Combat 이 자기 맥락 안에서** 한다(`chaseSignature` vs `blockedSignature`) —
  어그로처럼 Effects 가 남의 컴포넌트를 떼지 않는다.

**Verified**

- EditMode **2757건 중 실패 2건**(`boomerang`·`bomb_man` 문안 — 시트 소유 **선행 실패**).
- 신규 `DetectionChaseFieldTests`(8) + `DetectionLeakProofTests` 개정(10) 전부 통과.
  핵심 짝: **비행은 벽 너머를 감지 / 지상은 못 감** — 차이가 `traversalLayers` **한 바이트**뿐.

**Notes — 되돌리면 안 되는 것**

- **무제한을 추격판으로 옮기지 말 것.** 「아무 방어유닛이나」가 그쪽의 진짜 질문이라 공용
  사냥판이 정확한 답이고, 옮기면 보스 거동이 바뀐다.
- **`AttachChase` 는 빌드 성공 «직후»에 불러야 한다** — `tmpDist`/`tmpFlow` 는 다음 탐침이 덮어쓴다.
- **관성(grace) 중에 버퍼를 떼지 말 것.** 관성의 실체가 이 버퍼의 잔존이다.

**Follow-up**

- **계측 재실행 미실시.** 「감지 대상 ≠ 이동 도착지」가 **정의상 0%** 여야 한다 — 0이 아니면
  하강이 버퍼를 안 보고 있다는 뜻이라, 이 값이 곧 회귀 탐지기다.
- **Play 육안** — 비행이 **배치 구역 위로 파고드는** 그림이 납득되는지가 첫 항목(새 성질).
- **Android 실기기 비용** — 획득당 그리드 BFS 최대 3회. 실측 획득 빈도는 ≈0.44회/초지만
  기기 측정이 없다.
- `unit 5` 표식이 이제 **유한 감지에서는** 대상을 가리켜도 참이다 — 계약 6 을 다시 쓸 수 있다.
