using System.Numerics;
/// <summary>
/// Common delegates used by the timeline, such as for advancing time or communicating that an event occured.
/// </summary>
namespace Timeline
{
    /// <summary>
    /// Event that is raised when time advances on the timeline, allowing listeners to update their state based on the amount of time that has passed.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <param name="sender">Always the current timeline object</param>
    /// <param name="timeElapsed">How much time passes. Negative if an event is overshot for some reason.</param>
    public delegate void Advance<T>(object sender, T timeElapsed) where T : INumber<T>;
    /// <summary>
    /// Event that is raised when an event occurs on the timeline, allowing listeners to react to the event and update their state accordingly.
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline</typeparam>
    /// <param name="sender">The event that is occuring. Should be of type Okazo</param>
    /// <param name="okazo">Same as the sender, just typed</param>
    public delegate void Occur<T>(object sender,Okazo<T> okazo) where T : INumber<T>;
}