# 20 — A/B parity + 성능 게이트 + 스왑 (M1 종료)

## 목적

M1 의 마지막 unit. 신 sim 이 구 sim 과 **행동 동치**임을 골든으로 증명하고, Burst 상실 성능이
실기기에서 견디는지 재고, 그 뒤에 **세션 구현체를 교체**한다. 스왑이 1줄이 되도록 units 12~19 가
길을 닦아 놓은 상태다.

## 변경 대상

- 신규 A/B 러너: 같은 `MatchConfig`·seed·입력 스케줄로 구 sim(`LegacyMatchSessionAdapter`)과
  신 sim(`LocalSession`)을 각각 구동해 `LegacyTraceV0` 를 2개 뽑고 비교. unit 4 의 하네스·골든 러너 재사용
- 성능 계측 훅: tick 소요 p95/p99 · steady-state GC 할당
- 스왑 지점: 세션 구현체 생성 1곳(`GameManager`/`BattleBridge` 배선) — `LocalSession` 로 교체
- 구 ECS: **삭제하지 않는다.** asmdef 참조를 끊어 무력화만(정본 M1-7 — 물리 제거는 M2)

## 구현

- **parity 판정 축**(정본 §2·unit 4): 커맨드 receipt · semantic 이벤트 시퀀스 · 틱별 read model ·
  최종 상태+RNG 해시 · 점수(int) = **exact** / 연속 물리값(위치·잔여시간) = epsilon.
  동률 5지점은 실패로 치지 않되 발생 시 로그(청사진 ③ §6).
  ⚠ 구 trace 의 **tick 귀속 시프트**(16채널 `tick-1` / 2채널 `tick`)는 비교기가 보정한다 —
  신 sim 은 발생-tick 통일이므로 이건 의도된 차이다(청사진 ① §4·§9).
- **성능 게이트**(정본 §8, 스왑 **전** 필수): Android ARM64 IL2CPP 실기기, 피크 웨이브 soak.
  tick p95/p99 와 GC steady-state 를 기록하고 **프레임 예산 초과 시 스왑 보류** — Burst 없이 도는
  대가를 여기서 확인한다. 초과 시 후보: 핫 루프 수동 최적화 · 틱레이트 하향 · 부분 병렬화.
- **RTT 150ms 수용 리뷰**(상설 가드 ③): `LocalSession` RTT 노브를 켜고 전 스킬·카드 디자인 리뷰.
  통과 못 하는 스킬이 나오면 lag compensation 재론(ADR D7 재론 조건).
- 스왑 후 **롤백 경로 확인**: 구현체를 되돌리면 구 sim 으로 복귀 가능함을 1회 실측(스왑 반경이
  1곳이라는 주장의 증명).

## 완료 기준

- 골든 7종 A/B parity: exact 축 **불일치 0**, epsilon 축 허용 범위 내, 동률 로그만 잔존.
- 교차 골든(정본 결정 #4): Editor · **Android IL2CPP** 실행에서 같은 결과 — 이식 가능성의 증거.
- 성능: ARM64 IL2CPP 피크 웨이브에서 tick p95/p99·GC 수치 기재 + 프레임 예산 판정 PASS.
- RTT 150ms 리뷰 통과(실패 스킬 목록 0 또는 재론 결정 기재).
- 스왑 커밋의 diff 가 **세션 구현체 생성 1곳**(+ 배선). 롤백 실측 완료.
- PlayMode 전체 + EditMode 전체 실패 0, 사용자 Play 확인(전투 1판 체감).
- → **M1 종료.** README 상태를 "M1 완료"로 갱신하고 `m1_handoff_summary.md` 작성. M2 는
  Entities 패키지 물리 제거부터 시작한다.
