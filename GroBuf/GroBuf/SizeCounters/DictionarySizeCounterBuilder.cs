﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using GrEmit;

namespace GroBuf.SizeCounters
{
    // todo переписать
    internal class DictionarySizeCounterBuilder : SizeCounterBuilderBase
    {
        public DictionarySizeCounterBuilder(Type type)
            : base(type)
        {
            if(!(Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                throw new InvalidOperationException("Dictionary expected but was '" + Type + "'");
            keyType = Type.GetGenericArguments()[0];
            valueType = Type.GetGenericArguments()[1];
        }

        protected override void BuildConstantsInternal(SizeCounterConstantsBuilderContext context)
        {
            context.BuildConstants(keyType);
            context.BuildConstants(valueType);
        }

        protected override bool CheckEmpty(SizeCounterMethodBuilderContext context, GroboIL.Label notEmptyLabel)
        {
            var emptyLabel = context.Il.DefineLabel("empty");
            context.LoadObj(); // stack: [obj]
            context.Il.Brfalse(emptyLabel); // if(obj == null) goto empty;
            context.LoadObj(); // stack: [obj]
            context.Il.Call(Type.GetProperty("Count", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(), Type); // stack: [obj.Length]
            context.Il.Brtrue(notEmptyLabel); // if(obj.Length != 0) goto notEmpty;
            context.Il.MarkLabel(emptyLabel);
            return true;
        }

        protected override void CountSizeNotEmpty(SizeCounterMethodBuilderContext context)
        {
            var il = context.Il;
            il.Ldc_I4(9); // stack: [9 = size] 9 = type code + data length + dictionary count
            context.LoadObj(); // stack: [size, obj]
            var count = il.DeclareLocal(typeof(int));
            il.Ldfld(Type.GetField("count", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Stloc(count);
            context.LoadObj(); // stack: [size, obj]
            var entryType = Type.GetNestedType("Entry", BindingFlags.NonPublic).MakeGenericType(Type.GetGenericArguments());
            var entries = il.DeclareLocal(entryType.MakeArrayType());
            il.Ldfld(Type.GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Stloc(entries);

            var i = il.DeclareLocal(typeof(int));
            il.Ldc_I4(0); // stack: [9, 0]
            il.Stloc(i); // i = 0; stack: [9]
            var cycleStartLabel = il.DefineLabel("cycleStart");
            il.MarkLabel(cycleStartLabel);
            il.Ldloc(entries); // stack: [size, entries]
            il.Ldloc(i); // stack: [size, entries, i]
            il.Ldelema(entryType); // stack: [size, &entries[i]]
            il.Dup(); // stack: [size, &entries[i], &entries[i]]
            var entry = il.DeclareLocal(entryType.MakeByRefType());
            il.Stloc(entry); // entry = &entries[i]; stack: [size, entry]
            il.Ldfld(entryType.GetField("hashCode")); // stack: [size, entry.hashCode]
            il.Ldc_I4(0); // stack: [size, entry.hashCode, 0]
            var nextLabel = il.DefineLabel("next");
            il.Blt(typeof(int), nextLabel); // if(entry.hashCode < 0) goto next; stack: [size]
            il.Ldloc(entry); // stack: [size, entry]
            il.Ldfld(entryType.GetField("key")); // stack: [size, entry.key]
            il.Ldc_I4(1); // stack: [size, obj[i], true]
            context.CallSizeCounter(keyType); // stack: [size, writer(entry.key, true) = keySize]
            il.Add(); // stack: [size + keySize]
            
            il.Ldloc(entry); // stack: [size, entry]
            il.Ldfld(entryType.GetField("value")); // stack: [size, entry.value]
            il.Ldc_I4(1); // stack: [size, obj[i], true]
            context.CallSizeCounter(valueType); // stack: [size, writer(entry.value, true) = keySize]
            il.Add(); // stack: [size + valueSize]

            il.MarkLabel(nextLabel);
            il.Ldloc(count); // stack: [size, count]
            il.Ldloc(i); // stack: [size, count, i]
            il.Ldc_I4(1); // stack: [size, count, i, 1]
            il.Add(); // stack: [size, count, i + 1]
            il.Dup(); // stack: [size, count, i + 1, i + 1]
            il.Stloc(i); // i = i + 1; stack: [size, count, i]
            il.Bgt(typeof(int), cycleStartLabel); // if(count > i) goto cycleStart; stack: [size]

//            var keyValueType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
//            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(keyValueType);
//            var enumerator = il.DeclareLocal(enumeratorType);
//            il.Call(typeof(IEnumerable<>).MakeGenericType(keyValueType).GetMethod("GetEnumerator"), Type); // stack: [size, obj.GetEnumerator()]
//            il.Dup(); // stack: [size, enumerator]
//            il.Stloc(enumerator); // enumerator = obj.GetEnumerator(); stack: [size, enumerator]
//            il.Call(typeof(IEnumerator).GetMethod("Reset"), typeof(IEnumerator)); // enumerator.Reset(); stack: [size]
//            var endLabel = il.DefineLabel("end");
//            il.Ldloc(enumerator);
//            il.Call(typeof(IEnumerator).GetMethod("MoveNext"), typeof(IEnumerator));
//            il.Brfalse(endLabel);
//            var cycleStartLabel = il.DefineLabel("cycleStart");
//            il.MarkLabel(cycleStartLabel);
//            il.Ldloc(enumerator); // stack: [size, enumerator]
//            il.Call(enumeratorType.GetProperty("Current").GetGetMethod(), enumeratorType); // stack: [size, enumerator.Current]
//            var current = il.DeclareLocal(keyValueType);
//            il.Stloc(current);
//            il.Ldloca(current);
//            il.Call(keyValueType.GetProperty("Key").GetGetMethod(), keyValueType);
//            il.Ldc_I4(1);
//            context.CallSizeCounter(keyType);
//            il.Add();
//            il.Ldloca(current);
//            il.Call(keyValueType.GetProperty("Value").GetGetMethod(), keyValueType);
//            il.Ldc_I4(1);
//            context.CallSizeCounter(valueType);
//            il.Add();
//            il.Ldloc(enumerator);
//            il.Call(typeof(IEnumerator).GetMethod("MoveNext"), typeof(IEnumerator));
//            il.Brtrue(cycleStartLabel);
//            il.MarkLabel(endLabel);
        }

        private readonly Type keyType;
        private readonly Type valueType;
    }
}