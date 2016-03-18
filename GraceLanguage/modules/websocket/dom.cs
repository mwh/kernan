using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using Grace;
using Grace.Parsing;
using Grace.Execution;
using Grace.Runtime;

namespace WebSocketModules
{
    [ModuleEntryPoint]
    public class Dom : GraceObject
    {

        private static object[] noArgs = new object[0];

        private bool endRun;
        private RPCSink sink;

        public static GraceObject Instantiate(
            EvaluationContext ctx)
        {
            return new Dom(ctx);
        }

        private Dom(EvaluationContext ctx) : base("websocket/dom")
        {
            var interp = (Interpreter)ctx;
            sink = interp.RPCSink;
            var gfo = sink.SendRPC(-1, "init", noArgs) as GraceForeignObject;
            var win = new DomObject((int)gfo.IdentifyingData, sink);
            AddMethod("window", new ConstantMethod(win));
            //AddMethod("document", new ConstantMethod(new DomObject(1)));
            AddMethod("document", new DelegateMethod0Ctx(mDocument));
            AddMethod("sleep(_)", new DelegateMethod1(mSleep));
            AddMethod("run", new DelegateMethod0Ctx(mRun));
            AddMethod("yield", new DelegateMethod0Ctx(mYield));
            AddMethod("end", new DelegateMethod0Ctx(mEnd));
            AddMethod("ignoreResultOf(_) on(_)",
                    new DelegateMethodReq(mIgnoreResultOf_On));
        }

        private GraceObject mDocument(EvaluationContext ctx)
        {
            var ret = sink.SendRPC(0, "document", noArgs);
            var gfo = ret as GraceForeignObject;
            if (gfo == null)
                return ret;
            int okey = (int)gfo.IdentifyingData;
            return new DomObject(okey, sink);
        }

        private GraceObject mSleep(GraceObject arg)
        {
            var n = arg.FindNativeParent<GraceNumber>();
            if (n == null)
                return GraceObject.Done;
            Thread.Sleep((int)(n.Double));
            return GraceObject.Done;
        }

        private GraceObject mYield(EvaluationContext ctx)
        {
            GraceObject block;
            object[] args;
            while (sink.AwaitRemoteCallback(0, out block, out args))
                processCallback(ctx, block, args);
            return GraceObject.Done;
        }

        private GraceObject mRun(EvaluationContext ctx)
        {
            endRun = false;
            GraceObject block;
            object[] args;
            while (true)
            {
                if (sink.AwaitRemoteCallback(500, out block, out args))
                    processCallback(ctx, block, args);
                if (endRun || sink.Stopped)
                    break;
            }
            return GraceObject.Done;
        }

        private GraceObject mEnd(EvaluationContext ctx)
        {
            endRun = true;
            return GraceObject.Done;
        }

        private GraceObject mIgnoreResultOf_On(
                EvaluationContext ctx,
                MethodRequest req)
        {
            var obj = req[1].Arguments[0] as DomObject;
            if (obj == null)
                return GraceObject.Done;
            var str = req[0].Arguments[0].FindNativeParent<GraceString>().Value;
            obj.Ignore(str);
            return GraceObject.Done;
        }

        private void processCallback(EvaluationContext ctx,
                GraceObject block, object[] args)
        {
            var lArgs = new List<GraceObject>();
            foreach (var arg in args)
            {
                var gfo = arg as GraceForeignObject;
                if (gfo != null)
                {
                    lArgs.Add(new DomObject((int)gfo.IdentifyingData, sink));
                }
                else
                {
                    lArgs.Add((GraceObject)arg);
                }
            }
            var req = MethodRequest.WithArgs("apply", lArgs);
            block.Request(ctx, req);
        }

    }

    class DomObject : GraceObject
    {

        private int key;
        private RPCSink sink;

        private HashSet<string> ignoredMethodResults;

        public DomObject(int k, RPCSink s)
        {
            key = k;
            sink = s;
        }

        public void Ignore(string m)
        {
            if (ignoredMethodResults == null)
                ignoredMethodResults = new HashSet<string>();
            ignoredMethodResults.Add(m);
        }

        public override bool RespondsTo(MethodRequest req)
        {
            return true;
        }

        public override GraceObject Request(EvaluationContext ctx,
                MethodRequest req, GraceObject receiver)
        {
            var part = req[0];
            var isAssign = false;
            if (req.Count > 1)
            {
                if (req[1].Name == ":=(_)")
                {
                    part = req[1];
                    isAssign = true;
                }
            }
            object[] args = new object[part.Arguments.Count];
            int i = 0;
            foreach (var a in part.Arguments)
            {
                var d = a as DomObject;
                var s = a as GraceString;
                if (d != null)
                    args[i++] = new int[1] { d.key };
                else if (s != null)
                    args[i++] = s.Value;
                else
                    args[i++] = a;
            }
            // Request names include arities, which
            // the DOM doesn't understand.
            var name = req.Name;
            var idx = name.IndexOf('(');
            if (idx != -1)
                name = name.Substring(0, idx);
            if (isAssign)
                return sink.SendRPCNoResult(key, name, args);
            if (ignoredMethodResults != null)
            {
                if (ignoredMethodResults.Contains(name))
                    return sink.SendRPCNoResult(key, name, args);
            }
            var ret = sink.SendRPC(key, name, args);
            var gfo = ret as GraceForeignObject;
            if (gfo == null)
                return ret;
            int okey = (int)gfo.IdentifyingData;
            return new DomObject(okey, sink);
        }

    }
}
