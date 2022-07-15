using System;

namespace SqCommon;

public class SqException : Exception
{
    public SqException()
    {
    }
    public SqException(string message)
        : base(message)
    {
    }

    public SqException(string message, Exception inner)
        : base(message, inner)
    {
    }
}