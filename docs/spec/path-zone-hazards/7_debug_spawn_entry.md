# Debug Hazard Spawn

**작업 구분**: 7 (feature 검증 게이트)

## 목적

Hazard 의 첫 producer 진입점. Editor 메뉴 + BattleBridge 디버그 API. 본 단위 commit 후 hazard 동작 첫 관측. spec 검증 질문 두 가지 모두 PlayMode 에서 답.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DebugSpawnHazardAt(HazardSO, int2)` public method (Unit 6 의 `SpawnHazardWithVisual` 직접 호출하는 thin wrapper)
- Add: `Assets/_Project/Scripts/Battle/Debug/HazardDebugMenu.cs` (`#if UNITY_EDITOR`)

## BattleBridge.DebugSpawnHazardAt

```csharp
public Entity DebugSpawnHazardAt(HazardSO so, int2 cell)
{
    return SpawnHazardWithVisual(so, cell);  // Unit 6 에서 정의된 wrapper 재사용
}
```

= 디버그 진입점은 wrapper 와 본질적으로 동일. 명시적 별칭으로 둠으로써 미래 producer 가 어느 메서드를 부를지 명확.

## HazardDebugMenu (Editor 전용)

```csharp
#if UNITY_EDITOR
public static class HazardDebugMenu
{
    [MenuItem("Wassup/Battle/Debug/Spawn Poison Hazard Under Mouse")]
    static void SpawnPoison() => Spawn("Hazard_Poison_3x3");

    [MenuItem("Wassup/Battle/Debug/Spawn Ice Hazard Under Mouse")]
    static void SpawnIce() => Spawn("Hazard_Ice_3x3");

    [MenuItem("Wassup/Battle/Debug/Spawn Fire Hazard Under Mouse")]
    static void SpawnFire() => Spawn("Hazard_Fire_3x3");

    static void Spawn(string assetName)
    {
        if (!Application.isPlaying) return;
        var so = AssetDatabase.LoadAssetAtPath<HazardSO>($"Assets/_Project/Data/Hazards/{assetName}.asset");
        if (so == null) { Debug.LogWarning($"[HazardDebug] HazardSO not found: {assetName}"); return; }
        int2 cell = MouseToCellHelper();   // ObstacleDebugMenu 의 마우스→셀 utility 재사용 또는 동일 패턴
        BattleBridge.Instance.DebugSpawnHazardAt(so, cell);
    }
}
#endif
```

## 검증 (PlayMode, feature 게이트)

### 시나리오 1: Poison zone DoT
- 적 1마리 wave 시작, 경로 진행 중
- 디버그 메뉴 Poison spawn → 경로 앞 3×3 cell 영역에 zone
- 적 zone 진입 → HP 가 매 초 ~10 감소
- zone 빠져나오면 0.2s 후 DoT 종료
- 6초 후 zone visual + 효과 모두 사라짐

### 시나리오 2: Ice zone Slow
- Ice spawn → 적 진입 시 속도 0.4× 로 감소 (체감상 약 2.5배 통과 시간)
- zone 빠져나오면 0.2s 후 속도 정상화

### 시나리오 3: Fire zone 강한 DoT
- Fire spawn → 적 HP 매 초 ~20 감소 (Poison 의 2배)

### 시나리오 4: Composition (overlap)
- Ice + Poison 같은 위치 spawn → 적이 동시에 Slow + DoT 받음
- Ice 시각 + Poison 시각 둘 다 보임 (visual ⊥ effect 분리 검증)

### 시나리오 5: Visual ⊥ Effect 분리 검증
- Visual prefab 색상 zone 별 차이 확인
- Visual 이 hazard lifetime (6초) 내 표시, 만료 시 사라짐
- ECS 동작과 Visual 동작이 같은 시점에 스폰/소멸 (sync 일관성)

### 시나리오 6: API encapsulation 검증 (weak proof — 솔직)
- `BattleBridge.DebugSpawnHazardAt(so, cell)` 가 3 SO 모두 동일 호출로 동작.
- **한계**: 본 spec 의 producer 는 디버그 메뉴 1개뿐 → "다양한 producer plug-in" 이 *진짜로* 검증되지 않음.
- 검증 방식 제안 (코드 리뷰):
  1. `EffectSpawner.SpawnHazard` 의 시그니처가 `(em, HazardSO, originCell)` 만 받는지 — 외부 의존 0.
  2. `BattleBridge.SpawnHazardWithVisual` 의 시그니처가 `(HazardSO, int2)` 만 받는지 — producer 컨텍스트 의존 0.
  3. spec 4 의 미래 producer 의사코드 예시 (디펜더 on-place / 스킬 카드 / 장비 효과) 가 같은 API 만 호출하는지.
- 진짜 검증: 후속 spec (디펜더 on-place hazard 등) 통합 시 재확인 필요. 본 spec 은 *인프라가 막히지 않는다* 만 보장.

## 완료 기준

- 컴파일.
- PlayMode 시나리오 1~6 사용자 확인 통과.
- 콘솔 에러/경고 0.
- 본 spec 검증 질문 두 가지 사용자 통과 답변 수령:
  1. game feel — 3 zone 효과 의도대로 ✓
  2. encapsulation — SpawnHazard 단일 진입점 ✓
- 종료 후 `8_handoff_summary.md` 작성. README 상단 "상태: 완료 YYYY-MM-DD" 갱신.
