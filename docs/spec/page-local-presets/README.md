# page-local-presets — 스쿼드·드림캐쳐 페이지별 플레이어 프리셋

> 상태: **초안 2026-07-30**
> 선행: `authored-preset-removal` (**먼저 커밋되어야 한다** — 구 authored 프리셋이 제거된 상태를 전제) · `squad-character-page`(스쿼드 페이지) · `dreamcatcher-deck-page`(드캐 페이지) · `dreamstone-loadout`(스톤 4슬롯)
> 성격: 아웃게임 UI + 프로필 저장 의미론. **ECS/BattleBridge 무관.**

## 목표

스쿼드 페이지와 드림캐쳐 페이지가 각각 **최대 30개의 플레이어 소유 프리셋**을 갖는다. 각 페이지에서 프리셋 목록으로 자유롭게 오갈 수 있고, **선택 / 저장 / 리셋 / 삭제** 네 조작이 페이지에 고정 배치된다. 전투에 반입되는 것은 명시적으로 **확정(선택)** 한 프리셋이다.

### 검증 질문

플레이어가 스쿼드 페이지에서 프리셋을 여러 개 만들어 각각 다른 유닛·스톤 조합을 저장하고, 목록으로 오가며 비교한 뒤 하나를 **선택**해 확정하고, 게임을 시작하면 **확정한 프리셋의 저장된 내용**이 정확히 반입되는가? 드림캐쳐 페이지에서 동일하게 되는가?

## 핵심 발견 — 스키마 변경이 필요 없다

기존 `PlayerProfile` 은 **이미** 이 기능의 저장 구조다:

| 요구 | 기존 필드 | 비고 |
|---|---|---|
| 프리셋 30개 | `squads: List<SquadSave>` | 이미 리스트 (엔트리가 1개뿐이었음) |
| 프리셋별 이름 | `SquadSave.name` | 이미 존재 |
| 프리셋별 스톤 4칸 | `SquadSave.stoneIds` | 주석에 `squad-owned (not account-owned)` — 이미 프리셋 소속 |
| 확정 포인터 | `selectedSquadId` | 이미 존재 |
| 드캐 프리셋 + 이름 | `dreamcatcherDecks: List<DeckSave>` + `DeckSave.name` | 이미 존재 |

