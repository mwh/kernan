using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using Grace;
using Grace.Parsing;
using Grace.Execution;
using Grace.Runtime;
using Grace.Utility;

namespace WebSocketModules
{
    [ModuleEntryPoint]
    public class Serve : GraceObject
    {

        private Thread thread;
        private Interpreter interp;

        public static GraceObject Instantiate(
            EvaluationContext ctx)
        {
            return new Serve(ctx);
        }

        private Serve(EvaluationContext ctx) : base("websocket/serve")
        {
            AddMethod("end", new DelegateMethod0(mEnd));
            interp = (Interpreter)ctx;
            thread = WebSocketEndpoint.WSServeThread(interp);
        }

        private GraceObject mEnd()
        {
            ((WSOutputSink)interp.RPCSink).Stream.Stop();
            interp.RPCSink.SendEvent("execution-complete", "serve");
            interp.RPCSink.Stop();
            thread.Join();
            return GraceObject.Done;
        }
    }

}
