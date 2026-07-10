# Handoff — Attack Anim Speed Match

## Commit
`feat(anim-speed): 걷기 애니↔이동속도 동기 + 공격 애니↔공속 압축` (걷기+공격 동일 커밋).

## Implemented
- 공격 애니(Spine 공격 트랙)를 실제 발사 주기에 맞춰 압축 재생(compress-to-fit) → 공속이 "빠른 스윙"으로 체감됨.
- 산식: `TrackEntry.TimeScale = max(1, animDuration / attackAnimPeriod)`,
  `attackAnimPeriod = max(cooldownDuration/attackSpeedMul, hitDelaySec)` (AttackSystem 이 계산해 이벤트에 실음).
- **별도 튜닝 SO 없음** — 애니 배율은 공격속도 필드(SO `attackCooldown` + `attackSpeedMul` 버프 + `hitDelaySec`)에서만 파생(SoT 불변).
- `TrackEntry.TimeScale` 은 공격 애니만 스케일 → skeleton.timeScale(걷기/battleScale)과 독립 곱. 걷기 로직과 무충돌.
- 하한 1.0(구조 상수): 느린 공격은 자연속도+대기(슬로모 방지). 상한 없음(attackSpeedMul [0.2,5.0] + authoring 규율이 실질 한계).
- double-fire(2연발) 는 cooldownRemaining 0화 **전**의 정상 주기를 실어 무한배율 방지.

## Key Files
- `Assets/_Project/Scripts/Battle/Combat/UnitAttackVisualEvent.cs` — `attackAnimPeriod` 필드.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 주기 계산 + enqueue(mul 계산을 enqueue 앞으로 이동).
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `PlayAttack(attackAnimPeriod)` TrackEntry.TimeScale.
- `Assets/_Project/Scripts/Presentation/SpineUnitPool.cs` + `BattleBridge.cs` — 주기 전달 배선.

## Verified
- compile 클린(에러 0). Play 라이브 역산: interval 0.8→TS1.5, 1.2→1.0, 2.0→1.0(하한), 0.48→2.5, 0.24→5.0(캡 없음).
- 산식 critic 1회 → **불변 법칙 준수** 판정. MEDIUM #1(hitDelay 포함)·LOW #4(주석) 반영. MEDIUM #2(hit 프레임 정렬)·LOW #3(느린 공격 floor) 후속/수용.
- 사용자 확인: 쿨타임 6에서 애니 불변 = 하한 1.0 동작 "딱 좋음" 통과.

## Notes
- SoT: `attackCooldown` 은 "seconds between attacks"(작을수록 빠름), attack speed 아님. 하한 없음(0.001 도 통과).
- 시뮬 rate/데미지 불변 — 뷰는 주기 숫자만 읽음. ECS 경계 유지.
- 초안의 `AttackAnimSpeedStyle` SO+에셋+씬배선은 "별도 데이터 금지" 결정으로 제거됨(재도입 금지).

## Follow-up
- hit 프레임(hitDelay)을 압축 스윙 접촉점에 정렬(critic MEDIUM #2) — 순수 시각 desync.
- 초고속 상한/다단히트 전환, 적 vs 디펜더 거동 분리 — 필요 시.
