# 4 — LegacyTraceV0 골든 하네스

## 목적

M1 신 sim과의 A/B parity 기준선. 하네스 실행(units 2·3 위)에서 27채널 이벤트를 tick 스탬프·`SimEntityId` 축으로 기록한 `LegacyTraceV0`를 만들고, seed 코퍼스를 골든으로 저장한다. **직렬화 왕복을 통과시켜 기록**한다 — 네트워크에 못 탈 페이로드(오브젝트 참조 등)를 첫날부터 걸러내는 가드다. parity 기준과 동률 예외를 여기서 명문화한다.

## 변경 대상

- 신규 trace 기록기 (예: `Assets/_Project/Tests/Harness/LegacyTraceRecorder.cs`) — Bridge의 채널 drain 지점 tap (drain 소비 자체는 무변, 관찰만)
- 신규 `LegacyTraceV0` 스키마 — 헤더(`configHash`·seed·틱레이트·버전) + tick별 이벤트 레코드 + 최종 점수(int 4종)·상태 해시
- 골든 코퍼스 저장 위치 (예: `Assets/_Project/Tests/Golden/` — 트래킹 대상) + 재생성 메뉴
- EditMode/배치 러너 — 코퍼스 실행·비교 테스트

## 구현

기록 파이프: 이벤트 → 직렬화 → 역직렬화 → 재직렬화 byte 동일 검증 → 저장. 코퍼스는 seed·맵·덱 조합 N개(최소: 일반 판·보스 웨이브 판·멀티골 맵·드림캐쳐 다용 판·강제 웨이브·동시 사망 유발 시나리오·restart). **parity 기준(명문)**: semantic 이벤트 시퀀스·킬/유출 수·점수(int)·최종 상태 해시 = exact, 연속 물리값(위치·잔여시간) = epsilon. **동률 예외 목록(2026-08-03 교차검증 정정)**: KillAttribution 등량 데미지(이미 `KillAttribution.cs` 주석에 계약화) · Aggro capacity FIFO(실질 결정자는 AttackSystem 청크 순회 순서) · Cc/Stat merge 동키 충돌 · Dot merge 동키 충돌 · (unit 1이 해소한 HazardCast tiebreak 제외) · HazardSingleton 셀 순회(순서 미규정이나 메인스레드 삽입이라 재현은 됨) — 이 지점들의 차이는 parity 실패로 치지 않되 발생 시 로그. **ApplyStack 은 동률 지점 아님**(stackCount 가환 누적) — 단 병합 경로별 duration 정책 불일치(값 축 LWW · 지속 축 max · tickTimer carry-over, 예외로 ApplyStack 의 `remaining` 만 무조건 덮어쓰기)는 이식 계약으로 명문화한다. 사전 실패 테스트는 `DreamcatcherEffectTest.CardBuffs_ApplyToCurrentAndFutureMatchingUnits`(PlayMode — 별도 파일 아님. 초안의 "가디언 dmgTaken ×1.25" 서술은 코드 어서션 0.87 과 불일치라 실행 재확인 필요)이며 수리 또는 코퍼스에서 명시 제외를 결정해 기록.

## 확정 구현 (2026-08-04)

### trace 계약

- `LegacyTraceV0`는 `configHash`·match seed·tick rate·deck id·map goal 수, tick read model, event sequence, 최종 점수 4종과 상태 SHA-256을 담는다. Unity object, `Entity`, `NativeContainer`는 DTO에 넣지 않고 Bridge에서 plain scalar/string으로 정규화한다.
- battle clock은 JSON double 왕복 시 `0.15000000223517419 → 0.1500000022351742`로 표현이 바뀌는 것을 실제 하네스가 검출했으므로 `battleClockMicros` 정수로 저장한다. 저장 전 serialize → deserialize → serialize byte 동일 검사를 반드시 통과한다.
- 27개 운영 채널은 헤더에서 전부 manifest한다. 그중 **Bridge가 원래 소비하는 18개 출력 채널만 event stream에 직렬화**한다. 나머지 9개(`AggroHit`, `Cast`, `ThreatHit`, `BlinkRequest`, `EnemyCc`, `DotApply`, `CcClear`, `StatModifierApply`, `StackModifierApply`)는 ECS 내부 phase 전달 수단이라 M1 Mono sim의 외부 계약으로 승격하지 않고 `internalPhaseChannels`에 명시 제외한다. 이 구분은 소비자를 추가하거나 큐를 복제해 라이브 순서를 바꾸지 않기 위한 것이다.
- 입력 수락 여부는 27채널과 별도인 `CommandReceipt`로 기록한다. 이벤트 producer tick은 `SimEntityId` 등록부를 통해 정규화하며 `Entity.Index` 폴백은 금지한다.
- parity 비교는 이벤트 순서·카운트·점수·정규화 상태 해시를 exact로 본다. 위치·잔여시간 같은 연속값의 epsilon 비교기는 M1 A/B runner가 소유하며, 위 동률 예외가 발생하면 실패 대신 별도 로그를 남긴다.

### 골든 코퍼스

추적 위치는 `Assets/_Project/Tests/Golden/LegacyTraceV0/`다. 다음 7개를 각각 새 Play 세션에서 2회 실행하고, 두 JSON이 byte 동일할 때만 골든을 교체한다.

| 파일 | seed | 맵 풀 index | 추가 입력/검증 |
|---|---:|---:|---|
| `normal.json` | 202608041 | 0 | 기본 배치·전투 |
| `boss_wave.json` | 202608042 | 5 | tick 0에 5웨이브 호출, boss 관측 필수 |
| `multi_goal.json` | 202608043 | 2 | map goal 2개 이상 필수 |
| `dreamcatcher_heavy.json` | 202608044 | 1 | tick 10/20/30 카드 3건 수락 필수 |
| `forced_wave.json` | 202608045 | 3 | tick 0/20/40 강제 웨이브 |
| `simultaneous_death.json` | 202608046 | 4 | 같은 tick `EnemyKilled` 2건 이상 필수 |
| `restart.json` | 202608047 | 0 | 20 tick prelude 뒤 teardown/re-arm, configHash 유지 필수 |

재생성은 Unity 메뉴 `Wassup/Battle/Sim Harness/Regenerate LegacyTraceV0 Goldens`를 사용한다. batch에서는 같은 진입점을 `-executeMethod Wassup.EditorTools.LegacyTraceGoldenRunner.RegenerateGoldens`로 호출한다. 원래 `DevMapOverride` 값은 성공·실패 양쪽에서 복원한다.

### 사전 실패 테스트 처리

`DreamcatcherEffectTest.CardBuffs_ApplyToCurrentAndFutureMatchingUnits`를 Unity PlayMode에서 단독 재실행했고 **1/1 PASS**했다. 현재 어서션은 guardian `dmgTakenMul = 0.87 ± 0.01`이며 초안의 `×1.25` 서술이 잘못된 것이었다. gameplay 수정이나 코퍼스 제외는 하지 않는다. 골든 러너는 외부 dreamstone 오염을 피하려고 매 시나리오 `SetDreamstones(null)` 후 placement를 구성한다.

## 완료 기준

- 같은 seed 2회 실행 → trace diff **0** (코퍼스 전 시나리오).
- 직렬화 왕복 무손실 검증 통과.
- 골든 코퍼스 N개 저장 + 재생성 절차 문서화.
- parity 기준·동률 예외·CardBuffs 처리가 이 문서에 확정 기록됨. → **M0 완료. M1 units는 이 기준선 위에서 시작.**
