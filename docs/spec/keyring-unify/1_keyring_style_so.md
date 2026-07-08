# 1 · KeyringStyle SO + 아웃게임 슬롯 이전

## 목적

키링 스타일(스프라이트/머티리얼)의 단일 소스 `KeyringStyle` SO 를 신설하고, `LobbyKeyringSettings` 의 아트 슬롯 4개를 style 참조 1개로 이전한다. 아웃게임 비주얼 무변경.

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/KeyringStyle.cs`
- 신설: `Assets/_Project/Data/Config/KeyringStyleHologram.asset`
- 수정: `Assets/_Project/Scripts/Data/LobbyKeyringSettings.cs` — 아트 슬롯 4개 제거 → `KeyringStyle style` 1개
- 수정: `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` — `BuildRig`/`UpdateRig` 의 슬롯 참조 경로 교체
- 수정: `Assets/_Project/Data/Config/LobbyKeyringSettings.asset` — 마이그레이션
- 신설(일회용): 에디터 메뉴 마이그레이션 스크립트 (에셋 생성 + 슬롯 이전 + 구 슬롯 제거를 한 커밋에)

## 구현

- `KeyringStyle` 슬롯 6개: `ringSprite` / `cordSprite` / `uiCordMaterial` / `uiRingMaterial` / `worldCordMaterial` / `worldRingMaterial`. 월드 2슬롯은 unit 2 에서 채워짐(이번 unit 은 비워 둠).
- **2단 폴백**: `style == null` → 전체 절차적(현행 annulus + 단색 사각 + 기본 UI 머티리얼), style 내 개별 슬롯 null → 해당 요소만 폴백. per-slot 폴백 의미는 현행 LobbyKeyringDrag.cs:207-218 과 동일.
- SO 헤더 주석: **팔레트 변경 = UI/월드 머티리얼 2곳** (계약 5).
- `cordSprite` 는 UGUI 전용(세로 스트레치 Image). 월드 줄 텍스처는 `worldCordMaterial` 이 직접 보유 — 비대칭임을 SO 주석에 명시.
- 마이그레이션은 일회용 에디터 메뉴 스크립트로 (`execute_code` 는 이 프로젝트에서 커맨드라인 한계로 불가 — `lobby-keyring-drag/6_hologram_style.md` 전례): KeyringStyleHologram.asset 생성 → 현 LobbyKeyringSettings.asset 의 holo 스프라이트 2 + 머티리얼 2(cord/ring 동일 guid 공유) 를 옮겨 담기 → settings 에 style 할당. 스크립트는 커밋에 포함 후 다음 unit 에서 삭제 가능.
- **주의**: 폴백도 정상 동작이라 이전 실패(슬롯 누락 → 절차적 폴백)를 육안으로 놓치기 쉬움 — 검증에서 홀로그램이 "실제로" 보이는지 확인.

## 완료 기준

- compile 클린, 콘솔 에러 0.
- 아웃게임 키링 비주얼 무변경: reflection `BuildRig` 호출 또는 합성 드래그 + `timeScale=0` 동결 후 스크린샷 — 홀로 빔/링 렌더 확인 (절차적 폴백이 아님을 확인).
- style 참조 해제 시 절차적 비주얼 재현 (2단 폴백 회귀 없음).

확인 2026-07-08 — compile·콘솔 클린. 마이그레이션은 메뉴 스크립트 대신 에셋 YAML 직접
편집(guid 이전)으로 대체 — 같은 결과를 한 커밋에. reflection BuildRig + 오프스크린 렌더로
홀로 링/빔 렌더 확인(스크린샷), 런타임 클론(style=null)로 절차적 폴백 재현 확인. 리뷰 (none).
커밋 `143db2f8`.
