using NUnit.Framework;
using Project333.Runtime.Application.Online;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Board;
using Project333.Runtime.Domain.Cards;

namespace Project333.Tests.EditMode
{
    public sealed class BattleStateViewProjectorTests
    {
        [Test]
        public void CreateLocalPerspectiveState_WhenViewerWinsEndedServerBattle_ProjectsLocalVictory()
        {
            var view = CreateEndedView(viewerId: PlayerId.Player, winnerId: PlayerId.Player);

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(projectedState.IsEnded, Is.True);
            Assert.That(projectedState.Phase, Is.EqualTo(PhaseType.Ended));
            Assert.That(projectedState.Result.HasWinner, Is.True);
            Assert.That(projectedState.Result.Winner, Is.EqualTo(PlayerId.Player));
        }

        [Test]
        public void CreateLocalPerspectiveState_WhenViewerLosesEndedServerBattle_ProjectsLocalDefeat()
        {
            var view = CreateEndedView(viewerId: PlayerId.Player, winnerId: PlayerId.AI);

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(projectedState.IsEnded, Is.True);
            Assert.That(projectedState.Result.HasWinner, Is.True);
            Assert.That(projectedState.Result.Winner, Is.EqualTo(PlayerId.AI));
        }

        [Test]
        public void CreateLocalPerspectiveState_WhenViewerIsRemoteAiSeat_StillMapsViewerToLocalPlayer()
        {
            var view = CreateEndedView(viewerId: PlayerId.AI, winnerId: PlayerId.AI);

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(projectedState.IsEnded, Is.True);
            Assert.That(projectedState.Result.HasWinner, Is.True);
            Assert.That(projectedState.Result.Winner, Is.EqualTo(PlayerId.Player));
        }

        [Test]
        public void CreateLocalPerspectiveState_PreservesDamageTypeAndDefenses()
        {
            var view = new BattleStateViewDto
            {
                MatchId = "match-1",
                ViewerId = PlayerId.Player,
                ActivePlayerId = PlayerId.Player,
                TurnNumber = 1,
                Phase = PhaseType.Main,
                Player = CreatePlayerView(PlayerId.Player),
                Opponent = CreatePlayerView(PlayerId.AI),
            };
            view.Occupants.Add(new BoardOccupantViewDto
            {
                OwnerId = PlayerId.Player,
                Coord = new TileCoordDto { Column = 0, Row = 0 },
                RuntimeId = "magic-unit-runtime",
                CardId = "magic-unit",
                Kind = OccupantKind.Unit,
                AttackType = AttackType.Ranged,
                DamageType = DamageType.Magic,
                Attack = 7,
                PhysicalDefense = 4,
                MagicDefense = 6,
                CurrentHp = 12,
                MaxHp = 15,
                CanMove = true,
                HasRobot = true,
                RemainingAttacksThisTurn = 1,
            });

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var occupant = projectedState.PlayerBoard.GetOccupant(new TileCoord(0, 0));

            Assert.That(occupant, Is.Not.Null);
            Assert.That(occupant.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(occupant.PhysicalDefense, Is.EqualTo(4));
            Assert.That(occupant.MagicDefense, Is.EqualTo(6));
            Assert.That(occupant, Is.TypeOf<UnitState>());
            Assert.That(((UnitState)occupant).HasRobot, Is.True);
        }

        [Test]
        public void CreateLocalPerspectiveState_PreservesErasureAndOriginalCombatStats()
        {
            var view = new BattleStateViewDto
            {
                MatchId = "match-erasure",
                ViewerId = PlayerId.Player,
                ActivePlayerId = PlayerId.Player,
                TurnNumber = 1,
                Phase = PhaseType.Main,
                Player = CreatePlayerView(PlayerId.Player),
                Opponent = CreatePlayerView(PlayerId.AI),
            };
            view.Occupants.Add(new BoardOccupantViewDto
            {
                OwnerId = PlayerId.Player,
                Coord = new TileCoordDto { Column = 0, Row = 0 },
                RuntimeId = "erasure-runtime",
                CardId = "erasure-unit",
                Kind = OccupantKind.Unit,
                AttackType = AttackType.Melee,
                DamageType = DamageType.Physical,
                Attack = 14,
                OriginalAttack = 10,
                PhysicalDefense = 5,
                MagicDefense = 6,
                OriginalPhysicalDefense = 2,
                OriginalMagicDefense = 3,
                CurrentHp = 35,
                MaxHp = 45,
                OriginalMaxHp = 40,
                HasOriginalCombatStats = true,
                CanMove = true,
                IsDrained = true,
                IsErasure = true,
                HasRobot = true,
                RemainingAttacksThisTurn = 1,
            });

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var occupant = (UnitState)projectedState.PlayerBoard.GetOccupant(new TileCoord(0, 0));

            Assert.That(occupant.IsErasure, Is.True);
            Assert.That(occupant.IsDrained, Is.False);
            Assert.That(occupant.OriginalAttack, Is.EqualTo(10));
            Assert.That(occupant.OriginalMaxHp, Is.EqualTo(40));
            Assert.That(occupant.OriginalPhysicalDefense, Is.EqualTo(2));
            Assert.That(occupant.OriginalMagicDefense, Is.EqualTo(3));
            Assert.That(occupant.Attack, Is.EqualTo(14), "Buffs added after Erasure remain active after projection.");

            occupant.ApplyErasure();

            Assert.That(occupant.Attack, Is.EqualTo(10));
            Assert.That(occupant.MaxHp, Is.EqualTo(40));
            Assert.That(occupant.PhysicalDefense, Is.EqualTo(2));
            Assert.That(occupant.MagicDefense, Is.EqualTo(3));
        }

        [Test]
        public void CreateLocalPerspectiveState_PreservesOpponentHandCountWithoutRevealingCards()
        {
            var playerView = CreatePlayerView(PlayerId.Player);
            playerView.HandCount = 2;
            playerView.VisibleHandCardIds.Add("Goblin");
            playerView.VisibleHandCardIds.Add("Firebolt");

            var opponentView = CreatePlayerView(PlayerId.AI);
            opponentView.HandCount = 5;
            opponentView.VisibleHandCardIds.Add("MustNotBeRevealed");

            var view = new BattleStateViewDto
            {
                MatchId = "match-hidden-hand",
                ViewerId = PlayerId.Player,
                ActivePlayerId = PlayerId.Player,
                TurnNumber = 1,
                Phase = PhaseType.Main,
                Player = playerView,
                Opponent = opponentView,
            };

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(projectedState.Player.Hand.CardIds, Is.EqualTo(new[] { "Goblin", "Firebolt" }));
            Assert.That(projectedState.AI.Hand.Count, Is.EqualTo(5));
            Assert.That(projectedState.AI.Hand.CardIds, Has.None.EqualTo("MustNotBeRevealed"));
            Assert.That(projectedState.AI.Hand.CardIds, Has.All.StartsWith("hidden_"));
        }

        [Test]
        public void CreateLocalPerspectiveState_PreservesOwnedTemporaryReplicateMetadata()
        {
            var playerView = CreatePlayerView(PlayerId.Player);
            playerView.HandCount = 2;
            playerView.VisibleHandCards.Add(new HandCardViewDto
            {
                RuntimeId = "permanent-runtime",
                CardId = "ReplicateCard",
                IsTemporaryReplicate = false,
            });
            playerView.VisibleHandCards.Add(new HandCardViewDto
            {
                RuntimeId = "temporary-runtime",
                CardId = "ReplicateCard",
                IsTemporaryReplicate = true,
            });

            var view = new BattleStateViewDto
            {
                MatchId = "match-replicate-hand",
                ViewerId = PlayerId.Player,
                ActivePlayerId = PlayerId.Player,
                TurnNumber = 1,
                Phase = PhaseType.Main,
                Player = playerView,
                Opponent = CreatePlayerView(PlayerId.AI),
            };

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);

            Assert.That(projectedState.Player.Hand.Cards, Has.Count.EqualTo(2));
            Assert.That(projectedState.Player.Hand.Cards[0].RuntimeId, Is.EqualTo("permanent-runtime"));
            Assert.That(projectedState.Player.Hand.Cards[0].IsTemporaryReplicate, Is.False);
            Assert.That(projectedState.Player.Hand.Cards[1].RuntimeId, Is.EqualTo("temporary-runtime"));
            Assert.That(projectedState.Player.Hand.Cards[1].IsTemporaryReplicate, Is.True);
        }

