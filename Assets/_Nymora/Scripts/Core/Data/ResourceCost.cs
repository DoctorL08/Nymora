using Nymora.Core.Enums;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Cout d'un sort en ressources de combat.
    /// PA (max 8/tour), PM (max 3/tour), et eventuellement de la ressource de classe consommee.
    /// </summary>
    public readonly struct ResourceCost
    {
        public readonly int ActionPoints;       // PA — max 8 par tour
        public readonly int MovementPoints;     // PM — max 3 par tour
        public readonly int ClassResource;      // ressource de classe consommee (HG, PR, FD, PT)
        public readonly ResourceType ResourceKind;

        public ResourceCost(int actionPoints, int movementPoints = 0, int classResource = 0, ResourceType kind = ResourceType.None)
        {
            ActionPoints = actionPoints;
            MovementPoints = movementPoints;
            ClassResource = classResource;
            ResourceKind = kind;
        }

        public static ResourceCost Free => new ResourceCost(0);

        public bool IsFree => ActionPoints == 0 && MovementPoints == 0 && ClassResource == 0;

        public override string ToString()
        {
            string ap = ActionPoints > 0 ? $"{ActionPoints}PA" : string.Empty;
            string mp = MovementPoints > 0 ? $" {MovementPoints}PM" : string.Empty;
            string cr = ClassResource > 0 ? $" {ClassResource}{ResourceKind}" : string.Empty;
            return (ap + mp + cr).Trim();
        }
    }
}
