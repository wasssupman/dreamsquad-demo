# battle-content-finish — 「옵션은 SO · 전투 콘텐츠만」 마무리

## 상태

- **구현 완료 (2026-10-07)** — 단위 0~5 커밋 7(`c1289bc00` · `85e48a310` · `dedf84044` · `5a8c327c9` · `81c8f324c` · `3260b71b3` · 잔재 1) + 씬 커밋. 에디터 전체 재컴파일 0 · 헤드리스 코어 빌드 0. Play 한 판은 사용자 몫.

## 목표

전제: **유닛 · 드림캐쳐 기본 편성 · 맵 · 시즌 같은 옵션은 전부 SO 에서 정하고, 이 리포엔 전투 콘텐츠만 남는다.** 그 전제로 전체 프로젝트를 다시 훑어 아직 어긋난 것을 닫는다.

1. 판 저작이 **씬 컴포넌트 필드**(`BattleDriver` 직렬화 48 참조)에 있다 → SO 로 옮긴다(단위 0).
2. 소비처 0 인 SO 타입 · 코드 · 옛 enum 값 · static 이 남아 있다(단위 1).
3. GUID 참조 0 인 에셋 209개 · 55 MB 가 남아 있다(단위 2).
4. 한 번 쓰고 끝난 에디터 도구 · 안 쓰는 패키지(단위 3).
5. `Resources` 폴더 로드 · `Shader.Find` 폴백이 「SO 참조」 원칙 밖이고 somnia 는 `Resources` 폴더명을 거절한다(단위 4).
6. 지워진 시스템을 현행처럼 말하는 문서(단위 5).

## 조사 방법 (재현 가능)

- 에셋: 세션 스크래치 `guidrefs.py index` 로 `Assets/ProjectSettings/Packages` 의 텍스트 에셋(.unity/.prefab/.asset/.mat/.shadergraph…)과 .meta 에서 32-hex guid 를 전수 수집 → 인바운드 0 = 고아 후보. 그 다음 **basename 을 `.cs` 전체에서 다시 찾아**(테스트 경로 상수 · `Resources.Load` · `Shader.Find`) 문자열 로드를 거른다.
- 코드: `Scripts/` 선언 타입 전부에 대해 **선언 줄만 뺀 전 파일**(같은 파일의 파생·호출 포함) 참조 수를 셌다. MonoBehaviour 는 씬·프리팹의 `m_Script` guid 와 `AddComponent<T>` 로 다시 확인했다.
- 1차(자기 파일 밖만 센 것)에서 거짓 양성 4가 나왔고 2차에서 걸렀다 — 단위 1 「검증」 표. **단위 0 은 삭제가 0 인 이동**이다(없어지는 건 `SeasonRuntime` static 하나 — 소비처 = `BattleDriver` 한 함수).
- 3차(2026-10-07): **순방향 도달 검사**(씬 · `ProjectSettings` · `Resources` · 코드 경로 로드 41 을 루트로 GUID 를 따라감)로 삭제 집합 전부를 다시 물었다 — 닿는 것 0. 순방향에서만 드러난 것(프롭 SO↔프리팹 순환 17 세트 · 효과 타일 2) 은 D7 로 분리. 코드 삭제 파일 17 은 스크립트 guid 인스턴스(씬·프리팹·SO) 0 · 선언 타입의 바깥 참조 0 을 확인(예외 = 같이 지우는 테스트 · 단위 0 이 걷는 `BattleDriver` 의 `SeasonRuntime` 호출). 패키지 6 은 PackageCache guid 역참조 0.
- 씬: `BattleCoreScene` 의 프로젝트 MonoBehaviour 47개가 든 SO 참조를 폴더별로 셌다(`BattleDriver` 48 · 나머지 26 컴포넌트가 1~5개씩).

## 전수 결과 요약

