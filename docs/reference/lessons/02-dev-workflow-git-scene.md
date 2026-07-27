# 개발 워크플로우 · git · 씬 위생

테스트 배치, 격리 실행, git 커밋, 씬 저장/되돌리기, 시트↔SO 동기화에서 겪은 함정.

## EditMode 테스트 폴더 위치

EditMode 테스트 `.cs` 는 **`Assets/_Project/Tests/EditMode/`** 에 둔다(asmdef `Wassup.Tests.EditMode`). `Assets/_Project/Scripts/**/Tests/` 같은 곳에 만들면 asmdef 밖이라 `run_tests` 가 **0개 매칭**되거나(같은 클래스명이면) 중복 타입으로 컴파일이 깨진다. PlayMode 는 `Assets/_Project/Tests/PlayMode/`.

## 격리 테스트 리그 (에디터가 열려 있어도 배치 실행)

에디터가 메인 프로젝트를 열고 있으면(`Temp/UnityLockfile`) 배치 모드가 락 충돌로 불가. 해결 = 별도 worktree 리그:

1. `git worktree add --detach /Users/sy/dev/wassup-testrig <HEAD>`
2. `cp -Rc wassup/Library wassup-testrig/Library` — APFS clonefile(CoW)이라 대용량 Library 가 즉시·거의 0비용 복제
3. 검증할 uncommitted 파일만 리그에 cp → `Unity -batchmode -projectPath <rig> -runTests -testPlatform EditMode|PlayMode -testResults x.xml -logFile y.log`
4. 리그 갱신: untracked 파일 rm → `git checkout -f --detach <새HEAD>` → 재복사

- **신규 `.cs`**: 리그 Unity 가 생성한 `.meta` 를 메인으로 회수해 guid 고정 후 커밋.
- 리그가 stale 하면 HEAD 가 클린 체크아웃 컴파일 가능한지 먼저 의심(미커밋 의존 파일 탐지기 역할).

## git 인덱스 쓰기는 샌드박스 비활성 필요

이 환경에서 Bash 기본 샌드박스는 `.git/index` 쓰기를 격리/롤백한다 → `git add` 가 exit 0 을 반환하고도 아무것도 스테이징 안 됨, 이어지는 `git commit` 이 "no changes added" 로 조용히 실패.

- **처방**: index/커밋 쓰기 git(`git add`/`git commit`/`git rm`)은 Bash 호출에 `dangerouslyDisableSandbox: true`. 읽기 전용 git(`status`/`log`/`diff`)은 샌드박스로 무방.

## 병행 세션 커밋 위생 (같은 워크트리 2세션)

사용자/다른 세션이 같은 워크트리에서 병행 작업 중일 때(실제로 흔함):

- **`git commit --amend` 금지** — 인덱스에 병행 작업이 스테이징돼 있을 수 있어 통째로 삼킨다(과거 942078e 오염 사고).
- **명시 경로 스테이징만** 사용, 커밋 직전 `git diff --cached --stat` 로 파일 목록 검수.
- 같은 파일에 두 세션 변경이 섞이면 hunk 분리: 작은 diff 는 `git apply --cached`, 큰 재구성은 `git hash-object -w` + `git update-index --cacheinfo`(워크트리 무접촉).
- 히스토리 수정은 임시 worktree 에서 cherry-pick 재구성 → 트리 동등성 검증 → 본 워크트리는 `git reset --soft` 로 ref 만 이동(dirty 파일 무접촉).

## SaveScene 은 미저장 WIP 를 통째로 베이크한다

씬 컴포넌트(예: `BattleBridge`)의 serialized 필드를 바꾸고 `EditorSceneManager.SaveScene` 하면, **그 시점 에디터에 떠 있던 사용자 미저장 변경(Volume·카메라·GO 토글·신규 필드 기본값)이 전부 디스크에 박힌다**. `git diff` 가 내 1줄 + 대량 WIP 로 부풀어 오름.

- **처방**: 가능하면 저장 없이 in-memory 검증(→ `01-unity-mcp-operation.md`). 꼭 영속해야 하면 **내 delta 만 격리**: 씬 스냅샷(`cp`) → `git checkout HEAD -- Scene.unity` → 내 변경만 재적용 → `git add`+commit → 스냅샷 복원. 커밋 후 사용자에게 씬 WIP 잔존을 고지.

