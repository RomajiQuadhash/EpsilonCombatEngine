using System.Numerics;

/// <summary>
/// Delegates used by meters
/// </summary>
namespace Meters
{
    /// <summary>
    /// Raised when an instant change happens
    /// </summary>
    /// <typeparam name="M">The meter's unit of change</typeparam>
    /// <param name="sender">The meter having this change happen</param>
    /// <param name="before">What the meter was before the change</param>
    /// <param name="after">What the meter is after the change (should be the same as what it is now, but for completeness)</param>
    public delegate void InstantChange<M>(object sender,M before,M after) where M : INumber<M>;
    /// <summary>
    /// Raised when a value changes due to time progressing
    /// </summary>
    /// <typeparam name="T">The unit of the timeline</typeparam>
    /// <typeparam name="M">The meter's unit of change</typeparam>
    /// <param name="sender">The meter having this change happen</param>
    /// <param name="timeElapsed">How much time has the timeline advanced by?</param>
    /// <param name="before">What the meter was before the change</param>
    /// <param name="after">What the meter is after the change (should be the same as what it is now, but for completeness)</param>
    public delegate void ContinuousChange<T, M>(object sender, T timeElapsed, M before, M after) where T : INumber<T> where M : INumber<M>;
}