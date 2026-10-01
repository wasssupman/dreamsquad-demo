# design-blueprint — 현재 설계의 윤곽(blueprint) 다시 쓰기

**상태: 완료 2026-10-01 — 범위 축소.** 처음 합의한 7장 구조(units · skills · outgame · tournament · data-pipeline · decisions)는 쓰지 않았다.
사용자가 목적을 「세션에 넘길 가이드」로 좁혔고(2026-10-01), 그 관점에서 `CLAUDE.md` 재작성 + 한 장짜리 현시점 요약으로 바꿨다.

## 목적

새 세션이 `CLAUDE.md`(제약 · 함정 · 검증) → `docs/blueprint/README.md`(무엇이 있고 · 어떻게 맞물리고 · 어디가 정본인가) 두 장으로 현재 설계를 잡는다.
blueprint 는 현재 설계의 입구(지도)다. 「정식 프로젝트로 옮겨 갈 입력 묶음」(옛 브랜치 `blueprint` 의 성격)이 아니다.

## 한 일

| 산출물 | 내용 |
|---|---|
| `CLAUDE.md` 재작성 | 356줄 → 105줄. 「제약 — 어기게 되면 멈추고 묻는다」(이름으로 부른다 — 코드 주석의 옛 번호와 겹치지 않게) · 스택 · 전투 코어 · 값의 정본 · Unity 함정 · 검증 · 일하는 방식 · git. 옛 번호 제약의 행방은 `docs/reference/battle-core-architecture.md` §8 머리의 대조표 |
| `docs/blueprint/README.md` | 한 장 요약 — 한 줄 · 지금 단계 · 한 판의 생애 · 시스템 지도(영역 → 정본 → 코드 입구) · 값이 흐르는 길 · 알고 뺀 것 · 열린 것 · 읽는 순서 |
| 옛 blueprint 3장 삭제 | 몸 원장 2 · 전복 인벤토리 — 살아 있는 내용(저지 없음 · 몸 = 그림자 = 판정 · 아트 발주 규격)은 blueprint 6절로 흡수. 티어별 반경 · 유효 사거리 표는 옮기지 않았다(정본은 `DefenderUnitData.BodyRadiusTiles` · `AttackUnitData.BodyRadiusTiles`) |
| 옮긴 절차 | spec 진행 규칙(작업 단위 확인 · 완료 줄 · 파이프라인 커버리지 · spec 에 넣지 않는 것) → `docs/spec/README.md` 「진행 규칙」 |
| 고친 낡은 사실 | lessons 의 Spine 「4.2 고정」 → 4.3(2026-08-11 교체) · 테스트 lane 설명 · PlayMode 실행 범위 · `core-reviewer` 모델 고정 해제 |

## 작성 규칙 (blueprint 를 고칠 때)

- **윤곽만** — 무엇이 있고 · 어떻게 맞물리고 · 왜 그런가. 수치는 적지 않고 정본 위치만 가리킨다.
- **한 사실은 한 곳** — `docs/reference` 가 이미 말하는 것은 링크한다.
- **게임 언어가 먼저**, 코드 이름은 괄호. 옛 동작을 현재처럼 설명하지 않는다(알고 뺀 것으로 적는 것은 된다).
- 머리의 「기준 커밋」을 갱신한다. 설계를 바꾸는 spec 은 끝날 때 해당 줄을 고친다(`docs/spec/README.md` 「진행 규칙」).

## 후속 후보

- **reference 정합** — 설계 전환 전에 쓴 문서 판정(유지 · 흡수 · 삭제): `dreamcatcher-portability.md` · `review-skill-comparison.md` · `드림캐쳐_각성안_최종스펙_v1.md` · `keyring-portability.md` · `arknights-defense-mechanics.md`. `object-pipeline-map.md` 는 제약 번호 참조만 고쳤고 옛 전투 이름은 아직 섞여 있다.
- **보관 자료 처분**(사용자 결정) — 로컬 브랜치 `blueprint` · `prd.zip`. (`docs/production-transition/` 은 2026-10-01 사용자 결정으로 전용 검사 도구와 함께 삭제 — 필요하면 태그 `archive/pre-spec-reset`.)
- ~~기계 장치로 옮길 가드레일~~ — 2026-10-01 적용: `.claude/settings.json` 의 `git push` 확인창 · 강제 push / `--amend` 거절 + `.claude/hooks/guardrails.mjs`(옵션이 뒤에 붙은 강제 push · 아웃게임 PlayMode `run_tests` · `refresh_unity mode=force` 거절). 「경로 없는 커밋 거절」은 헝크 섞인 파일의 유일한 올바른 절차를 막아 넣지 않았다.
- ~~OMC 세션 시작 훅의 CLAUDE.md 중복 주입~~ — 2026-10-01 `AGENTS.md` 를 심링크에서 `CLAUDE.md` 안내 두 줄로 바꿔 해소(OMC 는 루트 `AGENTS.md` 를 무조건 주입한다). 자동 메모리의 ECS 시절 항목도 같은 날 정리(기기 로컬). 남은 것: `/doctor prompt-audit`.
- **새 세션에서 확인** — `CLAUDE.md` 가 새 본(제약 · 함정 · 검증)으로 들어오는가 · OMC 세션 시작 주입이 `AGENTS.md` 두 줄뿐인가 · `/doctor prompt-audit`(사용자 실행) 결과.
- **격리 리뷰(2026-10-01) 잔여** — 반영하지 않은 작은 지적:
  - 실맵 판 재현 레시피가 없다(`MatchDefinitionBuilder` + `MapStagePool` 엔트리 → EditMode.Assets 에서 N틱). 「버그는 재현 먼저」의 도구.
  - 공유 에디터에 남는 지속 부작용 목록에 `DevMapOverride`(PlayerPrefs) · `BattleDriver._fixedMapSeed`(씬 필드)가 없다.
  - 한글 표시명 → 에셋 id 찾는 법(에셋 YAML 이 유니코드 이스케이프라 한글 grep 이 0건).
  - `core-reviewer` 의 도달 진입점 목록이 §8-7 과 어긋난다(`ReachWithOrigin` 누락 · `InCellReach`) — §8-7 하나만 가리키게.
  - `CoreArchitectureTests` 에 결정론 검사가 없다(`System.Random` · `DateTime` · Dictionary 순회).
  - blueprint 시스템 지도의 경로 존재를 기계로 확인하는 검사가 없다.
  - 리뷰를 언제 돌리나(core-reviewer vs 일반 리뷰)가 `CLAUDE.md` 에 없다 · 리뷰 감지 훅이 선택 설치인 OMC 의 code-reviewer 를 추천한다.
