using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // outgame-tutorial unit 8 rev — `matchesPlayed` 는 로비 챕터 D 의 게이트다.
    //
    // 이 테스트가 존재하는 이유: 원래 이 카운터는 `SetPhase(Result)` 에서만 늘었고, 그래서
    // **나가기로 끝낸 판이 통째로 누락**됐다(MenuPopup.OnExit 은 Result 를 거치지 않는다).
    // 나가기 판도 AbandonMatch 가 0점 마감해 히스토리에 자기 엔트리를 남기므로, 챕터 D 가
    // 가르치는 그 히스토리와 카운터가 같은 것을 세야 한다.
    //
    // 저장은 ProfileSaver seam 으로 가로채 개발자의 실제 profile.json 을 건드리지 않는다.
    public class GameManagerMatchCountTests
    {
        private GameObject _go;
        private GameManager _gm;
        private PlayerProfileSO _holder;
        private List<PlayerProfile> _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = new List<PlayerProfile>();
            _holder = ScriptableObject.CreateInstance<PlayerProfileSO>();

            // 비활성 상태로 만들어 Awake(해상도 설정·싱글턴 등록)를 태우지 않는다.
            _go = new GameObject("GameManagerUnderTest");
            _go.SetActive(false);
            _gm = _go.AddComponent<GameManager>();

            typeof(GameManager)
                .GetField("profileSO", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_gm, _holder);
            _gm.ProfileSaver = p => _saved.Add(p);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_holder != null) Object.DestroyImmediate(_holder);
        }

        // 결과 화면까지 본 판 — SetPhase(Result) 가 기록을 구동한다.
        [Test]
        public void ResultPhase_RecordsOneMatch()
        {
            _holder.SetLoadedProfile(new PlayerProfile());

            _gm.SetPhase(GamePhase.Result);

            Assert.AreEqual(1, _holder.profile.matchesPlayed);
            Assert.AreEqual(1, _saved.Count, "디스크 저장은 1회");
        }

        // 나가기로 끝낸 판 — Result 를 거치지 않지만 히스토리에는 남으므로 세야 한다.
        // 이 테스트가 원래 결함(카운터가 두 판 중 한 판만 셈)의 회귀 방지선이다.
        [Test]
        public void ExitPath_RecordsMatch_WithoutResultPhase()
        {
            _holder.SetLoadedProfile(new PlayerProfile());

            _gm.RecordMatchPlayed(); // MenuPopup.OnExit 이 하는 일

            Assert.AreEqual(1, _holder.profile.matchesPlayed);
            Assert.AreEqual(GamePhase.None, _gm.CurrentPhase, "나가기는 Result 로 가지 않는다");
        }

        // 호출처가 둘이 되면서 래치가 필수다 — 한 판에서 두 신호가 겹쳐도 1회.
        [Test]
        public void TwoExitSignalsInOneMatch_CountOnce()
        {
            _holder.SetLoadedProfile(new PlayerProfile());

            _gm.SetPhase(GamePhase.Result);
            _gm.RecordMatchPlayed();
            _gm.RecordMatchPlayed();

            Assert.AreEqual(1, _holder.profile.matchesPlayed);
            Assert.AreEqual(1, _saved.Count);
        }

        [Test]
        public void AlreadyPlayedProfile_Increments_DoesNotReset()
        {
            _holder.SetLoadedProfile(new PlayerProfile { matchesPlayed = 4 });

            _gm.RecordMatchPlayed();

            Assert.AreEqual(5, _holder.profile.matchesPlayed);
        }

        // BattleScene 직접 Play — 프로필이 이번 세션에 로드된 적이 없다. 여기서 저장하면
        // 빈 인메모리 상태가 디스크의 스쿼드·덱을 덮는다.
        [Test]
        public void ProfileNotLoadedThisSession_DoesNotCountOrSave()
        {
            // SetLoadedProfile 을 부르지 않는다 → IsLoadedThisSession == false
            Assert.IsFalse(_holder.IsLoadedThisSession);

            _gm.RecordMatchPlayed();

            Assert.AreEqual(0, _holder.profile.matchesPlayed);
            Assert.IsEmpty(_saved, "로드되지 않은 프로필은 디스크에 쓰지 않는다");
        }

        // 저장이 실패해도 판 흐름을 막지 않는다(fail-open).
        [Test]
        public void SaveFailure_IsSwallowed()
        {
            _holder.SetLoadedProfile(new PlayerProfile());
            _gm.ProfileSaver = _ => throw new System.IO.IOException("disk full");

            Assert.DoesNotThrow(() => _gm.RecordMatchPlayed());
            Assert.AreEqual(1, _holder.profile.matchesPlayed, "인메모리 증가는 유지된다");
        }
    }
}
