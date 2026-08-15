using System.Collections;
using UnityEngine;
using Wassup.Core;

namespace Wassup.UI.Tutorial
{
    // 효과 타일("빛나는 타일") 배치 안내 (spec unit 26). 두 번째 판 배치 구간에서 보드 위
    // 효과 타일 하나를 월드 마커로 지목하고 한 줄을 얹는다 — 아이콘이 "무엇" 은 말하지만
    // "놓으면 이득" 이라는 사실 자체를 배울 자리가 없었다.
    //
    // 첫 판은 core 안내가 배치 구간을 다 쓰므로 제외한다. 리빌 안내(units 23~24) 뒤라는 순서는
    // 페이즈 흐름(Gimmick → Placement)이 이미 보장하므로 플래그로 체인하지 않는다.
    //
    // 본체에서 분리한 이유는 .GimmickReveal.cs 와 같다 — 공유 파일에는 lifecycle 호출만 남긴다.
    public sealed partial class FirstSessionTutorialController
    {
        // unit 26 — 사용자 확정본. 임의로 고치지 말 것.
        // 지목한 타일의 구체 효과가 아니라 **세 종류를 함께** 소개한다: 마커는 예시일 뿐이고
        // 나머지 둘도 같은 성격이라는 것이 이 안내의 요점이다. `재생` 이 아니라 `체력회복` 이다
        // (에셋 displayName 은 `재생 (+1 HP/s)` 이지만 읽는 코드가 없어 화면에 안 나온다 —
        //  effect-tile-icons 의 툴팁/범례 후속이 생기면 그때 한쪽으로 통일한다).
        //
        // 둘째 줄은 **아이콘 모양 ↔ 효과** 페어다. 타일 위 글리프가 곧 그 효과라는 매핑을
        // 문장이 직접 가르치므로, 플레이어가 색을 학습하지 않아도 처음 보는 타일을 읽을 수 있다.
        // 매핑 출처는 `docs/spec/effect-tile-icons/` 계약 1 — 칼(공격력 +25%) · 번개(공속 +20%) ·
        // 하트(재생 +1 HP/s). **아이콘을 재저작하면 이 문구도 함께 고쳐야 한다.**
        // (실제 아이콘 이미지를 문장에 넣으려면 TMP Sprite Asset 이 필요한데 프로젝트에 없다 —
        //  후속 후보. 지금은 모양 이름으로 페어링한다.)
        private const string EffectTileHintText =
            "빛나는 타일 위에 배치하면 유닛이 강해집니다!\n" +
            "칼=공격력 · 번개=공속 · 하트=체력회복";
        // **마커에 라벨을 달지 않는다**(2026-08-02 화면 확인). `ShowWorldMarker` 의 라벨은
        // preferLabelAbove 로 링 위에 붙는데, 보드 상단 타일이 뽑히면 그 플레이트가 상단
        // 말풍선의 둘째 줄을 파고들어 글자를 가린다. 게다가 말풍선이 이미 "빛나는 타일" 이라고
        // 부르므로 라벨은 같은 말을 두 번 하는 것이다 — 지울 때 잃는 정보가 없다.
        // core 안내의 마커(`적 등장`·`방어 목표`)가 라벨을 다는 건 옆에 설명 문장이 없어서다.

        private Coroutine _effectTileRoutine;
        // 탭 소비자가 셋이 된다(클래스 안내 · 스트레스 정지 · 이 스텝). 본체의 OnContinueTapped
        // 가 우선순위를 명시하므로 여기서는 "내가 대기 중인가" 만 든다. 셋은 실제로는 배타적이다
        // (클래스=1판 배치 · 스트레스=1판 전투 · 이 스텝=2판 이후 배치).
        private bool _effectTileWaitingTap;
        private bool _effectTileTapped;
        // **"내가 guidance 에 무언가를 세웠다"** 는 뜻이다 — 형제 파일(.GimmickReveal/.BattleHud)의
        // `_xActive` 와 같은 의미여야 정리 창구가 `Hide()` 를 부를 자격의 근거가 된다.
        // 그래서 코루틴 시작이 아니라 **실제 표시 직전**에 세운다. 시작 중복 방지는
        // `_effectTileRoutine` 이 맡는다(둘의 역할을 겹치면 표시 전 구간에서 남의 UI 를 걷는다).
        private bool _effectTileHintActive;

        // ── lifecycle (본체가 부른다) ────────────────────────────────────────

