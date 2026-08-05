# 16 — Bridge 규칙 적출 ③ 드림캐쳐: 덱 소유권 + 카드 원자 트랜잭션

## 목적

M1 규칙 적출의 최난도 묶음. 카드 사용이 지금 **5단계로 흩어져 롤백까지 있는 트랜잭션**이고
(효과 적용 → 지불 실패 시 revoke), 덱·게이지·부착 등록부가 MonoBehaviour 소유라 `cardInstanceId`
같은 안정 축이 성립하지 않는다(청사진 ① §2·§10-3).

## 변경 대상

- **Bridge 드림캐쳐 파셜**(`BattleBridge.Dreamcatcher.cs` 972줄, salvage 판정 **rewrite**):
  `ApplyDreamcatcherCard` · `ApplyDreamcatcherCardToUnit` · `ApplyBountyMark` ·
  `RevokeDreamcatcherEffects` · `WouldDreamcatcherCardApply`(preflight 미러 — 검증 공유로 소거)
- **`DreamcatcherHandController`**: 덱(`DreamcatcherCycleDeck`)·각성 게이지·부착 등록부(`_attachedTo`)
  소유권을 sim 으로. 컨트롤러는 **커맨드 발신 + 뷰 통지**만 남는다
- **스킬 캐스트**: `CastSkillAtTile` · `CastPortal` · `ApplySlowField` · `ApplyTornado` ·
  `ApplyMeteor` · `ApplyPortal` · `SpawnAllyBuffZone` — Active 카드 4변종의 실행부
- `DcApplicability`(순수 = conform) · `DcMechanic`(데이터 계약) 은 이동만

## 구현

- **`PlayCard` 를 한 틱 원자 트랜잭션으로**: 검증 전부 선행(손패 보유·타입·게이지≥cost·유출 허용치
  선불 가능·부착 캡·적용성 preflight[`DuplicateState` 포함]·Active 쿨다운·포탈 entry≠exit) → 통과 시
  효과+게이지 지불+유출 선불+손패 소비를 **함께** 적용. **`RevokeDreamcatcherEffects` 롤백 경로가
  소멸**한다(부분 적용이 불가능해지므로).
- `cardInstanceId` 는 덱이 sim 소유가 된 뒤에만 안정적이다 — 현 `entryId` 는 사이클 덱 로컬(청사진 ①
  §2 주의). 선물 셔플의 시드 파생도 `MatchConfig` 물질화 대상으로 옮긴다.
- 거절 사유를 **밖으로 낸다**: 현재 `Commit*` 이 전부 `bool` 이라 `DcRejectReason` 8종이 `false` 하나로
  접히고 UI 가 preflight 로 재계산한다 → receipt 에 실어 이중 계산 소거(청사진 ① §3).
  `Unclassified` 는 배선 버그 센티넬이므로 `InternalError` 로 분리.
- ⚠ `ApplySlowField` 는 아직 **스냅샷**(시전 시점 반경 스냅) — 백로그의 "감속장을 캐리어로"가 여기
  걸린다. **이 unit 범위 밖**(행동 변경이므로) 이지만 이식 시 그 예외를 주석으로 명시한다.

## 완료 기준

- compile 0 · EditMode 회귀 0 · **골든 `dreamcatcher_heavy` 포함 7종 byte diff 0**.
- 카드 4변종 receipt 에 거절 사유가 실려 나온다 — EditMode 로 사유별 단정(최소 6종).
- 덱·게이지·부착이 스냅샷에 실리고 직렬화 왕복 통과(청사진 ① §5 deck).
- `RevokeDreamcatcherEffects` 삭제 확인 — 롤백 경로 부재가 원자성의 증거.
- UI 에 preflight 미러 0(grep): `WouldDreamcatcherCardApply`·`CanAttachMore` 가 세션 질의로 대체.

---

## 정찰 결과 (2026-08-05) — 실측과 계약 정정

### 🚨 발견 — 부분 커밋 구멍이 실재한다 (직접 확인)

`DreamcatcherCycleDeck` 의 두 함수가 비대칭이다:

