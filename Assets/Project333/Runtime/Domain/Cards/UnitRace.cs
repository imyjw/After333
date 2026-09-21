using System;
using System.Collections.Generic;

namespace Project333.Runtime.Domain.Cards
{
    // Stable serialized values. No race is an empty collection, not a special race.
    public enum UnitRace
    {
        Robot = 1, Goblin = 2, Human = 3, Dragon = 4,
        Vampire = 5, Undead = 6, Orc = 7, Beast = 8, Elf = 9,
    }

    public static class UnitRaceCollection
    {
        public static IReadOnlyList<UnitRace> Copy(IReadOnlyList<UnitRace> races)
        {
            if (races == null || races.Count == 0) return Array.Empty<UnitRace>();
            var result = new List<UnitRace>();
            foreach (var race in races)
            {
                if (!Enum.IsDefined(typeof(UnitRace), race))
                    throw new ArgumentOutOfRangeException(nameof(races), "Unknown unit race.");
                if (!result.Contains(race)) result.Add(race);
            }
            result.Sort();
            return result.AsReadOnly();
        }

        public static bool Contains(IReadOnlyList<UnitRace> races, UnitRace race)
        {
            if (races == null) return false;
            for (var i = 0; i < races.Count; i++)
                if (races[i] == race) return true;
            return false;
        }

        public static UnitRace[] ToArray(IReadOnlyList<UnitRace> races)
        {
            var copy = new UnitRace[races?.Count ?? 0];
            for (var i = 0; i < copy.Length; i++) copy[i] = races[i];
            return copy;
        }
    }
}
