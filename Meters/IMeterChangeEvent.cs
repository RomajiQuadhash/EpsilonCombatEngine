using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Meters
{
    /// <summary>
    /// What every event that changes a meter has in common, whether it makes one change or makes a change and takes it back off later.
    /// Lets something hold "an event that changes this meter" without caring which kind it is.
    /// </summary>
    /// <remarks>Everything that implements this is an Okazo. AsOkazo gives it as one, for putting it on the timeline.</remarks>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <typeparam name="M">The numeric type used for the affected meter</typeparam>
    public interface IMeterChangeEvent<T, M> : IOkPriority where T : INumber<T> where M : INumber<M>
    {
        /// <summary>
        /// The meter this event changes.
        /// </summary>
        AbstractMeter<T, M> MeterAppliedTo { get; }
        /// <summary>
        /// The timeline this event is for.
        /// </summary>
        Timeline<T> OwningTimeline { get; }
        /// <summary>
        /// True once this event has done everything it is going to do to the meter.
        /// Used since not all meter change events are done after the first trigger.
        /// </summary>
        bool IsFinished { get; }
        /// <summary>
        /// This same event as an Okazo, which is what the timeline's AddEvent and AddPossibleEvent take.
        /// </summary>
        Okazo<T> AsOkazo { get; }
    }
}