        // 호출처는 OnPlacementReady **초입** — core 의 fail-open return 들보다 앞이다.
        // 그 뒤에 두면 두 번째 판은 `!ShouldRunCore` 에서 이미 return 해 여기 도달하지 못한다.
        private void StartEffectTileHint()
        {
            // 게이트 둘: 첫 판이 아니고(= core 안내가 배치 구간을 쓰지 않고), 아직 안 본 계정.
            // **`IsCorePending` 으로 첫 판을 가르지 말 것** — units 19~20 과 같은 함정이다.
            if (_awakeningLockedThisMatch || _effectTileRoutine != null) return;
            if (guidance == null || mapView == null)
            {
                // 형제 안내들과 같은 진단 일관성 — 조용한 영구 미발화를 만들지 않는다.
                Debug.LogWarning("[FirstSessionTutorial] guidance/mapView 미배선 — 타일 안내를 생략합니다.", this);
                return;
            }
            if (!TutorialProgress.ShouldRunEffectTileHint(profileSO)) return;

            _effectTileRoutine = StartCoroutine(EffectTileHintRoutine());
        }

        // 정리 단일 창구. 호출처 3곳 — 체인 정상 종료 · OnPhaseChanged · OnDisable.
        //
        // 코루틴 중단과 UI 원복의 조건이 **다르다**: 표시 전(진입 양보 구간)에 이탈하면 걷을
        // UI 는 없지만 코루틴은 죽여야 한다. 하나로 묶으면 그 구간의 코루틴이 살아남는다.
        private void StopEffectTileHint()
        {
            if (_effectTileRoutine != null)
            {
                StopCoroutine(_effectTileRoutine);
                _effectTileRoutine = null;
            }
            if (!_effectTileHintActive) return;
            _effectTileHintActive = false;
            _effectTileWaitingTap = false;
            _effectTileTapped = false;
            // 캐처와 카운트다운 홀드는 **여기 하나**가 해제한다. 잔류하면 배치 입력이 막힌 채
            // "배치 연습" 이 무기한 떠서 그 판을 플레이할 수 없다(클래스 안내와 같은 위험).
            guidance?.SetTapToContinue(false);
            placementView?.EndTutorialGate(restoreNormalPlacement: true);
            guidance?.ClearWorldMarkers();
            guidance?.Hide();
        }

        // 본체의 OnContinueTapped 가 부른다. 대기 중이 아니면 이 탭은 내 것이 아니다.
        private bool TryConsumeEffectTileHintTap()
        {
            if (!_effectTileWaitingTap) return false;
            _effectTileTapped = true;
            return true;
        }

        // ── 체인 ─────────────────────────────────────────────────────────────

