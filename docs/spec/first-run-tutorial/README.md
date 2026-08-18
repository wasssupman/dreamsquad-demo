# First-Run Tutorial — 계정 첫 판 온보딩

상태: 작성됨 2026-08-18 · units 0~7 미착수

`tutorial-content-teardown` 이 비워둔 자리에 새 온보딩을 짓는다. 로비에서 시작해
**한 판 안에서** 유닛 선택 → 배치 → 배치 스킬 → 드림캐쳐 부착까지 잇는 1회성 시퀀스다.

## 검증 질문

**처음 들어온 사람이 "유닛을 뽑아 길목에 놓고, 그 유닛에 드림캐쳐를 붙인다" 를
한 판 안에 스스로 해내는가.** 이 질문에 답하는 데 필요 없는 것은 전부 뺀다 —
각성/재배치/퇴근/기믹/스킬 상세는 이 spec 범위가 아니다.

## 시퀀스

| 구간 | 무엇이 보이나 |
|---|---|
| **L** 로비 | 딤 + START 구멍. "누가 더 많은 악몽을 제거하는지 시작해 보시죠" |
| **B1** 맵 설명 | 배치 가능 ↔ 불가 영역을 번갈아 칠하며 각각 안내 → "게임 목표: 최대한 많은 악몽 처치" |
| **B2** 카운트다운 | 기존 3 · 2 · 1 · GO! 연출 그대로 |
| **B3** 첫 배치 | 전투 N초 후 정지 → 캐논 셀 포커스 "유닛을 터치해보세요" → 지정 칸 "적들이 몰려오는 길목에 캐논을 배치해보세요" → 정지 풀고 배치 스킬 관람 → "강력한 배치 스킬들을 활용하여 전황을 유리하게 이끌어 보세요" |
| **B4** 부착 | 다시 정지 → **보드 위 캐논** 포커스 "다시 캐논 유닛을 선택해보세요" → 손패 열림 "하단 드림캐쳐 4개 중 맘에 드는 것을 터치해보세요" → 부착 연출 후 "드림캐쳐를 유닛에게 부착하여 더 강해질 가능성을 열어보세요!" |
| 종료 | 정지 해제. 3분 판을 그대로 이어서 플레이 |

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토대 | [0_tutorial_context_and_progress.md](0_tutorial_context_and_progress.md) | 진행 저장 필드 · 실행 판정 · 타이밍 SO · RESET 버튼 본체 |
| 1 | 데이터 | [1_fixed_map_and_waves.md](1_fixed_map_and_waves.md) | 튜토리얼 맵/웨이브를 랜덤 풀에서 분리하고 튜토리얼 판에서만 쓴다 |
| 2 | 뷰 | [2_unplaceable_highlight.md](2_unplaceable_highlight.md) | 배치 **불가** 영역 하이라이트 (지금은 가능 영역만 있다) |
| 3 | 로비 | [3_lobby_focus_step.md](3_lobby_focus_step.md) | 로비 강제 포커스 (L) |
| 4 | 배틀 골격 | [4_battle_sequence_skeleton.md](4_battle_sequence_skeleton.md) | 정지/재개 · 딤+구멍 · 카운트다운 홀드 · 스텝 러너 |
| 5 | 배틀 | [5_map_briefing_steps.md](5_map_briefing_steps.md) | 맵 설명 (B1) |
| 6 | 배틀 | [6_pick_place_onplace_steps.md](6_pick_place_onplace_steps.md) | 선택 → 배치 → 배치 스킬 (B3) |
| 7 | 배틀 | [7_dreamcatcher_attach_steps.md](7_dreamcatcher_attach_steps.md) | 드림캐쳐 부착 (B4) |
| 8 | 인계 | 8_handoff_summary.md | 구현 종료 시 작성 |

## 공통 원칙 (feature-wide 계약)

1. **튜토리얼은 게임 규칙을 하나도 소유하지 않는다.** 배치·부착·배치 스킬·코스트·게이지는
   전부 기존 경로가 처리한다. 튜토리얼이 결정하는 것은 **«언제 멈출지»와 «무엇을 열어둘지»
   둘뿐**이다. 튜토리얼이 대신 눌러주는 동작은 없다.
