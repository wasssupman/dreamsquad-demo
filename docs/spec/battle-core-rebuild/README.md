# battle-core-rebuild — 전투를 ECS 에서 순수 C# 「전투 코어」로 옮긴다

상태: **승인·진행 중 2026-09-23** — **조각 A 완료**(리뷰 전건 APPROVE): unit 0(main `4caee406`) · unit 1(`384e869b`·`dc0baa41`) · unit 2(`d5c16070` + 수정 `843b786a`) · unit 3(`0ae6b5cd`) · unit 4(`50ec0dae` + 수정 `0501630b`·`aedf3f7b`·`d12423bd` + 거점 스폰 `55688ef5` + 경로 방패 `7abfec27`) · 헤드리스 lane(`c74825be`).
**조각 B 구현 완료 — unit 5a·5b·5c**(5a `aa16ee9d`~`1b7e033b` · core-reviewer APPROVE. 5b — 드래그 배치·퇴근·제출 입력 · HUD 6 · 맵 오버레이 · 예고선 · 카메라 프레이밍. 5c `ebf055f4`·`605f15a7`·`2aad2ef4` + 코어 사건 `+DefIndex` — 판 종료 → 결과 화면 → 제출 게이트 · 전투 사운드 3 · 모드 진입 3단 · dev 토글). 이동 튜닝 정의표(`fae42944`)로 골든 11종 Unity 재굽기.
검증: Unity EditMode 코어 lane **371/371** · 새 PlayMode lane **21/21** · 헤드리스 3종(build 0 · test 360 · Check build 0) · Play 육안(결과 화면 · 사운드 3종 실계수 · 콘솔 에러 0).
장부 잔량: `bridge-methods` 미정 **0**(128 → 98 → 64 → 59 → 51 → 46 → 44 → 36(7a) → 28(7b) → **0**(7d); 6c 가 `ReconcileStatusFx`·`HostBodyRadiusOf` 를 닫았다 — 남은 44 는 unit 7 몫 = 조각 E 진입 조건 0) · `bridge-fields` **0** · `rules` 보류 **31**(번호 없는 보류 0) · `tools` 잔여 **0**(순찰 10 완료 · 재배치 11 은퇴 — 7d). **조각 C 구현 완료(2026-09-24)**: 6a(`a5180e9e`~`0087e9d2`) · 6a2(`56b8a4d8`·`732b5a00`) · 6b(`cd390e511`·`a3798db83`) · 6b2(`bed80d790`) · 6c(`eca3e47e7` + 후속 5 `405bf29f3`~`ef3606e4c`: 어그로 풀림·라스트런 닫힘 사건, `AttackResolved` 도형 스냅샷, 방패 마음 부수 피해 백스톱, 7a 예고 메모) · 드리프트 감사 수정 13건 — 리뷰 전건 APPROVE(6c 후속 5 보충 리뷰 포함, finding 0). 사건 0~55 · 트레이스 ~53. **플레이 1차 후속(2026-09-24)**: 드래그 도형 가이드 이식 `1b2a9483c` + 마크 자격 옛 규칙 `b3aa03dea`(APPROVE) · 테스트 정정 `bbfa490f4`·거점 최근접 증언 `f7fbbdd29` · 도달 패리티 `dd9d19193`(20,000건 불일치 0) · 부가 타격 D1 힐러 순위 `dc31350d0`·D2 클래스 필터 `eadd0d2b0`(APPROVE) · dev 편성 `72785fa35`(캐스터 4기 제외) · **플레이 2차**: 드래그 실루엣 이식 `279e017ee`(+테스트 포커스 독립 `cb3e34554`, APPROVE) · 장판 그림은 계측으로 옛 프리팹·스케일 동일 확인 · 화상 표식은 사용자가 드래곤 화염으로 확인. 검증(HEAD `cb3e34554`): 헤드리스 build 0 · test 529 · Check 0 · Unity EditMode 코어+Assets 766/768(선행 2) · PlayMode 코어 **51/51** · 골든 11종 무변. **조각 C 종료 → 다음 = unit 7(7a → 7b → 7c → 7d)**. **unit 7a 구현(2026-09-24)**: `eaa3abc72`·`cac943bcc`·`7498ea35b` — 바인딩 코어(등록부·디스패처·seam 6 · `Immediate` append)·라우팅 표·형 카탈로그·`IntentApplier`(S20 단일 표면)·`AttackMod` 5·FanOut(캐논 1:1)·유닛 저작 규칙 bake(배치 스킬 17·적 악몽·실드 캐스트). 헤드리스 test **590** · Check 0 · 골든 11종 무변 · 장부 미정 **36** · rules 보류 33. core-reviewer·사용자 플레이 대기. **사용자 결정 필요 1건**(스킬 피해의 처치 귀속 — 7a 문서). **unit 7b 구현(2026-09-24)**: `dd23e4578`·`3d03046e5`·`7eea580b0` — 카드 부착·시전 트랜잭션(① 적용 → ② 차감 → ③ 순환, `HandDeck` 효과 0줄) · `Applicability`(preflight = 커밋) · Squad 상속(퇴근 회수) · 배치 오라 규칙 둘 · 표식(상한 밖 · 처치 보상 배율) · 인수인계 · 액티브 5 concrete(전투 중만 · 판의 시계) · 사직서 임계 → 운석 barrage(7d 에서 이동) · 카드·드림스톤 bake · 사건 60~62 · 트레이스 57~59. 헤드리스 test **629** · Check 0 · 장부 미정 **28** · rules 보류 **32**. core-reviewer·Unity lane·사용자 플레이 대기. **사용자 결정 필요 +1**(배치 오라 수면의 시작 시점 — 7b 문서). **unit 7c 구현(2026-09-24)**: `b22e0708e`·`87f9c5781`·`e139d3625`·`674829015`·`1f7d524c2` — 손패 UI(옛 `UI/Dreamcatcher` 이식 · 손패 = `HandDeck.Hand` 읽기 모델 · 진입구 = 유닛 선택 · preflight `HandDeck.UsableReason`/`WouldAttach`) · 부착 카드 줄 둘(오버헤드·선택 패널) · 부착 범위 링(`RangeCatalog` → `RadiusWithOrigin`) · 표식 · 발동 임팩트(`DcVisualConfig` 개통) · 적중 펄스 · 스킬 빔 · 메커닉 오라 · 각성 항아리 · 프로필 확정 덱 + 판 시드 액티브 롤 → `_cards`. 헤드리스 test **630** · Check 0 · **장부 잔량 무변(미정 28) — 이 unit 은 닫을 미정 행이 0 인 unit 이다**(카드 UI 의 브리지 행은 이미 「HandDeck」·「뷰 풀」로 배정됐다). 씬 배선 완료 · Unity EditMode 코어+Assets 925/927(선행 2) · PlayMode 코어 **56/56** · 골든 무변. core-reviewer·사용자 플레이(7d 뒤) 대기. 실드·배치 스킬·수면·출혈 스킬·감속 오라는 7a 뒤 플레이에서 확인. **unit 7d 구현(2026-09-24) — 조각 D 완료 · 미정 0 · 조각 E 진입 조건 충족**: `277e8bcba`·`cef88d259`·`d4cded945`(기믹 바인딩 · 보스 비행 창/착지 슬램 · 궁극기 fireCap 1 · 보스 어그로 면역 · 분열 `OnSlain` · 길막 폭발 · 호접몽 파탄) · `ff3094ac4`(디버그 도구 2 + 커맨드 24·25 + `BindingDiagnosis`) · `f45a9ae7e`(회오리·포탈 장 그림) · `3cf864ad5`(착탄 예고 링 · 브레스). 헤드리스 test **667** · Check 0 · `check_ledgers.py` exit 0. Unity lane·골든 대조·씬 배선(7d 「미배선」 표)·core-reviewer·사용자 플레이 2차 대기 — MCP 세션 끊김. **unit 7e(2026-09-24) — 카드 52장 자동 증언**: `0164a940a`·`8d98e45f4`·`4d390a78a`·`8d901b34d` — `EffectWitness`·`CardProbe`(코어 · 판정·상태 없는 도구) · 52 케이스 전부 ○ · 굽기 스냅샷 · 자가진단 메뉴 · 부족분 5. 헤드리스 test **678** · Unity EditMode 코어+Assets 992/994(선행 2). core-reviewer 대기. 브랜치 `rebuild/battle-core`. 모든 커밋은 리드가 클린 export 로 build/test/Check 재실행해 검증한다.

