# 1 — 오라를 `DotEffect.element` 로 구동 (bridge 래치 삭제)

## 목적

상태 오라의 소스를 "bridge 가 스택 슬롯을 보고 추측"에서 "도트가 들고 있는 flavor"로 바꾼다.
추측이 사라지므로 래치·쿼리·매핑이 통째로 삭제되고, 결함 3건이 동시에 없어진다.

| 결함 | 왜 사라지는가 |
|---|---|
| stale 비트 (얼음 오라가 매치 끝까지 잔존) | 래치 자체가 없어짐 — 매 프레임 도트에서 직접 읽음 |
| 오귀속 (출혈만 아픈데 얼음 오라가 같이 뜸) | element 가 권위 — 실제 피해원만 켜짐 |
| 화염·독 오라가 영영 안 뜸 | 스택이 아니라 **해저드 도트**에 물림 → 처음으로 정상 동작 |

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 스택 오라 reconcile 블록 교체.
  **삭제**: `_stackAuraLatch` · `_stackAuraLatchDead` · `StackAuraFxKinds` · `StackAuraKind()` ·
  `_stackSlotQuery` / `_stackSlotQueryCreated` (선언 · 생성 · 해제 · 사용)
- `Assets/_Project/Scripts/Data/StatusFxKind.cs` — 소스 주석 갱신
- `Assets/_Project/Tests/PlayMode/BleedAuraOutlastsStackSlotTest.cs` — 관측 대상 교체

## 구현

스택 슬롯을 볼 이유가 사라졌으므로 `_stackSlotQuery` 를 버리고 `DotEffect` 버퍼를 가진 대상을
순회한다. flavor 가 `None` 이 아닌 슬롯마다 `Ensure`.

```
for each entity with DotEffect buffer:
    anchor = ResolveUnitViewTransform(e); if null → skip
    for each dot in buffer:
        if dot.remainingTime <= 0 || dot.element == None → skip
        fx = ElementToStatusFx(dot.element)      // 순수 static 매핑
        if fx.HasValue → statusFxSpawner.Ensure(e, fx.Value, anchor)
```

`StatusFxSpawner` 는 `BeginFrame`/`EndFrame` 으로 그 프레임에 `Ensure` 안 된 것을 내린다. 따라서
**꺼짐 처리를 따로 쓰지 않는다** — 도트가 끝나면 자동으로 사라진다. 래치가 하던 "언제 지울까"
문제가 통째로 없어지는 지점이다.

unit 0 이 이미 슬롯을 flavor 별로 분리했으므로, 한 대상에 출혈·화염이 같이 걸리면 **오라도 자연히
둘 다 뜬다.** 비트마스크 같은 별도 장치가 필요 없다.

⚠ 얼음은 `ApplyDot` 임계가 없어 뜨지 않는다 — 의도된 결과(README "알려진 결정").

## 완료 기준

- [x] `_stackAuraLatch` · `_stackAuraLatchDead` · `StackAuraFxKinds` · `StackAuraKind` ·
      `_stackSlotQuery` 가 코드에서 사라짐 (grep 0건)
- [x] `BleedAuraOutlastsStackSlotTest` 를 폐기하고 `DotAuraFromFlavorTest` 로 대체. 관측 대상이
      private 딕셔너리가 아니라 **`StatusFxSpawner._active`(실제 스폰 결과)** 다(1차 리뷰 지적 반영).
      뷰가 필요하므로 합성 더미 대신 **배치된 실제 유닛**을 피해자로 쓴다
- [x] **stale 회귀 테스트**: 냉기 도트를 얹고 그것만 만료시킨 뒤, 출혈 도트가 계속 도는 구간에서
      `IceStack` 오라가 **꺼져 있음**을 단언 (`00a65045` 의 OR 마스크에서는 영영 안 꺼졌다)
- [x] **동시 오라 테스트**: 출혈 + 냉기 도트가 같이 도는 대상에 오라 2개가 뜬다 — unit 0 이 슬롯을
      갈라놔서 별도 장치 없이 성립
- [x] 리그 PlayMode 55 통과 / 13 실패 = 베이스라인 동일. EditMode 1577 통과.
      (신규 테스트는 단독 실행 green — 배치 실행에서는 DOTween 로그 아티팩트로 실패하는데,
      폐기한 `BleedAuraOutlastsStackSlotTest` 도 베이스라인에서 같은 이유로 실패했다)
- [ ] Play 확인: 난도질꾼이 문 적에 출혈 오라만 / 화염 장판 위 적에 화염 오라 / 얼음 오라 없음
