# Unit 0 — 3.8 스켈레톤 리소스 전량 퇴역

## 목적

4.2 런타임 스왑 전에, 4.2 에서 로드 불가능한 3.8 export 리소스를 전부 제거한다. 런타임(`Assets/Spine`)은 이 unit 에서 건드리지 않으므로 커밋 시점 컴파일은 그린으로 유지된다.

## 변경 대상

- 삭제: `Assets/_Project/Spine/` 폴더 전체 (몬스터1 세트 + 미사용 player-main 중복 세트). 단 `SkeletonFlipX.asset` 은 먼저 `Assets/_Project/Characters/` 로 **이동**(우리 클래스의 인스턴스, GUID 보존 필요).
- 삭제: `Assets/_Project/Characters/` 의 스파인 파일 전부
  - player-main 세트 (`.skel.bytes`/`.atlas.txt`/`.png`/`_SkeletonData`/`_Atlas`/`_Material`)
  - BellKnight 세트 (동일 구성 + 중복 `.skel`/`.atlas`)
  - 미임포트 원본 8종: BellMage, DoubleWolf Long, DoubleWolf Short, FleshSwarmer, ForestWormBoss, HeartWolf, MutantShroom3, WolfLamb (`.skel`/`.atlas`/`.png`)
  - `Deco.png` 는 스파인 텍스처가 아니므로 사용처 확인 후 참조가 있으면 유지
- 수정: `Assets/_Project/Scenes/BattleScene.unity` — "Spine GameObject (BellKnight)" 오브젝트(씬에 직접 배치된 SkeletonAnimation 프리뷰) 제거

## 구현

1. clean git 상태 확인 후 시작 (dirty 파일 있으면 정지하고 질문).
2. `SkeletonFlipX.asset` + `.meta` 를 `Characters/` 로 이동 (mv, meta 동반 → GUID 보존).
3. 위 삭제 대상 제거 (`.meta` 동반).
4. BattleScene 에서 프리뷰 오브젝트 제거 (UnityMCP 사용).
5. Defender 16종 / Enemy_Vanguard / Enemy_Tanker 데이터 에셋의 `skeletonDataAsset` 필드는 dangling GUID 로 **의도적으로 남긴다** — 런타임에서 null 취급되고, unit 4 에서 신규 리소스로 재연결한다. 이번 unit 에서 데이터 에셋을 건드리지 않는다.

## 완료 기준

- [ ] 컴파일 에러 0 (런타임은 아직 3.8 그대로)
- [ ] BattleScene 로드 시 에러 0, Play 진입 가능 (유닛 스켈레톤 미표시는 허용)
- [ ] `Assets` 하위에 3.8 스켈레톤 데이터 파일 0건 (`find` 로 `.skel*`/`.atlas*`/`_SkeletonData` 검증)
- [ ] `SkeletonFlipX.asset` 이 GUID 유지한 채 `Characters/` 에 존재
