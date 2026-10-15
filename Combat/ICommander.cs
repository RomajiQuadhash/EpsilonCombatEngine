using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Combat
{
    /// <summary>
    /// Empty labeling interface that indicates the object inside is a commander
    /// </summary>
    /// <typeparam name="T">The numeric type used for the timeline in the combat</typeparam>
    public interface ICommander<T> where T : INumber<T>
    {
    }
}
