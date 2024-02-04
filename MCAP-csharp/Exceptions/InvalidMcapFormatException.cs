using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Exceptions
{
    public class InvalidMcapFormatException: McapException
    {
        internal InvalidMcapFormatException(string message) : base(message, null)
        {
        }
    }
}
