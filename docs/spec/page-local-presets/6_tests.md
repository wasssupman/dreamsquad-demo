# 6 — 테스트 수정 · 추가

## 목적

개명으로 깨지는 테스트를 고치고, 이 spec 이 도입한 **의미론**에 회귀 테스트를 붙인다. 가장 중요한 건 계약 3(저장/확정 분리)에 대한 테스트다 — 사용자가 명시적으로 고른 규칙이고, 틀리면 잘못된 편성으로 게임이 시작되는데 지금은 그걸 잡는 테스트가 없다.

## 변경 대상

**수정**:
- `Assets/_Project/Tests/PlayMode/SquadCarryInSmokeTest.cs` (`:24~52`)
- `Assets/_Project/Tests/PlayMode/DreamstoneCarryInSmokeTest.cs` (`:33~240`)
- `Assets/_Project/Tests/EditMode/DreamcatcherDeckAutosaveTests.cs` (자동 저장 → 명시 저장)
- `Assets/_Project/Tests/EditMode/` 의 개명 영향분 (`ProfileStoreTests`, `ProfileStoreDefaultDeckTests`, `LoadoutGateTests`, `Profile/DeckPruneTests`)

**신규**:
- `Assets/_Project/Tests/EditMode/Profile/PresetCommitSemanticsTests.cs`
- `Assets/_Project/Tests/PlayMode/PresetCarryInTest.cs`

## 구현

**1. PlayMode 스모크 2개** — 현재 `SelectedSquad()` 반환값에 직접 써서 편성을 세팅한다:
```csharp
var squad = profile.SelectedSquad();
squad.unitIds[0] = catalogIds[0];   // ← 살아있는 참조라 동작했다
```
개명 후에도 `CommittedSquad()` 는 살아있는 참조를 반환하므로(계약 6) **이 패턴은 계속 동작한다.** 따라서 수정은 개명 반영이 전부다. 단 계약 6 의 규율("리스트에 쓰는 것은 컨트롤러 저장 경로뿐")과 형태가 어긋나므로, 테스트가 **의도적으로 저장본을 직접 세팅한다**는 주석을 붙여 프로덕션 코드의 모범으로 오독되지 않게 한다.

**2. `DreamcatcherDeckAutosaveTests`** — 이름과 전제가 함께 바뀐다. "편집 즉시 저장" 을 검증하던 케이스를 "편집은 저장하지 않고, [저장] 호출이 저장한다" 로 전환. `ProfileSaver` 심으로 **저장 호출 횟수**를 세는 형태가 자연스럽다:
- 카드 추가/제거 N회 → 저장 호출 0회
- [저장] 1회 → 저장 호출 1회, 내용 일치
- 유효하지 않은 중간 덱(9/10)도 [저장]으로 저장됨(기존 계약 유지 — 게이트가 START 를 막는다)

파일명을 `DreamcatcherDeckSaveTests.cs` 로 개명하는 편이 정확하다.

**3. `PresetCommitSemanticsTests`** (핵심):
- **저장 없이 확정** → `CommittedSquad()` 가 돌려주는 것은 **저장본**이지 작업본이 아니다 (계약 3 회귀)
- 확정 포인터 변경이 프리셋 **내용을 바꾸지 않음**
- [저장]이 확정 포인터를 **바꾸지 않음**
- 확정분 삭제 차단 · 마지막 1개 삭제 차단
- 30개 초과 생성 차단
- 삭제 후 확정 포인터가 실존 엔트리를 가리킴
- 프리셋 A 편집 → B 로 전환 → A 로 복귀 시 A 의 **저장본**이 보인다(작업본 유실이 정상)
- **[되돌리기]가 저장본 기준 복원**이고 dirty 를 끈다 · 신규 빈 프리셋에서는 완전 비움 · 디스크 미기록 · 스톤도 함께 복원 (rev 2026-07-30)

**4. `PresetCarryInTest`** (PlayMode): 프리셋 2개를 만들어 서로 다른 유닛·스톤을 저장하고, #2 를 확정한 뒤 배틀 진입 → **#2 의 유닛·스톤이 반입**된다. 기존 `DreamstoneCarryInSmokeTest` 의 검증 방식(`StatModifierSlot` origin 직독)을 재사용해 기믹 오염을 피한다.

Play 중에는 MCP 뮤테이션·코루틴·에디터 틱이 동결되므로 입력은 reflection 으로 컨트롤러 메서드를 직접 구동한다(기존 PlayMode 테스트 관례).

## 완료 기준

- [ ] EditMode 전체 그린
- [ ] PlayMode 전체 그린
- [ ] `PresetCommitSemanticsTests` 8케이스 전부 그린
- [ ] `PresetCarryInTest` 그린 — 확정한 프리셋의 유닛·스톤이 반입됨
- [ ] "저장 없이 확정 → 저장분 반입" 케이스가 실제로 **실패할 수 있는 테스트**임을 확인 (저장/확정을 일부러 합쳐보면 빨개지는지 1회 검증 후 되돌린다)
- [ ] 개명 영향 테스트에서 **어서션 값이 바뀐 것이 없음** (이름 기본값 `"스쿼드 1"`/`"덱 1"` 과 자동→명시 저장 전환분은 예외)

---

**검증 기록 2026-07-30 · `2e4f4c63`** — EditMode **1660/1658 pass/0 fail** · PlayMode 프리셋 3건 통과(`f5f7608f`) · PlayMode 전체 79건 중 13건은 격리 실행으로 **사전 실패 확인**(이번 작업 귀속 0). `DreamcatcherDeckAutosaveTests` → `DreamcatcherDeckSaveTests` 전제 반전 완료. **미검증**: PlayMode 전체 재실행(마지막 전체는 `f5f7608f` 기준) · `ActiveAllyZoneTest`·`DreamcatcherCombatDamageTest` 격리 확정.
