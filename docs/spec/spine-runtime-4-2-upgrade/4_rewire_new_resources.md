# Unit 4 — 신규 리소스 임포트 + 재연결

> 선행 조건: 4.2 export 신규 리소스 수급 (외부 의존). 수급 전까지 이 unit 은 대기한다. 리소스가 캐릭터별로 나눠 도착하면 이 unit 을 rev 로 나눠 반복 적용한다 (rev 당 1커밋).

## 목적

신규 4.2 리소스를 규약(unit 3)대로 임포트하고, 끊겨 있던 데이터 에셋/씬 참조를 재연결해 게임 비주얼을 복구한다.

## 변경 대상

- 추가: `Assets/_Project/Characters/{SkeletonName}/` — 신규 스켈레톤 세트
- 수정: `Assets/_Project/Data/Defenders/*.asset` 16종 — `skeletonDataAsset` + 애니메이션 이름 필드
- 수정: `Assets/_Project/Data/Enemies/*.asset` — 스파인 사용 Enemy (현행: Vanguard, Tanker. 신규 리소스 구성에 따라 확대 가능)
- 수정(원복): unit 2 의 임시 wiring 제거
- 삭제: `Assets/Spine Examples/` (임시 wiring 용도 종료 후. 예제가 계속 필요하면 유지 여부를 사용자에게 확인)

## 구현

1. unit 3 규약 체크리스트대로 임포트 (rename → NFC/ASCII 확인 → 자동 생성 3종 확인 → 프리뷰 재생).
2. rig 방향 확인 → 필요 시 `SkeletonFlipX.asset` 부착.
3. 데이터 에셋 재연결: dangling GUID 필드를 신규 `_SkeletonData` 로 교체, 애니메이션 이름 필드를 실제 클립 이름으로.
4. unit 2 임시 wiring 원복.
5. Play 검증: 드래프트 → 배치(드래그 프리뷰) → 전투(idle/attack/피격/사망 페이드) → FaceToward 반전. Android 실기기 1회 확인 권장.
6. 콘솔에 스파인 로드 에러/경고 0 확인.

## 완료 기준

- [x] 스파인 사용 유닛 전원이 신규 리소스 참조 (Defender 16종 + Enemy_Vanguard/Tanker → Casual Character, 렌더 시각 확인은 잔여 항목 참조)
- [x] dangling `skeletonDataAsset` GUID 0건
- [x] 임시 wiring 잔재 0건 (spineboy/goblins 참조 제거), Spine Examples 삭제 (참조 0건 사전 검증)
- [x] 배치 스모크 PASS (4.2.43 로드, full_skins 스킨, Idle/Walk/Attack1/Die 존재, FlipX 동작), 콘솔 에러 0

## rev A 기록 (2026-07-07)

신규 리소스 = **Layer Lab "2D Art Maker — AMCasual Character"** (Spine 4.2.43 export, 파츠 스킨 480개, 원본 `.spine` 동봉 → 수급 규약 충족). 결정: Defender 16종 우선 적용, Enemy 는 몬스터형 리소스 후속 수급 전까지 동일 스켈레톤 임시 사용. 유저 에디터가 열려 있어 배치 검증은 격리 리그(worktree + Library CoW 클론, lessons 02)에서 수행.

**잔여(에디터 시각 확인 필요)**: ① rig 방향 — FaceToward 가 반대면 `Characters/SkeletonFlipX.asset` 을 CC SkeletonData 의 skeletonDataModifiers 에 부착 ② URP 실렌더 ③ Play 전투 시나리오 ④ Android 실기기. 파츠 조합 외형 시스템(Layer Lab 프리팹 export 방식 채택)은 별도 spec 으로 진행.
