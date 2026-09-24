# 10 — 인계: 전환 종료 표기 · 남은 것의 이관 (조각 E)

## 목적

**다음 세션이 이 spec 을 열 필요가 없게 만든다.** 전환이 끝나면 이 폴더는 역사서가 되고, 살아 있는 계약은 CLAUDE.md 「전투 코어 — 절대 제약」(unit 9 승격)과 `docs/reference/`(unit 9 교체)로 옮겨져 있어야 한다. 이 unit 은 남은 후보를 제자리로 옮기고 종료를 적는다. 코드 0줄.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 인계 요약 | `docs/spec/battle-core-rebuild/11_handoff_summary.md`(신설, 30~80줄 — CLAUDE.md 「Handoff 작성 규칙」 6섹션) |
| 세션 인계 | `handoff_session_2026-09-24.md` 상단에 「종료 — 정본은 `11_handoff_summary.md`」 한 줄 |
| 상태 | `README.md` 상단 `상태: 완료 YYYY-MM-DD` · 작업 표 8a~10 커밋 해시 |
| 백로그 | `docs/spec/README.md` 「Follow-up Backlog」(`:107~`)에 이 spec 절 신설 |
| 메모리 | `~/.claude/projects/-Users-sy-dev-wassup/memory/` — `project_battle_core_rebuild.md` 갱신 · `MEMORY.md` 포인터 |
| 파이프라인 맵 | `object-pipeline-map.md` 는 8c 가 새로 썼다 — 9 의 삭제 뒤 심볼 존재만 재확인(CLAUDE.md 워크플로 5) |

## 구현

1. **백로그로 옮기는 것**(항목마다 한 줄 + 이 spec 의 출처 포인터 — 상세는 옮기지 않는다):
   - README 「후속 후보」 전체(서버 API 확장 · 마음 N개 공유 체력 · 보류 재결정 · X25/X26/E21 · 결정론 등급 상향 · 효과 census 미배정 4 · `docs/spec` 옛 포인터).
   - `ledgers/rules.md` 「보류」 **31 중 조각 E 가 닫지 않은 것** — 8a·8b 가 닫는 것은 X19(HUD 게이팅·`GamePhase`) · X28(덱 스냅샷) · M17(dev 맵 슬롯)이고, 나머지는 행 번호 그대로 옮긴다.
   - 각 unit 의 「이식 제외」 중 등급이 `보류`·`후속 후보` 인 행(5b 방향 지정 배치·예고선 광휘·쿨타임 셰이더 · 5c 공격음 START·결과 단위 표기 · 7c 온보딩 첫 손패(8b 가 닫았으면 제외) · 9 덱 타이머 폴백(재판정 결과에 따라)).
   - 사용자 결정 ① 의 답이 「옮기지 않는다」였다면 온보딩 재설계.
2. **인계 요약의 Notes 에 반드시 남길 것**(되돌리면 안 되는 의도): 결정론은 같은 런타임 안의 계약 · 골든은 Unity 에서만 굽는다 · 판정 산식 하나(제약 13) · 매니저 없음 · 「기획 그대로 — 땜빵은 의도만」 · 사용자 결정 기록(인계 §2)의 위치.
3. **메모리.** `project_battle_core_rebuild.md` 를 「완료」로 바꾸고 함정 중 코어 운용에 계속 유효한 것(골든 float 드리프트 · export 재검증 · 공유 인덱스 커밋)만 남긴다. ECS 전용 메모(Burst BC1055 · lookup 제거 NRE · `ecs-reviewer` 매칭)는 **사실이 끝났다**고 표기하거나 지운다(메모리 규칙: 틀린 것은 지운다).
4. **머지·푸시.** 조각 E 경계 2 — `rebuild/battle-core` → main, squash 금지(unit 0 구현 2). 푸시는 사용자 승인 후. 워크트리 `wassup-core` 정리는 사용자 결정(지우면 브랜치 이력만 남는다).

## 이식 제외

N/A — 규칙을 옮기지 않는다.

## 파이프라인 커버리지

N/A — 문서 unit.

## 완료 기준

- [ ] `11_handoff_summary.md` 6섹션(Commit · Implemented · Key Files · Verified · Notes · Follow-up) · 30~80줄.
- [ ] `docs/spec/README.md` Follow-up Backlog 에 이 spec 절 — 옮긴 행 수 = (README 후속 후보 + rules 보류 잔여 + 이식 제외 보류) 합과 같다.
- [ ] README 상태 = 완료 · 작업 표 전 행 커밋 해시.
- [ ] `MEMORY.md` 의 이 spec 포인터가 완료를 말한다 · ECS 전용 메모 처분.
- [ ] main 머지 커밋 존재 · 푸시는 승인 기록과 함께.
