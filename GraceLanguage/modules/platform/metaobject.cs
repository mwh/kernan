using System.Collections.Generic;
using System.IO;
using Grace.Parsing;
using Grace.Execution;
using Grace.Runtime;

namespace Grace.Platform.ArtificialObject
{
    [ModuleEntryPoint]
    public class MetaobjectModule : GraceObject
    {

        public static GraceObject Instantiate(
            EvaluationContext ctx)
        {
            return new MetaobjectModule();
        }

        public MetaobjectModule() : base("platform/metaobject")
        {
            AddMethod("for(_)", new DelegateMethod1(mFor));
            AddMethod("evaluate(_)", new DelegateMethod1Ctx(mEvaluate));
            AddMethod("unproxy(_)", new DelegateMethod1(mUnproxy));
        }

        private GraceObject mFor(GraceObject arg)
        {
            return new Metaobject(arg);
        }

        private GraceObject mEvaluate(EvaluationContext ctx, GraceObject arg)
        {
            Node n = arg as Node;
            return n.Evaluate(ctx);
        }

        private GraceObject mUnproxy(GraceObject arg)
        {
            GraceObjectProxy p = arg as GraceObjectProxy;
            if (p != null)
            {
                var go = p.Object as GraceObject;
                if (go != null)
                    return go;
                return p;
            }
            return arg;
        }

    }

    class Metaobject : GraceObject
    {

        private GraceObject obj;

        public Metaobject(GraceObject obj)
        {
            this.obj = obj;
            AddMethod("addMethod(_,_)", new DelegateMethodReq(mAddMethod));
            AddMethod("request(_)", new DelegateMethod1Ctx(mRequest));
            AddMethod("size", new DelegateMethod0(mSize));
        }

        private GraceObject mAddMethod(EvaluationContext ctx, MethodRequest req)
        {
            var s = req[0].Arguments[0] as GraceString;
            var blk = req[0].Arguments[1] as GraceBlock;
            var meth = new BlockMethodReq(obj, blk);
            obj.AddMethod(s.Value, meth);
            return GraceObject.Done;
        }

        private GraceObject mRequest(EvaluationContext ctx, GraceObject oReq)
        {
            var gop = oReq as GraceObjectProxy;
            var req = gop.Object as MethodRequest;
            return obj.Request(ctx, req);
        }

        private Method createMethod(string name, GraceBlock code)
        {
            return null;
        }

        private GraceObject mSize()
        {
            return GraceNumber.Create(0);
        }

        private class BlockMethodReq : Method
        {
            readonly NativeMethodReq method;
            readonly GraceBlock block;
            readonly GraceObject obj;

            private GraceObject handler(EvaluationContext ctx, MethodRequest req)
            {
                var r2 = new MethodRequest();
                var rp = new RequestPart("apply", new List<GraceObject>(),
                    new List<GraceObject> {
                        obj, new GraceObjectProxy(req)
                    });
                r2.AddPart(rp);
                return block.Request(ctx, r2);
            }

            /// <param name="rm">Native method to wrap</param>
            public BlockMethodReq(GraceObject receiver, GraceBlock block)
                : base(null, null)
            {
                obj = receiver;
                this.block = block;
                method = this.handler;
            }

            /// <inheritdoc/>
            public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
            {
                MethodHelper.CheckNoInherits(ctx, req);
                return method(ctx, req);
            }
        }

    }
}
