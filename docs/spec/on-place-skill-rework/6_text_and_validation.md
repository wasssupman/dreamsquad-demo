# 6 — 문안 · 검증

## 목적

바뀐 세 배치 스킬이 **게임 안에서 그렇게 설명되게** 하고, spec 의 검증 질문 둘에 답한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — 규칙 기반 배치 스킬 문안 경로
- **스프레드시트** 유닛 탭 `desc` 열 — 캐논·배스티온·말파이트 3행 (⚠ 에셋 아님)
- `docs/spec/on-place-skill-rework/README.md` — 상태 라인 + 커밋 해시
- 신규 `7_handoff_summary.md`

## 구현

### `UnitKitSummary` — 규칙도 읽는다

`OnPlaceClause(OnPlaceEffectType)` 는 레거시 enum 만 안다. `onPlaceEffect == None` 이지만
`UnitSkillAbility` 로 배치 스킬을 가진 유닛(캐논·배스티온)은 지금 **설명이 조용히 빈다** —
그 파일의 주석이 이미 `default: return ""` 의 위험을 경고하고 있다.

`UnitSkillAbility` 의 `OnPlace` 규칙을 읽어 payload 별 문안을 내는 경로를 더한다:

```
EmitProjectilePattern → "배치 시 주변 적 전원에게 미사일 낙하"
AreaTaunt             → "배치 시 주변 적을 한꺼번에 끌어모음"
```

「어그로」가 아니라 **「도발」** 계열 어휘를 쓴다 — 게임 문안의 기존 표현이다
(`first-session-tutorial` unit 11).

⚠ **배스티온은 두 도발을 화면에서 갈라야 한다.** 현재 `desc` 마지막 줄이 이미
`특수 효과: 공격한 적 도발` 이다. 여기에 「주변 적 전원 도발」을 나란히 붙이면 **같은 단어 ·
같은 상태(`Aggroed`) · 같은 플레이스홀더 "!" 아이콘**이라 플레이어가 구분할 수 없다. sim 상
차이(상한 무시 · 즉시 · 시한)는 전부 숫자고 화면에 그 어휘가 없다. 유일하게 눈에 보이는 차이는
**「전원 / 한꺼번에」** 이므로 그 축으로 가른다:

- 배치 스킬: `주변 적을 한꺼번에 끌어모음`
- 특수 효과: `공격한 적이 나를 노림`

⚠ 레거시 enum 경로와 규칙 경로가 **둘 다 비어 있지 않으면** 문안이 두 줄 난다. unit 0 이
동시 선언을 loud warn 으로 막으므로 정상 데이터에선 발생하지 않지만, 문안 쪽도 규칙 우선으로
한 줄만 낸다.

### `desc` 는 시트에서 고친다 (⚠ 함정)

`desc` 는 `DefenderStatDto.desc` 로 **양방향 reflection 매핑**된다. 에셋만 고치면 다음 로그인
자동 임포트(`LoginAutoImport`)가 시트 값으로 되돌리고, 그 되돌림은 **한참 뒤 무관한 커밋에
섞여** 나타난다(`project_sheet_import_contaminates_commits`).

반대로 `onPlaceEffect`·`onPlaceRange`·`onPlaceDuration`·`onPlaceMagnitude`·`abilities` 는
DTO 에 컬럼이 없어 시트가 덮지 않는다 — units 2·4·5 의 에셋 편집은 안전하다.

시트 문안 초안:

| 유닛 | 배치 스킬 줄 |
|---|---|
| 캐논 | `배치 스킬: 주변 적 전원에게 미사일이 하나씩 떨어짐` |
| 배스티온 | `배치 스킬: 주변 적을 한꺼번에 끌어모음` · `특수 효과: 공격한 적이 나를 노림` |
| 말파이트 | `배치 스킬: 주변 적을 띄우고 3초 정지` |

⚠ 시트는 **읽기 전용 확인**(`curl`)으로 대조하고, 값 수정은 사용자가 시트에서 한다. 임포터는
dry-run 이 없어 돌리면 즉시 에셋에 쓴다(`project_sheet_verify_readonly`).

## 완료 기준

- [ ] EditMode `UnitKitSummaryTests` — 규칙만 가진 유닛(캐논·배스티온)이 **빈 문자열을 내지 않음**
- [ ] EditMode — **`OnPlaceEffectType` 과 배치 payload kind 를 `Enum.GetValues` 로 전수 순회**해
      빈 문자열이 없음을 고정한다. 지금은 `default: return ""` 함정이 테스트로 안 잡혀 있어
      같은 사고가 두 번 난다(`DcApplicabilityTests` 의 전수 검사 선례)
- [ ] 세 유닛의 캐릭터 페이지/카드 설명이 새 문안으로 뜬다(로비 → 스쿼드)
- [ ] **검증 질문 ① 확인**: unit 2 커밋의 `.cs` 변경이 테스트 파일 외 0
- [ ] **검증 질문 ② 육안 확인** — 배치 순간의 그림만 보고 무슨 일이 일어났는지 말할 수 있는가:
  - 캐논 → 반경 안에 미사일이 흩어져 내려와 터진다
  - 배스티온 → 적들이 경로를 버리고 몰려온다, 5초 뒤 흩어진다
  - 말파이트 → 튀어올랐다 떨어진 뒤 한동안 굳어 있다
- [ ] 전체 회귀: EditMode 전량 · on-place/어그로/투사체 PlayMode 전량
      (기존 실패 5건은 사전 실패로 분리 보고)
- [ ] `7_handoff_summary.md` 작성 + README 상태 라인 「완료 YYYY-MM-DD」
