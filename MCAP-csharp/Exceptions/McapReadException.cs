using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Exceptions
{
    public class McapReadException: McapException
    {
        public McapReadException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