따라서 이 spec 은 **타입 개명 + UI + 작업본 규율**이며 **JSON 마이그레이션이 없다.** `squad_1` 을 "프리셋 #1 로 승격" 할 필요도 없다 — 그건 이미 프리셋 #1 이다. `schemaVersion` 은 1 을 유지한다(변환 없는 bump 는 소음).

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_types_and_limits.md` | 타입 개명(`SquadSave`→`SquadPreset` 등) + `NormalizePresets` + `MaxPresets` + `PresetIds` + EditMode |
| 1 | 로직 | `1_preset_diff.md` | `PresetDiff.IsDirty` 순수 함수 + EditMode |
| 2 | UI 뷰 | `2_preset_bar_view.md` | `PresetBarView` — 커스텀 목록 팝업 + 이름 필드 + 버튼 4 + dirty 배지 (프레젠테이션 전용) |
| 3 | 통합 | `3_squad_page_wiring.md` | 스쿼드 페이지 — in-place 편집 제거 → 작업본 도입 + 4조작 배선 |
| 4 | 통합 | `4_dreamcatcher_page_wiring.md` | 드림캐쳐 페이지 — 동형 |
| 5 | 정리 | `5_seed_prune_fallback.md` | `EnsureDefault*` 시드 · `DeckPrune` 전체 훑기 · 빈-스쿼드 draft 폴백 검토 |
| 6 | 테스트 | `6_tests.md` | PlayMode 스모크 2개 수정 + "확정=저장분" 회귀 |
| 7 | 인계 | `7_handoff_summary.md` | handoff (구현 종료 시 작성) |

순서: 0 → 1 → 2 → (3 → 4) → 5 → 6 → 7. 0·1 은 순수 로직이라 UI 없이 테스트로 닫힌다. 3·4 는 서로 독립이므로 순서를 바꿔도 된다. 핵심 로직 단위(0·1·3) 종료 시 code-review.

## feature-wide 계약

1. **스쿼드 프리셋은 유닛+스톤 통합.** `SquadPreset` 한 객체가 `unitIds[7]` 과 `stoneIds[4]` 를 함께 들고, [저장] 한 번이 둘을 함께 기록한다. 스톤 전용 드롭다운·전용 저장 버튼 없음. **`DreamstonePreset` 타입은 만들지 않는다** — 1:1 로 묶인 병렬 리스트는 짝 어긋남이라는 없어도 될 실패 모드만 만들고, 그걸 수리하는 정규화 함수를 요구한다.
2. **저장 원칙: 구조는 즉시, 내용은 명시적.** 프리셋 **생성·삭제·확정**은 즉시 디스크에 쓴다. **내용 변경**(유닛·스톤·카드·이름)만 [저장] 대상이다. 대가는 수용한다 — `[+]` 직후 이탈하면 빈 프리셋이 남고 30 상한을 잡아먹는다(삭제 가능).
3. **저장과 확정은 완전 분리.** [선택]은 **디스크에 저장된** 내용을 확정할 뿐 작업본을 기록하지 않는다. 그 결과 "확정된 내용 ≠ 화면에 보이는 내용" 상태가 존재할 수 있으므로, **계약 4 의 가시화가 필수 짝**이다.
4. **미저장 상태를 상시 가시화한다.** dirty 일 때 프리셋 바에 `● 미저장 변경 — 반입은 지금 저장분` 배지 + [저장] 엑센트 색. 이게 없으면 계약 3 의 의미론이 조용히 잘못된 편성으로 게임을 시작하게 만든다. 팝업이 아니라 상시 표시다.
5. **JSON 필드명 불변.** `squads` · `dreamcatcherDecks` · `selectedSquadId` · `selectedDeckId` 를 그대로 쓴다. C# **타입명만** 바꾼다. 필드명까지 바꾸면 마이그레이션이 부활하고 얻는 건 정돈뿐이다 — 실기기 프로필 파괴 리스크와 교환할 값이 아니다. `List<SquadPreset> squads` 의 약한 불일치는 주석으로 감당한다.
6. **확정 접근자는 살아있는 참조를 반환한다.** `CommittedSquad()`/`CommittedDeck()` 은 투영이 아니라 리스트의 실제 객체다(개명 전 `SelectedSquad`/`SelectedDeck` 과 동일 동작). **반환값을 통한 쓰기를 막는 장치는 없다** — 읽기전용 타입은 제약 8 위반이라 만들지 않는다. 규율은 *"프리셋 리스트에 쓰는 것은 페이지 컨트롤러의 저장 경로뿐"* 이며 메서드 주석에 못박는다.
7. **작업본은 저장본의 분리 복제.** 페이지는 프리셋을 **직접 편집하지 않는다**(현 `SquadCharacterPageController` 의 in-place 변이 제거가 이 spec 의 실질 작업). dirty 는 작업본과 저장본의 순수 시퀀스 비교로 판정하며 플래그를 들고 다니지 않는다 — 뺐다 다시 넣으면 dirty 가 정확히 꺼져야 한다.
8. **빈 확정 프리셋 허용.** 리셋→저장으로 확정 편성이 0유닛/0카드가 될 수 있고, START 는 `LoadoutGate` 가 막는다. 단 `GameManager` 의 빈-스쿼드→draft 폴백이 게이트 우회 경로에 살아 있으므로 unit 5 에서 별도 검토한다.
9. **목록은 커스텀 팝업이다.** `TMP_Dropdown` 은 리치 셀을 못 만든다. `SquadRosterBrowser.EnsureGridBuilt` 의 스크롤 골격을 재사용한다.
10. **아키텍처.** 전부 MonoBehaviour 프레젠테이션 + `Wassup.Core` 프로필 헬퍼. ECS 맥락·BattleBridge 변경 0. 아키텍처 중립 계산(`PresetDiff`·`PresetIds`)만 순수 static 함수로 분리(제약 10).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트나 생성→렌더 경로 변경이 없다. 아웃게임 UI + 프로필 저장 의미론만 다루므로 `docs/reference/object-pipeline-map.md` 대조 대상이 아니다.

## 후속 후보

- **프리셋 슬롯 확장** — `PlayerProfile.MaxPresets` 를 const 에서 SO 로 이관(상한을 재화/과금 knob 으로 쓸 때).
- **프리셋 복제** — 기존 프리셋을 복사해 새 프리셋으로. `[+]` 가 빈 프리셋만 만드는 현 계약의 확장.
- **프리셋 간 diff 보기** — 두 프리셋을 나란히 비교.
- **확정 프리셋 뱃지를 로비에** — 로비에서 현재 확정 편성 요약을 보여주기.
- **스쿼드·드캐 프리셋 짝 확정** — 지금은 두 페이지가 완전 독립이다. "로드아웃 세트"(스쿼드+덱 한 쌍)를 한 번에 확정하는 상위 개념.
- **드래그로 프리셋 순서 재배열.**
