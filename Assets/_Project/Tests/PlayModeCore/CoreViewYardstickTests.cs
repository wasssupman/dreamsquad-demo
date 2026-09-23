using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;
using Wassup.Presentation;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5b — **뷰가 자를 새로 만들지 않았는가.**
    //
    // 이 파일이 소스를 읽는 이유: 여기서 막으려는 결함은 **값이 아니라 형태**다. 뷰가 최근접
    // 거점을 스스로 고르거나 거리를 스스로 재면 결과는 대개 맞고 **동률·경계에서만** 갈린다.
    // 런타임 단언으로 그걸 잡으려면 갈리는 그 판을 재현해야 하는데, 그 재현이 가장 잡기 싫은
    // 형태다(「가이드 ≠ 실제 이동선」이 옛 전투에서 두 번 그렇게 돌아왔다).
    //
    // 그래서 **호출부가 무엇을 부르는지**를 직접 못박는다. grep 을 테스트로 옮긴 것이다.
    public sealed class CoreViewYardstickTests
    {
#if UNITY_EDITOR
        private const string ViewDir = "Assets/_Project/Scripts/BattleCoreUnity/View/";

        // ⚠ **주석을 뺀 소스**를 본다. 이 검사가 막는 것은 «호출» 이지 «설명» 이 아니다 —
        // 안 빼면 「왜 이걸 부르면 안 되는지」를 적어 둔 헤더가 자기 검사에 걸린다(실제로 걸렸다).
        private static string Read(string path)
        {
            Assert.IsTrue(System.IO.File.Exists(path), path + " 가 없다");
            string src = System.IO.File.ReadAllText(path);
            src = System.Text.RegularExpressions.Regex.Replace(
                src, "/\\*.*?\\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(src, "//[^\n]*", "");
        }

        [Test]
        public void 예고선은_거점을_스스로_고르지_않는다()
        {
            string src = Read(ViewDir + "CoreSpawnAlertPresenter.cs");

            // 고르는 자는 하나다 — 이동 단계가 매 틱 쓰는 그 배열(M18).
            StringAssert.Contains("AiMove.TryPickStructure", src,
                "예고선이 이동과 같은 후보 배열로 묻지 않는다");

            // 자기 자를 만들면 방패·생존 배제가 한쪽에만 들어가 예고가 거짓이 된다.
            StringAssert.DoesNotContain("NearestIndex", src,
                "뷰가 최근접 거점을 직접 계산하고 있다 — 자가 둘이 됐다");
            StringAssert.DoesNotContain("StructureChoice", src,
                "거점 선택은 담당자(AiMovePhase)를 통해서만 부른다");
        }

        [Test]
        public void 예고선의_경로는_이동과_같은_함수에서_나온다()
        {
            string src = Read(ViewDir + "CoreSpawnAlertPresenter.cs");
            StringAssert.Contains("SpawnPathPreview.Build", src);
            // 평활화·흐름장을 뷰에서 직접 밟으면 「라인만 필드 계단」이 된다.
            StringAssert.DoesNotContain("PathSmoothing", src);
            StringAssert.DoesNotContain("FlowSlot", src);
        }

        [Test]
        public void 오버레이의_도달_판정은_정본_진입점_하나다()
        {
            string src = Read(ViewDir + "CoreMapOverlay.cs");

            // 제약 13 — 새 판정은 정본 진입점을 **호출만** 한다.
            StringAssert.Contains("AttackReach.InReach", src);
            StringAssert.DoesNotContain("SkillMath", src,
                "술어 본체를 뷰가 직접 부르고 있다 — 진입점을 건너뛰면 몸 항이 샌다");
            StringAssert.DoesNotContain("InCellRange", src,
                "격자 계층의 자는 사거리 판정에 쓰지 않는다");
        }

        [Test]
        public void 배치_판정은_입력에_복제되지_않았다()
        {
            string src = Read("Assets/_Project/Scripts/BattleCoreUnity/Input/DragPlacementInput.cs");

            // 판정의 진입점 둘만 부른다.
            StringAssert.Contains("placement.Judge", src);
            StringAssert.Contains("Command.PlaceDefender", src);

            // 옛 컨트롤러가 들고 있던 「미리 거르기」가 돌아왔는지 본다. 자원·상한을
            // 입력에서 물으면 그 물음이 두 번째 자다.
            StringAssert.DoesNotContain("CanAfford", src);
            StringAssert.DoesNotContain("OnBoard", src);

            // 사용자 결정 2026-09-23 — **보정(자석)은 은퇴했다.** 손끝이 가리킨 칸이 곧
            // 결과다. 되살아나면 「화면이 여기라고 말한 적 없는 칸에 유닛이 서는」 증상이
            // 그대로 돌아온다. 값이 아니라 **형태**라 런타임 단언만으로는 잘 안 잡힌다.
            StringAssert.DoesNotContain("TrySnapAnchor", src);
        }
#endif

        [UnityTest]
        public IEnumerator 씬_배선이_비어_있지_않다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);

            // 배선 누락은 **조용히** 아무 일도 안 일어나는 결함이다(5a 의 거점 인자와 같은 종류).
            // 「그 컴포넌트가 씬에 있나」까지만 묻는다 — 슬롯 값은 각 컴포넌트가 비면
            // 스스로 경고하거나 아무것도 안 그리므로 그쪽이 다음 사람에게 더 잘 말한다.
            Assert.IsNotNull(Object.FindAnyObjectByType<CoreDefenderTray>(), "트레이");
            Assert.IsNotNull(Object.FindAnyObjectByType<CoreScoreHud>(), "점수 HUD");
            Assert.IsNotNull(Object.FindAnyObjectByType<CoreCostDisplay>(), "코스트");
            Assert.IsNotNull(Object.FindAnyObjectByType<CorePlacementPhaseView>(), "배치 창");
            Assert.IsNotNull(Object.FindAnyObjectByType<CoreMenuPopup>(), "메뉴");
            Assert.IsNotNull(Object.FindAnyObjectByType<DragPlacementInput>(), "드래그 배치");
            Assert.IsNotNull(Object.FindAnyObjectByType<RetireInput>(), "퇴근");
            Assert.IsNotNull(Object.FindAnyObjectByType<SubmitInput>(), "제출");
            Assert.IsNotNull(Object.FindAnyObjectByType<CoreMapOverlay>(), "맵 오버레이");
            Assert.IsNotNull(Object.FindAnyObjectByType<CoreSpawnAlertPresenter>(), "예고선");
            Assert.IsNotNull(Object.FindAnyObjectByType<CameraDirector>(), "카메라 디렉터");
            Assert.IsNotNull(Object.FindAnyObjectByType<CoreCameraFeed>(), "카메라 피드");
            // 버튼이 눌리려면 EventSystem 이 있어야 한다 — 없으면 「시작」·「제출」이
            // 조용히 안 눌린다(누락의 전형).
            Assert.IsNotNull(Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(),
                "EventSystem");
        }

        [UnityTest]
        public IEnumerator 스테이지가_화면에_다_들어온다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);

            var cam = Camera.main;
            Assert.IsNotNull(cam);
            var grid = driver.BoardGrid;
            Assert.IsNotNull(grid, "보드 격자가 없다 — sim→view 가 성립하지 않는다");

            for (int i = 0; i < 10; i++) yield return null;

            // 5a 의 Play 에서 스테이지가 잘리고 회색 띠가 보였다. 원인은 카메라 값이 아니라
            // **보드 bounds 를 아무도 안 밀었다**는 것이었다 — 디렉터는 bounds 가 없으면
            // 포즈를 아예 안 쓴다(레시피가 있어도). 그래서 여기서 묻는 것은 포즈가 아니라
            // **판이 화면에 들어왔나**다.
            var size = driver.GridSize;
            var corners = new[]
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(size.x, 0, 0),
                new Vector3Int(0, size.y, 0),
                new Vector3Int(size.x, size.y, 0),
            };
            for (int i = 0; i < corners.Length; i++)
            {
                var vp = cam.WorldToViewportPoint(grid.CellToWorld(corners[i]));
                Assert.Greater(vp.z, 0f, "판 모서리가 카메라 뒤에 있다");
                // 여유 0.1 — 프레이밍이 가장자리를 약간 무는 것은 저작의 자유다.
                // 잘린 판은 이 폭으로 안 들어온다(5a 의 회색 띠는 판 절반이 밖이었다).
                Assert.That(vp.x, Is.InRange(-0.1f, 1.1f), "모서리 " + i + " 가 좌우로 나갔다");
                Assert.That(vp.y, Is.InRange(-0.1f, 1.1f), "모서리 " + i + " 가 상하로 나갔다");
            }
        }
    }
}
