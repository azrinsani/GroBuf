﻿namespace GroBuf.Writers
{
    internal interface IWriterCollection
    {
        IWriterBuilder<T> GetWriter<T>();
    }
}