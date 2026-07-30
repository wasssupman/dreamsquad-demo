# 3. 드림캐쳐 displayName 축약 규칙

## 목적

카드 선택 화면에서 이름만 훑어도 효과 방향을 파악할 수 있게 한다. 이름은 식별용 요약이고, 발동 조건·지속시간·수치는 구조화된 설명 템플릿의 책임으로 분리한다.

## 변경 계약

- 변경 대상은 `DreamcatcherCard.displayName`뿐이다.
- `id`, `.asset` 파일명, GUID, `effects[]`, `mechanics[]`, `attackMods[]`, `description`은 변경하지 않는다.
- 이름은 기본 2~5자(공백 제외)로 작성하고, 수치·횟수·지속시간을 넣지 않는다. `1코`는 기존 축 한정어로 허용한다.
- 약어는 `딜`=공격력, `속`=공격속도, `체`=체력/생존력, `이속`=이동속도, `CC딜`=CC 대상 추가 피해로 고정한다.
- 대상/발동어를 앞세운다: `실드`, `킬`, `사망`, `빈사`, `제물`, `가시` 등. 같은 실드 발동 카드도 결과어로 구분한다.
- active 카드도 같은 화면 언어를 쓰므로 영문 이름을 짧은 한글 동작명으로 통일한다.

## 확정 매핑

| id | 기존 displayName | 축약 displayName |
|---|---|---|
| `active_meteor` | Meteor | 운석 |
| `active_portal` | Portal | 포탈 |
| `active_power_surge` | Power Surge | 공격폭증 |
| `active_rapid_fire` | Rapid Fire | 속사 |
| `active_slow_field` | Slow Field | 감속장 |
| `active_tornado` | Tornado | 회오리 |
| `all_atk` | 올 핵딜 | 올딜 |
| `all_move` | 올 발업 | 올이속 |
| `bouncy_bead` | 통통 구슬 | 튕구슬 |
| `sub_butterfly_dream` | 호접몽 | 나비꿈 |
| `calamity_heart` | 재앙의 심장 | 시한폭탄 |
| `cornered_burst` | 궁지의 몸부림 | 궁지폭발 |
| `corpse_burst` | 터지는 악몽 | 시체폭발 |
| `cost1_as` | 1코 폭타 | 1코속 |
| `cost1_hp` | 1코 존버 | 1코체 |
| `cracked_grail` | 금이 간 성배 | 피값딜 |
| `devouring_craving` | 포식의 갈망 | 킬속 |
| `ember_bite` | 불씨 물기 | 출혈 |
| `execution_strike` | 끝맺는 일격 | 처형타 |
| `eye_on_the_end` | 끝을 보는 눈 | 우선조준 |
| `farewell` | 작별 선물 | 사망폭발 |
| `sub_fattened_offering` | 살찌운 제물 | 제물표식 |
| `frost_arrow` | 서리의 화살 | ~~빙결~~ → 스턴메이커 (아래 각주) |
| `frostbite` | 살을 에는 서리 | 동상 |
| `gale_shove` | 돌풍의 손길 | 밀치기 |
| `guardian_as` | 가디언 폭타 | 가디언속 |
| `guardian_fortress` | 가디언 풀존버 | 가디언벽 |
| `guardian_hp` | 가디언 존버 | 가디언체 |
| `heavy_strike` | 응축된 일격 | 강타 |
| `sub_incubus_pact` | 몽마의 계약 | 희생계약 |
| `last_flame` | 마지막 불꽃 | 불꽃폭주 |
| `last_stand` | 최후의 발악 | 빈사폭주 |
| `lullaby_dart` | 꿈결의 자장가 | 자장가 |
| `nightmare_afterglow` | 악몽의 여운 | 킬딜 |
| `poke_needle` | 콕콕 바늘 | 비수 |
| `ranger_as` | 레인저 폭타 | 레인저속 |
| `ranger_atk` | 레인저 핵딜 | 레인저딜 |
| `ranger_hp` | 레인저 존버 | 레인저체 |
| `shatter_hymn` | 산산이 부수는 성가 | CC딜 |
| `shield_burst` | 산산조각 | 실드폭발 |
| `shield_lull` | 고요한 파문 | 실드수면 |
| `slow_awakening` | 느린 각성 | 공속각성 |
| `thornmail` | 가시 갑옷 | 가시반격 |
| `tremor_plate` | 울리는 갑주 | 진동갑주 |

### 각주 — `frost_arrow` 재명명 (2026-07-31, dreamcatcher-content-3 unit 6)

"빙결"은 **`StackKind.Ice` 의 표시 라벨과 같은 단어**였다. 동상(`frostbite`)이 쌓는 것이 "빙결 1스택"
이라, 카드 선택 화면에서 동상을 찾다가 `frost_arrow` 를 열게 되는 충돌이 실제로 발생했다.
**이름은 다른 카드의 효과 어휘와도 겹치면 안 된다** — 이 규칙을 위 계약에 추가로 적용한다.

시트(`DcCards`)에는 이미 "스턴메이커"가 authoring 돼 있었고 SO 만 옛 값으로 남아 있었다(드리프트).
SO 를 시트에 맞추는 방향으로 통일했다.

## 운영/검증

- SO 반영 후 `Export Dreamcatcher → 시트 페이로드`를 다시 생성해 `DcCards.displayName`을 갱신한다. 기존 시트 행은 id 키를 유지하므로 rename이 아니라 값 갱신이다.
- 이름 매핑 테스트는 37개 카드(id별 exact value)를 잠가 누락·중복·영문 회귀를 검출한다.
- 설명 템플릿 테스트는 이름 변경과 독립적으로 기존 수치/조건 표시를 검증한다.

## 완료 기준

- [x] 37개 active/card SO의 displayName이 매핑과 일치한다.
- [x] id, GUID, effects/mechanics/attackMods가 이번 이름 변경 diff에 나타나지 않는다.
- [x] export snapshot의 37개 DcCards row와 SO displayName이 일치한다.
- [x] EditMode 전체 통과 및 중복 이름 0건을 확인한다.
