# 2 — 소진 셀을 만지면 판 위 그 유닛으로

## 목적

죽은 칸을 **유닛 명부**로 바꾼다. 소진 셀을 탭하든 끌든 카메라가 판 위 그 유닛으로 가고 선택된다.
"못 놓는 이유"를 말로 설명하는 대신 **쟤가 저기 있다는 것을 보여주는 것**이 답이 된다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryGetDeployedEntity` read seam
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — public 진입점
- `Assets/_Project/Scripts/UI/DefenderDragSlot.cs` — 소진 시 호출
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 참조 전달(`Bind`)
- `Assets/_Project/Scenes/BattleScene.unity` — 배선 1건

## 구현

**브리지 read seam** — `_defenderByTile` 에서 그 타입의 **첫 번째** 엔티티.

```csharp
// defender-board-limit 2 — 이 유닛 타입이 판에 있으면 한 기를 돌려준다(트레이 소진 셀 → 유닛 이동).
// 순환(2기 이상일 때 다음 기로)은 후속 — 상한 1 이 기본이라 후보가 항상 1기다.
public bool TryGetDeployedEntity(DefenderUnitData unit, out Entity entity)
```

**선택 진입점** — `DcInspectController` 의 기존 `private void Select(Entity)` 를 **그대로 재사용**한다.
새 선택 경로를 만들지 않는다(선택의 결과가 두 갈래가 되면 안 된다).

```csharp
// defender-board-limit 2 — 트레이 소진 셀에서 들어오는 선택. 보드 탭(HandleTap)과 같은 결과를
// 내야 하므로 Select 를 그대로 탄다. 재탭 토글은 없다 — 트레이 셀은 "가리키기" 전용이다.
public void SelectDeployed(Entity entity)
```

`Select` 내부가 `TryGetUnitViewAnchor` 로 유효성을 확인하고 실패 시 스스로 닫으므로 추가 가드는
필요 없다.

**슬롯** — unit 1 에서 넣은 소진 게이트 자리에서, 차단만 하지 말고 선택을 부른다.
`OnBeginDrag`(끌기)와 `OnPointerClick`(탭) **둘 다 같은 결과**다(계약 5). 드래그 쪽은 기존
`_suppressedDrag = true` 를 유지해 이후 `OnDrag`/`OnEndDrag` 가 세션을 열지 못하게 한다.

**배선** — `DefenderDragSlot` 은 런타임 생성이라 인스펙터로 참조를 못 받는다. `DefenderSelector` 에
`[SerializeField] DcInspectController` 를 두고 `Bind` 로 넘긴다(`costDisplay` 가 이미 쓰는 경로).

⚠ **씬 저장 주의** — 이 워크트리는 여러 세션이 공유하고 `BattleScene.unity` 가 이미 dirty 다.
저장하면 다른 세션의 미저장 in-memory 작업까지 함께 박힌다. 저장 전에 `git status` 로 확인하고,
안전하지 않으면 `.unity` YAML 에 `필드: {fileID}` 를 직접 넣는 경로를 쓴다.

## 알려진 결과 (설계 의도)

소진 셀을 만지면 **각성 손패가 열리고 트레이가 손패로 뒤집힌다.** "선택 = 손패 등장"이 기존 계약
(`selection-hand-attach`)이기 때문이고, 보드에서 그 유닛을 직접 탭한 것과 **완전히 같은 사건**이
되도록 의도한 것이다. 새 예외가 아니다.

## 완료 기준

- 컴파일 통과.
- Play 육안: 소진 셀을 **탭**하면 카메라가 판 위 그 유닛으로 가고 선택 리티클·상세 패널이 뜬다.
- 소진 셀을 **끌어도** 같은 결과가 나온다. 배치 프리뷰·슬로모·arm 은 시작되지 않는다.
- 정상 셀은 영향 없음 — 탭 = arm, 끌기 = 배치 그대로.
- 그 유닛이 죽어 셀이 정상으로 돌아온 뒤에는 이 경로가 더 이상 호출되지 않는다.
- 씬 배선이 저장됐고, 저장 diff 에 무관한 오브젝트 변경이 섞이지 않았다.

> **확인 2026-08-13** · 커밋 `9b629bfd`(구현) · `e8cb3f50`(봉인 게이트 추가) — 사용자 Play 확인 완료.
> 씬 배선(`dcInspect`)은 BattleScene YAML 에 직접 넣고 **그 한 줄만** 인덱스에 올렸다 — 씬 파일에
> 다른 세션의 미커밋 작업 126줄이 섞여 있었다.
