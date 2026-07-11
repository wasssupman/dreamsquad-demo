# 0 — 페이로드 계약 + 펄스 타겟 순수함수

## 목적

`AllyMoveSpeedAura` 페이로드를 정의 계층에 append 하고, 펄스 타겟 선택(타겟팅, sim-critical)을 순수함수로 확정한다. **동작 무변경** — enum/주석/순수함수+테스트만, 어떤 시스템도 이 kind 를 아직 디스패치하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — enum append + 필드 주석
- `Assets/_Project/Scripts/Battle/Combat/AuraPulse.cs` — 신규 (순수 static, `BarrageEpicenter.cs` 선례)
- `Assets/_Project/Tests/EditMode/AuraPulseTests.cs` — 신규

## 구현

### DcMechanic.cs — append-only

- `DcPayloadKind.AllyMoveSpeedAura = 9` **끝에 append** (기존 카드 int 직렬화 보존, nightmare-catcher 계약 2).
- `DcPayloadSpec` 신규 필드 **0** — 기존 필드 재사용을 주석으로 명시:
  - `magnitude` = 이속 증가 %(20 = +20%). `dreamcatcher-placement-aura` 의 "magnitude=%" 컨벤션. 음수 = 아군 슬로우(허용, aggregator floor 클램프).
  - `tileRange` = host 중심 Chebyshev 오라 반경.
  - `duration` = 펄스당 버프 TTL 초. **authoring 계약: duration > trigger.periodSeconds** (위반 시 범위 내 점멸 — README 계약 5).
  - `projectile` = null 유지 (비주얼 없음, MVP).

### AuraPulse.cs — 순수 타겟 선택

```
static void SelectTargets(NativeArray<int2> candidateCells, int2 hostCell,
                          int tileRange, ref NativeList<int> results)
```

- Chebyshev: `max(|dx|,|dy|) <= tileRange` 인 candidate 인덱스를 results 에 수집. 경계 **포함**(== tileRange 도 대상 — TileAoe 착탄 판정과 동일 관용구). `results` 는 **함수 진입 시 Clear**(재사용 안전, 크리틱 L1).
- host 자신 제외는 **여기서 하지 않는다** — entity 비교라 arm 책임(README 계약 3). host 와 같은 셀 candidate 는 여기선 포함.
- Entities 무참조 입력(int2/int)만 — `GridMath.WorldToCell` 셀 변환은 호출측(arm).

## 완료 기준

- [x] 컴파일 클린 (enum append 가 기존 카드 에셋 직렬화 안 깨짐 — 값 9 신규).
- [x] `AuraPulseTests` EditMode 4+: 경계 포함(정확히 tileRange)·대각(Chebyshev)·범위 밖 제외·빈 배열·hostCell 동일 셀 포함.
- [x] 기존 EditMode 스위트 그린(무회귀).

확인 2026-07-12 — 컴파일 클린 + EditMode 701/703 그린(신규 AuraPulse 6, skip 2는 무관 기존). 커밋은 unit 0 코드 커밋 참조.
