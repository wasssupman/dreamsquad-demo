# 4 — 문서 현행화 · 전체 검증 · 종료

## 목적

정리본의 문서가 코드와 맞게 한다. 아웃게임을 전제한 문장을 지우거나 「somnia 로 이관」으로 바꾼다.

## 변경 대상

- `CLAUDE.md`: 첫 단락(「아웃게임·인게임 UI·에셋은 데모용」 → 아웃게임은 이 리포에 없음) · 「스택」(전투 밖 MonoBehaviour 문장) · 「데이터」(로그인/로비 진입 임포트가 SO 를 메모리에서 덮는다 → 제거됨; 시트는 에디터 임포터만) · 「검증」 lane 표(`Wassup.Tests.PlayMode` 아웃게임 lane 삭제, e2e 스모크 문장 삭제) · 「Unity 함정」 중 프로필 백업 문장.
- `docs/reference/ingame-flow.md`: 로비·참가·결과 절은 **「somnia 아웃게임 소관」** 한 줄 포인터로 축약, 전투 절은 유지.
- `docs/reference/test-procedure.md`: lane 표 · 아웃게임 PlayMode 경고 삭제.
- `docs/reference/` 삭제: `keyring-portability.md` · `dreamcatcher-portability.md`(아웃게임 절; 전투 절이 있으면 그 부분만 남김) · `arknights-defense-mechanics.md` · `review-skill-comparison.md` · `드림캐쳐_각성안_최종스펙_v1.md`(사용자 확인 후).
- `docs/blueprint/README.md`: 「지금 서버와 닿는 곳」 절을 「이 리포에선 제거, somnia 가 담당」으로.
- `docs/spec/README.md`: 시작점에 `demo-diet/` 추가 · 백로그 중 아웃게임 항목(실서버 계정 정리 · 랭킹 확인 · `[Explicit]` 격리) 을 「somnia 이관」으로 표기.
- `tools/key_sheet_alpha.py` · `scripts/mobile/` 삭제(somnia CI 보유). `tools/analyze_sessions.py` 는 휴면 — 삭제.
- `.claude/skills/catchup/SKILL.md` 의 아웃게임 언급 정리.
- 이 spec README 상태 줄 → 완료.

## 검증 (spec 종료 조건)

- 검증 워크트리(4.7) 배치: 컴파일 0 · EditMode 전체 초록(선행 3 + CRLF 9 외 0) · `PlayMode.Core` 97 · 골든 11 무변 · 헤드리스 Core/Tests/Check 통과.
- 수치 기록: .cs 수 전/후 · `Assets` 용량 전/후 · 리포 추적 파일 수 전/후.
- `rg -i 'profile|tournament|login|lobby|keyring|UserSession' Assets/_Project/Scripts` → 전투 문맥의 주석 외 0.

## 구현 결과 (2026-10-07)

- 문서: `CLAUDE.md`(첫 단락 · 스택의 입구/출구 문장 · 데이터의 시트 임포트 · e2e 줄 삭제 · lane 표 · 아웃게임 PlayMode 줄 삭제) · `ingame-flow.md`(머리 · §2 도표를 입구 값/출구 사건으로 · 시트 문장) · `test-procedure.md`(`Wassup.Tests.PlayMode` 행 삭제 · 아웃게임 어휘 · MCP `run_tests` 인자 → 배치 CLI 인자) · `blueprint/README.md`(데모·서버 절 · 한 판의 생애 1·2·5·6 · 시스템 지도의 로그인/프로필/토너먼트 행 → 「바깥과의 경계」 한 행 · 값의 길 · 열린 것) · `docs/spec/README.md`(완료 목록 · 진행 중 · 백로그의 아웃게임 3건 somnia 이관) · `design-blueprint/README.md`(reference 문서 판정 기록) · `tools/README.md`.
- 삭제: `docs/reference/keyring-portability.md`(로비 키링 지식) · `review-skill-comparison.md`(옛 리뷰 도구 비교) · `tools/analyze_sessions.py`(휴면 — 로거 없음) · `scripts/mobile/`(somnia CI).
- **spec 과 달리 남긴 것**: `arknights-defense-mechanics.md`(적 이동 설계 대조군 — 전투 참고) · `dreamcatcher-portability.md`(드림캐쳐 메커닉 = 전투; 아웃게임 절 없음) · `tools/key_sheet_alpha.py`(유닛 스프라이트 시트 알파 도구 — 전투 아트 파이프라인, `lessons/03`) · `드림캐쳐_각성안_최종스펙_v1.md`(사용자 확인 대기). `.claude/skills/catchup/SKILL.md` 엔 아웃게임 언급이 없었다.
- 코드의 `ModeSelection.Lobby/FromLobby` 이름은 그대로 — somnia 이식(`Somnia.Battle.*` 개명) 때 함께.

**검증** — 사용자 결정(2026-10-07 「배치 검증 필요없음」)으로 배치 lane 은 돌리지 않았다. 단위마다 사용자 6.6 에디터의 재임포트·컴파일 에러 0 을 로그로 확인했고, 단위 3 뒤 사용자가 `BattleCoreScene` 을 Play 했다(선행 상태 `Bone not found: Gear` 외 전투 에러 없음). 헤드리스 코어 테스트는 이 머신의 Smart App Control 에 막혀 단위 0+1 때(4.7 워크트리)가 마지막이다.

**수치 (다이어트 전 `6a8dc40b2` → 후)**

| 항목 | 전 | 후 |
|---|---|---|
| 추적 파일 | 15,188 | 7,108 |
| `.cs` | 1,213 | 1,066 (`_Project` 818) |
| `Assets` 용량(du) | 803 MB | 511 MB |
| 벤더 7 | 331 MB | 156 MB |

`rg -i 'profile|tournament|login|lobby|UserSession' Assets/_Project/Scripts` → 47줄, 전부 전투 문맥(`HostProfile` · `AggroChaseMath` 의 profile 사거리 · `ModeSelection.Lobby`)과 주석.

## 완료 기준

- [x] 위 문서 전부 갱신 · 남긴 항목과 이유 기록 · `드림캐쳐_각성안` 은 사용자 확인 대기
- [x] 검증: 사용자 에디터 컴파일 0 · Play(사용자) — 배치는 사용자 결정으로 생략 · 수치표
- [x] 커밋(경로 지정) · README 상태 「완료 2026-10-07」