**조각 E 진입 시 상태(2026-09-25, HEAD `43cb8d65b`)**: `bridge-methods` 미정 0 · `bridge-fields` 0 · `tools` 잔여 0(4행 `SimOrderDumpMenu` 은퇴만 unit 9) · `rules` 보류 31 · 헤드리스 test 678 · EditMode 코어+Assets 992/994(선행 2) · PlayMode 코어 57/57. **그러나 「미정 0」은 배정의 끝이지 실현의 끝이 아니었다.** 새 주인이 코드에 없는 배정 3행(bridge-fields 1·31·55) · 새 씬에 짝이 없는 옛 화면 7종 + 메뉴 「나가기」 · 로비 진입 입력 대부분(편성·맵 풀·테스트 모드·토너먼트 채택·덱 스냅샷) · `GameManager` 안의 앱 전역 훅 2 · 브리지 static 미러 소비처 3 · 옛 폴더 안 저작 SO 1(자산 3)과 **ECS 컴포넌트가 붙은 길막 프리팹 2**(새 씬이 `Instantiate` 한다) · 옛 씬 폴더 안 볼륨 프로필 4 · 옛 씬 경로를 든 에디터 도구 4·Assets 테스트 다수 · 모바일 빌드 CLI 의 기대 씬 목록. 그래서 unit 8 을 **8a·8b·8c** 로 나눴다(실측 ~3.9k줄, 각 unit 문서 머리말). 격리 critic 25건 반영(2026-09-25).

**unit 8b 구현(2026-09-25) — 로비 교대**: `c899b6ab7`(진입 입력 — `MatchEntry` · 맵 풀 4갈래 · 웨이브 원천 7단 · 저작 플랜 시계 · 온보딩 칸 2 · 커맨드 26 · 덱 스냅샷 두 시점 · `AppBootstrap`) · `847305ce9`(온보딩 `CoreFirstRunGuide` · 철수 라벨 복구 · 손패 뒤집기 경합) · `f4cefe7d4`(결과·제출·나가기·기록 래치 + 씬 배선) · `d101b9dab`(**로비 시작 → `BattleCoreScene`** · 빌드 설정 · CLI 씬 목록 · 진입 테스트 재작성 14 · 옛 lane 경로 로더). 헤드리스 test **685** · Check 0 · Unity EditMode 코어+Assets 1005/1007(선행 2 · 골든 무변) · PlayMode 코어 **85/85** · 옛 부분집합 38/38 · `DreamSquadMobileBuildCliTests` 63/63 · `check_ledgers.py` exit 0 · rules 보류 **30**(X28 제거). Play 스모크 로비 → 판 → 제출 → 결과 → 로비(게스트 — 실제 랭킹 미확인). Android QA 빌드 미시도(keystore 숨김 입력). core-reviewer·사용자 플레이 4차 대기. ⚠ **저작 플랜의 판 길이**를 옛 규칙대로 옮겼다(온보딩 60초 · 테스트 플랜 끝없음) — 8b 「고친 것」. **리뷰 APPROVE**(2026-09-25 — M1 시드 기본값 문서 보충) · 리드 재검증 동일 수치(HEAD `7d5d99722`). 다음 = **8c**.

**unit 8a2 구현(2026-09-25) — 뷰 이전 잔여 9행**: `f7fb71693`(AI 전이 사건 64 · 트레이스 61 — 골든 하네스 비구독) · `12c1abee0`(궁극기 착지 예고 전용 링) · `0e4325fa0`(효과 타일 칸 + **새 씬 `SeasonRuntime.Bind` 누락 수정** — 로비 경로 판에서 효과 타일 0칸이었다) · `1eb9ce091`(사거리 칸 채움 · `IsPlacementRangeCell`) · `50d42ecc6`(드래그 흐림 · 체력 틴트 · 소환사 유지 루프) · `0cc59bee7`(마음 붕괴 연출 — 슬로모는 5c 가 이미 실현) · `6a5f580bf`(씬 배선 4) · `7d98d1b12`(자 검사) · `41b57d06d`(**행 9 — 월드 마음 스트레스 틴트·심박**, 리드 추가). `--owners` **exit 0** · 헤드리스 0·687·0 · Retire.Check 0 · EditMode 1011/1013(선행 2) · PlayMode 코어 93/93 · 옛 부분집합 38/38(리로드 직후) · CLI 63/63 · 골든 무변. core-reviewer · 플레이 4차 대기. **리뷰 APPROVE**(2026-09-25 — 행 1~8 · 행 9 부록, finding 0) · 리드 재검증 동일 수치(HEAD `03982fae7` · `--owners` 미실현 0). **조각 E 경계 1 도달 — 다음 = 사용자 플레이 4차 → main 머지.**

