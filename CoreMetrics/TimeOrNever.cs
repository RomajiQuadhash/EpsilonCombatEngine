using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CoreMetrics
{
	/// <summary>
	/// Used to represent when something may happen or if it won't ever happen
	/// </summary>
	/// <typeparam name="T">Format of time to use</typeparam>
	public class TimeOrNever<T> : IComparable<TimeOrNever<T>> where T : INumber<T>
    {
		public TimeOrNever() => _internalTime = T.Zero;

		public TimeOrNever(T t) => _internalTime = t;

		/// <summary>
		/// Set to true to mark this as invalid
		/// </summary>
		public bool IsNever;

		private T _internalTime;
		/// <summary>
		/// Gets the time if valid. If set, clears is never
		/// </summary>
		public T Time { 
			get
			{
				if (IsNever)
				{
					throw new NeverIsNotATimeException("Can't get the time, the time is Never!");
				}
				return _internalTime;
			}
			set 
			{
				_internalTime = value;
				IsNever = false;
			}
		}
        /// <summary>
        /// Compares the current instance to another TimeOrNever<T> object and returns an integer indicating their relative
        /// order. Never values are considered greater than any time value, and two Never values make an invalid comparison.
        /// </summary>
        /// <param name="other">The TimeOrNever<T> instance to compare with the current object. Can be null.</param>
        /// <returns>A negative integer if the current instance precedes the other; zero if they are equal; a positive integer if the
        /// current instance follows the other.
		/// If both instances are Never, throws a NeverIsNotATimeException.
		/// Special values:
		/// -100 if other is null
		/// -10 if this is Never and other is not Never
		/// 10 if this is not Never and other is Never
		/// </returns>
        public int CompareTo(TimeOrNever<T>? other)
		{
			if (other == null)
			{
				return -100;
			}
			if (IsNever && other.IsNever)
			{
				throw new NeverIsNotATimeException("No ordering between two events that never happen!");
            }
            if (IsNever && !other.IsNever)
			{
				return 10;
            }
			if (!IsNever && other.IsNever)
			{
				return -10;
            }
            return Time.CompareTo(other.Time);
        }
		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>A string value of "Never" if the object represents a 'never' state; otherwise, the string representation of the
		/// Time property or "Invalid Time" if the time property gives a null value.</returns>
		public override string ToString()
		{
            if (IsNever)
            {
                return "Never";
            }
            return Time.ToString() ?? "Invalid Time";
		}
    }
}
