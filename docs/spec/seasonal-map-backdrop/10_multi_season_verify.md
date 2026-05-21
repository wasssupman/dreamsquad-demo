# 10. Multi-Season Swap 검증

## 목적

4 시즌 모두 swap 가능한지 Play 진입으로 검증. 6번 단위가 Forest 만 검증했다면 본 단위는 Lava/Lunar/Cosmic 도 동일 절차로 확인.

## 변경 대상

문서/스크린샷만. 코드 변경 없음.

## 절차

각 시즌에 대해 반복:

1. Editor → `SeasonRegistry.asset` Inspector 열기.
2. `defaultSeason` 필드를 다음으로 차례로 교체:
   - `season_S1_forest`
   - `season_S2_lava`
   - `season_S3_lunar`
   - `season_S4_cosmic`
3. 각 교체 후 BattleScene 에서 Play 진입.
4. 매치 진입 직후 점검:
   - [ ] 시즌별 skybox 가 사방 일러로 채워진다 (회색 X).
   - [ ] EdgeProp 12개 (Forest) 또는 6개 (Lava/Lunar/Cosmic) 가 보드 둘레에 배치된다.
   - [ ] 보드/캐릭터/이펙트 가독성 유지.
   - [ ] 콘솔 에러/예외 없음.
   - [ ] Skybox 좌우 솔기(seam) 가 카메라 회전 시에도 보이지 않음.
5. 매 시즌마다 스크린샷 저장:
   - `Assets/Screenshots/seasonal_backdrop_forest_verify_2026-05-11.png`
   - `Assets/Screenshots/seasonal_backdrop_lava_verify_2026-05-11.png`
   - `Assets/Screenshots/seasonal_backdrop_lunar_verify_2026-05-11.png`
   - `Assets/Screenshots/seasonal_backdrop_cosmic_verify_2026-05-11.png`
6. Play 종료 → `_Backdrop` 정리 + `RenderSettings.skybox` 가 원래 값으로 복원되는지 확인.

## 검증 완료 후 처리

- `defaultSeason` 은 최종적으로 `season_S1_forest` 로 되돌린다 (Forest 가 기본 진입 시즌).
- 4 시즌 스크린샷 commit 에 포함.

## 미세 조정 (필요 시)

- 특정 시즌의 skybox 가 너무 어둡거나 채도가 강하면 해당 `backdropTint` 조정.
- `_Mapping`/`_ImageType` 가 잘못 설정돼 왜곡 발생 시 BackdropMounter Mount 코드에서 값 점검.

## 완료 기준

- 4 시즌 모두 Play 진입 OK, 체크리스트 전 항목 통과.
- 4 스크린샷 저장.
- `read_console` clean.

## 의존

- 선행: 6, 7, 8, 9
- 후행: 11 (handoff_summary)
