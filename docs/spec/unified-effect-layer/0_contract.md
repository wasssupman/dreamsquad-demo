# 0 — 계약 싣기 (문서)

## 목적
README 계약 1~8 을 구조 문서의 정본 자리에 싣고, 하드 케이스 탐침 테스트를 이 spec 의 완료 기준으로 등록한다. 코드 변경 0.

## 변경 대상
- `docs/reference/battle-core-architecture.md` — §1 설계 아웃라인의 트리거/효과 절(발사 요청·발사 명세 줄 근처)에 「효과 = f(원점) · 트리거 = 원점 산출기 · 출처 = 꼬리표」 3줄과 현 상태(「unit 1~4 로 옮겨 가는 중」) 표시. §8 불변식에 「요청 조립은 궤적 결합 종류(대상/칸/방향)로만 갈린다」를 **예정** 불변식으로 추가(unit 1 완료 시 확정 표기).
- `Tests/EditModeCore/HardCaseUnifiedSkillProbeTests.cs` · `HardCaseMeteorProbeTests.cs` — 머리 주석에 「`unified-effect-layer` 완료 기준」 한 줄. `[Ignore]` 사유 끝에 해제 담당 unit 번호.
- `docs/spec/README.md` 목록에 이 spec 행 · Follow-up Backlog 「통합 효과 층」 항목을 이 spec 으로 연결.

## 구현
- 문서는 계약과 길찾기만(diff 서술 금지). 파일:줄은 가이드 12 에 있으니 여기선 심볼 이름으로 가리킨다.
- `[Ignore]` 매핑: 케이스 1 두 건 → unit 2 · 케이스 2 반경 → unit 1 · 착탄 예고 → unit 1 · 낙하 그림 → unit 3.

## 완료 기준
- 문서 3곳 갱신 · 테스트 파일은 주석만 변경(헤드리스 918 동일, Ignore 5 동일).
- 커밋 1개(`docs(unified-effect-layer): 0 …`).
