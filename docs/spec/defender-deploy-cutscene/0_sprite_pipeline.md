# 0 — 프레임 전처리 파이프라인

## 목적

원본 33프레임(1280×720, 검정 불투명 배경)을 게임용 컷신 스프라이트로 만든다:
역순 리넘버 + 검정 배경 누끼(투명) + 50% 축소 + Unity Sprite 임포트.

## 변경 대상

- 입력: `Assets/_Project/Art/Cutscene/Ranger/ee30af77-..._frame_001..033.png` (원본, RGBA 이지만 알파 255)
- 출력: `Assets/_Project/Sprites/Cutscene/Ranger/Ranger_001..033.png` (640×360, 투명 배경)
- 전처리 스크립트: scratchpad 의 python(레포 밖). Pillow/numpy 사용.

## 구현

1. **역순 리넘버**: 원본 `frame_033` → `Ranger_001`, … `frame_001` → `Ranger_033`.
   (재생 순서가 원본 역방향)
2. **누끼(검정 배경 → 투명)**: 검정에 가까운 픽셀을 알파로. 하드컷 금지 —
   `a = clamp01((luma - lo) / (hi - lo))` 형태의 소프트 매트(lo≈8, hi≈40 근방에서
   시작해 결과 보고 조정). 경계 어두운 픽셀 계단현상/검은 테두리 남지 않게.
   외곽 연결 배경만 제거해 캐릭터 내부의 어두운 디테일은 보존(flood-fill 권장).
3. **50% 축소**: 640×360 (LANCZOS/bilinear). 축소는 매팅 **후** 수행.
4. **임포트 설정**: Sprite(Single), alphaIsTransparency=on, mipmap off, Bilinear,
   압축 없음(또는 프로젝트 UI 스프라이트 기본과 일치). PPU 는 표시 스케일과 무관하게
   기본값 유지(플립북은 픽셀 크기로 배치).

## 완료 기준

- `Sprites/Cutscene/Ranger/Ranger_001.png … Ranger_033.png` 33장 존재, 640×360.
- 어두운 배경 위에서 검은 사각 테두리/배경 잔상 없이 캐릭터만 보인다(에디터 육안).
- 각 png 에 `.meta` 동반, Sprite 로 임포트됨(넘버링 역순 확인: Ranger_001 = 원본 마지막 프레임).

_확인: 2026-07-14 — Ranger 33장·Archer 49장 누끼/역순/임포트 완료, 육안 검증 통과._
