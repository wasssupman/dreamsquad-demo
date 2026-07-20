# 5 — 로비 버튼 + 패널 씬 배선

## 목적

로비에 "프리셋" 버튼과 프리셋 패널을 실제 씬에 배선하고 Play 로 검증한다. 여기까지가 feature 완료.

## 변경 대상

- 수정: `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs`
- 씬: OutgameScene (MenuCanvas 하위) — UnityMCP 로 GameObject/버튼/참조 배선
- 신규 에셋: `SquadPresetCollection` 인스턴스(.asset) + 샘플 프리셋 authoring

## 구현

### OutgameMenuController (코드)

기존 `squadPanel`/`dreamcatcherPanel` 패턴을 그대로 확장:

```csharp
[SerializeField] private GameObject presetPanel;

public void OnOpenPreset() => RaiseExclusive(presetPanel);
```

- `ClosePanels()` 에 `if (presetPanel != null) presetPanel.SetActive(false);` 추가.
- `RaiseExclusive`/`menuRoot` 토글 로직은 그대로 재사용(추가 변경 불필요).

### 씬 배선 (UnityMCP — 수작업 미루기 금지, 제약: 자동화 후 Play 검증)

1. `PresetPanel` GameObject 생성(SquadPanel 형제, 동일 anchor stretch, 기본 비활성).
   `PresetPage` 컴포넌트 추가 후 `collection`/`profileSO`/`font` 할당.
2. 로비 메뉴에 "프리셋" 버튼 추가(기존 스쿼드/드림캐쳐 버튼과 동일 스타일/부모),
   `Button.onClick` → `OutgameMenuController.OnOpenPreset`.
3. `SquadPresetCollection` 에셋 생성 후 `PresetPage.collection` 에 할당. 샘플 프리셋 1~2개 authoring
   (units 7 + cards **현재 `EffectiveDeckSize`(= `DeckRuleConfig.deckSize`, 현 10)**, 기존
   DefenderUnit/DreamcatcherCard 에셋 사용). "10" 은 하드 상수가 아니라 데이터 주도값이다
   (과거 10→8→10 flip 이력) — 프리셋 카드 수는 라이브 deckSize 에 맞춰 authoring 해야 START 게이트를 통과한다.
4. `.meta` 짝 포함 커밋(경로 지정 add 시 .meta 누락 주의).

## 완료 기준

- [ ] Unity 컴파일 무오류, 콘솔 에러 없음.
- [ ] Play: 로비에서 "프리셋" 버튼 → 프리셋 패널 열림(다른 패널 배타적 닫힘), 목록 스크롤.
- [ ] 프리셋 아이템의 유닛 7·드캐 10·이름·적용 버튼이 authoring 한 SO 대로 표시.
- [ ] 적용 → 확인 팝업 → [적용] 후 스쿼드/드캐 페이지에 교체된 로드아웃 반영, 재진입에도 유지(저장됨).
- [ ] 패널 닫기 시 로비 버튼(menuRoot) 복귀.
- 확인 2026-07-20 (커밋 05c7c7b8): 씬 YAML 배선 검증(presetPanel/PresetPage refs non-zero, PresetButton.onClick→OnOpenPreset) + Play(OnOpenPreset 경로로 패널 렌더, 적용 반영). 샘플 프리셋 2개 authoring.
