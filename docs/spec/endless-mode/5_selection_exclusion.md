# 5 — 맵 선택에서 엔드리스 제외 (A안)

## 목적

랜덤/토너먼트 맵 선택이 **엔드리스 엔트리를 절대 뽑지 않도록** 제외한다. 엔드리스 진입은
DevMapOverride(추후 전용 버튼)로만. **토너먼트 경로를 손대는 유일한 지점 — 회귀 주의.**

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` 선택부 (라인 ~877-905)
- `Assets/_Project/Scripts/Data/MapDocumentPool.cs` (eligible 인덱스 헬퍼) 또는
  `Assets/_Project/Scripts/.../MapPoolSelect.cs`
- 신규 `Assets/_Project/Tests/EditMode/EndlessPoolExclusionTests.cs`

## 구현

1. 풀 엔트리의 모드 판별: `entry.deck != null && entry.deck.battleMode == BattleMode.Endless`.
2. **eligible 인덱스 목록** = 엔드리스가 아닌 엔트리들. 선택 분기 수정:
   - `SelectIndex`(랜덤/`fixedMapSeed`) 와 `SelectIndexFromTournamentSeed`(토너먼트) 는 **eligible
     목록 크기**로 뽑고, 뽑힌 순번을 실제 풀 인덱스로 매핑.
   - `DevMapOverride` 분기는 **clamp 그대로** — 엔드리스 인덱스 도달 가능(진입 경로).
   - `fallback0` 은 0번(비-엔드리스 전제)이라 무해. 단, 0번이 엔드리스가 되지 않도록 풀 순서 규약:
     **엔드리스 엔트리는 항상 풀 끝에** 둔다(문서화).
3. 결정론 유지: eligible 매핑이 시드→인덱스 안정성을 깨지 않게(정렬/순서 고정).

## 완료 기준

- **EditMode 테스트**: 엔드리스 엔트리를 포함한 풀에서
  - `SelectIndex`/`SelectIndexFromTournamentSeed` 가 다양한 시드에 대해 **엔드리스 인덱스를 절대
    반환하지 않음**.
  - `DevMapOverride=6` 은 엔드리스 인덱스 반환.
- 기존 맵 선택 테스트(있으면) green — 비-엔드리스 풀에서 선택 분포 불변.
