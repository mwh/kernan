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
            AddMethod("allocate", new DelegateMethodNode1(mAlloc));
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
            var at = new DelegateMethodNode1Ctx(mAt);
            var atPut = new DelegateMethodNodeReq(mAtPut);
            AddMethod("[]", at);
            AddMethod("at", at);
            AddMethod("[] :=", atPut);
            AddMethod("at put", atPut);
            AddMethod("size", new DelegateMethodNode0(mSize));
        }

        private GraceObject mAt(EvaluationContext ctx, GraceObject index)
        {
            var num = index.FindNativeParent<GraceNumber>();
            var i = (int)num.Double;
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
            data[i] = val;
            return GraceObject.Done;
        }

        private GraceObject mSize()
        {
            return GraceNumber.Create(data.Length);
        }

    }
}
