# 5. 안정도 뷰 — 게이지 + 붕괴 연출 + 씬 wiring

## 목적

안정도를 화면에서 읽을 수 있게 한다: 골 위 안정도 게이지 + 붕괴 순간 연출. v1 은 최소 — 프랍 교체/전역 HUD 는 후속.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/` (게이지 — 기존 타일 게이지 계열 재사용 검토)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (폴링 sync + collapsed drain 소비)
- `Assets/_Project/Scenes/BattleScene.unity` (wiring — `unity-feature-wiring` 스킬)

## 구현

1. **게이지**: **유닛 체력바와 동일한 오버헤드 UI**(`UnitOverheadUiLayer.SetUnit`, 사용자 결정 2026-08-04 "체력바는 유닛처럼 띄워"). `BattleBridge` 가 골 엔티티 `Health` 를 read-only 폴링(큐 아님) — 골은 뷰 풀에 없어 스크린 앵커만 셀 중심+구조물 높이(`goalOverheadHeight`)로 직접 투영하고 나머지는 유닛과 같은 계약. 붕괴 시 숨김은 `EndFrame` 미표시-자동-Hide 가 공짜로 처리. Legacy(비통합) 모드는 방어유닛 이원화와 동형으로 `TileHealthGaugeLayer` 폴백.
2. **붕괴 연출**: unit 4 의 `DrainGoalCollapsedEvents` 에서 one-shot VFX (`VfxSpawner` 슬롯 신설, `unity-vfx-integration` 스킬). 골 구조물 프랍은 v1 에서 유지(교체/파괴 아트는 후속 후보).
3. **씬 wiring**: BattleBridge SerializeField(게이지 레이어/VFX 슬롯) 할당까지 완료가 이 unit 의 완료. 수작업 이관 금지.
4. 게이지는 M>0 골에만 표시, 붕괴 시 제거. M=0 맵은 시각 변화 0.

## 완료 기준

- [x] Play(M>0 맵): 골 구조물 위 유닛식 오버헤드 체력바 표시·피격 감소·붕괴 시 바 소멸 + 록버스트 VFX — 사용자 Play 확인.
- [x] M=0 맵 시각 현행 동일.
- [x] 콘솔 클린 + 관련 스위트 30/30. 씬 wiring 은 BattleScene 미로드 상태라 YAML 직접 배선(`goalCollapsePrefab` non-zero fileID 확인, SaveScene 미사용).

구현 노트: 초안의 타일 게이지는 구현 후 사용자 결정("체력바는 유닛처럼 띄워")으로 오버헤드 UI 로 전환. 타일 게이지는 Legacy(비통합) 모드 폴백으로만 잔존. `goalOverheadHeight`(기본 1.1) 인스펙터 튜닝 가능.

2026-08-04 사용자 확인 완료.