2. **계정당 1회.** 판정은 `PlayerProfile` 의 전용 필드다. `matchesPlayed` 에 얹지 않는다 —
   그건 「계정의 첫 판은 토너먼트에 올리지 않는다」(서버 `complete` 500 우회)의 유일한
   신호이고, 두 규칙이 한 필드를 겸직하면 한쪽을 고칠 때 다른 쪽이 조용히 바뀐다.
3. **튜토리얼 판은 고정 지형·고정 웨이브.** "지정된 칸에 놓아보세요"가 성립하려면 지형이
   매번 같아야 한다. `MapDocument_Tutorial` + `WavePlan_Tutorial` 을 랜덤 풀에서 분리해
   튜토리얼 진입에서만 쓴다.
4. **편성은 프로필 기본값을 그대로 쓴다.** 튜토리얼이 스쿼드/덱을 조작하지 않는다.
   신규 계정은 `ProfileStore` 가 이미 기본 편성을 시드한다.
5. **전 구간 강제.** 안내가 요구하는 대상 외의 입력은 딤 + 구멍으로 막는다. 다만 **정지
   상태에서만** 강제한다 — 시간이 흐르는 동안 조작을 막으면 판이 손해를 본다.
6. **시간 제어는 `TimeManager.Request(TimeDomain.Battle, 0)` 만.** `Time.timeScale` 금지
   (`docs/reference` · `TimeManager` 도메인 시간제어).
7. **안내 UI 는 남겨둔 도구만 쓴다.** `TutorialGuidanceView`(문구·포커스링·월드마커) ·
   `OutgameTutorialOverlay`/`DimLayout`/`TapZone`(딤+구멍). 새 안내 위젯을 만들지 않는다.
8. **수치는 전부 SO.** 정지 시각·재개 길이·왕복 횟수는 `FirstRunTutorialConfig` 에서 나온다.
   문구는 컨트롤러의 `const` 로 둔다(옛 `OutgameTutorialController` 의 관용구).
9. **튜토리얼이 끝나도 판은 끝나지 않는다.** 정지를 풀고 3분 판을 그대로 이어간다.
   점수·만료·제출은 정상 경로다.
10. **막히면 흘려보낸다.** 각 스텝은 SO 의 타임아웃을 가지며, 만료되면 다음으로 넘어간다.
    안내가 진행을 영구히 막는 상태를 만들지 않는다.

## 전제 (확인된 값)

- `BattleConfig`: `placementPhaseEnabled: 0` (3초 카운트다운 자동 시작) · `gimmickEnabled: 0`
- 캐논: `id: cannon` · `cost: 5` · `placementLayers: Ground` — **경로 위에는 못 놓는다**.
  그래서 B3 의 문구는 "적들의 머리 위"가 아니라 **"길목"** 이다(배치 스킬 SkyStrike 가
  하늘에서 떨어져 적을 때리므로 의미는 유지된다).
- 코스트: `startingCost: 10` / `maxCost: 10` / `regenPerSec: 0.35` → 정지 시점에 캐논(5) 지불 가능
- 각성: `gaugeStart: 20` · `costUnit: 20` · `handSize: 4` → 판 시작 시 정확히 **1장** 부착 가능

## 파이프라인 커버리지

**N/A** — 새 플레이 오브젝트를 만들지 않고 생성→렌더 경로를 바꾸지 않는다. 이 spec 은
기존 오브젝트를 **가리키고 멈추는** UI 계층이다. 유일한 뷰 변경(unit 2, 배치 불가
하이라이트)은 기존 `TilemapMapView` 오버레이 타일맵에 색 한 층을 더하는 것이다.

## 후속 후보 (현 spec 범위 밖)

- **스킵 버튼** — 1회 · 짧아서 뺐다. 재실행은 개발 트레이 `RESET TUTORIAL`.
- **각성/재배치/퇴근 안내** — 2차 온보딩. 첫 판에 다 넣으면 길어진다.
- **`TutorialGuidanceStyle` 의 고아 필드 정리** — 옛 스텝 전용 값들(`classHintFallbackSeconds` 등).
  이 spec 이 무엇을 쓰는지 확정된 뒤 (`tutorial-content-teardown` 후속 후보에서 이관).
- **도구 계층 이름 재정리** (`Tutorial*` → 안내 도구를 뜻하는 이름) — 같은 출처.
- **온보딩 전/후 첫 판 이탈률 비교** — 이 spec 의 효과 측정. 계측 seam 이 따로 필요하다.
- **튜토리얼 문구 시트화** — 문구가 늘거나 다국어가 필요해지면.
