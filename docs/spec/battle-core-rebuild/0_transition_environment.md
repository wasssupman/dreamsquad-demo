# 0 — 전환 환경 (조각 0)

> 완전성 리뷰(`docs/plans/2026-09-22-battle-core-rebuild-census/06_plan_gaps_review.md`) 13건과 순서 오류 6건의 자리.
> 이 unit 은 규칙을 옮기지 않는다 — **옮기는 작업이 안전하게 돌 환경**을 만든다. 코드 0줄이 아니다(훅·스크립트·씬·에이전트 파일).

## 목적

조각 A 첫 커밋 전에, ① 새 코어가 돌 자리 ② 옛 전투를 얼려 둘 장치 ③ 새 코드를 볼 리뷰 도구 ④ 무엇을 어디로 옮기나의 장부가 전부 있어야 한다. 없으면 조각 A~D 가 무리뷰·무격리로 진행되고 unit 8 에서 브리지를 지우는 순간 91개 튜닝값과 도구 6개가 사라진다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 새 씬 | `Assets/_Project/Scenes/BattleCoreScene.unity` (신설 · 옛 `BattleScene.unity` 무변) |
| 브랜치·워크트리 | 브랜치 `rebuild/battle-core` · 워크트리 `/Users/sy/dev/wassup-core` |
| 동결 훅 | `.githooks/commit-msg`(pre-commit 은 메시지를 못 본다) + `git config core.hooksPath .githooks` (문서화: CLAUDE.md 상태 절 · README 계약 10) |
| 리뷰 도구 | `.claude/hooks/ecs-review-detector.mjs` (경로 추가, 완료) · `.claude/agents/core-reviewer.md` (신설, 완료) · `.codex/hooks/ecs-review-detector.mjs` (**보류** — 다른 세션이 편집 중인 dirty 파일. 그 세션 커밋 뒤 같은 패치) |
| 장부 | `docs/spec/battle-core-rebuild/ledgers/{bridge-methods,bridge-fields,rule-holders,tools,rules}.md` |
| 대조 스크립트 | `Tools/battle-core-rebuild/check_ledgers.py` (`--generate` 로 장부 초안 생성·분류 보존 / 인자 없이 대조, 불일치 = exit 1) |
| 착수 대기 spec 4건 | 각 README 상태 라인 한 줄 추가 |

## 구현

1. **씬.** `BattleCoreScene` 은 옛 브리지·뷰 풀을 **한 개도 참조하지 않는다.** 카메라·라이트·`BattleDriver` 자리(빈 GameObject)·HUD 캔버스 골격만. 뷰 풀은 unit 5 가 붙인다. 진입은 dev 메뉴 토글(`GameManager` 무변 — 씬 이름만 다르게 로드).
2. **브랜치·워크트리.** `git worktree add ../wassup-core -b rebuild/battle-core`. Unity 는 워크트리를 별개 프로젝트로 연다(testrig 선례). main 머지는 조각 B·D·E 경계에서 리뷰 후, **squash 금지**(unit 단위 커밋 보존). 워크트리 안에서도 스테이징은 경로 명시.
3. **동결 훅.** (`commit-msg` 훅 — 메시지와 스테이징을 둘 다 봐야 하므로) 스테이징에 `Assets/_Project/Scripts/Battle/**` 또는 `Scripts/Bridge/**` 가 있고 커밋 메시지 첫 줄에 `[old-battle]` 이 없으면 거부. 메시지는 「옛 전투는 동결 — 버그픽스면 `[old-battle]` 태그, 규칙 변경이면 새 코어에서」. 훅은 리포에 커밋되고 `core.hooksPath` 설정은 README 에 적는다(클론마다 1회).
4. **착수 대기 spec 4건 판정** (README 상태 라인에 기록): `wide-board-camera` 계속(판 밖) · `squad-slots-ten` 계속(아웃게임) · `wide-board-content` **보류 → 새 코어 unit 4 뒤** · `heart-stress-axis/12` **보류 → 새 코어 unit 4 에서 `HeartMeter` 로**.
5. **리뷰 도구.** 감지기 정규식에 `^Assets/_Project/Scripts/BattleCore/` 추가 → 매치 시 `core-reviewer` 로 라우팅. `core-reviewer.md` 체크리스트 = CLAUDE.md 「새 전투 코어 — 절대 제약」 6항 + README 계약 1·2·7·12 (매니저 이름 금지 · `UnityEngine` 참조 0 · 커맨드/이벤트 분리 · 소멸 이벤트 누락 · 담당자 밖 판정). 옛 경로는 `ecs-reviewer` 그대로.
6. **장부 3종 + 처분표 + 분류표.**
   - `bridge-methods.md`: 선언 369 → `{담당자 | 삭제 | 뷰 풀 | 미정}`. 「미정」은 0 이어야 조각 E 진입 가능.
   - `bridge-fields.md`: 직렬화 필드 91 → 새 주인(담당자별 SO 또는 뷰 풀 컴포넌트) + 현재 씬 값 스냅샷(대조표).
   - `rule-holders.md`: 브리지 밖 10 파일(`TilemapMapView` · `GameManager` · `DreamcatcherHandController` · `DraftController` · `SkillLoadoutController` · `CostRuntime` · `PlacementInput` · `SkillRuntime` · `PlacementCooldownRuntime` · `MatchTally`)의 **규칙 문장**을 뽑아 담당자로 귀속. `TimeManager` 는 예외 표기.
   - `tools.md`: 브리지/Entities 결합 도구 10개 → `{새 코어용 재작성(시점) | 은퇴}`. 삭제 예정 폴더 안 6개는 재작성 시점을 조각 C·D **앞**으로.
   - `rules.md`: census 「코드에만 박혀 있고 문서에 없는 규칙」 전량(6영역 합산) → `{필수 | 보류 | 제거}` + 한 줄 근거. 「필수」의 정의 = 없으면 조용히 망가지는 것.
