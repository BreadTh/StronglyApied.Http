using System;

namespace BreadTh.StronglyApied.Http
{
    public class OutOfTransformersException : Exception 
    {
        public OutOfTransformersException(string message = null, Exception innerException = null) 
            : base(message, innerException) 
        { }
    }
}

