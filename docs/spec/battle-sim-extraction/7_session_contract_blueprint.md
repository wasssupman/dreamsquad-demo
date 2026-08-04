# 7 — 청사진 ①: IMatchSession 세션 계약

## 목적

스왑 반경을 "세션 구현체 교체 1곳"으로 줄이는 전제(설계 정본 M1-3, ADR D4). `Local`/`Remote`/`Replay`/`Ghost` 4구현이 공유할 표면을 **백지에서**("애초에 plain C# 로 설계했다면") 필드 수준으로 확정한다. 이 문서군이 3장 캡·1주 timebox 의 1장이다.

## 변경 대상

- 신규 `docs/spec/battle-sim-extraction/m1_blueprint_session_contract.md` — 코드 0, 문서가 산출물

## 구현

계약에 담을 것 (M0 산출물에서 출발하되 번역본이 되지 않게 재도출):

- **SendCommand** — 플레이어 동사 전체: 배치·재배치·배치 스킬·드림캐쳐 카드(**다단계 트랜잭션의 원자 커맨드화** — 효과→지불→소비가 한 커맨드, 설계 정본 §8 MAJOR)·강제 웨이브·restart. 필드: `matchId/clientSeq/tick희망/페이로드`.
- **CommandReceipt** — unit 4 가 이미 기록 중인 스키마(`matchId/configHash/clientSeq/acceptedTick/order/rejectReason`)에서 출발, 멱등성·순번 규칙 명문.
- **OnTickEvents** — 이벤트 3분리 재확인: LegacyTraceV0 의 **출력 18채널**이 semantic AMR 후보, `internalPhaseChannels` 9 는 sim 내부(계약 밖). 각 이벤트의 필드에서 `Entity` 를 걷어내고 `SimEntityId` 축으로 정의. presentation projection 은 semantic 의 파생임을 명시.
- **InstallSnapshot** (day-1) — 범위 열거(설계 정본 §8): future wave·예약 커맨드·pending projectile/hazard·RNG 서브스트림·ID allocator + `snapshotTick/eventSeq/lastAcceptedCommandSeq/sessionEpoch/configHash`. 재접속 백로그 exactly-once 규칙.
- **읽기 모델** — HUD/뷰가 폴링하는 값의 전수(현 Bridge 공개면 실측: `NextWaveAvailable`·클럭·점수·게이지 등)를 tick-스탬프드 스냅샷 뷰로.
- **고스트 프로젝션** — 배치·점수·웨이브 마일스톤(웨이브 인덱스 정렬) 필터 필드.
- **LocalSession RTT 주입 노브** — 상설 가드 ③(엔지니어링 도구가 아니라 수용 기준).

## 완료 기준

- 커맨드/receipt/이벤트/스냅샷/읽기 모델이 **필드 수준**으로 열거되고, LegacyTraceV0 스키마와의 대응(동일/개명/신설/제외)이 표로 명시된다.
- 이벤트 18채널 각각에 대해 "semantic 에 남는가 / presentation 파생인가" 판정이 있다.
- critic 리뷰 1회(아키텍처 렌즈) 반영. 코드 변경 0.