| 영역 | 지금 | 판정 |
|---|---|---|
| 판 저작 | `BattleDriver` 필드 23개(편성 8 · 카드 12 · 액티브 풀 6+6 · 해저드 4 · 스택 4 · 카탈로그 3 · 튜닝 3 · 맵/덱/플랜/보너스/시즌) | 씬 → SO (단위 0) |
| 모드 | `MatchModeData` 1장 — 목표 · 시계 · 웨이브 원천(deck/plan) · mapPool · 기믹 · 배치/덱 규칙 | 이미 SO. 드라이버 `_deck/_plan/_mapPool` 은 모드가 비었을 때의 폴백 |
| 입구 | `MatchEntryInput` 값 + `MatchEntryContext` 1회 소비 static | 유지(씬 경계 carry) |
| static | `SeasonRuntime`(Bind 뒤 같은 함수에서 Active 읽음) · `TimeManager` · `SoundManager` · `RuntimeMaterialFactory` 캐시 · `BoardSpace` | `SeasonRuntime` 만 제거 |
| 죽은 SO 타입 | `RelocationSettings` · `ScoreRulesData`(빈 클래스) · `DreamcatcherDeck`(참조 0, 기본 덱 그릇으로 재사용 후보) · `MapDocument_MovementStress.asset`(스크립트 없음) | 단위 1 |
| 죽은 코드 | `DeployPhaseClock` · `BonusPullTrigger`(둘 다 테스트만) · `LegacyTraceV0` 클래스 · `TileHealthGauge*` · `UiFullBleedModalDim` · `FluidPaintView` · 정적 클래스 3 — 재검증에서 `StatAuraSkill`(산 스킬 3의 베이스) · `LegacyPayload`(굽기가 쓴다) 는 **제외** | 단위 1 |
| 닿지 않는 에셋 | **491 파일 · ≈149 MB**(장부 `ledger.md`) — A `_Project` 고아 폐포 306(91 MB) · B 그에 따라 죽는 벤더 127(51 MB) · C 테마 풀 밖 프롭/효과 타일 55(D7) · D 비활성 카드 3(D8). 순방향(씬·설정·Resources·코드 경로에서 닿는가)과 역방향(인바운드 0)이 일치한 것만 | 단위 2 |
| 에디터 | BattleCore 디버그 메뉴 11 · 저작 도구 10(Layer Lab 임포터 · GA 스트리퍼 포함) · 일회성 4 · 시트 임포터 9(+DTO 15 +SheetSync 2) | 단위 3 |
| 패키지 | probuilder · visualscripting · postprocessing · multiplayer.center · ai.navigation · collab-proxy — 코드 사용 0 + 에셋 참조 0. `timeline` 은 PixPlays 산 프리팹 2 가 써서 유지 | 단위 3 |
| Resources | `Assets/Resources/RuntimeMaterials/*.mat` 4 · `Shader.Find` 체인 3 파일 | 단위 4 |
| 문서 | 백로그의 「docs 잔여 5개」 · `milestone/` 2 · `각성안` · stale 문단 | 단위 5 |

## 공통 원칙

- **땜빵 금지** — 6.6 에서 처음 만든 모양으로. 옛 이름의 껍데기 · 호환 포워더를 남기지 않는다.
- 전투 코어(`BattleCore/` · `Skills/` · `UnitAi/`)는 손대지 않는다. 죽은 코어 타입(`LegacyTraceV0` · `AggroPolicy`)만 예외로 지운다 — 틱 순서 · 산식 무변.
- 삭제는 **참조 폐포**로만: GUID 인바운드 0 + 코드 식별자 참조 0 + 테스트 경로 로드 0. 하나라도 있으면 지우지 않고 표에 남긴다.
- 씬 YAML 은 에디터 안에서(일회용 MenuItem). 열린 씬을 밖에서 고치지 않는다.
- 검증 기준은 **에디터 컴파일 0 + 사용자 Play 한 판**(배치 검증 없음 — 사용자 결정 2026-10-07).

## 결정 대기 (작업 전 묶어서 한 번)

- **D1 규칙 저작의 자리** — 해저드 · 길막 · 스택 모디파이어 · 임뷰 상한 · 이동 튜닝 · 보너스 웨이브 · 시즌 레지스트리를 `MatchModeData` 에 흡수할지, 모드와 무관한 **콘텐츠 묶음 SO** 하나로 둘지. 추천: 묶음 SO(`BattleContent`) — 모드는 「닫힌 집합 + modeId 재현」이라 콘텐츠 목록을 품으면 모드마다 복제된다.
- **D2 기본 편성 SO** — `_defenders` · `_dreamstones` · `_cards` · `_activeCards` 를 `DefaultLoadout` SO 하나로. 카드 묶음은 참조 0 인 `DreamcatcherDeck` 을 재사용(추천) vs 삭제 후 새 타입.
- **D3 풀 밖 스테이지 2 + 옛 타일 아트** — `MapStage_Hello` · `MapStage_Building`(풀에 없음) 과 `Art/Theme/forest` 타일 png 30 · building/subway/street 잔여 png. 삭제 추천(31 MB).
- **D4 테스트 덱 · 플랜** — `Scripts/Data/Decks` 13 + `WavePlans` 6 중 ① 아무도 안 쓰는 **8**(`Deck_SiegeTest` · `Deck_WaypointLab` · `WaveB` · `WavePlan_*` 5 중 BossTest 제외) 삭제. ② 테스트만 여는 **덱 5**(`Deck_Twin/Spiral/Hook` = 사라진 맵의 덱 · `Deck_Ford/Isle` = 없는 Siege 스테이지의 덱)도 삭제하고 테스트 4 의 덱 목록을 라이브 4(`Duel/Serpent/Zig/Coil`)로 — 단 `LiveDeckBossAuthoringTests` 의 「6맵 ÷ 보스 3종 = 각 2맵」 규칙은 4맵에선 성립하지 않으니 **보스 배정 규칙을 다시 정한다**(설계). ③ `WaveA` · `WavePlan_BossTest` 는 순수 픽스처 — `Assets/_Project/Tests/Fixtures/` 로 옮겨 역할을 이름에 박는다. 산 덱 4 는 `Data/Decks/` 로.
- **D5 시트 임포터(Q2 재확인)** — 읽기(임포트)는 두고 **push/export 경로**(`SheetPushClient` · `*Exporter` · `SheetPushPayload`)만 뗄지, 전부 둘지, 전부 뗄지.
- **D6 문서 처분** — `docs/CODEX-HARNESSING.md` · `map-editor-reference-for-somnia.md` · `spec/claude-code-documentation-note.md` · `드림캐쳐_샘플덱_기획요약.md` · `milestone/` 2 · `reference/드림캐쳐_각성안_최종스펙_v1.md`(가리키는 곳 2 — demo-diet 기록 · design-blueprint README) · (선택) `reference/dreamcatcher-portability.md`(스스로 「설계 이력 문서」). 전부 삭제 추천.
- **D7 테마 풀 밖 프롭 17 세트 + 효과 타일 2 세트**(55 파일 · 4 MB) — 어느 테마 풀·스테이지에도 없다. 삭제 추천(되살릴 땐 git).
- **D8 비활성 카드 `Card_IncubusPact` 세트**(3 파일 · 2 MB) — 2026-08-08 「규칙이 안정되면 재판단」으로 보관 중. 전투만 남기는 기준이면 삭제, 보관이면 그대로.
- **D9 웨이포인트 기능** — 코어(`Move/WaypointProgress` · `SpawnPathPreview`) · 빌더 · 저작(`RouteMarker` · `SpawnMarker.routeIndex`)이 다 있는데 **라이브 스테이지 4 에 저작한 경로가 0**(전부 골 직행). 전투 기능이라 이 spec 은 손대지 않는 쪽을 추천(코어 무변 원칙) — 기능째 뺄 거면 별도 spec.

