using CoreMetrics;

namespace Meters
{
    /// <summary>
    /// Counts the ModifierGroups that have been made with it, and gives each one its number.
    /// A group puts its number in every key it gives out, so a key from one group can never be mistaken for a key from another group made with the same counter.
    /// Share one between the groups that must be told apart: the groups of one meter, or of every meter in a combat.
    /// It is cloned along with whatever uses it, so a clone carries on from the same count as its original without either changing the other.
    /// </summary>
    /// <remarks>Claude written, Romaji approved</remarks>
    public class ModifierGroupCounter : ICombatCloneable
    {
        /// <summary>
        /// How many groups have been made with this counter. The most recent one has this as its number.
        /// </summary>
        public int GroupsMade { get; private set; }
        /// <summary>
        /// Counts one more group and gives its number. The first is 1, since 0 is the number of a group made without a counter.
        /// </summary>
        public int TakeNextGroupNumber()
        {
            GroupsMade = checked(GroupsMade + 1);
            return GroupsMade;
        }
        public object Clone(CloneContext context)
        {
            if (context.TryGet(this, out ModifierGroupCounter existingClone))
            {
                return existingClone;
            }
            ModifierGroupCounter clone = new();
            context.Register(this, clone);
            clone.GroupsMade = GroupsMade;
            return clone;
        }
        public override string ToString()
        {
            return $"{nameof(ModifierGroupCounter)} that has numbered {GroupsMade} groups";
        }
    }
}
