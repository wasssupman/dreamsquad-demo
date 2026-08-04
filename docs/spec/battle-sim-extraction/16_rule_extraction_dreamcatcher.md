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
