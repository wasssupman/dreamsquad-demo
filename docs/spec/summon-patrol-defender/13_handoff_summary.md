# 13 — handoff summary (units 8·10·11·12, 2026-08-12)

unit 8 하나로 시작해 파생 작업 셋이 붙었다. 다음 세션이 읽을 순서와 위험 지점만 남긴다.

## Commit

| 커밋 | 범위 |
|---|---|
| `fix(combat)` unit 11 | `AttackReach` 신설 + 사거리 2단 게이트를 소비처 5곳에 통일 |
| `feat(summon-patrol)` unit 10 + unit 6 철회 | 유닛별 애니 구조 · 아군 링 제거 |
| `fix(outgame)` unit 12 | 고유 리그의 아웃게임 호환(크래시 2 + 크기 임시보정) |
| `chore(data)` | 고유 스파인 에셋 · 시트 밸런스 패스 · 방어유닛 크기 ×1.3 |
| `docs(summon-patrol)` | spec 갱신 |

## Implemented

- **unit 8** — CH1(소환사)·Doll(소환물) 고유 스켈레톤. 이 게임 최초의 비-`Casual Character` 리그. **코드 0**(파츠 경로는 `partSkins` 를 비우면 꺼진다).
- **unit 6 철회** — 발밑 아군 링 제거. 표식이 필요했던 근거(*"적과 같은 스켈레톤·같은 실루엣"*)를 unit 8 이 없앴다.
- **unit 10** — idle 변형 풀 · 조건부 루프 오버라이드 · 오버라이드 해제 시 원샷. 뷰 API 2개(`SetLoopOverride`/`ClearLoopOverride`)만 공개하고 조건은 Bridge 가 소유한다.
- **unit 11** — 사거리 2단 게이트(셀 → 물리거리). `AttackReach` 하나를 **5곳**이 공유한다.
- **unit 12** — 로딩 러너 크래시 · 스쿼드 상세 동일 지뢰 · 아웃게임 크기 임시보정.

## Key Files

- `Battle/Combat/AttackReach.cs` — **먼저 읽을 것.** 소비처 5곳 목록과 «한 곳만 고치면 무엇이 깨지는가»가 헤더에 있다.
- `Battle/Effects/PatrolAreaMath.cs` — `StepDir` → `CloseInDir`(격자 도착 후 물리 접근)
- `Presentation/SpineUnitView.cs` — 루프 오버라이드 · idle 변형 순환
- `Bridge/BattleBridge.cs` — `SyncSummonerAnimationState` / `IsPatrolAlive`
- `Core/SceneTransition.cs` · `UI/Outgame/SquadUnitDetailView.cs` — 리그별 초기 스킨

## Verified

- EditMode **2192 중 2189 통과 · 실패 0 · 스킵 3**(기존 `[Ignore]`)
- PlayMode `PatrolDefenderPlayTest` **2/2**
- 컴파일 에러 0 · 콘솔 에러 0
- 사용자 Play 확인 완료
- 투트랙 코드 리뷰(code-reviewer + ecs-reviewer) 후 블로커 전부 수정하고 재검증

## Notes — 되돌리면 안 되는 것

1. **사거리 술어는 5곳이 함께 본다.** 락/커밋 재판정(`AttackSystem` 3곳)을 셀 판정으로 되돌리면 «2칸에서 때린다»가 락 경로로 되살아난다. 리뷰가 실제로 잡아낸 자리다.
2. **`CloseInDir` 은 구역·벽을 본다.** 빼면 박스 이탈 후 경계 진동, 또는 벽에 밀려 «걷는 애니로 제자리».
3. **idle 변형은 `loop:true` 로 이어붙인다.** `loop:false` 로 바꾸면 `IsLocomotionLoopPlaying` 과 오버라이드 게이트가 동시에 오작동한다.
4. **`SetLoopOverride` 는 «값이 같으면 반환»이 아니다.** 트랙이 실제로 그 루프를 돌 때만 조기 반환한다 — 원샷 도중 요청이 오면 매 프레임 재시도해야 한다.
5. **`outgameScaleMul` 은 임시다.** 리그가 정규본이 되면 1 로 되돌리고 필드째 제거한다.

## 함정 (이번에 실제로 당한 것)

- **에셋 손편집이 날아간다.** Unity 가 그 에셋을 로드한 상태에서 시트 임포트가 돌면 **메모리 객체가 디스크를 덮어쓴다.** `idleVariants` 저작이 그렇게 사라졌고 코드 리뷰가 잡았다. `.asset` 값 저작은 Unity 를 거칠 것.
- **Play 직후 EditMode 테스트는 거짓 실패한다.** 시트 리프레셔가 카드·유닛 SO 를 *in-memory only* 로 덮으므로, 도메인 리로드 한 번 태우고 돌려야 한다(드림캐쳐 카드 테스트 2건이 그렇게 빨갛게 났다).
- **테스트 수를 확인할 것.** 신규 테스트 파일이 asmdef 참조 누락으로 컴파일에 실패하면 Unity 는 **직전 빌드로 돌고 "실패 0"** 을 낸다. 2184 가 그대로였던 것으로 발견했다(spine 참조 누락).

## Follow-up

README 하단 "후속 후보" 참조. 우선순위 높은 셋:
1. 사거리 술어 미러 동치성 PlayMode 단언 — 이번 리뷰가 우회 경로를 찾아낸 축이다
2. 순찰병 타격 성립 PlayMode 단언 — 교착 클래스 전체를 덮는 가장 싼 그물
3. 아웃게임 크기 정규화 — `outgameScaleMul` 제거 조건