7. **대조 스크립트.** `check_ledgers.py` 가 `bridge-methods.md` 의 이름을 코드 선언과 대조(누락·유령 둘 다 실패), `bridge-fields.md` 를 `[SerializeField]` 선언과 대조. EditMode 가 아니라 CLI — 훅·CI 없이 손으로 돈다.
8. **서버 payload 판정 기록.** v1 제출은 `KillScoreTimed` 만. `WaveClear`·`TimeAttack` 은 `submitsReport=false`. `TournamentMatchReporter.ReportResult` 시그니처 무변. README 후속 후보에 「서버 API 확장」.
9. **`SimEntityId` 센티널 확정.** 새 코어: Match 호스트 = 0 · 유닛/투사체 = 1 부터 스폰 순번 · `None = -1`(정렬에 절대 안 들어감). 옛 코어(`int.MaxValue` 미발급)와 다르다 — 골든 축이 갈리므로 옛 트레이스와의 비교는 **id 가 아니라 순서**로 한다(계약 3).

## 이식 제외

N/A — 이 unit 은 규칙을 옮기지 않는다.

## 완료 기준 (2026-09-23 실측)

- [x] `BattleCoreScene.unity` 존재(YAML 직접 생성: 카메라·조명·`BattleDriver`·`HudCanvasRoot`, 브리지 참조 0). **부팅 콘솔 에러 0 은 Unity 에디터가 열릴 때 확인**(작성 시점 MCP 세션 없음).
- [x] `git worktree list` 에 `/Users/sy/dev/wassup-core [rebuild/battle-core]`.
- [x] 훅 검증(dry-run): `Scripts/Battle/_hooktest.cs` 스테이징 + 태그 없는 메시지 → exit 1 + 거부 메시지 / `[old-battle]` 태그 → exit 0.
- [x] 감지기: `BattleCoreScene.unity` 스테이징 상태에서 「리뷰」 프롬프트 → `<core-review-context>` 주입(core-reviewer 안내).
- [x] `check_ledgers.py` exit 0 — 메서드 **367**(스크립트 파서 기준; 리뷰의 369 는 grep 방식 차이) · 필드 **91** = 씬 키 91. 미정 128(조각 E 진입 조건 0 — 유닛별 이전 시 확정).
- [ ] `rules.md` 행 수 = census 합계(에이전트 작성 중) · `rule-holders.md`(에이전트 작성 중).
- [x] 착수 대기 spec 4건 README 에 판정 한 줄.
- [x] 8·9 의 결정 기록.
