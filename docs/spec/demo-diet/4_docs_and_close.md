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

## 완료 기준

- [ ] 위 문서 전부 갱신 · 삭제 승인 항목은 사용자 확인 기록
- [ ] 검증 4종 초록 · 수치표
- [ ] 커밋(경로 지정) · README 상태 「완료 YYYY-MM-DD」
