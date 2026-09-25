# 11 — 인계 요약: battle-core-rebuild (전환 종료 2026-09-25)

> 이 spec 의 **정본 인계**다. 긴 이력은 `handoff_session_2026-09-24.md`(§6 커밋 지도 · 함정 1~30 · §10 옛 상태 라인 원문), 계약은 `README.md` → unit 문서, 살아 있는 제약은 CLAUDE.md 「새 전투 코어 — 절대 제약」과 `docs/reference/battle-core-architecture.md` 다.

## Commit

- **main 머지 `fecb0fef3`** — 조각 C·D·E(8a~9c) 편입, squash 없음. 조각 A·B 는 앞서 `d9fbe90c` 로 머지됐다.
- 조각 A: unit 0 `4caee406` · 1~4 `384e869b`~`7abfec27` · 조각 B: 5a `aa16ee9d`~`1b7e033b` · 5b `f979550d`~`e3731485` · 5c `ebf055f4`~`725df315`.
- 조각 C: 6a `a5180e9e`~`0087e9d2` · 6a2 `56b8a4d8`·`732b5a00` · 6b `cd390e511` · 6b2 `bed80d790` · 6c `eca3e47e7`.
- 조각 D: 7a `eaa3abc72`~`752bb7a63` · 7b `dd23e4578`~`3fb35b8ff` · 7c `b22e0708e`~`88617a8fe` · 7d `277e8bcba`~`3cf864ad5` · 7e `0164a940a`~`8d901b34d`.
- 조각 E: 8a `0da5d10c9`~`6bcdee28a` · 8b `c899b6ab7`~`d101b9dab` · 8c `66c77be6c`~`7b3931631` · 8a2 `f7fb71693`~`41b57d06d` · 8d `90d84fce2`~`eff6833cc` · 9 `45d43c8a0`(CLAUDE.md 승격)·`e548eda90`(옛 전투 삭제)·`60c09db21`(Entities 제거) · 9c `27297a0cb`~`8e4b19c82`.

## Implemented

- 전투가 순수 C# `Wassup.BattleCore`(`noEngineReferences`)에서 돈다. 참조는 `Unity.Mathematics`·`Wassup.Skills`·`Wassup.UnitAi` 뿐이다.
- 매니저·브리지 없음. `BattleMatch` 는 담당자 8 을 만들고 틱 순서를 나열만 한다.
- 입력은 커맨드 + receipt, 사건은 `SimEntityId` 키의 값 스냅샷 이벤트다. 모든 소멸 경로가 소멸 이벤트를 낸다.
- 매치 모드는 닫힌 집합(`MatchModeData` SO → `ModeDef`, `IMatchGoal` concrete 3). v1 제출은 `KillScoreTimed` 만.
- 카드 52장 · 기믹 · 보스 · 분열 · 배치 스킬이 바인딩(트리거→발동 rev 3)으로 돈다. 52장 자동 증언(`CardProbe`) 전부 ○.
- Unity 층 = 정의표 물질화(`MatchDefinitionBuilder`) · 시간(`BattleDriver`) · 뷰 풀(각자 구독) · 입력. 로비 「시작」이 `BattleCoreScene` 을 연다.
- 옛 ECS 전투 1,215 파일 삭제 · Entities·Entities Graphics 패키지 제거 · `Wassup.Battle.*` 네임스페이스 0. 튜토리얼 전량 제거(결정 ④).
- 옛 규칙 복원 9c(방향탄 튕김 · 감지 직업 필터 · 도발 리프레시 긴 쪽 · 방벽 광역 면제 · 관통탄 가까운 적부터 · 예보 경로 옛 방식).

## Key Files

