using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Exceptions
{
    public abstract class McapException: Exception
    {
        internal McapException(string message, Exception inner):base($"MCAP exception: {message}", inner) { }
    }
}
