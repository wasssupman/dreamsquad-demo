# Codex 구현 가이드 — dreamstone-loadout

> 이 문서는 구현을 이어받는 Codex 세션용 지도다. 계약의 source of truth 는 `README.md` 와 각 번호 문서 — 이 가이드는 읽는 순서와 함정만 압축한다.

## 읽기 순서

1. 루트 `CLAUDE.md` (절대 제약 — ECS 경계 / BattleBridge 단일 창구 / 하드코딩 수치 금지 / 스코프 엄수)
2. 이 폴더 `README.md` — feature-wide 계약 10개 + 검증 질문
3. 담당 작업 단위 파일 하나 (`{N}_{topic}.md`) — 그 파일만으로 작업 완료 가능해야 함

## 구현 순서 / 의존

`0 → 1 → 2 → 3`, 한 번에 한 unit, unit 당 커밋 1개.

- **0** 데이터 모델 (`DreamstoneData`/`Grade`/`Catalog` SO + 등급 캡 validator + 에셋 16종) — 의존 없음
- **1** `SquadSave.stoneIds` 4슬롯 + `SetStoneSlot(index, id)` 도우미 — 0 의 id 규칙 참조
- **2** 스쿼드 페이지 UI 재편 (슬롯 탭 → 피커 모달, 유닛/스톤 겸용) — 1 의 도우미 사용
- **3** 전투 반입 (set-then-apply + axis All + PlayMode smoke) — 0/1 의존

각 unit 완료 기준 충족 → 사용자 확인 요청 → 통과 시 해당 문서 "완료 기준" 하단에 확인 일자 + 커밋 해시 1줄 추가 후 커밋.

## 핵심 함정 (설계 크리틱에서 실코드로 검증된 것)

1. **`BeginPlacement()` 가 매치 효과 레지스트리를 클리어한다** (BattleBridge.cs:806 부근 `_activeDcEffects.Clear()` + `_dcStackCounter = 100`). 스톤 등록을 배치 진입 전에 하면 전부 지워진다. 반드시 unit 3 의 **set-then-apply** 패턴(`SetDreamstones` 는 pending 저장만, `BeginPlacement` 가 클리어 직후 적용)을 따를 것.
2. **`MatchesDcAxis` 에 `All` 분기 누락 = 조용한 no-op** (default → false, 에러 없이 스톤 무효). smoke 테스트로 분기 통과를 실제 확인.
3. **`StartTestModeMatch` 는 `StartSquadMatch` 의 호출자가 아니라 별도 미러 메서드** (GameManager.cs:215 부근). 스톤 등록을 양쪽 모두에 넣을 것.
4. **`CardTargetAxis.All` 은 enum 끝에 append** — 기존 DreamcatcherCard .asset 직렬화 값(0~2) 보존.
5. 버프는 multiplier ≥ 1 → additive 합산 (`modifier-additive-authoring` 정책, BattleBridge.cs:2293 주석). 스톤 validator 가 `percent > 0` 을 강제하므로 1.0 경계를 넘는 채널을 만들지 말 것.

## 테스트 / 검증

- EditMode 테스트는 **`Assets/_Project/Tests/EditMode/`** 에 둔다 (`Scripts/` 아래 금지 — asmdef 밖이라 run_tests 매칭 실패). PlayMode 는 `Assets/_Project/Tests/PlayMode/`.
- 실행: Unity Test Runner (UnityMCP `run_tests` 또는 에디터). 라이브 Play 검증은 에디터 **포커스** 필요(비포커스면 시뮬 tick 정지).
- unit 2/3 은 씬 wiring(OutgameScene/BattleScene 참조 할당)이 완료 조건에 포함 — 자동화 가능하면 UnityMCP 로, 불가하면 사용자에게 명시적으로 요청하고 완료 선언하지 말 것.

## 리뷰 케이던스

각 unit 구현 후 투트랙 리뷰(code-reviewer 일반 품질 + ecs-reviewer ECS 도메인) → 수정 → 다음 unit. 설계 크리틱은 이미 1회 완료(2026-07-04, 스펙에 반영됨).

## 리뷰 요청 시그널 (Claude 세션 감시용)

리뷰는 병행 Claude 세션이 수행한다. unit 하나의 완료 기준을 충족하면 아래 한 줄을 마커 파일에 **append** 하라 (디렉토리 없으면 생성. 이 파일은 세션 간 시그널 전용 — **커밋 금지**):

```bash
mkdir -p .omc/review-requests
echo "unit=<N> status=ready ts=$(date -u +%Y-%m-%dT%H:%M:%SZ) note=<한줄 요약>" >> .omc/review-requests/dreamstone-loadout.log
```

Claude 세션이 이 파일과 새 커밋을 감시하고 있다가 리뷰를 시작한다. 리뷰 지적 반영 전까지 다음 unit 으로 넘어가지 말 것.

## 검증 질문 (feature 전체의 합격선)

스쿼드 페이지에서 드림스톤을 최대 4개 장착·저장하고 게임을 시작하면, 배치된(그리고 이후 배치되는) 모든 아군 유닛에 스톤 스탯이 매치 내내 적용되는가? 유니크 공격력 스톤 4개 = 정확히 +30%인가?
