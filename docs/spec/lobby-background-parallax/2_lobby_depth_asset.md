# 2 — 로비 뎁스맵 bake (평탄화) + 임포트

## 목적

난간·가로등 같은 얇은 근경 구조가 **흡수된** 저주파 뎁스 1장을 만든다. 이 spec 의 핵심 자산 —
평탄화가 곧 "늘어짐 없음"의 근거다.

## 변경 대상

- New: `Assets/_Project/Art/Depth/lobby_bg_depth.png` (R8, 낮/밤 공유 1장)
- Modify(옵션): `Assets/_Project/Modules/DepthParallax/Tools~/depth_bake.py` — `--flatten` 노브 추가

## 구현

- **입력**: `Assets/_Project/Art/lobby_bg_day.png` (2730×1536). 낮 기준 1장만 bake —
  낮/밤 flat 뎁스 상관 0.998·평균차 1.2% 로 **공유 가능함이 실측됨**(README 계약).
- **레시피 (실측 검증됨)**:
  1. DA-V2 Small 로 뎁스 추론(저해상 640×360 충분 — 뎁스는 저주파).
  2. 글로벌 퍼센타일(2/98) 정규화. 흰색=near.
  3. **평탄화: `median(9)` → `GaussianBlur(12)`** → 절벽(p99.5 gradient) 70.3 → 4.5.
     난간 살/육각 패턴·가로등 기둥이 흡수되고, 하늘=far / 중경 / 테라스=near 구조는 보존.
  4. half-res R8 grayscale PNG 출력.
- **`--flatten` 노브**(권장): 위 median+blur 를 `depth_bake.py` 옵션으로 넣어 재현 가능하게.
  (컷신용 캐릭터 뎁스는 평탄화하면 안 되므로 **기본 off**.)
- **임포트**: `DepthMapBaker` 로 R8/linear/no-mip/uncompressed/non-atlased.
- **극성**: 뒤집혀 보이면 자산 재bake 말고 `_DepthSign` 으로 반전(자산은 흰색=near 관례 유지).

## 완료 기준

- `lobby_bg_depth.png` 존재, R8/linear/no-mip 임포트 확인.
- 육안: 하늘 검정(far) / 테라스 밝음(near) / **난간·가로등 디테일이 보이지 않음**(흡수됨).
- 정량: flat 뎁스 p99.5 gradient < 10 (절벽 없음 = 늘어질 대상 없음).
- 낮/밤 두 스프라이트 어느 쪽에 물려도 구조가 맞음(상관 0.998 근거).
