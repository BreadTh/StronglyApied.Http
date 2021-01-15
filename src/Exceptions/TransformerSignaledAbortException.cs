using System;

namespace BreadTh.StronglyApied.Http
{
    public class TransformerSignaledAbortException : Exception 
    {
        public TransformerSignaledAbortException(string message = null, Exception innerException = null) 
            : base(message, innerException) 
        { } 
    }
}

