using System.Collections.Generic;
using System.IO;
using Grace.Parsing;
using Grace.Execution;
using Grace.Runtime;

namespace Grace.Platform.Memory
{
    [ModuleEntryPoint]
    public class PlatformMemory : GraceObject
    {

        public static GraceObject Instantiate(
            EvaluationContext ctx)
        {
            return new PlatformMemory();
        }

        public PlatformMemory() : base("platform/memory")
        {
            AddMethod("allocate", new DelegateMethod1(mAlloc));
        }

        private GraceObject mAlloc(GraceObject arg)
        {
            var num = arg.FindNativeParent<GraceNumber>();
            var i = (int)num.Double;
            return new FixedSizeArray(i);
        }

    }

    class FixedSizeArray : GraceObject
    {

        private GraceObject[] data;

        public FixedSizeArray(int size)
        {
            data = new GraceObject[size];
            var at = new DelegateMethod1Ctx(mAt);
            var atPut = new DelegateMethodReq(mAtPut);
            AddMethod("[]", at);
            AddMethod("at", at);
            AddMethod("[] :=", atPut);
            AddMethod("at put", atPut);
            AddMethod("size", new DelegateMethod0(mSize));
        }

        private static void checkIndex(
                EvaluationContext ctx,
                int i,
                GraceObject[] data
            )
        {
            if (i < 0 || i >= data.Length)
                ErrorReporting.RaiseError(ctx, "R2013",
                    new Dictionary<string, string> {
                        { "index", "" + i },
                        { "valid", "0.." + (data.Length - 1) }
                    },
                    "IndexError: Invalid index " + i
                );
        }

        private GraceObject mAt(EvaluationContext ctx, GraceObject index)
        {
            var num = index.FindNativeParent<GraceNumber>();
            var i = (int)num.Double;
            checkIndex(ctx, i, data);
            var ret = data[i];
            if (ret == null)
            {
                ErrorReporting.RaiseError(ctx, "R2008",
                    new Dictionary<string, string> {
                        { "name", "index " + i },
                        { "receiver", ToString() }
                    },
                    "UninitialisedReadError: Cannot read from index " + i
                );
            }
            return ret;
        }

        private GraceObject mAtPut(EvaluationContext ctx, MethodRequest req)
        {
            var index = req[0].Arguments[0];
            var val = req[1].Arguments[0];
            var num = index.FindNativeParent<GraceNumber>();
            var i = (int)num.Double;
            checkIndex(ctx, i, data);
            data[i] = val;
            return GraceObject.Done;
        }

        private GraceObject mSize()
        {
            return GraceNumber.Create(data.Length);
        }

    }
}
