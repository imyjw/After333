using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project333.Runtime.Application.Accounts;
using UnityEngine;

namespace Project333.Tests.EditMode
{
    public sealed class PendingRunRewardPresentationTests
    {
        private const string Server = "https://reward-presentation-tests.invalid";
        private readonly List<string> _keys = new List<string>();
        private string _account;
        private string _run;

        [SetUp]
        public void SetUp()
        {
            _account = Guid.NewGuid().ToString("D");
            _run = Guid.NewGuid().ToString("D");
            TrackKey(Server, _account);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var key in _keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            _keys.Clear();
        }

        [Test]
        public void SavedMarkerReloadsCanonicalRunIdAndContainsOnlyPresentationIdentity()
        {
            PendingRunRewardPresentation.Remember(Server, _account, new Guid(_run).ToString("N"));

            Assert.AreEqual(_run, PendingRunRewardPresentation.GetPendingRunId(Server, _account));
            Assert.AreEqual(_run, PlayerPrefs.GetString(TrackKey(Server, _account)));
            Assert.AreEqual(_run, PendingRunRewardPresentation.GetPendingRunId(Server + "/", _account.ToUpperInvariant()));
        }

        [Test]
        public void RememberSameRunIsIdempotentAndConfirmClearsIt()
        {
            PendingRunRewardPresentation.Remember(Server, _account, _run);
            PendingRunRewardPresentation.Remember(Server, _account, _run);
            PendingRunRewardPresentation.Clear(Server, _account, _run);
            PendingRunRewardPresentation.Clear(Server, _account, _run);

            Assert.IsEmpty(PendingRunRewardPresentation.GetPendingRunId(Server, _account));
            Assert.IsFalse(PlayerPrefs.HasKey(TrackKey(Server, _account)));
        }

        [Test]
        public void DifferentAccountAndEndpointCannotReadOrClearTheMarker()
        {
            var otherAccount = Guid.NewGuid().ToString("D");
            var otherServer = Server + "/other";
            TrackKey(Server, otherAccount);
            TrackKey(otherServer, _account);
            PendingRunRewardPresentation.Remember(Server, _account, _run);

            Assert.IsEmpty(PendingRunRewardPresentation.GetPendingRunId(Server, otherAccount));
            Assert.IsEmpty(PendingRunRewardPresentation.GetPendingRunId(otherServer, _account));
            PendingRunRewardPresentation.Clear(Server, otherAccount, _run);
            PendingRunRewardPresentation.Clear(otherServer, _account, _run);
            Assert.AreEqual(_run, PendingRunRewardPresentation.GetPendingRunId(Server, _account));
        }

        [Test]
        public void LateConfirmationCannotClearANewerRunDisplayMarker()
        {
            var newerRun = Guid.NewGuid().ToString("D");
            PendingRunRewardPresentation.Remember(Server, _account, _run);
            PendingRunRewardPresentation.Remember(Server, _account, newerRun);
            PendingRunRewardPresentation.Clear(Server, _account, _run);

            Assert.AreEqual(newerRun, PendingRunRewardPresentation.GetPendingRunId(Server, _account));
            PendingRunRewardPresentation.Clear(Server, _account, newerRun);
            Assert.IsEmpty(PendingRunRewardPresentation.GetPendingRunId(Server, _account));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-run")]
        [TestCase("00000000-0000-0000-0000-000000000000")]
        public void InvalidRunIdCannotCreateOrClearPendingPresentation(string invalidRun)
        {
            Assert.Throws<ArgumentException>(() => PendingRunRewardPresentation.Remember(Server, _account, invalidRun));
            PendingRunRewardPresentation.Remember(Server, _account, _run);
            PendingRunRewardPresentation.Clear(Server, _account, invalidRun);
            Assert.AreEqual(_run, PendingRunRewardPresentation.GetPendingRunId(Server, _account));
        }

        [Test]
        public void MissingScopeCannotCreateOrReadAMarker()
        {
            Assert.Throws<ArgumentException>(() => PendingRunRewardPresentation.Remember(null, _account, _run));
            Assert.Throws<ArgumentException>(() => PendingRunRewardPresentation.Remember(Server, null, _run));
            Assert.IsEmpty(PendingRunRewardPresentation.GetPendingRunId(null, _account));
            Assert.IsEmpty(PendingRunRewardPresentation.GetPendingRunId(Server, null));
        }

        [Test]
        public void CorruptMarkerDoesNotInventARunOrDeleteUnknownDataAndExplicitOpenRepairsIt()
        {
            var key = TrackKey(Server, _account);
            PlayerPrefs.SetString(key, "corrupt-display-only-data");
            PlayerPrefs.Save();

            Assert.IsEmpty(PendingRunRewardPresentation.GetPendingRunId(Server, _account));
            PendingRunRewardPresentation.Clear(Server, _account, _run);
            Assert.AreEqual("corrupt-display-only-data", PlayerPrefs.GetString(key));
            PendingRunRewardPresentation.Remember(Server, _account, _run);
            Assert.AreEqual(_run, PendingRunRewardPresentation.GetPendingRunId(Server, _account));
        }

        private string TrackKey(string url, string owner)
        {
            var key = (string)typeof(PendingRunRewardPresentation).GetMethod("Key", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { url, owner });
            if (!_keys.Contains(key)) _keys.Add(key);
            return key;
        }
    }
}
