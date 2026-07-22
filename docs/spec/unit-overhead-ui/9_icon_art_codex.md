# 9. 아이콘 아트 생성 [Codex/art]

## 목적

스택 아이콘 sprite 생성. **Codex 경로로 세션 밖 수행** — 이 문서는 브리프(README 확장 섹션) 입력과 산출물 기록.

## 산출물 (Codex 완료 2026-07-22)

- `Assets/_Project/Art/StackIcons/icon_stack_fatigue.png` — 피로도(차가운 회청 계열).
- `Assets/_Project/Art/StackIcons/icon_stack_heat.png` — 열기(따뜻한 주홍 계열).

## import 계약 (확인됨)

- 256×256 RGBA, 투명 배경, alphaIsTransparency.
- textureType Sprite(8), spriteMode Single(1), enableMipMap 0, 무압축·quality 100.
- git diff --check + 알파 채널 검증 통과(사용자 확인).

## 소비

- `StackIconRegistry.asset`(unit 10)에 `Fatigue→fatigue`, `Heat→heat` 매핑. 뷰(unit 7)가 registry 로 해석.
- 브리프 원본: README 확장 섹션 "아이콘 아트 브리프" — CLAUDE.md Visual Direction 준수(캐주얼·작은크기 가독·단순 실루엣, RPG/타로/다크 금지, 숫자 미포함).

## 완료 기준

- 두 sprite 존재 + import 계약 충족. ✅
- 색 계열 분리(피로도=cool / 열기=warm)로 한 행에서 구분. ✅
