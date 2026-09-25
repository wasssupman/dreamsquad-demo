using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 8d — **첫 판 안내를 걷은 뒤 로비가 무엇을 하나**(사용자 결정 ④ 2026-09-25).
    //
    // ① 옛 세이브(안내 완료·배웅 플래그가 박힌 profile.json)가 그대로 열리고 다른 필드를 잃지 않는다. 필드를 지워도
    //    `JsonUtility` 가 모르는 키를 버리기 때문이고, 그 동작을 여기서 실제 로드 경로(`ProfileStore.LoadOrCreateAt`)로 증언한다.
    // ② 안내를 끝내지 않은 옛 계정도 **첫 판 뒤에는** 토너먼트 참가 신청이 나간다. 로비 게이트에 남은 조건은
    //    `IsLoadedThisSession && IsFirstMatch` 하나다(서버 `complete` 500 우회 — 안내와 무관한 옛 규칙이라 남겼다).
    public sealed class LobbyEntryAfterDecision4Tests
    {
        // 결정 ④ 전의 세이브 모양 — 안내 완료 false · 배웅 true · 판 3회 · 저장 편성 하나.
        private const string OldSaveJson =
            "{\"schemaVersion\":1,\"matchesPlayed\":3,\"firstRunTutorialDone\":false,\"firstRunLobbyOutroDone\":true,"
            + "\"squads\":[{\"id\":\"squad_old\",\"name\":\"옛 편성\",\"unitIds\":[\"unit_a\"],\"stoneIds\":[]}],"
            + "\"selectedSquadId\":\"squad_old\"}";

        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "wassup_8d_profile_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_path, OldSaveJson);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_path + ".bak")) File.Delete(_path + ".bak");
        }

        [Test]
        public void 옛_프로필_JSON은_안내_플래그가_있어도_그대로_열리고_다른_필드를_잃지_않는다()
        {
            var p = ProfileStore.LoadOrCreateAt(_path, null);
            LogAssert.NoUnexpectedReceived();   // 파싱 실패면 경고 + 백업 + 기본 프로필로 갈아 끼운다

            Assert.IsNotNull(p);
            Assert.AreEqual(3, p.matchesPlayed, "매치 이력이 살아남았다");
            Assert.AreEqual("squad_old", p.selectedSquadId, "확정 편성 포인터가 살아남았다");
            var squad = p.CommittedSquad();
            Assert.IsNotNull(squad);
            Assert.AreEqual("unit_a", squad.unitIds[0], "편성 내용이 살아남았다(기본 편성으로 갈아 끼워지지 않았다)");

            // 다시 저장하면 걷은 키가 사라진다(다음 저장에서 정리 — 마이그레이션 없음).
            ProfileStore.SaveAt(_path, p);
            StringAssert.DoesNotContain("firstRunLobbyOutroDone", File.ReadAllText(_path), "배웅 플래그는 스키마에서 걷혔다");
        }

        [Test]
        public void 안내를_끝내지_않은_옛_계정도_첫_판_뒤에는_참가_신청이_나간다()
        {
            var p = ProfileStore.LoadOrCreateAt(_path, null);
            Assert.IsFalse(OutgameMenuController.IsFirstMatch(p),
                "matchesPlayed 3 인 계정이 첫 판 우회에 걸렸다 — 안내 미완주가 여전히 참가 신청을 막고 있다");
            // 남은 우회는 「계정 첫 판」 하나다(서버 500 — 사용자 결정 대기). 새 계정의 첫 판은 여기에 걸린다.
            Assert.IsTrue(OutgameMenuController.IsFirstMatch(new PlayerProfile()), "matchesPlayed 0 = 계정 첫 판");
        }
    }
}
