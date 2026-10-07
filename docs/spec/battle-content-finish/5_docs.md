# 5 — 문서 처분 · stale 문단 · 종료

## 목적

지워진 시스템을 현행처럼 말하는 문서를 걷고, 이 spec 이 바꾼 설계(저작 → SO · Resources 없음)를 정본 문서에 반영한다.

## 삭제 (D6)

| 문서 | 근거 |
|---|---|
| `docs/CODEX-HARNESSING.md` | Phase · TRD 전제의 옛 에이전트 운용 안내 — 가리키는 곳 0 |
| `docs/map-editor-reference-for-somnia.md` | 다른 프로젝트용 조사 · 런타임 설명 12곳이 옛 전투 기준 |
| `docs/spec/claude-code-documentation-note.md` | `docs/spec/README.md` 원칙의 복제 고아 |
| `docs/드림캐쳐_샘플덱_기획요약.md` | 6월 초안 · 참조 0 |
| `docs/milestone/gameplay-design-summary.md` · `-quick.md` | 스스로 「정본 아님」 배너 · 옛 전투 기준 12곳 |
| `docs/reference/드림캐쳐_각성안_최종스펙_v1.md` | 각성 규칙의 정본은 코드 + `ingame-flow.md`. 가리키는 곳 2(`demo-diet/4` 기록 · `design-blueprint/README`)는 「삭제됨」으로 |
| (선택) `docs/reference/dreamcatcher-portability.md` | 머리에 「⚠ 설계 이력 문서다」 — 정본은 `skill-data-table/tables.md`. 가리키는 곳은 기록 2 뿐. 「지운 문서는 평소 읽지 않는다 · 이력은 git」 원칙대로 삭제 후보 |

유지: `arknights-defense-mechanics.md`(설계 대조군 · 사실 기록) · `shadow-quality-settings.md`(URP 그림자 손잡이 — 현행) · `dreamcatcher-card-schema.md`(`battle-core-architecture` 가 링크).

## stale 문단 (현행처럼 말하는 것만)

- `docs/reference/battle-core-architecture.md`(12 hit) — 로비/프로필/토너먼트 리포터를 현행 입출력처럼 쓴 줄 → `MatchEntryInput` · 사건 4 로. §8 불변식은 무변.
- `docs/reference/lessons/03-rendering-assets.md`(13 hit) — 머리에 이력 표시는 있다. Tilemap 채움 · Tile 캐시 절은 **삭제**(이력은 git 에 있다 — 「지운 문서는 평소 읽지 않는다」).
- `docs/reference/object-pipeline-map.md` · `map-stage-authoring.md` — 단위 0 뒤 「드라이버 저작」 행을 SO 로. `map-stage-authoring.md:87-88` 의 Duel 생성기 두 줄은 단위 3 에서 걷는다(`lessons/03:129` 의 `GaProjectileStripper` 줄은 도구가 남으니 그대로).
- `docs/blueprint/README.md` §3-1 「입력이 없으면 `BattleDriver` 의 저작 필드가 기본값」 → 「`DefaultLoadout` SO」 · §4 「값이 흐르는 길」에 `BattleContent` · `RuntimeMaterialSet` · §5 「코어가 SO 를 읽는 곳」 무변.
- `CLAUDE.md` — 「Unity 함정」의 `Shader.Find` 항목: `Resources/RuntimeMaterials` 등록 안내 → `RuntimeMaterialSet` 슬롯 추가로. 「스택」에 저작 SO 두 장 한 줄.
- `docs/reference/test-procedure.md` — 테스트 어셈블리 수가 바뀌지 않으면 무변.

## 종료

- `docs/spec/README.md` 「진행 중 spec」 → 없음 · 직전 완료 = 이 spec. 백로그에서 해소된 줄 삭제: 「Assets 안 지운 spec 경로 6곳」 중 `CostConfigRuntimeRefresher`(이미 없음) · 「docs 잔여 5개 삭제」 · 「`BlockingHazardPresenter` 의 `Shader.Find`」 · 「어셈블리 분할」은 그대로(비목표).
- 세션 메모리 `somnia-migration-goal` 갱신(다음 = 개명 · 반입).

## 완료 기준

- [x] 위 문서 7 삭제 · `rg -l "각성안|CODEX-HARNESSING|map-editor-reference|샘플덱_기획요약|milestone/" docs CLAUDE.md` → 기록 줄(demo-diet · 이 spec)뿐
- [x] blueprint · CLAUDE.md · lessons/03 · battle-core-architecture 갱신 커밋 1

## 구현 결과 (2026-10-07) — `81c8f324c`

- 문서 8 삭제(`dreamcatcher-portability` 포함). lessons/03 Tilemap 절 6(71줄) 삭제. 아키텍처 · 객체 지도 · score-formula · blueprint · CLAUDE.md 를 현행(바깥 입력 · `MatchFinished` 사건 · SO 두 장 · `RuntimeMaterialSet`)으로. 덱 경로 `Data/Decks`. spec README 종료 + 백로그 3줄 해소.
