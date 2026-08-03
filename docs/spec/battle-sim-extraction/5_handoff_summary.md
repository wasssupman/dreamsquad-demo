# 5 — Handoff: 설계·스펙 세션 → 구현 세션 (별도 클론)

> 이 문서는 구현 0줄 시점의 인계다. M1+ 작업 단위는 7번부터 이어 쓴다.

## Commit

- 스펙 커밋 제목: "docs(battle-sim-extraction): ECS 제거 재설계 스펙 신설 — M0 units 0~4 + 설계 정본 v6"
- 병행 세션 커밋과 분리해 **cherry-pick으로 게시**했으므로 해시는 원 워크트리(`1833ce74`)와 origin이 **다르다** — 커밋 제목으로 식별하라.

## 상태

- **설계 완료·스펙 작성됨·구현 0.** unit 0 착수 전이며, 사용자 승인 범위는 "스펙 작성"까지다. 구현 시작 전 사용자에게 unit 0 착수를 확인하라.
- 읽는 순서: ① `README.md`(계약·마일스톤 지도) ② `docs/plans/2026-08-03-battle-sim-extraction-design.md`(설계 정본 v6 — 왜 이런 순서인지의 근거 전부) ③ `6_decision_record.md`(기각 대안·재론 조건 — 결정을 재론하고 싶어지면 먼저) ④ `0_system_order_capture.md`부터 번호순.
- 설계 정본 v6는 Claude critic 2트랙 + ECS 시맨틱 감사 6트랙 + Codex 적대 리뷰의 수렴본. Codex 원문 아티팩트는 원 세션 워크트리의 `.omc/`(비추적)에만 있으나 요지는 정본 §8에 전부 반영됨.

## 헛발 방지 (이 순서·이 결정이 나온 이유)

1. **"Mono 전환" ≠ MonoBehaviour-per-unit.** 목적지는 엔진-프리 순수 C# 틱 라이브러리 + Unity는 프레젠테이션. 유닛마다 Update() 짜기 시작하면 전부 재작업이다.
2. **골든 하네스부터 만들지 마라.** 현행 sim은 비결정이다 — 가변 프레임 dt(`BattleScaledRateManager`가 프레임당 1회 갱신), Mono/ECS 이중 시계, `SkillRuntime`의 별도 `Time.deltaTime`. **dt 상수 주입은 fixed tick이 아니다**(프레임레이트에 비례해 게임 속도가 변함). units 0~3이 골든(unit 4)에 선행하는 이유.
3. **`Entity.Index`가 발사 패턴 RNG seed에 직접 들어간다**(`AttackSystem`의 `math.hash(int2(Index, fireCount))`) + 타겟팅 동률 tiebreak. unit 1(SimEntityId) 전에 뜬 골든은 신 sim과 비교 불가 = 무효.
4. **27채널을 단일 이벤트 스트림으로 붕괴하지 마라.** 같은 틱 소비 계약인 내부 phase 메시지(CastEvents→AttackSystem, BlinkRequests)가 있다 — 이벤트 3분리(내부 phase queue / AMR / presentation projection)가 계약이다.
5. **"sim은 CoreCLR에서 돈다"고 가정하지 마라.** 클라는 Android IL2CPP다. 커맨드로그 재시뮬 검증은 M3 전까지 advisory flag만 — exact 자동 판정 금지.
6. **틱 페이즈 순서의 정본은 unit 0의 캡처다.** 기억·스케치로 짜지 마라 (예: CC 감쇠는 이동 **후** — `CcDecaySystem [UpdateAfter(MovementSystem)]`).
7. 점수는 전부 int — parity 비교는 exact(±ε 금지). `RequireForUpdate` 게이트는 35개 시스템의 **행동**이다(초안 39 는 주석 전용 4파일 포함 grep 수치. IncomingDamage 0이면 Regen 힐도 정지) — 항상-틱으로 바꾸면 조용히 깨진다.
8. **M0의 스코프는 "하네스 모드 한정 결정론"이다.** 라이브 게임 경로는 unit 1의 동률·난수열 변화(의도됨) 외에 무변이어야 한다. fixed tick 상시화는 M1 신 sim의 몫.

## 레포 고유 함정 (구현 중)

- `LoginAutoImport`가 로그인 시 SO 스탯을 시트값으로 덮는다 — 골든 오염 원천, unit 3에서 차단. 골든 diff는 configHash로 "시트 드리프트 vs 코드 회귀"부터 가른다.
- Bash 샌드박스에서 git add/commit이 무산될 수 있다(exit 0인데 index 롤백) — git 쓰기는 샌드박스 비활성으로.
- `dotnet build` "오류 0"은 거짓일 수 있다 — 신규 .cs는 csproj에 없어 건너뛴다(1초 빌드가 신호). 컴파일 검증은 Unity 리임포트/UnityMCP 기준.
- main HEAD에 PlayMode 사전 실패 존재 — `DreamcatcherEffectTest.CardBuffs_ApplyToCurrentAndFutureMatchingUnits`(별도 파일 아님, 메서드다. 초안의 "가디언 dmgTaken 여분 ×1.25" 는 코드 어서션 0.87 과 불일치 — 실행으로 재확인) — unit 4에서 수리 또는 명시 제외 결정.
- 원 워크트리는 병행 세션 다수 — 클론이므로 해당 없음. 단 머지 시 스테이징은 경로 명시로.

## Verified

- 코드 변경 0 — compile/test 영향 없음. 스펙·설계 문서만 커밋됨.

## Follow-up

- unit 0부터 번호순 구현(한 번에 한 unit, 완료 확인 후 커밋 — CLAUDE.md 워크플로우).
- 기본값 2건이 README 계약에 박혀 있다: 콘텐츠 동결(신규는 신 lib에만) · lag compensation 미채택. 이견이 생기면 구현 전에 README를 갱신하고 사용자와 확인.
- M0 완료 시 M1 units(7~)를 설계 정본의 M1 절 기준으로 분해해 이어 쓴다.
