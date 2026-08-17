# 11 — 철거: 두 조준을 만든 특수 케이스

> 선행: unit 10(캐논이 새 축으로 넘어간 뒤). 이 유닛은 **순수 삭제**다 — 새 기능 0.

## 목적

unit 1·8 이 「칸 조준 궤적으로 적 조준을 흉내내려고」 넣은 장치를 걷어낸다. 캐논이 unit 10 에서
적 조준 궤적으로 옮겨갔으므로 이들의 **소비자가 0** 이 된다. 남겨두면 다음 사람이 「셀 낙하탄에
`target` 을 실으면 되는구나」를 다시 배운다.

지우면 `TileAoe` 가 **다시 순수 광역**으로 돌아온다 — 「그 칸에 있는 것을 때린다」 한 문장.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs`
  — `TileAoe` 팔의 `designated` 게이트
- `.../Projectile/Emission/ProjectileEmitterSystem.cs`
  — Cell 바인딩 fan-out 분기 전체(rank 삽입정렬 · `cellSlot`/`inCell` · 칸내 시차) + `SubCellOffset`
- `.../Projectile/Emission/PatternScope.cs` — 이력 주석 갱신(결말을 적는다)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryBuildPatternSlot` 저작 검증에
  「fan-out × 비-Entity 조준」 거절 추가. 삭제한 분기가 **조용히 no-op 으로 흐르지 않게**
  authoring 시점에 loud 로 끊는다(이 파일의 기존 관례 = `scopeTileRange 0` 거절과 같은 자리)
- `Assets/_Project/Tests/PlayMode/OnPlaceSkyStrikeTest.cs`
  — 접기/게이트를 전제한 단언이 있으면 새 계약으로 갱신

## 구현

1. **`designated` 게이트 삭제.** 기존 `TileAoe` 발사는 **전부 `target` 을 비워 둔다**(unit 8 감사
   완료) → 그들에겐 no-op. 캐논은 이제 `SingleSplash` 라 이 팔에 오지 않는다.
2. **Cell fan-out 분기 삭제.** 오늘 `fanOutToAllCandidates: 1` 은 `Pattern_Cannon_Strike` 뿐이고
   unit 10 이 그것을 Entity 분기로 옮긴다 → 유일 소비자가 사라진다. `SubCellOffset`(0.28타일
   비켜 떨어뜨리기)도 같이 사라진다 — **적 조준은 겹칠 수가 없다**(적끼리 분리 반경이 있다).
   이게 「낙하가 2발로 보인다」가 구조적으로 해소되는 지점이다.
3. **주석에 결말을 남긴다.** `PatternScope` 의 이력이 지금 「접기 → 임자 게이트」에서 끝나 있다.
   그 게이트가 왜 결함이었는지(조준이 둘 → 예고 시간만큼 어긋남 → 실측 0.8타일 > 0.5타일)와
   무엇으로 대체됐는지(축 개통)를 이어 적는다.

## 완료 기준

- 삭제 후 **전 테스트 초록** — unit 9 의 2개 + 기존 5개 + `ProjectileSystemTests` +
  `PatternScopeTests` + `MovementBindingTests`.
- `ProjectileHitSystem` 의 `TileAoe` 팔에 `target` 을 읽는 코드가 **한 줄도 없다**(grep 으로 확인).
- 순 코드량이 **줄어든다** — unit 1 의 접기와 unit 8 의 게이트·정렬·오프셋이 전부 사라지고
  추가된 것은 궤적 1종뿐.
- 기존 광역 발사(메테오 · 보스 barrage · 진동갑주 · ballistic 평타) Play 육안 무변화.

### 검증 (2026-08-17)

- PlayMode `OnPlaceSkyStrikeTest` **7/7 초록**(기존 5 + unit 9 의 2).
- EditMode 전체 **2304/2307 초록 · 실패 0**(스킵 3건은 전부 기존 `[Ignore]`).
  그중 `ProjectileSystemTests.TileAoe_Payload_Damages_Every_Enemy_In_Impact_Range` 가 초록이라
  **게이트 제거가 순수 광역을 깨지 않았다**는 직접 증거다.
- `PatternTargetingTests.MovementBinding_ClassifiesEveryKnownKind` 초록 — 분류 전수 + 새 핀.
- 코드량: fan-out 분기 118줄 → 85줄, 헬퍼 `SubCellOffset` 제거, 착탄 게이트 1줄 제거.
  **순 삭제**이고 추가된 것은 궤적 1종이다.
- 남은 것: **Play 육안**(미사일이 적 위에서 터지는가 · 뭉친 적에게 발수가 적 수를 따라가는가).