## dirty 씬 checkout 은 사용자 카메라를 날릴 수 있다

`git checkout HEAD -- BattleScene.unity` 로 Play 오염을 되돌릴 때, working tree 의 **사용자 미커밋 카메라/씬 설정까지 커밋값으로 되돌아가 날아간다**(실제 사고 2026-07-04).

- **처방**: dirty 씬을 checkout/revert 하기 전에 **무엇이 날아가는지 diff 로 확인**(특히 Main Camera transform/FOV·라이팅). Play 오염(runtime 이동)과 사용자 authored 설정을 구분. **되돌리기 전 백업 필수.**

## 시트↔SO 드리프트는 "간헐 버그"로 위장한다

스탯 시트(정본)를 고친 뒤 **에디터 임포터를 재실행하지 않으면 디스크 SO 는 옛 값**으로 남는다. 이때 `LoginAutoImport` → 런타임 리프레셔(`DcSheetRuntimeRefresher`/`UnitStatRuntimeRefresher`)가 로그인 직후 시트를 **비동기 in-memory 로만** 덮으므로, 같은 에디터 세션 안에서도 "임포트 내려앉기 전 = 디스크 옛 값 / 후 = 시트 새 값"으로 판정이 갈려 **간헐 재현 버그처럼 보인다**. 리컴파일·에디터 재시작마다 디스크 값으로 리셋돼 계속 재발한다.

- 실사례 (2026-07-19): `DeckRuleConfig` deckSize 시트=10·디스크=8 드리프트 → START 게이트가 "드림캐쳐 덱 10/8" 팝업을 간헐 노출. 에셋 mtime 이 10→8 커밋 시각(4일 전) 그대로인 것이 결정적 증거였다(3a6879fd 로 해소).
- **진단**: 에셋 파일 mtime 확인 → `curl {baseUrl}/{탭}` 으로 시트 실측(읽기 전용, importer 는 dry-run 없음) → 둘 대조.
- **처방**: 임포터 전체 실행은 무관 에셋 churn 을 만드니, 드리프트 난 필드만 UnityMCP `manage_scriptable_object`(modify) 로 반영 — 디스크와 로드된 인스턴스가 동시에 갱신된다. **시트 값을 바꾸면 에셋 동기화까지가 한 세트**라는 걸 잊지 않는 게 근본 예방.

## GitLab 미러(ALB 뒤)는 SSH 로만 대용량 전송이 된다

사내 GitLab(`gitlab.playlinks.co`)에 HTTPS 로 clone/fetch 하면 **응답 끝부분이 잘린다** — `curl 56 ... server closed abruptly (missing close_notify)` + `N bytes of body are still expected`. 초기 단일 푸시는 별도로 `HTTP 413`.

- **원인**: 인증서 발급자가 `O=Amazon`(ACM) = **AWS ALB 가 TLS 종단**. 대용량 응답에서 graceful close 없이 연결을 끊는다. 잘린 양이 매번 다르다(90/1,560/7,996/47,354 B) → 크기 한도가 아니라 **시간 기반 컷오프**. 같은 PC 에서 GitHub 수백MB 클론은 정상이고, GitLab housekeeping(팩 재정리+bitmap) 후에도 동일 → 클라이언트·회선·레포 상태 전부 무관. 작은 레포는 안 걸려서 "다른 프로젝트는 잘 되는데 이것만" 으로 보인다.
- **진단 1줄**: `echo | openssl s_client -connect <host>:443 -servername <host> | openssl x509 -noout -issuer` → 발급자가 Amazon 이면 앞단이 AWS LB.
- **처방**: **SSH remote 를 쓴다** (`git@gitlab.playlinks.co:<group>/<proj>.git`). HTTPS 정상화는 인프라 몫 — ALB **idle timeout**(기본 60s)·**desync mitigation mode**, 뒤단 nginx `client_max_body_size`.
- **미러 동기화 절차**(GitHub 이 유일한 저작 지점): `git pull` → `git push gitlab main:refs/heads/master`(+ 원하면 `main`). 보호 브랜치라도 **fast-forward 면 직접 푸시가 통과**한다.
- **깨뜨리지 말 규칙 2개**: ① GitLab 에서 커밋하지 않는다(웹 편집·GitLab 발 MR) ② 프로젝트 merge method = **Fast-forward**. 어기면 master 에만 있는 커밋이 생겨 다음 fast-forward 가 막히고, 보호 브랜치라 force 도 불가해진다.
- **운영 규칙의 정본은 `CLAUDE.md` "원격 저장소 · 푸시 전략"** 이다(여기는 함정·진단 기록). 2026-07-27 확정: GitHub 정본 · `GitHub main` ≡ `GitLab master` · **GitLab `main` 은퇴**(중복 계보로 분기를 만들어 정렬 후 폐기).
- **실제로 겪은 분기 사례**: 동료가 GitLab 에 직접 푸시(+ `main`/`master` 에 같은 작업을 따로 커밋)해 양방향 분기가 두 번 발생. 중복 여부는 **`git patch-id --stable` 대조**로 판정했다(SHA 는 달라도 도입 변경이 같으면 편입 불필요). 해소는 `git merge gitlab/master` → 양쪽 푸시.