- `TryGetCard`(`:59-66`) — `IndexInQueue` **또는** `_attached` 를 본다
- `UseUnit`(`:80-88`) — `IndexInHand`(**큐 앞 N칸**, `:108-112`)를 요구한다

그래서 **이미 부착된 entryId** 또는 **손패 밖(큐 6번째 이후) entryId** 는 `TryGetUsableAttach` 를
통과하고, `CommitAttach`(`DreamcatcherHandController.cs:330-355`)가 ④ `ApplyDreamcatcherCard`(ECS 쓰기)
와 ⑤ `TryPayLeakAllowance`(**비가역**)를 끝낸 **뒤** `AttachAndSpend:360` 에서 실패한다. 그 시점에:

- 손패도 게이지도 그대로 (⑥ 미실행)
- 유출 허용치는 **이미 차감됨** (환불 경로 없음)
- 회수 핸들이 `_attachedTo` 에 등록되지 못해 **영영 revoke 불가**

코드 주석은 `// guarded by TryGetUsable` 이라고 적었지만 **가드가 아니다.** 같은 구멍이
`CommitMarkEnemy`(`:382-388`)에도 있고, `SpendAndRecycle`(`:441-446`)은 `UseAndRecycle` 의 **반환값을
아예 무시**하고 `Spend` 를 실행한다(스킬은 나갔고 쿨다운도 물렸는데 카드는 제자리, 게이지는 차감).

라이브 D&D 는 손패 슬롯만 노출해 도달하지 않는다. 다만 unit 13-C3 이 카드를 커맨드로 바꾸면서
`c.CardHandle` 이 그대로 전달되는 경로가 생겼다(`LegacyMatchSessionAdapter.cs:250`) — **현재는 잠재적**
이고 리플레이·잘못된 핸들에서 열린다.

### ⚠ 계약 정정 — `RevokeDreamcatcherEffects` 는 삭제 불가

프로덕션 호출처가 **2곳**이다: `DreamcatcherHandController.cs:350`(롤백 — 이것만 소멸) ·
`:259`(**host 사망 회수** — 살아 있어야 한다). PlayMode 4곳도 브릿지 계약으로 직접 부른다.
⇒ 완료 기준을 **"`CommitAttach` 안의 revoke 호출 0(grep)"** 으로 한다. 함수 삭제가 아니다.
(삭제하면 host 사망 시 squad 버프·placement-aura 가 영구 잔존하고, `PlacementAuraTest` 3건이 이미
실패 중이라 회귀가 조용히 묻힌다.)

### 골든은 이 unit 을 거의 증인하지 못한다

`dreamcatcher_heavy` 는 `ApplyHarnessDreamcatcherCard` → `ApplyDreamcatcherCardHosted`
(`BattleBridge.LegacyTrace.cs:166-172`)로 **컨트롤러·손패·게이지·덱·부착제한을 전부 우회**한다 —
`PlaceDefenderAs` 와 같은 구조의 하네스 전용 seam 이다. 골든이 실제로 보는 것은 둘뿐:

1. **`_dcStackCounter` → `ModifierHeader.stackId` → `StatModifierSlot`** (정규 상태 라인)
2. 커맨드 문자열 `"ApplyDreamcatcherCard:{id}"` + `handle > 0` 판정

⇒ **이 unit 의 유일한 골든 지뢰는 `_dcStackCounter` 다.** 이 카운터는 카드·드림스톤·`SelfStatBuff`·
`DreamCocoon`·`BountyMark`·`RegisterPlacementAura` **6개 지점이 공유**하고 `BeginPlacement` 에서 100 으로
리셋된 직후 `ApplyPendingDreamstones` 가 먼저 소비한다. **할당 지점을 늘리거나(예: 판정 단계에서 미리
발번), 카운터를 쪼개거나, 리셋↔드림스톤 순서를 건드리면 골든이 즉시 움직인다.**

실리지 **않는** 것: 게이지 · 덱/큐 순서 · `_attachedTo` · `_bountyMarked` · `_activeDcEffects` ·
`DcTriggerSlot` · `SkillRuntime` 쿨다운 · `_dcHandleCounter`/`_dcInstanceCounter`.

