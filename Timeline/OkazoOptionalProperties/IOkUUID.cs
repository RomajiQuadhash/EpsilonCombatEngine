using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timeline.OkazoOptionalProperties
{
    public interface IOkUUID
    {
        /// <summary>
        /// Guarentee that there's at least one difference between two events
        /// </summary>
        public int UUID { get; }
    }
}
