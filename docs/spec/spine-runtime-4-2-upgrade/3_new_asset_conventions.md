# Unit 3 — 4.2 신규 리소스 export/임포트 규약

## 목적

신규 스켈레톤 리소스를 만들거나 외주/구매로 수급할 때 매번 같은 함정(확장자, 한글 파일명, PMA)에 빠지지 않도록 규약을 한 곳에 고정한다. 문서 작업만 있는 unit.

## 변경 대상

- 수정: `docs/reference/lessons/03-rendering-assets.md` — "Spine 4.2 리소스 수급 규약" 섹션 추가 (unit 1 에서 재작성한 기준 위에)

## 구현

규약에 담을 내용 (조사에서 확정된 것 + Spine 공식 문서 기준):

1. **Export**: Spine Editor **4.2.xx** 에서 export. 바이너리(`.skel`) 권장, JSON 은 디버깅용. 스켈레톤 데이터는 major.minor 가 일치하는 런타임에서만 로드된다 (4.2 export ↔ 4.2.xx 런타임. 4.3 런타임 불가, 패치 버전만 상호 호환).
2. **원본 `.spine` 보존 (필수)**: export 산출물과 함께 원본 `.spine` 프로젝트 파일을 repo 에 커밋한다 (`Assets` 밖, 예: `art/spine/{SkeletonName}.spine`). 3.8 리소스 전량 폐기의 근본 원인이 원본 부재였다 — 원본만 있으면 향후 4.3+ 런타임 업그레이드는 재제작이 아니라 재-export 로 끝난다.
3. **확장자 rename**: `.skel` → `.skel.bytes`, `.atlas` → `.atlas.txt` (spine-unity 임포터 인식 조건. 3.8 시절 BellMage 등 8종이 rename 누락으로 임포트조차 안 됐던 전례).
4. **파일명**: ASCII 만. 한글명은 macOS NFC/NFD 정규화 문제로 아틀라스-텍스처 연결이 깨진다 (몬스터1 전례, 3중 수정 필요했음).
5. **텍스처/알파**: PMA(premultiplied alpha) export 를 기본으로 하고 아틀라스 export 설정과 Unity 텍스처 임포트 설정(sRGB, Alpha Is Transparency 끔)을 일치시킨다. `SpineUnitView` 의 사망 페이드가 PMA transparent 전제(`Skeleton.A` 직접 조작)임.
6. **rig 방향 관례**: "ScaleX=+1 에서 -x(왼쪽)를 바라본다". 어기는 rig 은 SkeletonData 의 `skeletonDataModifiers` 에 `SkeletonFlipX.asset` 부착으로 정규화.
7. **애니메이션 클립 이름**: 데이터 에셋 필드(idle/attack/drag/death)로 흡수하므로 강제 규약은 없지만, `idle`/`attack`/`death` 표준 이름 권장.
8. **배치 위치**: `Assets/_Project/Characters/{SkeletonName}/` 폴더 단위 (3.8 시절처럼 평평하게 쌓지 않는다).
9. **검증 절차**: 임포트 직후 `_SkeletonData`/`_Atlas`/`_Material` 자동 생성 확인 → Skeleton 프리뷰에서 애니 재생 확인 → 콘솔 경고 0.

## 완료 기준

- [ ] lessons 03 에 규약 섹션 추가 커밋
- [ ] 규약이 unit 4 의 체크리스트로 그대로 재사용 가능한 수준으로 구체적
