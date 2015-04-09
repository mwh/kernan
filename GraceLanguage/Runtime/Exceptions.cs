using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{

    public class GraceExceptionPacketException : Exception
    {
        public GraceExceptionPacket ExceptionPacket { get; private set; }

        public GraceExceptionPacketException(GraceExceptionPacket gep)
        {
            ExceptionPacket = gep;
        }
    }

    public class GraceExceptionPacket : GraceObject
    {
        private string message;

        public string KindName { get; set; }

        private List<string> stackTrace;

        public string Description
        {
            get
            {
                return KindName + ": " + message;
            }
        }

        public List<string> StackTrace
        {
            get
            {
                return stackTrace;
            }
        }

        public GraceExceptionPacket(string kind, string message)
        {
            this.message = message;
            KindName = kind;
            initialise();
        }

        public GraceExceptionPacket(string kind, string message,
                List<string> stackTrace)
        {
            this.message = message;
            KindName = kind;
            this.stackTrace = stackTrace;
            initialise();
        }

        public GraceExceptionPacket(GraceExceptionKind kind, string message)
        {
            this.message = message;
            KindName = kind.Name;
            initialise();
        }

        public GraceExceptionPacket(GraceExceptionKind kind, string message,
                List<string> stackTrace)
        {
            this.message = message;
            KindName = kind.Name;
            this.stackTrace = stackTrace;
            initialise();
        }

        public GraceExceptionPacket(string message)
        {
            this.message = message;
            initialise();
        }

        private void initialise()
        {
            AddMethod("asString",
                new DelegateMethodNode0(
                    new NativeMethod0(this.AsString)));
            AddMethod("message",
                new DelegateMethodNode0(
                    new NativeMethod0(this.Message)));
        }

        new public GraceObject AsString()
        {
            return GraceString.Create(KindName + ": " + message);
        }

        public GraceObject Message()
        {
            return GraceString.Create(message);
        }

        public static void Throw(string message)
        {
            var gep = new GraceExceptionPacket(message);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        public static void Throw(GraceExceptionKind kind, string message,
                List<string> stackTrace)
        {
            var gep = new GraceExceptionPacket(kind, message, stackTrace);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        public static void Throw(GraceExceptionKind kind, string message)
        {
            var gep = new GraceExceptionPacket(kind, message);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        public static void Throw(string kind, string message)
        {
            var gep = new GraceExceptionPacket(kind, message);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        public static void Throw(string kind, string message,
                List<string> stackTrace)
        {
            var gep = new GraceExceptionPacket(kind, message, stackTrace);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }
    }

    public class GraceExceptionKind : GraceObject
    {
        public string Name { get; private set; }
        public GraceExceptionKind Parent { get; private set; }
        public HashSet<string> Children;

        public GraceExceptionKind(string name)
        {
            Name = name;
            initialise();
        }

        public GraceExceptionKind(GraceExceptionKind parent, string name)
        {
            Parent = parent;
            Name = name;
            initialise();
        }

        private void initialise()
        {
            Children = new HashSet<string>();
            Children.Add(Name);
            AddMethod("asString",
                new DelegateMethodNode0(
                    new NativeMethod0(this.AsString)));
            AddMethod("match",
                new DelegateMethodNodeReq(
                    new NativeMethodReq(this.Match)));
            AddMethod("raise",
                new DelegateMethodNode1Ctx(
                    new NativeMethod1Ctx(this.Raise)));
            AddMethod("refine",
                new DelegateMethodNode1Ctx(
                    new NativeMethod1Ctx(this.Refine)));
        }

        public GraceObject AsString(EvaluationContext ctx)
        {
            return GraceString.Create(Name);
        }

        public GraceObject Match(EvaluationContext ctx, MethodRequest req)
        {
            var target = req[0].Arguments[0];
            var gep = target as GraceExceptionPacket;
            if (gep == null)
            {
                return Matching.FailedMatch(ctx, target);
            }
            if (Parent == null || Children.Contains(gep.KindName))
                return Matching.SuccessfulMatch(ctx, target);
            return Matching.FailedMatch(ctx, target);
        }

        public GraceObject Refine(EvaluationContext ctx, GraceObject name)
        {
            var asGraceString = name.Request(ctx,
                    MethodRequest.Nullary("asString")) as GraceString;
            var nameStr = asGraceString.Value;
            AddDescendant(nameStr);
            return new GraceExceptionKind(this, nameStr);
        }

        private void AddDescendant(string nameStr)
        {
            Children.Add(nameStr);
            if (Parent != null)
                Parent.AddDescendant(nameStr);
        }

        public GraceObject Raise(EvaluationContext ctx, GraceObject message)
        {
            var msg = "<<No message>>";
            var asGraceString = message.Request(ctx,
                    MethodRequest.Nullary("asString")) as GraceString;
            if (asGraceString != null)
            {
                msg = asGraceString.Value;
            }
            GraceExceptionPacket.Throw(this, msg, ctx.GetStackTrace());
            return GraceObject.Done;
        }
    }

}
