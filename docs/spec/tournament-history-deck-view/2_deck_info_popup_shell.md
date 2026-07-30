# 3 — DeckInfo 팝업 셸 + 스쿼드 탭

## 목적

덱을 보여주는 **순수 프레젠테이션 팝업**을 만든다. 페이로드를 받으면 구성되고, 네트워크·세션·프로필을 모른다. 이 unit 에서 탭 프레임과 좌/우 레이아웃, 견고성 계약, 그리고 스쿼드 탭까지 한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/DeckInfoPopup.cs`
- 신규 `Assets/_Project/Tests/EditMode/DeckInfoPopupTests.cs`

## 구현

**입력**

```csharp
public void Show(TournamentDeckInfo.Payload payload, string title);
```

`payload == null` 이 정상 입력이다(덱 정보 없음). 카탈로그 3종은 **`Setup(units, stones, cards)` 로 주입한다** — 이 팝업은 히스토리 패널이 런타임에 만들어서 씬에 오브젝트가 없고, 따라서 자기 `[SerializeField]` 가 채워지지 않는다. `[SerializeField]` 는 **패널**이 들고 팝업에 넘긴다. 셋 중 무엇이 null 이어도 동작해야 한다(id 만으로 렌더). 자기-빌드 캔버스 + 모달 dim + 닫기 — 은퇴한 `TournamentDetailPopup` 의 빌드 패턴을 따른다(`UiCanvasSetup` / `UiRoundedSprite` / `UiLayer`, 중첩 캔버스라 `overrideSorting` 필수).

**레이아웃**

- 상단 탭 2개: `스쿼드` / `드림캐쳐`. 탭 전환은 데이터 재조회 없이 뷰만 갈아끼운다.
- 각 탭: **좌 = 선택된 항목 상세**, **우 = 목록**. 목록에서 항목을 고르면 좌측이 갱신된다.
- 목록 아래에 **"프리셋 적용" 버튼 영역**(두 탭 공유). 이번 spec 은 **자리와 모양까지**이고 버튼은 `interactable = false` 로 둔다(계약 12). 나중에 끼워 넣으면 두 탭 레이아웃을 다시 짜야 해서 지금 잡는다. **내 덱을 볼 때는 통째로 숨기고** 목록이 그 자리까지 내려온다 — `Show(..., allowPresetApply)` 로 호출자가 판정해 넘긴다.
- 탭 진입 시 기본 선택 = 그 탭 목록의 첫 항목. 목록이 비면 좌측은 "없음" 안내.

**스쿼드 탭** — 우측 목록은 두 섹션이다: `유닛`(`payload.squad.units`) + `드림스톤`(`payload.squad.stones`). 둘 다 **가변 개수 그리드**다(계약 10 — 고정 슬롯 금지). 좌측 상세는 선택된 항목의 종류에 따라 그린다.

- 유닛: `DefenderCatalog.ById` → `portrait` / `displayName` / `rarity` / `desc`
- 스톤: `DreamstoneCatalog.ById` → `icon` / `displayName` / `grade`

**견고성 (계약 9 — 이 유닛의 본체)**

| 입력 | 표시 |
|---|---|
| `payload == null` | 두 탭 모두 "덱 정보가 없습니다" |
| 섹션 배열이 빔 | 그 섹션만 "없음", 나머지는 정상 |
| 카탈로그가 모르는 id | **슬롯 유지** + raw id 텍스트 + 플레이스홀더 아트 |
| 카탈로그가 null | 전 항목을 raw id 로 렌더 (예외 없음) |
| 개수 초과/부족 | 온 만큼 전부 그린다 |
| 아트가 null인 항목 | 플레이스홀더 (이름은 그대로) |

**순수 분리** — "id 목록 + 카탈로그 → 표시 항목 목록(이름/아트/미해석 여부)" 변환은 MonoBehaviour 밖 순수 함수로 둔다. 위 표 전체가 이 함수의 테스트 대상이 된다(제약 10 — 비자명한 분기 + 회귀 가치).

## 완료 기준

- [x] 컴파일 통과
- [x] EditMode: 위 견고성 표 6줄 전부 — 예외 없이 기대 항목 수/라벨이 나온다 (`DeckInfoDisplayTests` 8건 + `DeckInfoPopupTests` 7건)
- [x] **자기 rect 를 먼저 편다** — 런타임 생성 자식이라 RectTransform 이 기본 100×100 이고 중첩 캔버스는 rect 를 구동하지 않는다. 안 펴면 dim 이 화면을 못 덮어 암전이 사라지고 뒤 페이지로 클릭이 통과한다(`PresetConfirmPopup.EnsureBuilt` 선례)
- [x] 선택이 없을 때 좌측 컬럼을 **끄지 않고** 안내를 띄운다 (끄면 380px 가 구멍으로 보인다)
- [x] EditMode: 미해석 id 가 **버려지지 않고** raw id 로 남는다 (`UnknownId_KeepsSlot_AsRawId`)
- [x] 팝업을 열고 탭을 오가도 선택이 각 탭별로 유지된다 (`TabSelection_IsKeptPerTab`)
- [x] 스쿼드 탭에서 유닛/스톤을 고르면 좌측 상세가 바뀐다
- [x] "프리셋 적용" 버튼이 자리에 있고 **비활성**이다 — 눌러도 아무 일이 없고, 콘솔에 경고/예외도 없다

확인: 2026-07-30 EditMode green + unit 4 에서 실서버 Play 시각 확인 완료.