- `Assets/_Project/Scripts/BattleCore/Match/BattleMatch.cs` — 조립 지점(담당자 · 틱 순서).
- `Assets/_Project/Scripts/BattleCore/Trigger/IntentApplier.cs` — 스킬의 단일 쓰기 표면.
- `Assets/_Project/Scripts/BattleCoreUnity/{BattleDriver,MatchDefinitionBuilder,MatchEntry}.cs` — 시간 · 정의표 · 로비 진입.
- `Assets/_Project/Scenes/BattleCoreScene.unity` · `Assets/_Project/Tests/{EditModeCore,EditModeAssets,GoldenCore,PlayModeCore}/`.
- `tools/battle-core-rebuild/headless/*.csproj` · `check_ledgers.py` · `ledgers/`(장부 — 동결, 역사).
- `docs/reference/battle-core-architecture.md` · `test-procedure.md` · `object-pipeline-map.md` · `.claude/agents/core-reviewer.md`.

## Verified

- 헤드리스 export(HEAD `8b9d58ae2` — 9c 종료): build 0 · test **894/894**(Ignore 0) · Unity 층 Check 0 · manifest entities 0.
- Unity EditMode 3 어셈블리 **2510/2512** — 선행 2 = `bomb_man`·`boomerang` 문안(시트 몫).
- PlayMode 코어 **97/97** · 골든 11 Verify 일치(Unity 정본) · `check_ledgers.py` 3종 exit 0.
- 사용자 플레이 4차(결정 ⑨): 로비 → 판 → 결과 → 로비 흐름 통과. 개별 그림은 전수 확인이 아니다.
- 미시도: Android QA 빌드 · 로그인 계정 실제 랭킹.

## Notes

- **결정론은 같은 런타임 안의 계약이다.** 골든은 Unity 에서만 굽는다(Mono 확장 정밀도 — `lessons/04` 마지막 절). 헤드리스는 Golden 카테고리 제외.
- **판정 산식은 하나다(제약 13).** 새 판정은 정본 진입점을 호출만 한다. 인라인 도달 판정은 리뷰 HIGH.
- **매니저 없음 · 「기획 그대로 — 땜빵은 의도만」**(계약 1·2). 규칙 차이가 보이면 옛 규칙으로 되돌리는 것이 기본이다.
- 사용자 결정 기록 = `README.md` 「사용자 결정 — 조각 E」 ①~⑩ + `handoff_session_2026-09-24.md` §2. 되돌리지 말 것:
  - ④ 튜토리얼은 전투·로비 둘 다 없다. 계정 첫 판 참가 생략(`IsFirstMatch`, 서버 500 우회)만 존치(⑦-1).
  - ⑦-2 부가 피해(스플래시·스윕·튕김·재조준)는 거점(마음·본능)도 친다. 방벽 광역 면제는 유지.
  - ⑧ 예보 경로는 옛 방식(적 저작 경로 > 레인 기본 > 최단), 예고선 병합은 새 방식.
  - ⑩ 서버 권위 실시간 서버는 **목표일 뿐** 이 spec 에 서버 코드는 없다. 커맨드가 서버 로직의 키워드가 된다. UI·에셋은 데모용.
- `ledgers/` 는 전환 도구라 이제 갱신하지 않는다. 코어 변경의 리뷰는 `core-reviewer` 가 한다.

## Follow-up

- 백로그 = `docs/spec/README.md` 「Follow-up Backlog」 → 「전투 코어 전환 — 남은 것」 절. 결정 ⑩ 에 따라 (가) 서버 권위 spec 몫 · (나) 코드 정식 설계 · (다) 데모 UI 보류 · (라) 시기상조 · (마) 사용자 몫으로 나눴다.
- 9d 잔여 정리는 별도 진행 중이다(README 작업 표 행 예약).
- 헤드리스 csproj 4 의 `UnityScriptAssemblies` 기본값이 아직 워크트리(`wassup-core`)를 가리킨다. 워크트리 정리 전에 main 워크트리로 옮겨야 한다(10 구현 5).
- 푸시는 사용자 승인 대기(main 이 origin 보다 254 커밋 앞).