        [Test]
        public void CreateLocalPerspectiveState_PreservesFirewallPersistentEffectState()
        {
            var view = new BattleStateViewDto
            {
                MatchId = "match-firewall",
                ViewerId = PlayerId.Player,
                ActivePlayerId = PlayerId.AI,
                TurnNumber = 2,
                Phase = PhaseType.Main,
                Player = CreatePlayerView(PlayerId.Player),
                Opponent = CreatePlayerView(PlayerId.AI),
            };
            view.PersistentEffects.Add(new PersistentEffectViewDto
            {
                SourceCardId = "Firewall",
                OwnerId = PlayerId.Player,
                EffectId = "firewall",
                AppliedTurn = 1,
                EndConditionText = "two triggers",
                TargetRow = 1,
                RemainingTriggers = 1,
                EffectDamage = 38,
                EffectDamageType = DamageType.Magic,
                TargetsOwnerBoard = true,
            });

            var projectedState = BattleStateViewProjector.CreateLocalPerspectiveState(view);
            var effect = projectedState.PersistentEffects[0];

            Assert.That(effect.SourceCardId, Is.EqualTo("Firewall"));
            Assert.That(effect.OwnerId, Is.EqualTo(PlayerId.Player));
            Assert.That(effect.TargetRow, Is.EqualTo(1));
            Assert.That(effect.RemainingTriggers, Is.EqualTo(1));
            Assert.That(effect.EffectDamage, Is.EqualTo(38));
            Assert.That(effect.EffectDamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(effect.TargetsOwnerBoard, Is.True);
        }

        private static BattleStateViewDto CreateEndedView(PlayerId viewerId, PlayerId winnerId)
        {
            var opponentId = viewerId == PlayerId.Player ? PlayerId.AI : PlayerId.Player;
            return new BattleStateViewDto
            {
                MatchId = "match-1",
                ViewerId = viewerId,
                ActivePlayerId = viewerId,
                TurnNumber = 1,
                Phase = PhaseType.Ended,
                IsEnded = true,
                HasWinner = true,
                WinnerId = winnerId,
                Player = CreatePlayerView(viewerId),
                Opponent = CreatePlayerView(opponentId),
            };
        }

        private static BattlePlayerViewDto CreatePlayerView(PlayerId playerId)
        {
            return new BattlePlayerViewDto
            {
                PlayerId = playerId,
                MaxHandSize = PlayerState.BaseMaxHandSize,
            };
        }
    }
}
