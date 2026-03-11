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
	public class TimeOrNever<T> where T : INumber<T>
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
	}
}
