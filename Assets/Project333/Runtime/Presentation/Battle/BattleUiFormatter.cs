using System.Collections.Generic;
using Project333.Runtime.Domain.Battle;
using Project333.Runtime.Domain.Cards;
using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleUiFormatter
    {
        public static string FormatTileTitle(PlayerId ownerId, int column, int row)
        {
            return $"{ownerId} ({column},{row})";
        }

        public static string FormatTileOccupant(OccupantState occupant)
        {
            if (occupant == null)
            {
                return "Empty";
            }

            var summary = $"{occupant.CardId}{System.Environment.NewLine}" +
                          $"{occupant.Kind} / {occupant.AttackType}{System.Environment.NewLine}" +
                          $"ATK {occupant.Attack} / HP {occupant.CurrentHp}/{occupant.MaxHp}";

            var statusLabels = new List<string>();
            if (occupant.IsDrained)
            {
                statusLabels.Add("Drained");
            }

            if (occupant.IsErasure)
            {
                statusLabels.Add("Erasure");
            }

            if (occupant.IsSealbound)
            {
                statusLabels.Add($"Sealbound {occupant.SealboundOwnerTurnStartsRemaining}");
            }
            else if (!occupant.IsDrained && occupant.HasSummoningSickness)
            {
                statusLabels.Add("Summoning Sick");
            }

            if (occupant.IsHiding)
            {
                statusLabels.Add("Hiding");
            }

            if (occupant.HasFlying)
            {
                statusLabels.Add(occupant.HasActiveFlying ? "Flying" : "Flying Off");
            }

            if (occupant.SpellPower > 0)
            {
                statusLabels.Add(occupant.EffectiveSpellPower > 0
                    ? $"SpellPower +{occupant.SpellPower}"
                    : $"SpellPower +{occupant.SpellPower} Off");
            }

            if (occupant.InvincibleEffects.Count > 0)
            {
                statusLabels.Add(occupant.IsInvincible ? "Invincible" : "Invincible Off");
            }

            if (occupant.HasShielder)
            {
                statusLabels.Add(occupant.HasActiveShielder ? "Shielder" : "Shielder Off");
            }

            if (occupant.HasEndure)
            {
                statusLabels.Add(occupant.EndureUsed ? "Endure Used" : "Endure");
            }

            if (statusLabels.Count > 0)
            {
                summary += System.Environment.NewLine + string.Join(" / ", statusLabels);
            }

            return summary;
        }

        public static string FormatHandCard(int slotIndex, string cardId)
        {
            return string.IsNullOrWhiteSpace(cardId)
                ? $"Slot {slotIndex}: Empty"
                : $"Slot {slotIndex}: {cardId}";
        }

        public static string FormatHighlightLabel(BattleHighlightState highlightState)
        {
            return highlightState switch
            {
                BattleHighlightState.Selected => "[Selected]",
                BattleHighlightState.Playable => "[Playable]",
                BattleHighlightState.MoveTarget => "[Move]",
                BattleHighlightState.SwapTarget => "[Swap]",
                BattleHighlightState.AttackTarget => "[Attack]",
                BattleHighlightState.SpellTarget => "[Spell]",
                BattleHighlightState.DragHoverTarget => "[Drag]",
                _ => string.Empty,
            };
        }

        public static string FormatResources(ResourceSet resourceSet)
        {
            if (resourceSet == null)
            {
                return "M:0 Q:0 P:0 G:0";
            }

            return $"M:{resourceSet.Mana} Q:{resourceSet.Qi} P:{resourceSet.Power} G:{resourceSet.Gold}";
        }
    }
}