## 체크리스트 (진행)

결정 D1~D9 는 2026-10-07 전부 **추천안으로 승인**됐다.

- [x] **0** 판 저작 → SO — `BattleContent.cs` · `DefaultLoadout.cs` · 에셋 2 · 기본 덱 갱신 · 드라이버/선택/진입/빌더/메뉴/테스트 10 · `SeasonRuntime` 삭제 · `Lobby` → `External` · 컴파일 0 · 씬 배선(일회용 메뉴) · 커밋
- [x] **1** 죽은 타입·코드 — SO 타입 3 + 에셋 3 · 코드 9 · `GamePhase.Draft/Tally` + 카메라 SO int 이동 · 컴파일 0 · 커밋
- [x] **2** 닿지 않는 에셋 — 장부 A(306) · B(127) · C(55) · D(3) · 덱/플랜 이동 + 테스트 상수 · `reach.py` 재실행 = 남기는 것뿐 · 커밋
- [x] **3** 에디터 도구 4 · push 3(export 는 유지 — 헤더 계약 테스트의 타입 원천) · 패키지 6 · 모듈(lock 기준) · `map-stage-authoring.md` 2줄 · 컴파일 0 · 커밋
- [x] **4** `RuntimeMaterialSet` SO · `Resources` 폴더 삭제 · `Shader.Find` 0 · 커밋
- [x] **5** 문서 8 삭제 · stale 문단 · blueprint/CLAUDE.md · spec README 종료 · 커밋
- [ ] **리뷰** — 전체 diff 코드리뷰(`core-reviewer` + code-review) → 지적 반영 커밋
- [ ] **Play 한 판**(사용자) — 편성 8 · 덱 12 · 효과 타일 · 프롭 · 탄 궤적 · 배치 음성 · 오버레이 · 카드 구김

## 구현 문서

| 단위 | 문서 | 크기 |
|---|---|---|
| 0 | `0_match_authoring_so.md` — 판 저작 → SO · `SeasonRuntime` 제거 · `ModeSelection.Lobby` 개명 | M |
| 1 | `1_dead_types_and_code.md` — 죽은 SO 타입/에셋 · 죽은 코드 · `GamePhase` 잔재 값 | S |
| 2 | `2_orphan_assets.md` + `ledger.md` — 닿지 않는 에셋 491(묶음 A~D) | S~M |
| 3 | `3_editor_tools_and_packages.md` — 일회성 에디터 도구 4 · 패키지 6 · 빌트인 모듈 | S |
| 4 | `4_resources_to_so.md` — `Resources` 머티리얼 4 → SO 참조 · `Shader.Find` 체인 제거 | S |
| 5 | `5_docs.md` — 문서 처분 · stale 문단 · blueprint/CLAUDE.md 갱신 · spec 종료 | S |

순서는 0 → 1 → 2 → 3 → 4 → 5. 단위마다 커밋(경로 지정) + 에디터 컴파일 0 확인 + 사용자 확인.

## 비목표

- `Wassup` → `Somnia.Battle` 개명 · 폴더 재배치(다음 spec — somnia 반입).
- 전투 규칙 변경 · 밸런스 · 시트 값.
- 액티브 스킬(`SkillData` · `_activePool`)의 드림캐쳐 분리(백로그 U18) — 지금은 「판마다 굴린 액티브 2장」으로 **산 기능**이다. 옛 「액티브 스킬 버튼」 HUD 만 없다.
- `EnemyCatalog`(런타임 소비 0 · 테스트 3 + 시트 exporter 가 읽음)는 저작 색인으로 **유지**.
