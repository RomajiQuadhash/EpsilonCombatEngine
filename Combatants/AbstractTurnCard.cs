using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timeline;
using Timeline.OkazoOptionalProperties;
using System.Numerics;

namespace Combatants
{
    /// <summary>
    /// A card for a combatant that represents that combatant's turn.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="F">Type of the data stored on the card</typeparam>
    /// <param name="timeToEvent">How far in the future this card is</param>
    /// <param name="owningTimeline">What timeline this Card should be owned by</param>
    /// <param name="priority">Either ReturnAIControl or ReturnPlayerControl</param>
    public abstract class AbstractTurnCard<T,F>(T timeToEvent, Timeline<T> owningTimeline, PriorityRank priority) : Timeline.Card<T,F>(timeToEvent, owningTimeline, priority) where T : INumber<T>
    {
        /// <summary>
        /// Enforces that the priority of a TurnCard must be either ReturnAIControl or ReturnPlayerControl,
        /// since it represents a combatant's turn starting.
        /// </summary>
        public new PriorityRank? Priority { get =>base.Priority; set
            {
                if (value != PriorityRank.ReturnAIControl && value != PriorityRank.ReturnPlayerControl)
                {
                    throw new ArgumentException($"Priority must be either {PriorityRank.ReturnAIControl} or {PriorityRank.ReturnPlayerControl} for a TurnCard, since it is marking your turn starting, but was {value}");
                }
                base.Priority = value;
            }
        }
        public override bool HaltsInstantAction { get => true; 
            set => throw new InvalidOperationException($"{this} is a Turn Card and hence always halts instant action"); }
        public override string ToString()
        {
            return "Turn!"+base.ToString();
        }
    }
}
