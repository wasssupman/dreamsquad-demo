# 5 · 고리/줄 아트 + 줄 샤인 이펙트 (rev)

## 목적

절차적 단색 리그를 아트 품질로: 고리/줄을 생성 텍스처 스프라이트로 교체하고,
줄에 UV 를 따라 흐르는 광택 밴드 + 트윙클 커스텀 UI 셰이더를 입힌다.
Screen Space Overlay 캔버스라 파티클 대신 UI Image + 셰이더 방식.

## 변경 대상

- 신설: `Assets/_Project/Shaders/UICordShine.shader` — UGUI 호환 unlit 셰이더
- 신설: `Assets/_Project/Sprites/Keyring/keyring_ring.png`, `keyring_cord.png`
  (에디터 코드로 절차 생성해 PNG 저장 — 이후 정식 아트로 교체 가능)
- 신설: `Assets/_Project/Art/KeyringCordShine.mat`
- 수정: `LobbyKeyringSettings.cs` — 아트 슬롯 3개 추가
- 수정: `LobbyKeyringDrag.cs` — BuildRig 에서 슬롯 사용
- 수정: `LobbyKeyringSettings.asset` — 슬롯 할당 + cordColor 흰색(텍스처가 색 보유)

## 구현

- **SO 슬롯**: `ringSprite` / `cordSprite` / `cordMaterial`. **미할당이면 기존 절차적
  annulus + 단색 사각 + 기본 UI 머티리얼로 폴백** — 계약 9 유지.
- **셰이더**: UI/Default 골격(스텐실/클립 프로퍼티 포함) + uv.y 로 흐르는 샤인 밴드
  (`_ShineColor/Speed/Width/Strength`) + 셀 해시 트윙클(`_SparkleScale/Speed/Strength`).
  틴트는 vertex color(=Image.color=cordColor) 경유.
- **텍스처**: 고리 = 금속 광택 도넛(상단 라이팅 + 스펙큘러 아크), 줄 = 폭 방향
  라운드 셰이딩 + 대각 브레이드 힌트, 길이 방향 스트레치 전제(simple Image).
- 줄 Image 는 simple stretch — uv.y 가 줄 전장 0..1 이라 샤인 밴드가 줄 전체를
  한 번에 훑는다.

## 완료 기준

- compile 클린, 콘솔 에러 0.
- 드래그 시 고리가 금속 링, 줄이 로프 텍스처로 보이고 줄 위로 광택 밴드가
  주기적으로 흐르며 미세 반짝임이 있다 (사용자 시각 확인).
- 슬롯 비우면 기존 절차적 비주얼로 폴백 (회귀 없음).

확인 2026-07-07 — compile 클린, 사용자 확인 후 스타일 방향을 홀로그램(unit 6)으로
선회. 로프 스타일 에셋은 보존. 커밋 `7ba9a285`.
