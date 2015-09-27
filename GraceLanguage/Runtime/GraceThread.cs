using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>
    /// Grace object representing a concurrent thread of execution,
    /// which runs a Grace block in a separate interpreter.
    /// </summary>
    public class GraceThread : GraceObject
    {
        private Thread thread;
        private Interpreter interpreter;
        private GraceBlock block;
        private GraceExceptionPacket exception;

        /// <param name="ctx">Current interpreter</param>
        /// <param name="blk">Grace block to apply in the new thread</param>
        public GraceThread(EvaluationContext ctx, GraceBlock blk)
        {
            AddMethod("join", new DelegateMethod0(mJoin));
            interpreter = ((Interpreter)ctx).Copy();
            interpreter.NestRequest("<new thread>", 0, "<new thread>");
            block = blk;
            thread = new Thread(start);
            thread.Start();
        }

        private void start()
        {
            var apply = MethodRequest.Nullary("apply");
            try
            {
                block.Request(interpreter, apply);
            }
            catch (GraceExceptionPacketException gepe)
            {
                exception = gepe.ExceptionPacket;
                ErrorReporting.WriteException(exception);
            }
        }

        private GraceObject mJoin()
        {
            thread.Join();
            if (exception != null)
                throw new GraceExceptionPacketException(exception);
            return GraceObject.Done;
        }
    }
}