        private IEnumerator EffectTileHintRoutine()
        {
            // 호출 스택(OnPlacementReady)에서 한 프레임 빠져나온다. 같은 이벤트의 다른 구독자가
            // 아직 자기 UI 를 세우는 중일 수 있어서다.
            // **"카메라가 자리 잡을 때까지 기다린다" 가 아니다** — 월드 마커는 일회성 투영이
            // 아니라 TutorialGuidanceView.Update 가 매 프레임 WorldToScreenPoint 를 다시 돌린다.
            // 카메라가 pitch 를 트윈해도 마커는 스스로 따라가므로 양보로 막을 것이 없다.
            yield return null;

            var camera = mainCamera != null ? mainCamera : Camera.main;
            if (camera == null)
            {
                // 배선 사고다 — 아래 "타일 0개" 와 성격이 다르므로 로그 등급을 나눈다.
                Debug.LogWarning("[FirstSessionTutorial] 카메라 없음 — 타일 안내를 생략합니다.", this);
                _effectTileRoutine = null; // 자기 자신을 StopCoroutine 하지 않게 먼저 놓는다
                StopEffectTileHint();
                yield break;
            }

            // **효과 타일이 0개인 맵이 있다** — desert 테마는 `effectTiles: []` 다. 그 판에서는
            // 안내를 생략하고 **완료를 저장하지 않는다** → 효과 타일이 있는 다음 판에 정상 노출.
            // 배선 사고가 아니라 정상 플레이 경로이므로 경고가 아니라 로그다(unit 20 선례).
            if (!TryPickFarthestFromMessage(camera, out Vector3 world))
            {
                Debug.Log("[FirstSessionTutorial] 효과 타일 없음 — 타일 안내를 이번 판은 생략합니다.", this);
                _effectTileRoutine = null;
                StopEffectTileHint();
                yield break;
            }

            // 타일 하나만 지목한다. 3개를 다 찍으면 화면이 번잡해지고, "여러 종류가 있다" 는
            // 문구가 나른다.
            _effectTileHintActive = true;

            // **카운트다운을 잡는다.** 아래 탭 캐처가 배치 입력을 막는 동안 30초가 계속 흐르면
            // 안내가 플레이어의 배치 시간을 먹는다. core 안내가 쓰는 그 게이트 그대로다
            // (신규 seam 없음 — 카운트다운 소유권은 PlacementPhaseView 에 남는다).
            placementView?.BeginTutorialGate();

            // 전용 앵커로 **카운트다운 바로 아래까지 끌어올린다**. 기본(184)·WorldMarker(320)는
            // 둘 다 보드 위라 링과 겹쳤다(2026-08-02 화면 확인 2회). 위 타일 선택과 짝이다 —
            // 둘 중 하나만으로는 맵에 따라 또 겹친다.
            guidance.SetMessageAnchor(TutorialGuidanceView.MessageAnchor.EffectTile);
            guidance.ShowWorldMarker(camera, world, label: null, guidance.EffectTileMarkerColor);
            guidance.ShowMessage(EffectTileHintText, showSkip: false);
            // 캐처는 FullBleedRoot, 마커·말풍선은 SafeAreaRoot 다. 같은 캔버스에서 나중 sibling
            // 이 이기므로 dim 위에 마커가 밝게 남는다 — 지목이 흐려지지 않는다.
            guidance.SetTapToContinue(true);

            // 탭 대기. 폴백은 **클래스 안내와 같은 값을 공유한다**(`classHintFallbackSeconds`) —
            // 같은 페이즈에서 같은 캐처가 만드는 같은 위험(탭 유실 → 배치 입력이 막힌 채 판이
            // 진행 불가)이라 별도 knob 을 두지 않는다.
            //
            // 대기 동안 **마커가 한 번이라도 실제로 보였는지** 지켜본다. 월드 마커는 매 프레임
            // SafeAreaRoot 안쪽인지로 표시가 갈리므로(UpdateWorldPulse), 화면 가장자리 셀이
            // 뽑히고 인셋이 큰 기기에서는 꺼진 채로 지날 수 있다. 그때 저장하면 플레이어는
            // "빛나는 타일" 이 뭔지 못 본 채 계정당 1회를 잃는다.
            _effectTileTapped = false;
            _effectTileWaitingTap = true;
            bool markerSeen = false;
            float elapsed = 0f;
            float limit = Mathf.Max(0.1f, guidance.ClassHintFallbackSeconds);
            while (!_effectTileTapped && elapsed < limit && _effectTileHintActive)
            {
                markerSeen |= guidance.HasVisibleWorldMarker;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _effectTileWaitingTap = false;
            if (!_effectTileHintActive) yield break; // 정리 경로가 이미 캐처·게이트를 걷었다

            // 완료 저장은 **실제로 보여준** 경로에만 둔다. 정리 창구가 저장까지 하면 조용히
            // 끝난 판에서 안내가 소진돼 플레이어가 문구를 영영 못 본다(unit 24 계약).
            if (markerSeen) CompleteEffectTileProgress();
            else Debug.Log("[FirstSessionTutorial] 타일 마커가 화면 밖 — 저장하지 않고 다음 판으로 미룹니다.", this);

            _effectTileRoutine = null;
            StopEffectTileHint();
        }

        // 말풍선은 화면 **상단**에 고정이고 링은 보드 어디든 뽑힌다. 그래서 상단 타일이 뽑히면
        // 오프셋을 아무리 올려도 겹친다 — 애초에 **가장 아래 타일**을 고른다.
        // (screen y 가 클수록 화면 위쪽이므로 최소값을 찾는다. 화면 밖으로 투영되는 타일은
        //  후보에서 뺀다 — 어차피 마커가 보이지 않는다.)
        private bool TryPickFarthestFromMessage(Camera camera, out Vector3 world)
        {
            world = default;
            float bestScreenY = float.MaxValue;
            bool found = false;
            for (int i = 0; i < mapView.EffectTileCount; i++)
            {
                if (!mapView.TryGetEffectTileAnchor(i, out Vector3 candidate)) continue;
                Vector3 screen = camera.WorldToScreenPoint(candidate);
                if (screen.z <= 0f) continue; // 카메라 뒤
                if (!found || screen.y < bestScreenY)
                {
                    bestScreenY = screen.y;
                    world = candidate;
                    found = true;
                }
            }
            return found;
        }

        private void CompleteEffectTileProgress()
        {
            if (profileSO == null || !profileSO.IsLoadedThisSession || profileSO.profile == null) return;
            if (!TutorialProgress.CompleteEffectTileHint(profileSO.profile)) return;
            TrySaveProfile();
        }
    }
}
