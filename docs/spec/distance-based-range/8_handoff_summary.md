# 8 — handoff summary

> 다음 사람이 **여기부터** 읽는다. 최신 계약은 README 와 번호 문서가 우선한다.

## Commit

| 범위 | 커밋 |
|---|---|
| spec 작성·critic 수렴 | `9d814792` ~ `84b1e543` |
| 하네스 결함(골든이 전투를 못 담았다) | `89e65d05` · `fade423a` |
| unit 0 안전망 | `674cf654` · `a119fa73` |
| unit 1 술어 수렴 + 리뷰 반영 | `426cff4a` · `d17d05d2` · `8774882b` |
| 코퍼스에 연속 이동 아군 + 게이트 2종 | `820ed079` |
| unit 3 몸(`bodyRadius`, 저작 0) | `957c34d2` |
| unit 4a 자 교체 → **rev 2 진짜 원** | `85edd03e` → `72314313` |
| unit 4b 광역 · 4d 히스테리시스 | `2ab53e51` · `40de7dd2` |
| unit 5 표기(채움→링, 5커밋) | `0a36370d` · `f872cabd` · `13495b1c` · `ae380a6c` · `8b6ac4bb` · `b612fc54` |
| unit 7 대상 마크 | `5f360f62` |
| 리팩토링 · 리뷰 반영 2회 | `dab26b88` · `9db3d5c4` · `b950064f` |

## Implemented

- **사거리 술어가 하나다.** `SkillMath.InBodyReachWithHalfExtent` 가 **유일한 본문**이고
  `AttackReach`·`TileAoe`·배치 프리뷰·링·마크가 전부 그것을 지난다. `bothContinuous` 인자는 은퇴 —
  **그 인자의 존재 자체가 「사거리 안의 뜻이 누가 묻느냐에 따라 달랐다」였다.**
- **자 = 몸 사이 거리.** 1×1 끼리는 `|Δ| ≤ R + 0.5` = **진짜 원**. `halfExtent` 는 다칸 몸용으로
  살아 있고(오늘 호출부 0, **테스트가 계약을 진다**), 0.5 는 「한 칸의 몸 반지름」이다.
- **전투원에게 몸이 생겼다**(`bodyRadius`, `HitRadius`). 사거리·투사체 충돌·광역이 전부 더한다.
  **값은 전부 0** — 저작은 unit 6.
- **획득/유지가 갈렸다**(`h = 0.1`). 재서 정했다(정지 적 프레임 지터 0.047·0.051 실측).
- **표기가 판정에서 나온다.** 링·채움이 같은 SDF 셰이더 하나이고 `_HalfExtent`·`_Range` 는
  **판정 입력의 복사본**이다. 무효면 채도만 떨어진다 — 빨강은 고스트 충돌 전용.
- **사거리 안 공격 대상에 발밑 마크.** 빨강을 **시간으로** 가른다(무효면 끈다).
  방향 유닛·지원형은 안 켠다.
- 코퍼스가 8건(`summoner` 추가 — 순찰병이 구조적으로 못 들어왔었다) + 게이트 3종
  (공허·왕복·셋업) + `Bake Missing Goldens Only`.

## Key Files

| 무엇 | 어디 |
|---|---|
| **술어 본문(유일)** | `Scripts/Skills/SkillMath.cs` |
| 변환·소비처 목록 11 | `Scripts/Battle/Combat/AttackReach.cs` |
| 광역 | `Scripts/Battle/Combat/TileAoe.cs` |
| 획득/유지 | `Scripts/Battle/Combat/TargetPersistence.cs` |
| 몸 | `Scripts/Battle/Units/HitRadius.cs` · `Data/AttackUnitData.bodyRadius` |
| 표기(링·채움·마크) | `Scripts/Core/TilemapMapView.cs` · `Shaders/PlacementRangeRing.shader` |
| 대상 수집 | `BattleBridge.RefreshRangeTargetMarks` |
| 하네스·골든 | `Editor/Battle/SimHarnessRunner.cs` · `SimGoldenMenu.cs` |

## Verified

- EditMode **2671건 / 실패 2건** — 둘 다 **선행**(`boomerang`·`bomb_man` 문안, 시트 소관).
- PlayMode 교착 카나리아 통과. 골든 8건 전건 통과. 고정 스텝 **2회 실행 일치**.
- 코드 리뷰 2라운드 → **APPROVE**(CRITICAL 1 · HIGH 5 · MEDIUM 6 · LOW 9 전건 처리).

## Notes — 되돌리면 안 되는 것

1. **술어 본문을 복제하지 마라.** 두 벌이 되는 것이 이 spec 이 없애려던 문제다.
2. **표기가 모양을 다시 그리지 마라.** 프리뷰·링·마크가 전부 판정 함수를 지난다.
3. **정렬로 끊김을 피하지 마라.** 링·채움은 바닥 대역이다 — 세계에 그린 도형이 스프라이트를
   관통하면 UI 로 읽혀 물성이 깨진다. **끊김은 채움이 흡수한다.**
4. **`SelfBodyRadiusTiles` 를 √2−1 미만으로 내리지 마라** — 반경 1 폭발 20건이 십자로 붕괴한다
   (테스트가 막는다).
5. **`.mat` 의 `_Thickness` 를 올리면 마크 쿼드 여유를 함께 본다** — 안 그러면 테가 사각으로 잘리고
   증상은 「마크가 각졌다」로 나온다.
6. **2026-08-28 예외(사거리 칸에서 불가 표시 양보)를 되살리지 마라** — 채움 두 겹이 싸우던
   증상 대응이었고 지금은 겹칠 픽셀이 없다.
7. 「왜 없는지」를 적어 둔 곳 둘(`CoreShielded` 미필터 · `defenderClass` 미복제) — 지우면 다음
   사람이 같은 검산을 처음부터 다시 한다.

## Follow-up

- **unit 6(유일한 미완)** — 보스 `bodyRadius` 저작 + HP/방어 보상. ⚠ **같은 커밋**이어야 한다:
  `bodyRadius` 는 SO 지만 HP 는 **시트 정본**이라 `.asset` 만 고치면 다음 로그인 임포트가 되돌린다.
  면적 손실 재계산(사거리 1 **무손실** · 3 이 −25% 로 최악)은 `6_golden_regen_and_tuning.md`.
- **실기기/공성 맵 확인 3건** — 거점 사각 마크(적 거점이 있는 맵 필요) · 사거리 5 링 판독 ·
  unit 4d 진동 육안(이제 링이 그 도구다).
- **후속 후보** — 「누가 회복되나」 표기(힐러. 색 축을 새로 여는 별개 결정) · 해저드 장판/회오리의
  원-사각 불일치(비목표로 남겼다).
