# 19 — gameplay 시계 정책 + 커맨드로그 이중 기록

## 목적

스왑 전에 남은 두 계약을 닫는다. ① 시계: UI 제스처 6종이 전투 시뮬 클럭을 늘리는 구조를 처분한다
(청사진 ① §2·§10-4). ② 커맨드로그: 무결성 정본을 **기록만** 시작한다(ADR D2 — 재시뮬 판정은 M3 전까지
advisory).

## 변경 대상

- **시계 정책**: `TimeManager` Battle 도메인 lease 7 요청자. `MenuPopup`(scale 0·priority 100)과
  튜토리얼 힌트(scale 0)는 **`Pause` 커맨드로 승격**. 나머지 5(드래그 배치·재배치·조준·인스펙트·
  손패 열기)는 **뷰 전용 시간으로 격하**
- `Sim` 틱 드라이버 — 라이브 fixed tick 상시화(M0 은 하네스 한정이었다). `_battleClock` 을 Bridge 에서
  sim 으로 최종 이관(unit 14 가 유보한 것)
- 신규 `Assets/_Project/Scripts/Core/Session/CommandLogWriter.cs` — 수락 커맨드 + receipt 를
  로컬 파일로 append. **전송 없음**(네트워크 코드 금지 유지)

## 구현

- ⚠ **격하는 행동 변경이다**(리뷰 M10): `CostRuntime`·`PlacementCooldownRuntime` 이 Battle dt 로
  tick 하므로 슬로모가 지금 **코스트 회복·배치 쿨다운을 늦춘다**. 뷰 전용으로 내리면 그 rate 가
  바뀐다 → **골든 재생성이 필요한 최초의 unit**이고, 재생성 전에 변경 내용을 문서에 명시한다
  (`configHash` 는 불변이므로 diff 원인이 코드 변경임이 판독 가능 — unit 3 계약).
- 대안 검토 결과 기록: (a) 슬로모를 sim 커맨드로 승격 = Remote 에서 남의 sim 을 늘리므로 기각
  (청사진 ① §2), (b) 통화만 unscaled 로 = 슬로모 중 코스트가 정상 회복돼 조준이 이득이 됨 → 밸런스
  변경, (c) 뷰 전용 격하 = **채택**(슬로모는 그림만 늦추고 sim 은 계속 흐른다).
- 커맨드로그 스키마 = receipt + 커맨드 페이로드(청사진 ① §3). 매치당 1파일, `configHash` 헤더 동봉.

## 완료 기준

- compile 0 · EditMode 회귀 0 · PlayMode 스모크(일시정지·드래그 슬로모·조준이 각각 의도대로).
- **골든 7종 재생성 + 변경 사유 기재** — 이 unit 이 유일하게 골든을 바꾼다. 재생성 전/후 diff 를
  읽어 "슬로모 구간의 통화 누적"만 달라졌음을 확인(다른 축이 움직이면 회귀다).
- 커맨드로그가 매치당 1파일 생성되고 receipt 순번에 갭이 없다(EditMode 로 왕복 파싱).
- 라이브가 fixed tick 으로 돌고 `_battleClock` 이 Bridge 에 없다(grep).
