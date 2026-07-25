# 7 — 필드 형태 rev: 3필드 → `attachType` + `attachValue`

## 목적

부착 제한을 **판별자 1개 + 값 1개**로 줄인다. 사용자 제안(2026-07-25): 종류별 companion 필드보다 `attachType`/`attachValue` 가 직관적이다.

| 이전 (units 0~6) | 이후 (rev) |
|---|---|
| `attachRequire` (`DcAttachRequireKind`) | `attachType` (`DcAttachType`) |
| `attachRequireClass` (`DefenderClass`) | `attachValue` (string) — `Class` 면 클래스 이름 |
| `attachRequireUnitId` (string) | 같은 칸 — `UnitId` 면 유닛 id |

## 왜 지금인가

제한이 걸린 카드가 **0장**이고 시트 열도 아직 없다(에셋 0곳·export 0곳에서 키 부재 확인). 마이그레이션 비용이 0인 유일한 시점이다. 카드 하나라도 값이 들어가거나 시트에 열이 생긴 뒤에는 시트 수정 + 에셋 재직렬화가 따라붙는다.

## 이 rev 가 없애는 것

**리뷰 M2 함정이 구조적으로 소멸한다.** 구 설계는 두 companion 필드가 공존해서, `UnitId` 였던 카드에 `attachRequireClass=Support` 가 잔존한 상태로 kind 를 `Class` 로 바꾸면 의도(가디언 전용)와 다른 결과(서포트 전용)가 조용히 나왔다. `Class`+`Support` 는 유효 조합이라 validator 도 침묵했다. 값 칸이 하나면 종류를 바꿀 때 **같은 칸의 값을 반드시 함께 보게 된다** — 우회(export 에서 companion 을 보이게)가 아니라 원인 제거다.

부수 효과로 오타 검출 시점이 일관돼진다. 구 설계는 클래스 오타는 import 예외, 유닛 id 오타는 validator 였다. 이제 둘 다 validator 가 담당한다.

## 대가와 보완

- **인스펙터 드롭다운 상실**: `DefenderClass` enum 필드가 아니게 되어 손으로 이름을 적는다. 시트가 정식 제어 경로이고 거기선 원래 손으로 적었으므로 실손실은 Unity 직접 편집뿐. 필요해지면 `DreamcatcherCardEditor`(이미 존재)에 `attachType==Class` 일 때 드롭다운을 그려 되돌릴 수 있다.
- **import 즉시 예외 상실** → `TryParseAttachClass` + validator 가 대신한다. 오타(`Gaurdian`)·숫자(`2`)·`None`·빈 값 모두 fail-closed 로 막고, validator 가 **문제 값과 허용 이름을 문구에 담아** 신고한다.

## 값 해석 계약

`TryParseAttachClass`(`DreamcatcherAttachEval`)가 "무엇이 유효한 클래스 값인가"의 단일 지점 — 판정·무효검사·문안·validator 가 모두 이걸 쓴다.

- **대소문자 무시**(`guardian`·`GUARDIAN` 허용). 시트에 손으로 적는 값이고 이름끼리 대소문자만 다른 쌍이 없다.
- **숫자 문자열 배제**: `Enum.TryParse` 는 `"2"` 를 통과시키므로 **이름 왕복 검사**로 막는다. 시트에 숫자를 적었을 때 우연히 엉뚱한 클래스가 되는 추적 불가 버그를 차단.
- **`None` 은 실패**(제한으로서 무의미) → fail-closed.
- **유닛 id 는 ordinal 유지** — 저장 키라 대소문자가 다르면 다른 유닛이다. 클래스와 규칙이 다른 점은 의도.

## 완료 기준

- compile 에러 0.
- 옛 이름(`attachRequire*` / `DcAttachRequireKind`) 잔존 0 — 코드·테스트·문서 전부.
- EditMode 전체 green + 신규: 대소문자 허용 / 오타·숫자·`None`·빈값 fail-closed / `TryParseAttachClass` 계약 / validator 의 오타 신고 문구(문제 값 + 허용 이름).
- 부착 게이트 e2e green, validator 실사 스캔 위반 0.

확인 2026-07-25 — 컴파일 에러 0 · 옛 이름 잔존 0 · EditMode **1343건**(1341 pass / 0 fail / 2 기존 Ignore) · PlayMode e2e 2/2 pass · validator 실사 `카드 44장 중 0건`.
