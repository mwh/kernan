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

        private static Dictionary<string, Method> sharedMethods;

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
            sharedMethods = new Dictionary<string, Method>
            {
                { "at", new DelegateMethodTyped<ByteString>(mAt) },
                { "[]", new DelegateMethodTyped<ByteString>(mAt) },
                { "size", new DelegateMethodTyped<ByteString>(mSize) },
                { "++", new DelegateMethodTyped<ByteString>(mConcat) },
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
            MethodHelper.CheckArity(ctx, req, 1);
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

        private static GraceObject mConcat(EvaluationContext ctx,
                MethodRequest req,
                ByteString self)
        {
            MethodHelper.CheckArity(ctx, req, 1);
            var oth = req[0].Arguments[0].FindNativeParent<ByteString>();
            if (oth == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "index", "1" },
                            { "part", req.Name },
                            { "required", "byte string" },
                        }, "ArgumentTypeError: Needed byte string");
            var d2 = new byte[self.data.Length + oth.data.Length];
            self.data.CopyTo(d2, 0);
            oth.data.CopyTo(d2, self.data.Length);
            return new ByteString(d2);
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
