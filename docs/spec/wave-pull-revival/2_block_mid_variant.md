# 2 — 묶음 가운데 변주

## 목적

3웨이브 묶음의 **가운데 한 번**에 다른 성격의 무리를 섞는다. 지금은 벌떼가 세 번 연속이라 묶음 안에서 «당길까»가 무차별 판단이다 — 두 번째나 세 번째나 오는 게 똑같으니 고민할 게 없다. 가운데가 다르면 「지금 당기면 벌떼 위에 힐러가 얹힌다」가 계산 대상이 된다.

**길은 건드리지 않는다**(계약 5). 성격 슬롯만 바뀌고 어느 입구로 오는지는 묶음 내내 고정이다 — 그래야 「이쪽을 보강하자」는 결정이 계속 보상받는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/WaveConceptData.cs` — `variantSlots` 신설
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — 묶음 루프의 슬롯 선택
- `Assets/_Project/Data/WaveConcepts/Concept_*.asset` — 변주 저작
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` — `waveGeneratorVersion` 3 → 4
- 테스트: `Tests/EditMode/WaveConceptVariantTests.cs`(신규) · `WaveConceptGenerationTests` 갱신

## 구현

**1. 저작 필드** — 컨셉 SO 에 슬롯 배열을 하나 더 둔다:

```csharp
[Tooltip("블록 가운데 웨이브에 추가로 끼는 편성(교체 아님). 비어 있으면 변주 없음.")]
public WaveConceptSlot[] variantSlots = Array.Empty<WaveConceptSlot>();
```

**교체가 아니라 삽입이다** — 가운데 웨이브 편성 = 본 슬롯 **+** 변주 슬롯. 교체하면 그 웨이브에 블록의 성격이 통째로 사라져 「배우고 → 대응하고 → 겨우 버티고」의 압력 상승이 끊기고, 브리핑 스트립의 블록 라벨(`conceptLabel`)과도 어긋난다. 삽입이면 탱커 블록은 계속 탱커 블록이면서 「지금 당기면 탱커 위에 저격수가 얹힌다」가 생긴다(PRD §3.3 「탱커 블록 안에 지원형 스웜 **삽입**」).

수량 총량은 곡선이 그대로 소유하므로, 슬롯이 하나 늘면 그 웨이브는 **같은 총량을 더 잘게 나눠** 받는다(`DistributeSlotCounts`). 별도 배율을 두지 않는다 — 두면 계약 4(총량은 곡선 소유)가 깨진다.

**비어 있으면 현행 그대로**다(계약 6). 5종 중 일부만 저작해도 나머지는 무회귀이고, 저작 안 한 컨셉이 조용히 깨지지 않는다.

**2. 어느 웨이브가 변주인가** — 묶음의 **두 번째** 웨이브(`i % holdWaves == 1`). 첫 웨이브는 성격을 가르치는 자리라 순수해야 하고, 마지막은 그 성격의 시험대다(`wave-concept-blocks` 의 «배우고 → 대응하고 → 겨우 버티고»). 가운데가 유일하게 비어 있는 칸이다. `holdWaves` 가 1~2 면 변주 웨이브가 없거나 마지막과 겹치므로 **`holdWaves >= 3` 일 때만** 적용한다.

**3. 생성기 연결** — 묶음 경계에서 한 번 뽑는 구조(`WavePatternGenerator.cs:159`)는 그대로 두고, **합쳐진 배열(본 + 변주)을 그 자리에서 미리 만든다**. 웨이브 루프는 둘 중 하나를 고르기만 한다. 컨셉·lane 배정은 여전히 묶음 속성이다.

**입구는 새로 뽑지 않는다** — `AssignLanes` 를 다시 부르지 않고 묶음이 이미 확정한 배정을 물려받는다(같은 `laneGroup` 이 본 편성에 있으면 그 입구, 없으면 본 편성 입구를 순서대로 재사용). 계약 5 를 지키는 동시에 **rng 소비가 한 번도 늘지 않는다** — 그래서 변주를 저작하지 않은 덱은 편성이 byte-identical 로 유지된다. `waveGeneratorVersion` 은 저작이 실제로 들어가는 덱에서만 올린다.

**4. 변주 저작** — 5종 중 성격이 단단한 것부터. 예: 「벌떼」 가운데에 지원형 1슬롯, 「중장」 가운데에 원거리 1슬롯. 변주 슬롯도 본 슬롯과 같은 `classFilter × altitude` 규칙을 타므로 완화 ladder(`PickSlotUnitIndex`)와 fail-open(계약 5, `wave-concept-blocks`)이 그대로 적용된다.

**「공습」에는 변주를 저작하지 않는다** — 비행 묶음 가운데 지상이 섞이면 대공 배치를 방금 한 플레이어가 그 웨이브에만 헛돈 셈이 된다. 「공습」은 `countMul` 0.3 의 소수 압박이라 3연속이어도 지루하지 않다.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러/경고 0
- [ ] EditMode: `variantSlots` 가 빈 컨셉은 3웨이브 편성이 **현행과 동일**하다(무회귀)
- [ ] EditMode: 변주 저작 시 묶음의 2번째 웨이브만 다른 슬롯을 쓰고 1·3번째는 본 슬롯이다
- [ ] EditMode: 변주 웨이브의 lane 배정이 묶음의 나머지와 **같은 입구**를 쓴다(계약 5)
- [ ] EditMode: `holdWaves` 가 1·2 인 덱에서 변주가 적용되지 않는다
- [ ] EditMode: 같은 덱·같은 `waveSeed` 3회 생성 시 100웨이브 시퀀스 완전 일치(결정론)
- [ ] EditMode: 7덱 스폰 창 불변식 pin(`WaveKillBudgetPinTests`) 무회귀
- [ ] Play: 묶음 가운데 웨이브에서 다른 성격의 무리가 눈에 띄게 섞여 들어온다
