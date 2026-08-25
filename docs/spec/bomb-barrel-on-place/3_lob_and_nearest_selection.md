# 3 — 곡사 배럴 궤적 + 발사 명세 「최근접」 선택

## 목적

배치 스킬이 기존 **발사 명세(패턴)** 어휘로 표현되게 하는 두 조각. ① 곡사로 날아가
착탄 시 설치물을 세우는 궤적, ② 「사거리 안 **가장 가까운** 적」 선택 규칙.
이 단위는 단독으로는 아무 동작도 안 한다(캐논의 토대 단위 선례).

## 변경 대상

- `Assets/_Project/Scripts/Data/ProjectileData.cs` — `ProjectileFlightMode` append + 설치물 SO 참조
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — flightMode→(이동,착탄) 매핑 + index 해석
- `Assets/_Project/Scripts/Data/ProjectilePatternData.cs` — `PatternSelectionRule.Nearest`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/PatternTargeting.cs` — 최근접 분기
- `Assets/_Project/Tests/EditMode/PatternTargetingTests.cs` — 최근접 단언

## 구현

- **`ProjectileFlightMode.BallisticBlocker`**(끝에 append) → 브리지 매핑에
  `(MovementKind.BallisticArcToPoint, PayloadKind.SpawnBlocker)` 한 줄.
  **emitter 의 분기 로직은 손대지 않는다** — `MovementBinding.Of(BallisticArcToPoint)` 가 이미
  셀 바인딩으로 분류한다. (아래 시그니처 변경으로 emitter 의 **호출 인자 한 개**는 바뀐다 —
  「emitter 무변경」이라고 쓰지 말 것, 리뷰 지적.)
- **설치물 SO 참조**: `ProjectileData` 에 `BlockingHazardSO spawnBlocker`. 브리지가 스폰 셋업에서
  레지스트리 인덱스로 바꿔 unit 2 의 `blockerDataIndex` 에 넣는다(SO→index 변환은 브리지 몫).
- **`PatternSelectionRule.Nearest`**(끝에 append): `PatternTargeting.Select` 에 분기 추가.
  ⚠ 최근접은 **시전자 칸을 알아야** 하므로 함수에 `int2 casterCell` 파라미터가 붙는다.
  **전 호출부가 컴파일 에러로 깨지므로** 인자를 채워 넣어야 한다(emitter 포함). 기존 두
  규칙은 `casterCell` 을 무시하므로 결과는 안 변한다(무회귀 단언으로 고정).
  오버로드를 새로 만들지 않는다 — 진입점이 둘이면 어느 쪽이 최근접을 지원하는지가 흐려진다.
  동률 tie-break 는 기존 규칙(row-major 셀 키 rank)을 **그대로 재사용**해 결정론을 유지한다.
  거리는 셀 체비셰프로 잰다(후보 필터 `scopeTileRange` 와 같은 자).
- 「최근접」은 폭탄맨 평타·상시 길막 캐스터가 이미 쓰는 규칙이다. 이 단위는 그 뜻을 발사
  명세 어휘로 **옮겨 적는 것**이지 새 판정을 만드는 게 아니다.

## 완료 기준

- [x] compile 0 에러.
- [x] `PatternTargetingTests`: 최근접 후보를 고른다 · 동거리는 기존 tie-break 와 같다 ·
      **기존 두 규칙의 결과가 한 건도 안 바뀐다**(회귀 핀).
- [x] flightMode enum 전수 테스트 통과.
- [x] 전체 EditMode 회귀 없음.

확인 2026-08-22 · 신규 단언 5건(최근접 선정·스냅샷 순서 무관·동률 tie-break·빈 풀·**기존 두 규칙
무회귀**) green. Play 에서 baked 슬롯 실측: `sel=Nearest scope=2 mv=BallisticArcToPoint pl=SpawnBlocker`.