**unit 8c 구현(2026-09-25) — 지울 수 있다는 증명**: `66c77be6c` `[old-battle]`(저작 타입 이사 → `Scripts/Data/Authoring/` · 길막 프리젠터 ECS 의존 제거) · `1f4f934f2`(옛 씬 전용 입력 `*.OldBattle.cs` 부분 파일 · `FilterHiddenSkills` 새 층으로) · `236497b39`(볼륨 4 → `Art/Theme/<맵>/` · 옛 씬 경로 도구·테스트) · `e7a3cdf34`(`Retire.Check.csproj` · `check_ledgers.py --retire-prune/--retire-assets/--owners`) · `54d01eb2d`(`retire-set.md` 퇴역 590 파일 · C# 574 · 113,228줄 + 보류 2 · `owner-aliases.md` · 장부 실현 대조) · `7b3931631`(파이프라인 맵 전면 재작성). `Retire.Check` 오류 **0** · `--retire-assets` exit 0 · 헤드리스 0·685·0 · EditMode 코어+Assets 1005/1007(선행 2 · 골든 무변) · PlayMode 코어 85/85 · 옛 부분집합 38/38 · CLI 63/63. ⚠ **`--owners` exit 1** — 「새 주인」은 배정됐는데 새 씬에 실체가 없는 옛 기능 12행(효과 타일 칸 표시 · 궁극기 착지 예고 · 마음 붕괴 연출 · 드래그 중 적 흐림 · 적 체력 틴트 · 소환사 유지 애니 · AI 전이 트레이스 · 사거리 칸 채움) — 리드 판단으로 **8a2**(unit 9 앞 필수)가 닫는다 · 사거리 칸 채움(T3·T13)은 8a2 목록 밖이라 처분 미정. **보류 2**(`IngameCharacterTest` · `MenuPopup` dev 토글) 사용자 결정 대기. core-reviewer · 플레이 4차 · main 머지 대기. **리뷰 APPROVE**(2026-09-25 — M1 T3·T13→8a2 · L1 `Shader.Find` 선행) · 리드 재검증 동일 수치 + Retire.Check 0/음성 13. 리드 결정: dev 실험대 2 은퇴(a). 다음 = **8a2**(구현 중) → 플레이 4차 → main 머지.

**unit 8d 구현(2026-09-25) — 튜토리얼 전량 제거(결정 ④)**: `90d84fce2`(새 씬 안내 `CoreFirstRunGuide` · 진입 종류 Onboarding · 안내 전용 읽기 창 7 · 씬 오브젝트 2 + 배선 2) · `f3640b218`(코어 칸 `PinnedHandFront`·`BonusPullSuppressed` · `HandDeck` pinnedFront · 커맨드 26) · `c34658e28`(로비 참가 게이트의 안내 조건 · 배웅 플래그 · `LobbyTutorialStep` · RESET TUTORIAL · 로비 씬 오브젝트 5 + 배선 1 · 옛 씬 전용 `firstRunTutorialDone` → `PlayerProfile.OldBattle.cs`) · `e56c743c7`(`DcInspectPanelView.ActionRect` → `*.OldBattle.cs` · 키링 `DragStarted`) · `d613116fc`(`WavePlan_Tutorial`·`Deck_Tutorial` · 소비처 0 필드 · 스킬 문서) · `eff6833cc`(퇴역 집합 4b 묶음 · 598 파일 · C# 579 · 114,282줄 · 장부 G11·G12·D5·X6·메서드 4 삭제 · 은퇴 spec 5). 헤드리스 0·**680**·0 · `Retire.Check` 0 · `check_ledgers.py` 3종 exit 0 · 골든 11 무변 · EditMode 코어+Assets 1003/1005(선행 2) · PlayMode 코어 **91/91** · 옛 부분집합 38/38 · CLI 63/63. 계정 첫 판 참가 생략(서버 500 우회)은 존치. core-reviewer 대기. **리뷰 APPROVE**(2026-09-25 — finding 0 · LOW 2) · 리드 재검증 동일 수치(HEAD `5de171dfe`). **다음 = unit 9(구현 중) → 플레이 4차(결정 ⑤) → main 머지.**

**unit 9 구현(2026-09-25) — ECS 은퇴**: 문서 `66e122ec6`(참조 18편) · `45d43c8a0`(CLAUDE.md 옛 절 삭제·코어 절 승격 · 스킬 4 · 루트 README) · 리뷰 도구 `57c8af9df`·`b5c691841` · 장부 `2658e8b5d`·`e9b741760`(보류 2 → retire · **Entities 의존 잎 7 → 3c** · 짝 지도) · `7482f7ba6`(**짝 없는 옛 테스트 33 파일 이식** · 규칙 차이 4 `[Ignore]`) · `e548eda90` `[old-battle]`(**옛 전투 삭제** 1,215 파일 −125,684줄) · `10ea8cffe`(옛 생성기 + 스킬) · `60c09db21`(**Entities 패키지 제거** · asmdef 6) · `64dc493da`(네임스페이스 → `Wassup.Skills`·`Wassup.Data.Authoring`) · `9a8756016`(인스펙터 문구) · 훅 파일 삭제. 콘솔 에러 0 · 헤드리스 0·**876/880**·0 · EditMode 1320 · .Assets 285/287(선행 2) · .Core 887/891(건너뜀 4) · PlayMode.Core **95/95** · 골든 11 무변 · `check_ledgers.py` 3종 exit 0 · 로비 60fps. ⚠ **사용자 결정 필요 4**(옛 규칙과 코어가 다른 것 — `9_ecs_retirement.md` 「고친 것」). Android QA 빌드 미시도. core-reviewer · 플레이 4차 · main 머지 · `core.hooksPath` unset 대기. **리뷰 APPROVE**(2026-09-25 — finding 0 · LOW 2 반영) · 리드 재검증 동일 수치 + Check csproj 보정(`2c298b3b1`). **다음 = 사용자: 옛 규칙 차이 4건 결정 · 플레이 4차 · main 머지 → 10.**

### 사용자 결정 — 조각 E

- **④ 튜토리얼 전량 제거(2026-09-25 사용자 결정 「튜토리얼 모두 제거하자」 → 범위 질문에 「둘 다」)**: 전투 첫 판 온보딩(새 씬 `CoreFirstRunGuide` · 옛 `FirstRunTutorialController` · 온보딩 플랜/설정 · 코어 온보딩 칸 2 · 로비의 완료 플래그 게이트)과 로비 온보딩(로드아웃 4스텝 차단 오버레이) 전부. **결정 ①(온보딩 이전)은 이 결정으로 대체된다.** 튜토리얼 완료 게이트가 사라져 미완주 계정도 참가 신청이 나간다. ⚠ 계정 **첫 판**의 참가 생략(`IsFirstMatch`)은 튜토리얼이 아니라 서버 `complete` 500 우회(tutorial-offline-match)라 **존치**(리드 판단 2026-09-25 — 서버가 고쳐지면 별도 결정). 구현 = unit **8d**(9 앞).
- **⑤ 플레이 4차는 unit 9 뒤(2026-09-25 사용자: 「플레이 확인은 unit9 를 진행하고 하는게 나을거 같음」)**: 8d → 9(ECS 은퇴) 를 먼저 끝내고 플레이 4차를 한 번에 본다. 결정 ③ 의 공유 시점(main 머지)도 그 플레이 통과 뒤로 옮긴다 — 머지 게이트는 하나(플레이 4차)로 유지.
- **⑥ 옛 규칙 vs 코어 차이 6건(unit 9 이식이 드러냄)** — 리드 판단 2026-09-25: 전투 규칙 4건(방향탄 관통 소진 뒤 호밍 튕김 · 감지 후보 직업 필터 · 도발 리프레시 긴 쪽 유지 · 길막 방벽 광역 면제)은 「기획 그대로」 기본값으로 **옛 규칙 복원 = unit 9c**(사용자가 뒤집으면 되돌린다). 표현 2건(같은 입구 종별 예고선 · 예보 경로 해석)은 **사용자 결정 대기**(옛 방식 vs 코어 방식).

**확정 3건(2026-09-25 사용자 답 「권장대로 진행」 — ③ 도 기본값 (a) 8c 머지 때·게이트 = 플레이 4차)**: ① 첫 판 온보딩 = **(a) 새 씬으로 옮긴다** — 완주 → `firstRunTutorialDone` 저장 → 다음 판 참가 신청 발행까지가 8b 완료 기준. ② 모바일 빌드 CLI·테스트의 기대 씬 목록 = **(a) `OutgameScene` + `BattleCoreScene` 으로 수정 허용** — `DreamSquadMobileBuildCli.cs` 의 `ExpectedScenes`·거부 문구와 `DreamSquadMobileBuildCliTests.cs` 의 같은 목록만 바꾸고 그 파일의 다른 부분(서명·방향·출력 검증 등)은 무변. **미결 1건**: ③(아래 — 답 전까지 기본값 (a)).

아래는 질문 당시의 문면(선택지·결과)이다.

① **〔확정 (a)〕 계정 첫 판 온보딩(`first-run-tutorial`)을 새 전투 화면으로 옮기나?** 지금 새 계정의 판은 온보딩을 끝낼 때까지 60초 저작 웨이브 위에서 「유닛 선택 → 배치 → 배치 스킬 → 퇴근 → 다시 배치 → 드림캐쳐 부착」 안내를 띄운다(`FirstRunTutorialController.cs` 908줄 · spec 상태 「진행 중 2026-08-24 · 수동 Play 확인 대기」). ⚠ **완료 기록(`firstRunTutorialDone = true`)을 쓰는 곳은 이 컨트롤러 하나뿐이고**(`:719`), 로비는 그 기록이 없는 동안 **토너먼트 참가 신청을 생략**한다(`OutgameMenuController.cs:280~292`). 그래서 옮기지 않고 그냥 두면 **새 계정은 토너먼트에 영영 오르지 못하고**, 매 판이 60초 저작 웨이브·첫 손패 고정·보너스 당김 억제 판이 된다. 선택지:
   - **(a) 옮긴다 — 기본값.** 옛 게임 그대로다. 안내 도구(`TutorialGuidanceView`·`OutgameTutorialOverlay`)는 재사용하고 컨트롤러만 새 읽기 모델로 다시 쓴다(~0.7k줄). 완주하면 완료 기록 → 다음 판부터 참가 신청이 나간다.
   - **(b) 옮기지 않고 온보딩을 끈다.** 새 씬에서는 온보딩 판 규칙(저작 웨이브·첫 손패·보너스 억제)을 걸지 않는다. 로비는 온보딩 조건을 보지 않고 「계정 첫 판 우회」(`matchesPlayed == 0`)만 남긴다. 결과: 새 계정은 첫 판 한 번만 토너먼트 밖이고 그 뒤 정상 참가한다. 온보딩은 재설계 전까지 없다(후속 후보).
   - (c) 옮기지 않고 그대로 둔다 — 위 ⚠ 의 결과. 권하지 않는다.
   ⚠ 7c 「이식 제외」의 「튜토리얼 콘텐츠는 전량 제거됐다(76038c26)」는 그 뒤(08-19~24)에 새로 지은 이 온보딩을 빠뜨린 문장이다. → `8b`

② **〔확정 (a)〕 모바일 빌드 CLI 의 기대 씬 목록을 바꿔도 되나?** 빌드 CLI 는 켜진 씬이 「정확히 `OutgameScene` 다음 `BattleScene`」이 아니면 빌드를 거부한다(`DreamSquadMobileBuildCli.cs:33~37·603~610`). 그 테스트도 같은 목록을 단언한다(`DreamSquadMobileBuildCliTests.cs:333`). 이 두 파일은 「mobile-build 복구 커밋 보호」(변경 금지) 대상이다. 로비가 새 씬으로 들어가면(8b) 이 목록을 안 바꾸는 한 **Android·iOS QA 빌드가 막힌다.** 선택지:
   - **(a) 목록을 `BattleCoreScene` 으로 바꾸는 것을 허가한다 — 기본값.** CLI 한 줄 + 테스트 한 줄. 나머지 보호 내용(서명·방향 등)은 무변.
   - (b) 파일은 그대로 두고 **새 씬을 `BattleScene.unity` 경로로 들인다**(옛 씬 파일을 먼저 치운다). CLI·테스트·`SceneNames` 는 무변이다. 대신 옛 씬을 8b 에서 치우게 되어 옛 판과 나란히 비교할 수단이 한 unit 일찍 사라진다.
   - (c) 둘 다 불허 → 로비 교대를 보류하고 조각 E 는 8a·8c 까지만.

③ **〔확정 (a) — 2026-09-25 「권장대로 진행」〕 새 전투를 동료가 받는 시점.** 로비 교대를 8b 로 당기면, 8c 의 main 머지(조각 E 경계 1)에서 **동료·GitLab 미러가 새 전투를 받는다**. 옛 전투를 지우는 unit 9 보다 한 단계 빠르다. 선택지:
   - **(a) 8c 머지 때 — 기본값.** 게이트 = 사용자 플레이 4차 통과. 옛 씬 파일은 unit 9 까지 저장소에 남아 되돌리기가 싸다.
   - (b) unit 9 머지 때 한 번에 — 8b 의 교대 커밋은 브랜치에만 두고 main 에는 9 와 같이 실린다.

**에이전트가 정한 것 5건**(코드 구조라 질문 대상이 아니다). ⑴ unit 8 을 **셋**으로(화면 → 진입 → 폐쇄). ⑵ `Faction` 을 unit 9 에서 `Wassup.Skills` 네임스페이스로 — 규칙 diff 0 단독 커밋(파일 수는 삭제 뒤 재측정 — 2026-09-25 옛 것 제외 87~157). ⑶ 옛 PlayMode lane(`Wassup.Tests.PlayMode`)은 **아웃게임 lane 으로 존치**하고 Entities 참조만 뺀다. 진입 테스트 6건은 8b 가 새 씬 대상으로 다시 쓴다. ⑷ 덱 스냅샷은 `BattleLogger` 를 새 씬에 들이지 않고 `TournamentDeckInfo.Serialize` 를 직접 부른다(두 번째 매니저 금지 — `CoreMatchOutcomePresenter.cs:107~109`). 배틀 JSON 로그 파일은 은퇴한다(유일한 소비 도구가 은퇴한 PRD 가설 스크립트 — `tools/analyze_sessions.py:2`). rules X28 은 「제거」로 닫는다. ⑸ 덱 타이머 폴백(작업 표 unit 4 행 「unit 9 에서 제거」)은 **이미 해소 — 필드 존치**다. 판 길이는 모드 단독이고(`MatchDefinitionBuilder.cs:340`), 필드는 `configHash` 에 든다.

**고지 2건**. ⑴ 결과 화면 뒤 HUD 는 **옛 게임처럼 숨긴다**(5c 가 차이로 기록한 것 — 되돌리는 것이 기본). ⑵ 옛 `BattleScene` 은 unit 9 에서 지운다(결정 ②(b) 면 8b) — 그 뒤 옛 전투로 돌아갈 길은 git 이력뿐이다.

설계 입력: [`docs/plans/2026-09-22-battle-core-rebuild-census/`](../../plans/2026-09-22-battle-core-rebuild-census/) — 6영역 census(약 395행) · 종합(`00`) · 상호 리뷰(`01`·`03`) · 트리거→발동 rev 3(`04`) · 매치 모드 연구(`05`) · **계획 완전성 리뷰(`06`, 13건 — 이 rev 2 의 근거)**. 핵심 클래스 UML 은 [`class-diagram.md`](class-diagram.md), 매치 모드는 [`match-mode-design.md`](match-mode-design.md).
선행 spec 처리: `battle-sim-extraction` **M0 완료·M1+ 폐기**(후계 = 이 spec) · `battlebridge-dissolution` **흡수** · `ecs-lifecycle-teardown` **은퇴**.
선행 완료(2026-09-23): **CLAUDE.md 범위별 재편**(상태 라인 · `[옛 전투]` 꼬리표 · 「새 전투 코어 — 절대 제약」 6항). `AGENTS.md` 는 symlink 라 자동 동기.



### 사용자 결정 기록 2026-09-24 (투사체)

① 시전자 착탄 효과는 **그 시전자가 쏘는 모든 탄**에 적용 ② 같은 부여 겹침 = **합, 상한 있음** ③ 부여 어휘는 **기존 착탄 효과 그대로**(화염·출혈 스택 등, 신설 없음) · F30 = **(a) 고친다**(출처 = 발사자). 감사에서 발견한 라이브 결함: 발사 명세 선정 규칙 enum 번호 어긋남(12 중 11 오독) → 수정 중.

### 사용자 결정 필요 — 조각 C(효과) 착수 전 · **1건**

**같은 적이 쏘는 디버프가 이제 안 쌓인다**(F30). 지금은 발사마다 새 슬롯이 생겨 **곱으로 누적**된다 — 출처로 투사체 개체를 보내서 생긴 라이브 결함이다. 고치면 곱누적이 상시 배율이 되어 **킨들러류 원거리 적이 눈에 띄게 약해지므로** 수치 재조정과 한 묶음이 된다. 이번에 고칠 것인가, 결함을 박제하고 뒤로 미룰 것인가? 답이 오기 전에는 **현행(투사체 출처)을 박제**한다.

**에이전트가 정한 것 3건**(코드 구조·축이라 질문 대상이 아니다). ⑴ 불 스택 규칙을 **저작 자산별로** 가른다 — 지금은 종류당 한 벌이라 드래곤을 올리면 킨들러가 같이 올라간다. **값은 오늘과 같게** 저작하므로 판은 안 바뀐다. ⑵ 한 몸에 상태가 여럿일 때 **무엇이 이겨 보이나**는 6c 플레이에서 확인한 뒤 데이터로 굳힌다(옛 코드의 암묵 순서를 베끼지 않는다). ⑶ 존 효과의 **진영 축을 연다**(제약 8 이 이 하드 게이트를 명시 지목했다) — 라이브 저작은 오늘과 같은 값이다.

**고지 1건**(질문이 아니라 제약 13 적용의 결과). **레드불을 스치듯 지나가도 먹게 된다** — 지금은 같은 칸이어야 먹는데, 제약 13 이 예외를 배치 판정 하나로 못박았으므로 픽업도 「칸 반폭 + 내 몸」 자를 쓴다. 판정이 넓어지며, 체감 확인은 6c 플레이 항목이다.

### 사용자 결정 필요 — 조각 D(트리거→발동) 착수 전 · **3건**

전부 **플레이어가 겪는 규칙**이다(CLAUDE.md 작업 지침 0). 답이 오기 전에는 각 항목의 **기본값**으로 구현하고 해당 unit 의 「이식 제외」 표에 「사용자 답 대기」로 적는다.

① **같은 카드를 두 장 붙인 유닛이 죽거나 «적을 죽이면» 몇 번 터지나?** 지금은 **한 번**이고, 같은 유닛을 **퇴근시키면 두 번**이다 — 같은 규칙인데 문이 갈린다. 종류가 다른 카드는 어느 쪽에서도 각각 다 터지므로 갈리는 것은 **같은 카드 여러 장**뿐이고, 억제가 걸린 문은 **자기 죽음과 처치 둘**이다(작별 선물 · 시체 폭발). 그 제한은 옛 사망 이벤트가 값 한 벌만 실을 수 있어 생긴 **기계의 한계**이고(퇴근 쪽 코드가 그렇게 적어 뒀다) 새 코어엔 그 한계가 없다. **기본값 = 사망·처치도 장수만큼**(계약 2 「땜빵은 의도만 옮긴다」). → 7b · `rules.md` E4

② **보스가 도약할 자리를 고르는 자가 사각형인 채로 둘까?** 판정은 전부 원 자로 통일됐는데(제약 13) 밀집도 계산만 아직 사각 자다. 원으로 바꾸면 **보스가 내려앉는 칸이 달라진다** = 밸런스 변경. **기본값 = 현행 사각 자 박제**(폭탄맨 폴백을 unit 3 이 같은 이유로 보류한 것과 같은 처분). → 7d

③ **자고 있는 유닛이 주기 스킬을 계속 쓰는 것은 사양인가 버그인가?** 옛 주기 트리거는 행동 잠금(기절·수면)을 **읽지 않는다**(실측). 스펙은 사양으로 썼고 사용자는 **버그로 읽었다**(Play 관측 2026-08-11). 고치면 주기 스킬을 가진 **전원**의 동작이 바뀐다. **기본값 = 현행 박제**. → 7d

④ **스킬로 죽인 적이 그 유닛의 「처치」 규칙(시체 폭발 등)을 켜나?**(7a 발견) 옛 전투는 스킬의 직접 피해에 출처가 없어 처치 규칙은 안 켰고 점수는 줬다. 새 코어는 점수가 귀속된 죽음에만 나서 둘을 같이 못 옮긴다. **기본값 = 출처를 싣는다**(점수 유지, 부작용 = 처치 규칙이 켜진다). → `7a_binding_core_and_skills.md` 「사용자 결정 필요 (7a)」

**에이전트가 정한 것 3건**(코드 구조·축이라 질문 대상이 아니다). ⑴ 조각 D 를 **7a·7b·7c·7d 넷**으로 나눈다 — 복사·적응 대상이 실측 **19,255줄**(파일을 한 번씩만 센 합)이고 카드 화면만 8,149줄이다 — 옛 검사 패널 1,439줄은 5b 가 이미 선택 패널로 이식했다. ⑵ 저작 enum(트리거 10 · 페이로드 33)을 **코어가 같은 번호로 미러**하고 pin 테스트가 대조한다(6a 의 `CoreSkillEnumPinTests` 선례) — 어셈블리가 갈려 컴파일러가 못 잡는다. ⑶ 「쓰기는 발행으로만」(S20)의 강제 수단을 asmdef 에서 **`IntentApplier` 단일 표면 + 아키텍처 테스트**로 옮긴다.

**고지 3건**(질문이 아니라 확인의 결과). ⑴ **재배치(유닛을 다른 칸으로 옮기기)는 안 옮긴다** — `defender-clock-out/0` 이 2026-08-13 팀 리뷰로 진입구를 껐고 퇴근이 그 자리를 대신한다. 라이브에 없는 기능이라 되살리는 것은 새 기능이다. ⑵ **마메모의 「웨이브 회전 정지」에 해당하는 훅은 코드에 없다**(rev 3 §8 확인 대기 해소) — 실제 기제는 웨이브 생성기의 보스 호위 후처리이고 unit 4 가 이미 이식했다. ⑶ **「한 발이 반경 안 전원에게」는 질문이 아니라 이식 필수다** — unit 3 이 「켠 곳이 0 이면 은퇴」로 이월했는데 라이브 저작이 **하나 있고**(`Pattern_Cannon_Strike.asset:30`) 그것이 **캐논의 1:1 융단폭격**이다. 안 배선하면 캐논 배치 스킬이 조용히 한 발만 쏜다.

> **반영 시점(1프레임 지연)은 질문이 아니다.** rev 3 §4 가 이 축을 **「변경 없음」**으로 닫았다 — 지연을 만든 것은 채널이 아니라 **단계 순서**이고, 새 코어가 같은 순서를 쓰므로 규칙이 그대로 계승된다.
- **범위 그림 vs 판정(플레이 1차)**: 그림은 «범위 + 자기 몸», 판정은 «+ 대상 몸»(제약 13). 옛 게임과 동일함을 실측으로 확인했고 사용자 답 **(a) 그대로**. 대상 몸은 어느 그림에도 그리지 않는다(`directional-attack-shape/7:52`).
- **스킬 피해의 출처(7a)**: 스킬 직접 피해(브레스·지진·광역 부수 피해)에 **시전자를 싣는다 (a)** — 점수 귀속 유지, 부작용으로 처치 규칙이 켜진다. 옛 게임(출처 없음·점수는 킬러 무시)과 다른 새 규칙 — 사용자 결정 2026-09-24.
- **배치 오라 수면 시작 시점(7b)**: **활성화 순간(새 코어)** — 저작 초 전부를 잔다. 옛 게임은 스폰 순간이라 배치 모션만큼 깎였다 — 사용자 결정 2026-09-24.

## 상위 목표

**현재 게임이 실현한 기획(규칙과 흐름)을 Mono 전제에서 새로 설계한 전투 코어로 옮긴다.** 완벽 재현은 목표가 아니다 — 원래 로직과 흐름을 최대한 따르되, 땜빵·우연은 상세를 옮기지 않고 의도만 옮긴다. 세세한 차이는 이식 뒤 사용자가 플레이하며 고친다. **`BattleBridge` 는 데모용 임시 수단이었고 이번에 소멸한다** — 어디로도 흩어지지 않고, 그 일의 담당자들이 각자 갖는다.

- 규칙의 정본 = census. 구조의 정본 = 이 spec + UML. 기존 ECS 구조는 「왜 그 규칙이 필요했나」의 증거로만.
- 산출물 = 엔진을 모르는 `Wassup.BattleCore` + Unity 층(정의표 빌더·드라이버·뷰 풀·입력·HUD). 서버·네트워크 없음.

## 검증 질문

> **같은 modeId+seed 로 헤드리스 EditMode 에서 판이 끝까지 돌고, 사용자가 실기기에서 플레이했을 때 「지금 게임과 같은 게임」이라고 느끼는가.**

부수 질문: 전투 코드 어디에도 `Unity.Entities` 가 없고, `manifest` 에서 Entities 계열이 사라져도 asmdef 4개가 초록인가.

## 작업 단위 (세로 조각 순 · rev 2)

| 조각 | 파일 | 작업 구분 | 목적 |
|---|---|---|---|
| **0 환경** | `0_transition_environment.md` | **전환 환경** (완전성 리뷰 13건의 자리) | ① **씬**: 새 `BattleCoreScene.unity` — 옛 `BattleScene` 무변, 뷰 풀 다툼 없음 ② **브랜치·워크트리**: `rebuild/battle-core` 브랜치 + 전용 워크트리(main 은 승인 push 하나로 전부 실린다) · main 머지는 조각 B·D·E 경계에서 리뷰 후 ③ **동결 장치**: `.githooks/pre-commit`(`core.hooksPath`) 이 `Scripts/Battle/**`·`Bridge/**` 스테이징을 `[old-battle]` 태그 없이 거부 + 착수 대기 spec 4건 판정(카메라·squad-slots = 계속 / wide-board-content·heart-stress-axis 12 = 새 코어에서) ④ **리뷰 도구**: `ecs-review-detector` 에 `Scripts/BattleCore/` 경로 추가 + `core-reviewer` 에이전트(코어 제약 6항 체크리스트) — 첫 커밋 전 ⑤ **귀속표 3종**: 브리지 메서드 369 + 직렬화 필드 91 + **브리지 밖 규칙 보유자 10**(`TilemapMapView`·`GameManager`·`DreamcatcherHandController`·`DraftController`·`SkillLoadoutController`·`CostRuntime`·`PlacementInput`·`SkillRuntime`·`PlacementCooldownRuntime`·`MatchTally`; `TimeManager` 는 의도된 예외) → 담당자 / 삭제, 기계 대조 스크립트 동반 ⑥ **도구 10개 처분표**(6개가 삭제 예정 폴더 안): 재작성 시점을 조각 C·D 앞으로 ⑦ **규칙 분류표**(census 「코드에만 있는 규칙」 ~100건 → 이식 필수 / 보류 / 제거) ⑧ **서버 payload 판정**: v1 제출은 `KillScoreTimed` 만, `WaveClear`·`TimeAttack` 은 `submitsReport=false`(로컬) — 서버 API 확장은 후속 후보 ⑨ `SimEntityId` 센티널 확정(Match 호스트 0 · 유닛 1~ · None = -1) |
| **A 뼈대** | `1_skeleton_and_harness.md` | 뼈대 + 하네스 | `Wassup.BattleCore` asmdef · `MatchDefinitionBuilder`(SO→정의표, **`configHash` 의 새 소유자 = `MatchDefinition`**) · `BattleWorld`/`Unit`/틱 골격 · 커맨드+receipt · `EventBus` · **새 코어 헤드리스 러너**(옛 러너와 병존 — A/B 는 둘이 동시에 있을 때만 가능) · 새 코어 골든(`LegacyTraceV0` 포맷) |
| | `2_map_and_movement.md` | 맵·이동 | `MapSnapshot` 수신 · 흐름장 슬롯 · 통행층별 NavGrid · 장애물 재빌드 · 이동 결정 순서 · 평활화·충돌·분리 · 감지·어그로·도발 이동 · 골 도달 · 순찰 |
| | `3_combat.md` | 전투 판정 | 공격 루프(START/RESOLVE) · 도달 산식(제약 13) · 타겟팅·락·히스테리시스 · 방향 도형 · 투사체 궤적×페이로드 · 발사 명세 · 피해·실드·킬 귀속 · 사망 2단계 · UnitAi 상태 · `AttackMod` 축 |
| | `4_match_owners_and_mode.md` | 매치 담당자 + 모드 | 담당자 8(`MatchClock`·`WaveScheduler`·`CostLedger`·`PlacementService`·`HeartMeter`·`ScoreLedger`·`HandDeck`·`GimmickHost`) 각자 상태+규칙+틱 단계+이벤트 · `MatchModeData` SO → `ModeDef` · `IMatchGoal` + concrete 3 · **완료 기준에 포함**: `enemy-wave-integration` 스킬 갱신(같은 커밋 의무) · 모드 유효성 테스트(`EditModeAssets` lane) · 덱 타이머 이관은 「모드가 이기고 덱 값 폴백」으로 **조각 E 머지까지** 유지 후 unit 9 에서 제거(2026-09-25 정정: 판 길이는 이미 모드 단독 — 필드는 `configHash` 에 들어 존치, `9_ecs_retirement.md` 구현 6) |
| **B 첫 플레이** | `5a_driver_and_unit_views.md` | Unity 층 1/3 | `BattleDriver`(**`Build(…, structures:)` 필수** · `TimeManager` 리스 → 틱 발행률) · `ViewOrder` 상수로 정렬 방출 · 유닛/투사체/피해숫자/히트바/오버헤드/도약 뷰 풀 각자 구독 · **직렬화 필드 91 의 새 주인 4분류**(코어 정의표 / 뷰 설정 SO 7 / 씬 배선 참조 / 삭제) · 디버그 도구 2(감지 프로브·장애물) · **새 PlayMode lane `Wassup.Tests.PlayMode.Core`** · 파이프라인 커버리지(유닛·투사체). 상태 FX·빔·VFX·오라 풀은 **사건이 열리는 unit 6·7 에서** |
| | `5b_input_hud_overlay.md` | Unity 층 2/3 | 드래그 배치 입력 → 커맨드+receipt(판정 0) · 퇴근·제출 입력 · HUD 5(읽기 모델) · 맵 오버레이(`AttackReach.InReach` 호출만) · **예고선 = `StructureChoice` 호출만**(M18) · 카메라 재사용 |
| | `5c_match_outcome_and_audio.md` | Unity 층 3/3 | **판 종료 → 결과 화면 → 제출 게이트**(`submitsReport && allowSubmit`, `ReportResult` 시그니처 무변 — 지금은 브리지 안에만 있다) · **전투 사운드 3종**(브리지 안 호출 3건의 새 주인, 클립은 뷰 데이터 SO) · 모드 진입 3단 · 「아직 안 보이는 것」 표 → **카드 없이 판이 돈다 — 사용자 플레이 1차**(질문 = 배치·이동·전투·점수·종료의 손맛) → main 머지 |
| **C 효과** | `6a_modifiers_cc_dot_stacks.md` | 효과 1/4 — 슬롯 | 모디파이어(병합 4축, 전역 번호판 폐기 → **`SlotTag` = (종류, 판별자) 짝** — 판별자를 접으면 옛 슬롯 충돌 버그가 재현된다)·스택(2축 · 임계 규칙은 **저작 자산별**)·군중 제어(런타임 슬롯 **3**; `Slow`·`DoT` 는 저작 토큰)·지속 피해((출처, 원소) 2축)·실드(FIFO · 시간 만료 없음)·최대체력 배율(`Unit.BaseMaxHealth`) · **부여는 큐가 아니라 관문 함수**(옛 3채널 소멸)이되 **반영 시점은 단계 위치가 계승**(F29 = rev 3 §4 「변경 없음」) · **회수 = 슬롯 삭제**(F27·F28·F33 동시 해소) · `ModifierAuthoring` salvage(상한 = `(배율−1)×최대중첩`) · **새 phase 신설 없음** · `DeterminismTests` 확장 |
| | `6a2_projectile_onhit_gate.md` | 효과 1.5/4 — 탄 관문 | **시전자가 쏘는 모든 탄이 시전자의 착탄 효과를 싣는다**(사용자 결정 2026-09-24 ①) · 부여 슬롯(합·상한 ②) · 어휘 신설 없음(③: 화염 = `ApplyStack(Fire)`) · `SpawnRequested` 한 곳에서 접음 · 킨들러 화염·난도질꾼 출혈이 탄에 실리는 첫 자리(unit 3 이월 해소) · 공격 수식자 5종은 같은 슬롯의 다른 종류(실행 unit 7) |
| | `6b_hazards_fields_effect_tiles.md` | 효과 2/4 — 물건 | 존 장판(연속 원 · 감속은 **이동속도 모디파이어** · **진영 축 개방**, 제약 8)·길막(문은 「부서짐」 하나)·장 캐리어(겹치면 가장 강한 값 · 재발행 주기 **재산출**)·효과 타일(적용 + **회수**) · 디버그 커맨드 2 |
| | `6b2_pickups_resignations_gimmick_stacks.md` | 효과 3/4 — 기믹 셈판 | 픽업(제약 13 자로 소비)·라스트런(`ProgressiveStates.LastRun`)·사직서(누적·임계)·열기/피로(피로 누적은 스탯 적용 **뒤** = 1틱 지연 박제) · **기믹 수치를 `GimmickDef` 로**(config 싱글턴 4 소멸) · **`[Periodic]` seam — enum + 호출부** · 디버그 커맨드 3 · 장부: E6(표식 없음 · 사건이 정본)·M3(**완료 — unit 2 가 이미 분리**) · **골든 11종 Unity 재굽기**(조각 C 의 마지막 코어 변경) |
| | `6c_effect_views_and_tools.md` | 효과 4/4 — 그림·도구 | 5a 가 일부러 안 만든 뷰 풀 4(상태 FX·빔·VFX·오라) + 해저드·픽업·사직서 뷰 · 오버헤드 **실드 비율·스택 아이콘**(5a 이월) · 선택 패널 **델타 칩**(5b 이월, 재곱 금지) · 배치 연출 VFX·카메라 흔들기(5b 이월) · 임팩트 소켓 높이(5a 이월) · 방패 걸린 마음의 **부수 피해 제외 소비처**(unit 4 이월 — 6c 후속 4 에서 `HeartMeter` 백스톱으로 완료) · **디버그 도구 재작성 3**(tools.md 6·7·9) · 장부 마감 + `check_ledgers.py` exit 0 → **사용자 플레이(조각 C)** |
| **D 트리거** | `7a_binding_core_and_skills.md` | 트리거→발동 1/4 — 바인딩 코어 | **정본 = [`04_trigger_layer_rev3.md`](../../plans/2026-09-22-battle-core-rebuild-census/04_trigger_layer_rev3.md) — rev 2→3 정정 9건(Squad 수명=호스트 · 기믹 per-unit 타이머 · PlacementAura 2바인딩 · SplitOnDeath=OnSlain · fireCap≠lifetime · BFS 직접 재진입만 · AreaBlast 병합 철회 · skillId 은퇴 범위 · 사망 seam 스냅샷)은 7a 의 첫 표** · `BindingDef`/`BindingRegistry`/`TriggerDispatcher` · **정적 라우팅 표 유지**(프리뷰가 드래그 중에 형을 묻는다) · `CoreSkillContext`+`IntentApplier`(S20 을 「단일 쓰기 표면」으로 닫는다) · concrete **33**(형 표 = 제약 13) · seam **6**(`Immediate` append, Cast 없음) · `AttackMod` 5 · 유닛 저작 스킬(적 악몽·배치 스킬·퇴근) · **`FanOutToAllCandidates` 이식**(캐논 1:1 융단폭격 — unit 3 의 조건부 은퇴 전제가 거짓이었다) · 장부 **44 → 36** |
| | `7b_card_rules_and_bindings.md` | 트리거→발동 2/4 — 카드의 규칙 | unit 4 가 「효과 자리는 진단 통로로」 남긴 구멍을 닫는다: 부착·시전이 **바인딩을 실제로 붙인다**(동기 트랜잭션 ①적용 ②차감 ③순환) · Squad = **host 유닛 수명**(소멸 ∪ 퇴근) · `CostRate` = 메타 intent `SetCostRegenMul` · PlacementAura **2 바인딩** · 표식 `fireCap 1` + 소멸까지 부착(부착 상한 밖) · `trigger == None` 3장 · 인수인계 = **손패 집합 연산** · `Applicability` 코어 이전(preflight ↔ bake 한 함수) · 회수 = **슬롯 삭제**(6a 축 재사용) · 장부 **36 → 28** |
| | `7c_card_views_and_hand_ui.md` | 트리거→발동 3/4 — 카드의 화면 | **이 spec 최대 덩어리(실측 8,149줄)**: 손패 뷰 · 드래그 슬롯 · 포커스 락온 · 각성 게이지·항아리 독 · 카드면·문안(**formatter 가 이긴다**) · 흡수 비행 · 타겟 화살 · **부착 범위 링**(반경은 코어 `RangeCatalog` 가 준다 — 뷰 재계산 0) · 선택 패널·오버헤드 **부착 카드 줄**(6c 이월 2행) · 표식 뷰 · 입력 → 커맨드(판정 0) · **장부 잔량 무변(28)** |
| | `7d_gimmicks_boss_split_and_ledger.md` | 트리거→발동 4/4 — 기믹·보스·분열·장부 | 6b2 가 남긴 「무엇이 언제 그것을 놓는가」: 기믹 4(유닛 호스트 per-unit 타이머 · **필터는 기믹마다 다르게 현행** · 레드불만 Match 주기 · 사직서 드랍=사망 seam + **운석 barrage**) · 보스(위협 귀속 C25 이월 · `fireCap 1` 은 궁극기만 · 착지 선정 자) · 분열 `OnSlain` · 호접몽 · 순찰 소환 · **마메모 훅 없음 확인**(웨이브 호위 후처리가 그 기제 — rev 3 §8 해소) · 디버그 도구 2(tools 10·11) · **장부 28 → 0 + `check_ledgers.py` exit 0 = 조각 E 진입 조건** · **재배치는 이식 제외**(진입구가 이미 꺼져 있다) → 사용자 플레이 2차 |
| | `7e_card_effect_verification.md` | 검증 장치 — 카드 효과 자동 증언 | 카탈로그 52장 전량 「굽기 → 강제 발동 → 의도 종류별 관측」(`EffectWitness`·`CardProbe`, Assets lane) · 저작 스냅샷(canonical 텍스트) · Play 중 자가진단 메뉴 · 비자명 규칙 부족분. 규칙 변경 0 — ×는 보고만 |
| **E 전환** | `8a_view_transfer.md` | 전환 1/3 — 옛 화면에만 있는 것 | **대조표 먼저**(옛 씬 GUID × 새 씬) · 당김·보너스 알약(커맨드 10·13 은 있고 입력이 없다) · 보너스 포탈(`portalLingerSec` 는 뷰가 읽는다) · 보스 경보 · 기믹 리빌 · 메뉴 웨이브 브리핑 · 페이즈 먹이(`CoreCameraFeed` → `BattleCoreUnity/`, 카메라 + **BGM**) · 결과 뒤 HUD 게이팅 · `ResultScreen` 이 `MatchOutcome` 직접 · 손패 유체 배경 · **브리지 static 미러 소비처 3**(새 씬에서 프랍 37개 거리 틸트가 꺼져 있다) · `GamePhase` 자기 파일로. 장부 「새 주인」이 코드에 없던 3행(bridge-fields 1·31·55)의 실체 |
| | `8b_match_entry_and_lobby_switch.md` | 전환 2/3 — 판 진입·퇴장 + 로비 교대 | 편성·돌·코스트 배율·시드·**맵 풀 4갈래**·테스트 모드·토너먼트 참가 채택·덱 스냅샷·`matchesPlayed`·나가기(0점 제출)·앱 전역 훅(60fps·트윈 풀 — `GameManager` 안의 `[RuntimeInitializeOnLoadMethod]`)·해상도 캡·DPI 임계 → 새 자리(rule-holders G3~G24) · **첫 판 온보딩(사용자 결정 ①)** · 마지막 커밋 = `SceneNames.Battle` + 빌드 설정 + 모바일 빌드 CLI 씬 목록(**사용자 결정 ②**) · 덱 스냅샷은 `TournamentDeckInfo.Serialize` 직접 · Android QA 빌드 1회차 → 사용자 플레이 4차 |
| | `8c_bridge_retirement_closure.md` | 전환 3/3 — 지울 수 있다는 증명 | **퇴역 집합 = export 에서 지우고 컴파일한다**(`Retire.Check.csproj`) + 자산 GUID 폐포(뿌리 = 빌드 씬·Resources) · 새 주인 실재 검사(`--owners` — 칸 문법 = 첫 백틱 심볼 + 별칭표) · 옛 폴더 안 저작 타입·ECS 컴포넌트 이사(`BlockingHazardSO`·길막 프리팹 2 의 `BlockingHazardPresenter` · `FilterHiddenSkills` 등 — `.meta` 동반, GUID 보존) · **`Scenes/BattleScene/` 의 볼륨 프로필 4개**(스테이지 프리팹 5개가 부른다) 이사 · 도구 장부 누락 1(`ReachDebugGizmos` 은퇴) · `object-pipeline-map.md` 전면 재작성 · `Faction` 네임스페이스 결정 → 사용자 플레이 4차 통과 뒤 main 머지(조각 E 경계 1 · **결정 ③**) |
| | `8a2_view_transfer_remainder.md` | 전환 잔여(8c 뒤 · 9 앞) — 장부가 「새 주인」이라 적었지만 실체가 없던 뷰 8행 | 8c `--owners` 발견: 효과 타일 칸 표시(T15) · 궁극기 착지 예고 칸(T16·T17) · 마음 붕괴 연출+슬로모(`HeartCollapsed` 30 · 틱 발행률) · 드래그 중 적 흐림 · 적 체력 틴트 · 소환사 유지 애니메이션 · AI 전이 트레이스(있으면 흡수) · 사거리 칸 채움(T3·T13 — 리드 기본값 이식, 사용자 은퇴 선택 가능). 끝나면 `--owners` exit 0 |
| | `8d_tutorial_removal.md` | 튜토리얼 전량 제거(결정 ④ · 8a2 뒤 · 9 앞) | 새 씬 온보딩 `CoreFirstRunGuide` · 코어 온보딩 칸 2 + 커맨드 26 · 프로필 플래그·로비 참가 게이트 · 로비 로드아웃 온보딩 4 파일 + 씬 오브젝트 · 설정/플랜/덱 자산 · 테스트 5 파일. 옛 씬에 묶인 것은 퇴역 집합으로(unit 9). 새 계정 첫 판부터 참가 신청 |
| | `9_ecs_retirement.md` | ECS 은퇴 | `[old-battle]` 태그로 삭제(`Battle/`·`Bridge/`·`Editor/Battle/`·퇴역 집합·`BattleScene`·옛 골든·`EntitiesClientSettings`) · 패키지 −Entities·Entities Graphics(**Burst·Collections 는 남는다** — URP 의존) · asmdef diff · 테스트 삭제마다 코어 짝 · 옛 PlayMode lane 은 아웃게임 lane 으로 존치(Entities 참조만 제거) · 리뷰 도구·동결 훅 은퇴 · 네임스페이스 `Wassup.Battle.*` 0 · 덱 타이머 폴백 = 이미 해소·필드 존치(`configHash`) · 옛 웨이브 생성기 + `enemy-wave-integration` 스킬 같은 커밋 · 문서 18편·스킬 5 · **CLAUDE.md 편집 목록**(옛 절 삭제 · 코어 절 승격 · 번호 보존) · Android QA 빌드 2회차 |
| | `10_handoff.md` | 인계 | `11_handoff_summary.md` · 후속 후보·보류 잔여·이식 제외 보류 → `docs/spec/README.md` Follow-up Backlog · 메모리 · main 머지(조각 E 경계 2) |

**조각의 「초록」 정의**: 조각 A = EditMode core+assets lane 초록. **조각 C 는 6a·6b·6b2 까지 EditMode 초록이고, 6c 가 그 효과를 화면에 올리므로 사용자 플레이가 붙는다**(초안의 「조각 C = EditMode 만」은 뷰 풀이 unit 6 안에 들어온 뒤로 더 이상 맞지 않는다). 조각 B·D = + 새 PlayMode lane 스모크 + 사용자 플레이 확인(질문은 그 조각의 「아직 안 보이는 것」 표를 뺀 범위로 — 5c). 조각 E = 옛 lane 은퇴 후 새 lane 만으로 초록 + Entities 0건.
**진행 규칙**: 조각 안의 unit 은 전부 구현한 뒤 한 번에 테스트한다. 각 unit 문서 하단에 **「이식 제외」 표**(일부러 안 옮긴 것 + 이유). 플레이 중 이상하면 그 표부터 본다. **장부 소진**: `bridge-methods.md` 미정 128 은 조각 E 에서 한꺼번에 재분류하지 않는다 — 5a·5b·5c·6a·6b·6b2·6c·7 각 완료 기준이 자기 몫(뷰·입력·HUD·결과·사운드 / 슬롯·해저드·효과 뷰 / 카드·기믹·보스·도약·재배치)을 「새 주인」 또는 「삭제」로 닫고, unit 종료 시 `check_ledgers.py` 의 잔량을 상태 라인에 숫자로 적는다. 조각 C 의 배정은 **59 → 51 → 46 → 46 → 44**(6a **8행** · 6b **5행** · 6b2 **0행** · 6c **2행**)이고, 조각 D 가 **44 → 36 → 28 → 28 → 0**(7a **8행** · 7b **8행** · 7c **0행** · 7d **28행**)으로 닫는다 — **미정 0 이 조각 E 진입 조건**이다. ⚠ 6b 의 효과 타일 3행과 6c 의 뷰 풀 행들은 **이미 배정된 행의 주인 정정·확정**이라 잔량을 줄이지 않는다 — 6b2 가 0행인 것도 누락이 아니라 그 영역의 브리지 행이 이미 `GimmickHost`·「디버그/로그」로 배정돼 있기 때문이다. `rules.md` 보류 51 은 전부 unit 번호 또는 「후속 후보」를 단다.

## Feature-wide 계약

1. **규칙은 옮기고 기계는 옮기지 않는다.** 컴포넌트·시스템·큐·ECB·Burst 우회는 가져오지 않는다.
2. **땜빵·우연은 의도만 옮긴다.** unit 0 분류표가 정본. ⚠ 없으면 조용히 망가지는 세부(처치 드레인→전멸 판정 순서 · 분열 자식 셀 양자화 · 어그로 배타성 등)는 「필수」다.
3. **검증 = 의도 규칙의 EditMode 테스트 + 사용자 플레이.** 옛 골든은 참고. 새 코어는 자기 골든(unit 1 의 러너)을 갖고, 옛 러너와 **병존**하는 동안만 A/B 비교가 가능하다.
4. **전투 코어는 엔진을 모른다.** `noEngineReferences`, 참조는 `Unity.Mathematics`·`Wassup.Skills`·`Wassup.UnitAi`(둘 다 이미 같은 형태 — 실현 가능 검증됨). salvage 시 `NativeArray` → 배열.
5. **결정론**: 고정 틱 1/60 · 단일 스레드 · `SimEntityId` 오름차순. 슬로모·정지 = 틱 발행률. 종료 후 틱 0. **같은 런타임 안의 계약이다** — 런타임 간 비트 동일은 약속하지 않는다(2026-09-23 실측: Unity Mono 는 float 식을 확장 정밀도로 평가해 .NET 9 헤드리스와 약 300틱부터 1 ulp, `kill_race_3min` 은 9,887틱에서 이벤트 순서까지 갈렸다. IL2CPP 는 또 다르다). 골든의 정본 런타임 = Unity EditMode, 헤드리스 lane 은 골든 제외.
6. **값의 정본은 판 밖.** 시트→SO→`MatchDefinitionBuilder`→`MatchDefinition`(plain, `configHash` 소유). 시트 파이프라인은 브리지·Entities 참조 0 — 무변.
7. **커맨드 ≠ 이벤트.** 커맨드 = 동기 + receipt. 이벤트 = 값 스냅샷, `SimEntityId` 키. **모든 소멸 경로는 소멸 이벤트를 낸다**(뷰 폴링의 후계).
8. **트리거→발동은 rev 3.** 정적 (트리거,페이로드)→(concrete,형) 표 유지 · 세대 BFS 는 직접 재진입만 · `AttackMod` 축 5종 · 매치 핵심 규칙은 바인딩 밖.
9. **제거 확정**: 캐스터 4기 + 캐스트 기계 · 유출 한도·몽마의 계약·적 마음 판정·뽑기 폴백 진입. 소환사 유지.
10. **콘텐츠 동결 — 장치로 강제한다.** pre-commit 훅 + CLAUDE.md 범위 규칙. 카메라·UI·아웃게임은 계속, 옛 전투 규칙 변경은 `[old-battle]` 태그 커밋(버그픽스)만.
11. **ECS 제약은 범위 꼬리표로 과도기를 지난다**(CLAUDE.md 재편 완료). unit 9 에서 옛 절 삭제.
12. **매니저를 두지 않는다.** `BattleBridge` 는 어디로도 흩어지지 않는다 — 귀속표대로 담당자가 갖거나 삭제. 검사 대상은 브리지만이 아니라 **브리지 밖 규칙 보유자 10** 까지. 판정·상태·저장은 담당자만. 순서 의존은 이벤트 구독 순서. 뷰도 통합 뷰 없이 풀마다 구독.
13. **매치 모드는 닫힌 집합.** `IMatchGoal` + concrete 3 을 v1 에(제약 8 개정). `WaveClear`·`TimeAttack` 의 마음 붕괴 = **패배**(통로는 `stress_full` 그대로). 한 토너먼트 = 한 모드. 슬롯 append-only. **v1 서버 무변**(제출은 `KillScoreTimed` 만).

## 파이프라인 커버리지

모든 플레이 오브젝트의 생성→렌더 경로가 바뀐다. `object-pipeline-map.md` 는 unit 8c 에서 코어 기준으로 전면 재작성. 그 전 unit 은 「이 unit 이 여는 정거장」만 적는다. **예외: 5a 는 유닛·투사체 아키타입 표를 자기 문서에 둔다** — 뷰 풀을 신설하는 unit 이라 CLAUDE.md 규칙상 필수.

## 후속 후보 (범위 밖)

- **서버 API 확장**(modeId·sortDirection·leaderboardId) — 서버는 이 저장소 밖. v1 은 단일 제출 모드.
- 마음 N개 공유 체력(`heart-stress-axis/12`) — 새 코어 unit 4 위에서(`HeartMeter` 가 체력을 들어 이사 비용 0).
- 규칙 분류표 「보류」 재결정 — 사용자 플레이 뒤.
- 스폰 측면 오프셋의 순번 파생(X25 — 지금은 가변 상태) · 슬로모 느낌 재확인(X26, 틱 발행률로 바뀐 뒤 플레이로) · 희귀도 축(E21 — 소비처 0, 아웃게임 UI 몫).
- 결정론 등급 상향(리플레이·스냅샷) · 틱 30Hz 실측 · 트리거 연쇄 깊이 근거 · 마메모 웨이브 훅 위치 확인.
- `docs/spec` 1,157건의 옛 포인터는 역사서라 두고, `docs/reference` 만 unit 9 에서 고친다(2026-09-25 재측정 18편 — `9_ecs_retirement.md` 문서 목록).
- **효과 census 에서 조각 C 에 자리를 못 준 것**(전부 「소비처가 없어 지금 정하면 근거 없는 결정이 된다」): ⑴ `regenPerSec` 의 음수 강제 고정 처리 — 생산자 0 ⑵ 결합식 바닥/천장 4개(`[0.2,5]`·`[0.15,3]`·`0.05`)의 **SO 저작화** — 지금은 근거 주석 동반 상수 ⑶ 감속장 스냅샷(F35, 「안에 있는 대상이 영향을 받는다」의 마지막 예외) — 생산자가 unit 7 ⑷ 실드 부여의 한 틱 지연을 즉시로 통일할지(unit 3 이 현행 비대칭으로 이미 결정 — 뒤집으려면 별도 근거 + 골든 재굽기).
- **9b 규칙 증언 이식 잔여 51** — `ledgers/retire-test-pairs.md` 「부분 공백」 표(51 파일 · 76 행 · 옛 테스트 · 규칙 한 줄 · 코어 규칙 위치 `파일:줄`)가 입력. 규칙 문장을 코어 하네스로 다시 쓴다. 그 표가 찾은 **규칙 누락 1**(도발 재부여 시 긴 잔여 유지 — 코어 `AiMovePhase.cs:235` 가 덮어쓴다) · **규칙 다름 의심 1**(적 광역이 길막 방벽을 친다 — `TargetDefaults.cs:19`)은 사용자 결정 대기.
- **unit 9 가 남긴 것**: ⑴ (9b 로 분리) ⑵ 옛 규칙과 다른 4건의 결정(방향탄 관통 소진 튕김 · 감지 직업 필터 · 예고선 병합 · 예보 경로 해석 — 테스트는 `[Ignore]` 로 대기) ⑶ 코어 `IntentApplier.SpawnField` 의 포탈 입구 반경이 표기 전용 `SkillMath.CellShapePaddingTiles` 를 판정에 쓴다(값은 옛 `tileSize*0.5` 와 같다 — 제약 13 모양만 어긋남) ⑷ 아웃게임 PlayMode 의 `[Explicit]` 라이브 서버 테스트 2 는 assembly 실행에도 돌아 실서버에 가입을 시도한다(`AuthE2ETest`).
- **`unit-stats-and-modifiers` spec 2편의 은퇴 표기** — 그 문서가 요구하는 고정소수점 scale 1000 + 「가산 후 1회 승산」은 **채택하지 않는다**(모든 저작 수치가 재조정 대상이 된다). 새 코어는 현행 float `(1+Σadd)×Πmul` 이고, 그 결정으로 두 문서가 죽는다 — 문면 정리는 unit 9 의 문서 목록에 붙인다.
