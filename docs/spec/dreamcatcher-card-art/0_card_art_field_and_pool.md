# 0 · 데이터 — art 필드 + 카드 풀 10종

## 목적

`DreamcatcherCard` SO 에 아트 필드를 추가하고, 카드 풀을 6 → 10 종으로 확장한다(신규 메커닉 없음, 기존 효과 채널만).

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `public Sprite art;` 필드 추가(effects 뒤, 직렬화 순서 append).
- `Assets/_Project/Data/Dreamcatcher/Card_*.asset` — 신규 4종 생성.
- `Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset` — 10종 배열 등록.

## 구현

### art 필드
`DreamcatcherCard` 에 `public Sprite art;` 를 `effects` 필드 **뒤에** 추가. 기존 6 asset 은 art 미지정(null) — 폴백 렌더로 안전. 직렬화 순서 append 라 기존 값 relabel 없음.

### 신규 카드 4종 (모두 Normal, 기존 채널)
| id | displayName | axis | effect |
|---|---|---|---|
| `all_atk_8` | All ATK +8% | All(3) | AttackDamage(0) +8 |
| `all_move_10` | All Move +10% | All(3) | MoveSpeed(3) +10 |
| `ranger_hp_12` | Ranger HP +12% | ClassRanger(0) | EffectiveHealth(2) +12 |
| `guardian_as_8` | Guardian AS +8% | ClassGuardian(1) | AttackSpeed(1) +8 |

기존 `Card_RangerAtk10.asset` YAML 을 템플릿으로 GUID 만 신규 발급. category 는 기본 Normal(0).

### 카탈로그
`DreamcatcherCardCatalog.asset` `cards` 배열에 신규 4 asset GUID append → 총 10종. 순서: 기존 6 + 신규 4(위 표 순).

## 완료 기준

- [ ] `DreamcatcherCard` 컴파일 통과, `art` 필드가 인스펙터에 노출.
- [ ] 신규 4 asset + meta 생성, 각 고유 GUID.
- [ ] 카탈로그 `cards.Length == 10`, 중복 GUID 없음.
- [ ] 기존 덱(`DreamcatcherDeck_Default`)/저장 덱 로드 회귀 없음(카드 풀만 증가).
