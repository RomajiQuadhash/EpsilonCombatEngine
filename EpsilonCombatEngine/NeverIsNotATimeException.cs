using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EpsilonCombatEngine
{
	/// <summary>
	/// Exception raised when trying to read the time value of a TimeOrNever when it is currently Never
	/// </summary>
	[Serializable]
	internal class NeverIsNotATimeException : Exception
	{
		public NeverIsNotATimeException(): base() { }
		public NeverIsNotATimeException(string message) : base(message) { }
		public NeverIsNotATimeException(string message, Exception inner) : base(message, inner) { }
	}
}
