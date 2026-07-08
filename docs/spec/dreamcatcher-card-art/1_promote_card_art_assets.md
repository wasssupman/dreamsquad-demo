# 1 · 에셋 — 이미지 승격 + art 배정

## 목적

루트의 테스트 PNG 10장을 정식 Sprite 에셋으로 승격하고, 10 카드에 순서대로 배정한다.

## 변경 대상

- 이동: `dreamcatcher-card-test-01~10.png` → `Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_01~10.png`
- 각 PNG 의 `.meta` — Sprite 임포트 설정(textureType 8, spriteMode 1).
- `Assets/_Project/Data/Dreamcatcher/Card_*.asset` — `art` 필드에 스프라이트 참조 배정.

## 구현

### 임포트 설정 (meta)
UI Image 용 Sprite 로 임포트. 핵심 필드:
- `textureType: 8` (Sprite)
- `spriteMode: 1` (Single)
- `sRGBTexture: 1`, `alphaIsTransparency: 1`
- `maxTextureSize: 2048`, mipmap off(UI) — `enableMipMap: 0`
- 각 meta 고유 `guid`.

기존 `dreamcatcher_tarot_test_01.png.meta` 를 참고하되 textureType/spriteMode/mipmap 을 위 값으로 교정.

### art 배정 (순서 자동)
카탈로그 배열 순서 = 카드 순서. card[i] → `dreamcatcher_card_{i+1:00}.png` 의 스프라이트(`{fileID: 21300000, guid: <tex-guid>, type: 3}`).

카탈로그 배열 실제 순서 기준:

| # | 카드 | 이미지 |
|---|---|---|
| 1 | Card_Cost1As5 | 01 |
| 2 | Card_Cost1Hp10 | 02 |
| 3 | Card_GuardianFortress | 03 |
| 4 | Card_GuardianHp15 | 04 |
| 5 | Card_RangerAs10 | 05 |
| 6 | Card_RangerAtk10 | 06 |
| 7 | Card_AllAtk8 (신규) | 07 |
| 8 | Card_AllMove10 (신규) | 08 |
| 9 | Card_RangerHp12 (신규) | 09 |
| 10 | Card_GuardianAs8 (신규) | 10 |

배정은 임시 — 사용자가 인스펙터 `art` 필드에서 자유 재조정(계약).

## 완료 기준

- [ ] PNG 10장이 Art 폴더에 존재, 루트에서 제거.
- [ ] 에디터 포커스 시 10장 Sprite 로 임포트(경고 없음).
- [ ] 10 카드 `art` 가 각 스프라이트 참조(에디터에서 썸네일 표시).
- [ ] 카드 페이지에서 이미지 렌더 확인(unit 2 이후).