## Git Bash 의 `ssh` 는 한글 사용자명 PC 에서 작동하지 않는다

키와 `known_hosts` 를 정상 생성해도 `Host key verification failed`. `ssh -v` 를 보면 `identity file /c/Users/\271\332\273\363\301\330/.ssh/id_ed25519 type -1` — **개인키도 known_hosts 도 "없음"** 으로 본다.

- **원인**: MSYS OpenSSH 가 HOME 의 한글(`박상준`)을 **CP949 바이트**로 해석하는데, 파일은 UTF-8 경로에 만들어져 서로 다른 디렉터리를 가리킨다.
- **처방**: `git config --global core.sshCommand "C:/Windows/System32/OpenSSH/ssh.exe"`. Windows 기본 OpenSSH 는 Win32 API 로 경로를 풀어 정상 동작한다. 수동 `ssh`/`ssh-keyscan` 도 이 바이너리를 직접 호출한다.

## 대용량 초기 푸시는 커밋 범위를 조각내 순차 푸시

빈 원격에 306MB 레포를 처음 푸시하면 **모든 객체가 단일 HTTP 요청**이 되어 한도(nginx `client_max_body_size`, GitLab 기본 250m)에 걸린다.

- **처방**: `git rev-list --reverse <ref>` 로 커밋 목록을 만들고 중간 지점들을 **순차 푸시**(`git push <url> <sha>:refs/heads/<branch>` 반복). 각 요청은 원격에 없는 객체만 보내므로 조각마다 한도 밑으로 떨어진다. 실측: 1,838 커밋을 17조각으로 나눠 전부 통과.
- **분할점 선정**: `git rev-list --disk-usage --objects <sha>` 로 누적 크기를 재서 균등하게 자른다.

## 보호 브랜치에 force 없이 무관 히스토리 편입 (`-s ours`)

새 GitLab 레포의 기본 브랜치(`master`, README 1커밋)를 우리 히스토리로 갱신하려면 non-fast-forward 인데, **기본 브랜치는 자동 보호돼 force push 가 거부**된다.

- **처방**: 그 README 커밋을 **부모로만** 편입한다 — `git merge -s ours --allow-unrelated-histories <sha>`. 트리는 불변(파일 변화 0)이고, 이후 master 갱신이 fast-forward 가 된다.
- **주의**: 이 머지 커밋을 **GitHub 에도 올려야** 한다. GitLab 에만 두면 두 원격 히스토리가 갈라져 이후 동기화마다 같은 편법을 반복한다.

## `Assets/Screenshots/` 는 비추적 스크래치 — 통삭제 금지

`Assets/Screenshots/` 는 dev 스크래치 폴더. git 은 폴더 `.meta` 만 추적하고 내부 PNG 는 **의도적 비추적**(MCP screenshot 결과물도 여기). `rm -rf` 같은 통삭제 금지 — **내가 만든 파일명만** 지운다(비추적은 git 복구 불가, `rm` 은 휴지통 안 거침).
