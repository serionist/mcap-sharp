using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Exceptions
{
    public class McapWriteException : McapException
    {
        public McapWriteException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }
}
