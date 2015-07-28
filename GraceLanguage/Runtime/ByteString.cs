using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Grace.Execution;

namespace Grace.Runtime
{
    class ByteString : GraceObject
    {
        private byte[] data;

        private static Dictionary<string, MethodNode> sharedMethods;

        public byte this[int index]
        {
            get
            {
                return data[index];
            }
        }

        public ByteString(byte[] d)
        {
            data = (byte[])d.Clone();
            addMethods();
        }

        private static void createSharedMethods()
        {
            sharedMethods = new Dictionary<string, MethodNode>
            {
                { "at", new DelegateMethodNodeTyped<ByteString>(mAt) },
                { "[]", new DelegateMethodNodeTyped<ByteString>(mAt) },
                { "size", new DelegateMethodNodeTyped<ByteString>(mSize) },
            };
        }

        private static GraceObject mSize(EvaluationContext ctx,
                MethodRequest req,
                ByteString self)
        {
            return GraceNumber.Create(self.data.Length);
        }

        private static GraceObject mAt(EvaluationContext ctx,
                MethodRequest req,
                ByteString self)
        {
            var arg = req[0].Arguments[0];
            var index = arg.FindNativeParent<GraceNumber>();
            var idx = index.GetInt() - 1;
            if (idx < 0 || idx >= self.data.Length)
                ErrorReporting.RaiseError(ctx, "R2013",
                        new Dictionary<string, string> {
                            { "index", "" + (idx + 1) },
                            { "valid", self.data.Length > 0 ?
                                "1 .. " + self.data.Length
                                : "none (empty)" }
                        }, "IndexError: Index out of range");
            return getByteObject(self.data[idx]);
        }

        private static GraceObject getByteObject(byte b)
        {
            if (byteObjects[b] == null)
                byteObjects[b] = GraceNumber.Create((int)b);
            return byteObjects[b];
        }

        private void addMethods()
        {
            if (sharedMethods == null)
                createSharedMethods();
            AddMethods(sharedMethods);
        }

        public override string ToString()
        {
            return "ByteString[" + String.Join(" ",
                        from b in data
                        select b.ToString("X2")
                    ) + "]";
        }

        private static GraceObject[] byteObjects = new GraceObject[256];
    }

}
