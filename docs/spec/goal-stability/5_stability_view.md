# 5. 안정도 뷰 — 게이지 + 붕괴 연출 + 씬 wiring

## 목적

안정도를 화면에서 읽을 수 있게 한다: 골 위 안정도 게이지 + 붕괴 순간 연출. v1 은 최소 — 프랍 교체/전역 HUD 는 후속.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/` (게이지 — 기존 타일 게이지 계열 재사용 검토)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (폴링 sync + collapsed drain 소비)
- `Assets/_Project/Scenes/BattleScene.unity` (wiring — `unity-feature-wiring` 스킬)

## 구현

1. **게이지**: unit-health-display 후속 후보("blocking hazard 체력 표시 — 타일 게이지 재사용 검토")를 이 자리에서 소화한다. `TileHealthGaugeLayer/View` 재사용이 1순위 — 골 셀 위 게이지, `BattleBridge` 가 골 엔티티 `Health` 를 read-only 폴링(큐 아님, 기존 체력 표시 관용구). 재사용이 구조적으로 안 맞으면 최소 전용 게이지(월드스페이스 바 1개)로 대체하고 사유를 기록.
2. **붕괴 연출**: unit 4 의 `DrainGoalCollapsedEvents` 에서 one-shot VFX (`VfxSpawner` 슬롯 신설, `unity-vfx-integration` 스킬). 골 구조물 프랍은 v1 에서 유지(교체/파괴 아트는 후속 후보).
3. **씬 wiring**: BattleBridge SerializeField(게이지 레이어/VFX 슬롯) 할당까지 완료가 이 unit 의 완료. 수작업 이관 금지.
4. 게이지는 M>0 골에만 표시, 붕괴 시 제거. M=0 맵은 시각 변화 0.

## 완료 기준

- [ ] Play(M>0 맵): 골 위 게이지 표시·피격 감소·붕괴 시 VFX + 게이지 제거 — 게임뷰 스크린샷 확인.
- [ ] M=0 맵 시각 현행 동일.
- [ ] 콘솔 클린(누락 슬롯 경고 없음).