### 조각 분해 — 동결 구간 가능 / 불가

동결 구간에 **가능**:

| 조각 | 내용 | 증인 |
|---|---|---|
| **16-A** | `DcRejectReason`·`DcApplicability`·`DreamcatcherAttachEval` → `Wassup.Sim.Match`(15-B 선례. 셋 다 이미 엔진 무참조) | compile + 기존 3 테스트 |
| **16-B** | `CommandReject` 누락 4종 **맨 뒤 append** (아래) | EditMode 사유별 단정 |
| **16-C** | 순수 판정기 `MatchCardRules` — 검증 8종을 한 함수로(`MatchPlacementRules` 템플릿) | **골든 아님.** EditMode 6~8건 |
| **16-D** | **커밋 원자화** — `검증 전량 → 적용 → 지불 → 소비`. 손패 멤버십을 ①로 끌어올리면 위 구멍 3개가 동시에 닫히고 ⑤' 롤백이 무의미해진다 | **골든 아님.** EditMode 회귀 3케이스 |
| **16-E** | 사유를 receipt 로(어댑터가 `Card_NotInHand` 로 접는 것 해소) | EditMode 6종 |
| **16-F** | UI preflight 미러 3종 소거 → 세션 질의. ⚠ `DreamcatcherAttachRequirementE2ETest` 가 그 API 를 직접 단정 — **재작성 필요** | PlayMode 재작성 |
| **16-G** | 게이지 소유권 sim 이관 + `SupportedGauge=true` | **골든 아님.** EditMode + HUD 스모크 |

동결 **해제 후**(unit 18 이후): 덱/손패 상태 이관 + `cardInstanceId` 승격(셔플이 독립 `System.Random`
이라 공용 스트림 편입 시 `meteorRng.state` 축이 움직인다) · `SkillRuntime` tick 이관(15-C 와 동일 사유) ·
`ApplyDreamcatcherCardToUnit` bake 본체(~480줄, unit 18 맥락 port) · 하네스 카드 seam 전환.

### `CommandReject` 는 불충분 — 4종 신설

현 14종은 `DcRejectReason` 8종과 손패/게이지/캡/유출/쿨다운/포탈을 덮지만, **실제 거절 5가지가 전부
`Card_NotInHand` 로 접히고 있다**:

| 거절 | 필요한 값 |
|---|---|
| 부착 제한 불일치(role/unitId) | `Card_AttachRequirementMismatch` |
| 대상이 디펜더 아님 / 적 아님 | `Card_TargetKindMismatch` |
| 이미 표식된 적 | `Card_AlreadyMarked` |
| 카드가 아무것도 기여하지 않음(`attached==0`) | `Card_NoEffect` |
| Active 인데 전투 미진행 | 기존 `Session_PhaseClosed` 재사용 |

부착 제한 **데이터 무효**는 `Session_InternalError` 로 충분(시트/배선 버그 센티넬).

### 완료 기준 (개정)

- compile 0 · EditMode 회귀 0 · **골든 7종 byte diff 0**.
- **부분 커밋 불가의 증거**: 손패 밖 entryId · 이미 부착된 entryId · 타입 불일치 3케이스에서
  **ECS 쓰기 0 · 유출 허용치 불변 · 게이지 불변**을 EditMode 로 단정.
- `CommitAttach` 안의 `RevokeDreamcatcherEffects` 호출 0(grep). **함수 자체는 남는다.**
- 카드 4변종 receipt 에 거절 사유가 실린다 — EditMode 최소 6종(신설 4종 포함).
- 게이지가 `ReadModel` 로 서빙되고 `SupportedGauge=true`(HUD PlayMode 스모크 수치 일치).
- UI preflight 미러 0(grep).
- **덱·`cardInstanceId` 승격은 이 unit 의 완료 기준이 아니다** — 동결 해제 후로 이관.

### 문서 드리프트

`BattleBridge.Dreamcatcher.cs` 는 **1,022줄**이다(문서의 "972줄" 은 stale).
