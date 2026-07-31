# 0 — 문구 4단 분리 + 아이콘

## 목적

`displayName`(정서 카피) 하나가 룰 이름 자리까지 겸직해서 "무슨 기믹인지 모르겠다"가 났다. 문구를 4단으로 쪼개 각 필드가 자기 화면 하나만 책임지게 하고, 0.5초 재인지 경로인 **아이콘**을 신설한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Gimmick/GimmickData.cs` — 필드 3개 추가
- `Assets/_Project/Data/Gimmick/Gimmick_{Burnout,RedBull,ClockOut,Onsen}.asset` — 카피 기입 + 아이콘 배선
- `Assets/_Project/Art/GimmickIcons/` — 아이콘 4종 (신규 폴더)
- `Assets/_Project/Scripts/UI/GimmickGuideView.cs` — 기존 카드가 새 필드 소비 (위치·수명 불변)

## 구현

**필드 추가** (`GimmickData` base — concrete 4종이 공유):

| 필드 | 타입 | 길이 | 소비처 |
|---|---|---|---|
| `ruleLabel` | string | 2~4자 | 리빌 주제목 (unit 1) |
| `summary` | string | 15~25자 | 리빌 한 줄 (unit 1) |
| `icon` | Sprite | — | 리빌 대형 아이콘 (unit 1) |

`displayName`(정서 카피)·`description`(3줄)은 **그대로 둔다**. 정서 카피는 이 게임의 톤이라 버리지 않는다 — 리빌의 부제로 내려갈 뿐이다.

**대상 뱃지 필드는 만들지 않는다**(계약 1). `summary` 가 이미 대상을 말한다.

**4종 카피** (에셋 기입):

| 에셋 | `ruleLabel` | `summary` |
|---|---|---|
| Burnout | 번아웃 | 내 유닛은 오래 둘수록 약해진다 |
| RedBull | 레드불 | 밟으면 폭주 — 빨라지고, 곧 아프다 |
| ClockOut | 사직서 | 아군이 쓰러지면 사직서, 5장이면 메테오 |
| Onsen | 과열 | 처음엔 회복, 오래되면 화상 (죽진 않음) |

**아이콘 4종** — `Assets/_Project/Art/GimmickIcons/icon_gimmick_{burnout,redbull,clockout,onsen}.png`.

- 번아웃·과열은 기존 `Art/StackIcons/icon_stack_fatigue.png` / `icon_stack_heat.png` 의 시각 언어를 계승한다 — 같은 개념이 같은 그림으로 읽혀야 오버헤드 스택 아이콘과 충돌하지 않는다.
- **실질 신규 아트는 레드불·사직서 2개.** 임포트 설정은 `Art/StackIcons/` 전례를 따른다(같은 용도·같은 표시 크기대).
- 캐주얼 디펜스 아트 방향: 작은 크기에서 읽히는 단순 실루엣, 밝고 선명, 게임 어포던스가 분명할 것. RPG 컨셉아트·다크 판타지 금지.

**뷰 소비** — `GimmickGuideView.Populate`:
- 카드 제목을 2층으로: `ruleLabel` 주(대형) + `displayName` 부제(소형). 본문 = `summary` + `description`.
- 칩 라벨을 `"특수룰 · {displayName}"` → **아이콘 + `ruleLabel`**.
- 폴백 체인: `ruleLabel` → `displayName` → `gimmickId`. 아이콘 미할당이면 라벨만 표시(에셋↔코드 디커플링, `StackIconRegistry.IconFor` null 전례).
- **카드/칩의 위치·수명은 건드리지 않는다.** 이 유닛은 문구만 바꾼다 — 카드 은퇴는 unit 3 소관이고, 그래야 이 커밋을 단독 revert 해도 카드가 정상 동작한다.

## 완료 기준

- 컴파일 에러 0, EditMode 회귀 없음(기존 통과 수 유지).
- 4종 에셋 전부 `ruleLabel`/`summary`/`icon` 채워짐 — 빈 필드 없음.
- Play: 배치 페이즈 카드에 `번아웃` 이 주제목, `"불금은 없습니다!"` 가 부제로 보이고, 접힌 칩에 아이콘 + `번아웃` 이 보인다.
- `icon` 을 일부러 비운 상태에서 크래시 없이 라벨만 표시된다.
- 기믹 비활성(`gimmickEnabled=false`) 매치에서 무변화.
- **이 커밋 단독 revert 시** 기존 카드가 원래 문구로 정상 동작한다.
