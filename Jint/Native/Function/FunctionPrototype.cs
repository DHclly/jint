﻿using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Function
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.4
    /// </summary>
    public sealed class FunctionPrototype : FunctionInstance
    {
        private FunctionPrototype(Engine engine) : base(engine, null, null, false)
        {
        }

        public static FunctionPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new FunctionPrototype(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Function prototype object is the standard built-in Object prototype object
            obj.Prototype = engine.Object.PrototypeObject;

            obj.FastAddProperty("length", 0, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("constructor", Engine.Function, false, false, false);
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToString), true, false, true);
            FastAddProperty("apply", new ClrFunctionInstance<object, object>(Engine, Apply), true, false, true);
            FastAddProperty("call", new ClrFunctionInstance<object, object>(Engine, Call, 1), true, false, true);
            FastAddProperty("bind", new ClrFunctionInstance<object, object>(Engine, Bind, 1), true, false, true);
        }

        private object Bind(object thisObj, object[] arguments)
        {
            var target = thisObj as ICallable;

            if (target == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }
            
            object thisArg = arguments.At(0);
            var f = new BindFunctionInstance(Engine);
            f.TargetFunction = target;
            f.BoundThis = thisArg;
            f.BoundArgs = arguments.Skip(1).ToArray();
            f.Prototype = Engine.Function.PrototypeObject;

            var o = target as FunctionInstance;
            if (o != null)
            {
                var l = TypeConverter.ToNumber(o.Get("length")) - (arguments.Length - 1);
                f.FastAddProperty("length", System.Math.Max(l, 0), false, false, false); 
            }
            else
            {
                f.FastAddProperty("length", 0, false, false, false); 
            }
            

            var thrower = Engine.Function.ThrowTypeError;
            f.DefineOwnProperty("caller", new AccessorDescriptor(thrower, thrower) { Enumerable = false, Configurable = false }, false);
            f.DefineOwnProperty("callee", new AccessorDescriptor(thrower, thrower) { Enumerable = false, Configurable = false }, false);


            return f;
        }

        private object ToString(object thisObj, object[] arguments)
        {
            var func = thisObj as FunctionInstance;

            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError, "Function object expected.");       
            }

            return System.String.Format("function() {{ ... }}");
        }

        public object Apply(object thisObject, object[] arguments)
        {
            var func = thisObject as ICallable;
            object thisArg = arguments.At(0);
            object argArray = arguments.At(1);

            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            if (argArray == Null.Instance || argArray == Undefined.Instance)
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argArrayObj = argArray as ObjectInstance;
            if (argArrayObj == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            object len = argArrayObj.Get("length");
            uint n = TypeConverter.ToUint32(len);
            var argList = new List<object>();
            for (int index = 0; index < n; index++)
            {
                string indexName = index.ToString();
                object nextArg = argArrayObj.Get(indexName);
                argList.Add(nextArg);
            }
            return func.Call(thisArg, argList.ToArray());
        }

        public override object Call(object thisObject, object[] arguments)
        {
            var func = thisObject as ICallable;
            if (func == null)
            {
                return new JavaScriptException(Engine.TypeError);
            }

            return func.Call(arguments.At(0), arguments.Length == 0 ? arguments : arguments.Skip(1).ToArray());
        }
    }
}