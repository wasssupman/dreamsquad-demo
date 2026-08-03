# 4 — LegacyTraceV0 골든 하네스

## 목적

M1 신 sim과의 A/B parity 기준선. 하네스 실행(units 2·3 위)에서 28채널 이벤트를 tick 스탬프·`SimEntityId` 축으로 기록한 `LegacyTraceV0`를 만들고, seed 코퍼스를 골든으로 저장한다. **직렬화 왕복을 통과시켜 기록**한다 — 네트워크에 못 탈 페이로드(오브젝트 참조 등)를 첫날부터 걸러내는 가드다. parity 기준과 동률 예외를 여기서 명문화한다.

## 변경 대상

- 신규 trace 기록기 (예: `Assets/_Project/Tests/Harness/LegacyTraceRecorder.cs`) — Bridge의 채널 drain 지점 tap (drain 소비 자체는 무변, 관찰만)
- 신규 `LegacyTraceV0` 스키마 — 헤더(`configHash`·seed·틱레이트·버전) + tick별 이벤트 레코드 + 최종 점수(int 4종)·상태 해시
- 골든 코퍼스 저장 위치 (예: `Assets/_Project/Tests/Golden/` — 트래킹 대상) + 재생성 메뉴
- EditMode/배치 러너 — 코퍼스 실행·비교 테스트

## 구현

기록 파이프: 이벤트 → 직렬화 → 역직렬화 → 재직렬화 byte 동일 검증 → 저장. 코퍼스는 seed·맵·덱 조합 N개(최소: 일반 판·보스 웨이브 판·멀티골 맵·드림캐쳐 다용 판·강제 웨이브·동시 사망 유발 시나리오·restart). **parity 기준(명문)**: semantic 이벤트 시퀀스·킬/유출 수·점수(int)·최종 상태 해시 = exact, 연속 물리값(위치·잔여시간) = epsilon. **동률 예외 목록**: KillAttribution 등량 데미지·Aggro capacity FIFO·Cc/Stat·Stack/Dot merge 동키 충돌·(unit 1이 해소한 HazardCast tiebreak 제외)·HazardSingleton 셀 순회 — 이 지점들의 차이는 parity 실패로 치지 않되 발생 시 로그. 사전 실패 테스트(CardBuffs PlayMode — main HEAD부터 가디언 dmgTaken ×1.25 실패)는 수리 또는 코퍼스에서 명시 제외를 결정해 기록.

## 완료 기준

- 같은 seed 2회 실행 → trace diff **0** (코퍼스 전 시나리오).
- 직렬화 왕복 무손실 검증 통과.
- 골든 코퍼스 N개 저장 + 재생성 절차 문서화.
- parity 기준·동률 예외·CardBuffs 처리가 이 문서에 확정 기록됨. → **M0 완료. M1 units는 이 기준선 위에서 시작.**
