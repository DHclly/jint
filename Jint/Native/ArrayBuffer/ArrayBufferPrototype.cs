#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.ArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-arraybuffer-prototype-object
/// </summary>
internal sealed class ArrayBufferPrototype : Prototype
{
    private readonly ArrayBufferConstructor _constructor;

    internal ArrayBufferPrototype(
        Engine engine,
        ArrayBufferConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get byteLength", ByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            [KnownKeys.Constructor] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["detached"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get detached", Detached, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["maxByteLength"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get maxByteLength", MaxByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["resizable"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get resizable", Resizable, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["resize"] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "resize", Resize, 1, lengthFlags), PropertyFlag.NonEnumerable),
            ["slice"] = new PropertyDescriptor(new ClrFunctionInstance(_engine, "slice", Slice, 2, lengthFlags), PropertyFlag.NonEnumerable)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1) { [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("ArrayBuffer", PropertyFlag.Configurable) };
        SetSymbols(symbols);
    }

    private JsValue Detached(JsValue thisObject, JsValue[] arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.detached called on incompatible receiver " + thisObject);
        }

        return o.IsDetachedBuffer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer.prototype.maxbytelength
    /// </summary>
    private JsValue MaxByteLength(JsValue thisObject, JsValue[] arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.maxByteLength called on incompatible receiver " + thisObject);
        }

        if (o.IsDetachedBuffer)
        {
            return JsNumber.PositiveZero;
        }

        long length = o.IsFixedLengthArrayBuffer
            ? o.ArrayBufferByteLength
            : o._arrayBufferMaxByteLength.GetValueOrDefault();

        return length;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer.prototype.resizable
    /// </summary>
    private JsValue Resizable(JsValue thisObject, JsValue[] arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.resizable called on incompatible receiver " + thisObject);
        }

        return !o.IsFixedLengthArrayBuffer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybuffer.prototype.resize
    /// </summary>
    private JsValue Resize(JsValue thisObject, JsValue[] arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.resize called on incompatible receiver " + thisObject);
        }

        var newLength = arguments.At(0);
        var newByteLength = TypeConverter.ToIndex(_realm, newLength);

        o.AssertNotDetached();

        o.Resize(newByteLength);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer.prototype.bytelength
    /// </summary>
    private JsValue ByteLength(JsValue thisObject, JsValue[] arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.byteLength called on incompatible receiver " + thisObject);
        }

        if (o.IsDetachedBuffer)
        {
            return JsNumber.PositiveZero;
        }

        return JsNumber.Create(o.ArrayBufferByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybuffer.prototype.slice
    /// </summary>
    private JsValue Slice(JsValue thisObject, JsValue[] arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.slice called on incompatible receiver " + thisObject);
        }

        o.AssertNotDetached();

        var start = arguments.At(0);
        var end = arguments.At(1);

        var len = o.ArrayBufferByteLength;
        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
        var first = relativeStart switch
        {
            double.NegativeInfinity => 0,
            < 0 => (int) System.Math.Max(len + relativeStart, 0),
            _ => (int) System.Math.Min(relativeStart, len)
        };

        double relativeEnd;
        if (end.IsUndefined())
        {
            relativeEnd = len;
        }
        else
        {
            relativeEnd = TypeConverter.ToIntegerOrInfinity(end);
        }

        var final = relativeEnd switch
        {
            double.NegativeInfinity => 0,
            < 0 => (int) System.Math.Max(len + relativeEnd, 0),
            _ => (int) System.Math.Min(relativeEnd, len)
        };

        var newLen = System.Math.Max(final - first, 0);
        var ctor = SpeciesConstructor(o, _realm.Intrinsics.ArrayBuffer);
        var bufferInstance = Construct(ctor, new JsValue[] { JsNumber.Create(newLen) }) as JsArrayBuffer;

        if (bufferInstance is null)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (bufferInstance.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (bufferInstance.IsDetachedBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (ReferenceEquals(bufferInstance, o))
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (bufferInstance.ArrayBufferByteLength < newLen)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        // NOTE: Side-effects of the above steps may have detached O.

        if (bufferInstance.IsDetachedBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var fromBuf = o.ArrayBufferData;
        var toBuf = bufferInstance.ArrayBufferData;
        System.Array.Copy(fromBuf!, first, toBuf!, 0, newLen);
        return bufferInstance;
    }
}
