using Project333.Runtime.Domain.Resources;

namespace Project333.Runtime.Application.Online
{
    public sealed class ResourceSetDto
    {
        public int Mana { get; set; }

        public int Qi { get; set; }

        public int Power { get; set; }

        public int Gold { get; set; }

        public static ResourceSetDto FromDomain(ResourceSet resources)
        {
            if (resources == null)
            {
                return new ResourceSetDto();
            }

            return new ResourceSetDto
            {
                Mana = resources.Mana,
                Qi = resources.Qi,
                Power = resources.Power,
                Gold = resources.Gold
            };
        }
    }
}
