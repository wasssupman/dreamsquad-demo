# 0 — 프레임 전처리 파이프라인

## 목적

AI 영상 추출 프레임(흰 배경, 알파 없음, 경계 노이즈)을 게임용 투명 스프라이트로 만드는
재사용 가능한 전처리 절차를 확립한다.

## 변경 대상

- `Assets/_Project/Sprites/{char}/{anim}/{char}_{anim}_{NNN}.png` (hello 147장, world 130장)
- 파이프라인 스크립트(레포 밖 scratchpad): `reprocess_v4_numpy.py` 계열

## 구현 (soft matting v4)

1. 4변 3px 테두리 무조건 투명화 — 영상 프레임 가장자리 회색 띠 제거.
2. 외곽 flood-fill 로 배경 확정 (근백색 min≥222 & 채도폭≤18, 외곽 연결 성분만).
3. 배경 인접 3px 밴드에서 알파 연속 추정 `a = clamp((255-min(rgb))/80)` —
   하드컷 금지(계단 자글거림 원인). 가우시안(σ0.8)으로 노이즈 평활 후 `a^1.7` 매트 수축.
4. 흰색 un-blend: `F = (C-(1-a)·255)/a` — 경계 픽셀의 흰 기운 제거.
5. 투명영역 RGB 를 최근접 전경색으로 딜레이션 — bilinear 흰 번짐 방지.

임포트 설정: Sprite(Single), alphaIsTransparency, mipmap off, Bilinear, 비압축.

## 완료 기준

- 어두운 로비 배경 위 4배 확대에서 흰 테두리/점 노이즈/하단 띠 없음. (2026-07-07 확인)
- 밝은 이펙트(attack 스파클)가 침식에 깎이지 않음 — 세트별 임계 조정으로 해결.
