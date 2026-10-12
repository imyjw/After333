using System;
using NUnit.Framework;
using Project333.Runtime.Application.Commands;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Presentation.Battle;
namespace Project333.Tests.EditMode
{
    public sealed class TrapCombatLogTests
    {
        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void InstallationNeverLooksUpHiddenIdentityEvenForMalformedLog(PlayerId owner)
        {
            var log=new BattleCombatLogEntryDto{EntryType=BattleCombatLogEntryType.TrapInstalled,SourceOwnerId=owner,SourceCardId="secret",TargetCardId="also-secret"};
            var text=BattleCombatLogEntryFormatter.Format(log,id=>throw new InvalidOperationException("Hidden lookup: "+id));
            Assert.That(text,Does.Contain("트랩").And.Contain("설치").And.Not.Contain("secret"));
            Assert.That(CombatLogFormatter.FormatCommand(owner,new InstallTrapCommand("secret")),Does.Contain("트랩").And.Not.Contain("secret"));
        }
        [TestCase(PlayerId.Player)] [TestCase(PlayerId.AI)]
        public void MirrorActivationNamesReflectionWithoutReturningTheAttacker(PlayerId owner)
        {
            var text=BattleCombatLogEntryFormatter.Format(new BattleCombatLogEntryDto
                {EntryType=BattleCombatLogEntryType.TrapTriggered,SourceOwnerId=owner,SourceCardId="TrapMirrorField",TargetCardId="Skeleton"},
                id=>id=="TrapMirrorField"?"트랩-미러 필드":"스켈레톤");
            Assert.That(text,Does.Contain("트랩-미러 필드").And.Contain("스켈레톤").And.Contain("취소").And.Contain("마법 피해 반사").And.Not.Contain("패로"));
        }
        [Test] public void SuccessfulActivationNamesTheNowPublicCardAndReturnedUnit()
        {
            var text=BattleCombatLogEntryFormatter.Format(new BattleCombatLogEntryDto{EntryType=BattleCombatLogEntryType.TrapTriggered,SourceOwnerId=PlayerId.Player,SourceCardId="TrapReturn",TargetCardId="Skeleton"},id=>id=="TrapReturn"?"트랩: 리턴":"스켈레톤");
            Assert.That(text,Does.Contain("트랩: 리턴").And.Contain("스켈레톤").And.Contain("패").And.Contain("취소"));
        }
    }
}
