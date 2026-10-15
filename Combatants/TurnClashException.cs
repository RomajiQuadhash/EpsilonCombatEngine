using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Combatants;
/// <summary>
/// Exception raised when someone tries to take a turn when there's already one pending or clear the turn when it was already done so
/// </summary>
public class TurnClashException : Exception
{
    public TurnClashException() { }
    public TurnClashException(string message) : base(message) { }
    public TurnClashException(string message, Exception inner) : base(message, inner) { }
}
