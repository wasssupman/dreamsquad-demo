# 0 — 네온 에셋 임포트 (순수 추가 커밋)

## 목적

이후 unit 이 참조할 새 에셋만 프로젝트에 추가한다. 기존 에셋·씬·코드 무변경 —
이 커밋 revert 시 새 파일만 사라진다.

## 변경 대상

- `Assets/_Project/Art/lobby_bg_neon_night.png` (신규) — 사용자 제공 네온 시티 밤 배경 1670×941
- `Assets/_Project/Art/Depth/lobby_bg_neon_depth.png` (신규) — 위 배경용 저주파 뎁스맵 640×360
- `Assets/_Project/Art/LobbyIcons/neon_glyph_squad.png` (신규) — 시안 추출 흰 글리프(사람들)
- `Assets/_Project/Art/LobbyIcons/neon_glyph_deck.png` (신규) — 시안 추출 흰 글리프(카드)
- `Assets/_Project/Art/LobbyIcons/neon_glyph_history.png` (신규) — 시안 추출 흰 글리프(트로피)

## 구현

1. 시안 원본(스크래치패드 파이프라인)에서 글리프 3종을 휘도 기반 알파로 추출
   (흰 글리프 + 투명 배경, 채도 필터로 뱃지/네온 오염 제거).
2. 뎁스맵은 기존 `lobby_bg_depth.png` 패턴 준수 — 하늘=어두움(far), 빌딩=중간,
   광장=밝음(near), 강한 가우시안 블러(늘어질 절벽 없는 저주파 1장, 낮/밤 공유 전제).
3. 파일 복사 후 Unity refresh 로 `.meta` 생성 확인, 텍스처 임포트 타입:
   배경·글리프 = Sprite(2D and UI) — 기존 `lobby_bg_night.png` 임포트 설정과 동일 계열.
   뎁스맵 = 기존 `lobby_bg_depth.png` 설정과 동일 계열(Default, sRGB off 여부 대조).
4. 커밋: 신규 5파일 + `.meta` 5짝만 경로 명시 스테이징.

## 완료 기준

- Unity 콘솔 임포트 에러 0, 5개 에셋이 프로젝트 뷰에서 정상 프리뷰.
- `git show --stat` 에 신규 10파일(에셋+메타)만 포함.
