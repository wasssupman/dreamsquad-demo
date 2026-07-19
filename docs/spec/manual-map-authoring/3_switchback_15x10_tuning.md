# 3. 15×10 스위치백 리레이아웃 + 원경/유닛 스케일/드래그 줌아웃 튜닝

## 목적

초기 ArkFunnel(20×10, 이른 합류 + 긴 공유 구간)이 단조롭다는 플레이 피드백을 반영해 경로를 재설계하고, 축소된 보드에 맞춰 화면 밀도(원경·유닛 크기·드래그 줌아웃)를 튜닝한다.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocument_ArkFunnel.asset` — GUID 유지 덮어쓰기
- `Assets/_Project/Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset` — `ringRadius`
- `Assets/_Project/Scenes/BattleScene.unity` — `BattleBridge.tilemapCharacterScale`
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` — `focusFovDelta`

## 구현

- **맵 15×10 스위치백** (walk 47): 위 레인은 ㄹ자 되감기(~33칸, y9→y7→y5 세 번 왕복 — 포켓 타워가 같은 적을 2~3회 타격), 아래 레인은 독립 루트(~17칸)로 골 (2,5) 직전에만 합류. 비대칭 길이로 압박 타이밍이 엇갈린다.
- **원경**: `ringRadius` 6→10 — 바닥 링 타일 + 원경 프랍이 보드 밖 10칸까지 깔려 줌아웃/연출 풀백에서 빈 배경 차단.
- **유닛 스케일**: `tilemapCharacterScale` 0.42→0.504 (+20%). 뷰/드래그 프리뷰/ECS 엔티티 전부 이 값에 곱해지므로 일괄 적용, 유닛별 `spineVisualScale` 상대비는 유지.
- **드래그 줌아웃**: `focusFovDelta` +6°→+4° — D&D/탭투플레이스 중 줌아웃 완화. 에셋 값이라 Play 중 실시간 반영.
- 카메라 홈 포즈는 15×10 fit 후보를 시도했으나 **사용자 결정으로 원복** — 씬의 카메라 값은 사용자 authored.

## 완료 기준

- [x] BFS/2×2 재검증 통과 (레이아웃 교체마다 필수)
- [x] 사용자 Play 확인 ("좋은 마무리")
- [x] 씬 저장 재베이크(0.42 재유입) 해소 확인 — 디스크 0.504, BattleScene 미오픈 상태 점검

확인 2026-07-19 — 커밋 `12a9518d`
