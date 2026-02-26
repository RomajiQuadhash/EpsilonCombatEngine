using System.Numerics;

namespace EpsilonCombatEngine
{
	/// <summary>
	/// This class keeps track of a value that may change over time.
	/// A counter can also tell how long until it passes a threshold or how far in the past that threshold was crossed.
	/// This only works if the counter is linear over that time, however!
	/// </summary>
	/// <typeparam name="T">Number type to use</typeparam>
	public class Counter<T> where T : INumber<T>
    {

    }
}
