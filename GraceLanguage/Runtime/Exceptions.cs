using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{

    /// <summary>Native exception wrapping a Grace exception packet</summary>
    /// <remarks>Because a GraceObject cannot be thrown, this wrapper
    /// is required in order to use the native exception-handling mechanism
    /// to implement Grace exceptions.</remarks>
    public class GraceExceptionPacketException : Exception
    {
        /// <summary>Grace exception this object represents</summary>
        public GraceExceptionPacket ExceptionPacket { get; private set; }

        /// <param name="gep">Grace exception to be wrapped</param>
        public GraceExceptionPacketException(GraceExceptionPacket gep)
        {
            ExceptionPacket = gep;
        }
    }

    /// <summary>Grace exception object</summary>
    public class GraceExceptionPacket : GraceObject
    {
        private string message;

        /// <summary>Name of the ExceptionKind from which this
        /// exception packet arose</summary>
        public string KindName { get; set; }

        private List<string> stackTrace;

        /// <summary>Human-readable description of this exception
        /// packet</summary>
        /// <value>This property gives the exception kind, a colon,
        /// and the message the exception packet was created using
        /// </value>
        public string Description
        {
            get
            {
                return KindName + ": " + message;
            }
        }

        /// <summary>Human-readable stack trace</summary>
        /// <value>This property gives the stack trace at the point
        /// where the exception packet was created, from the field
        /// stackTrace.
        /// </value>
        public List<string> StackTrace
        {
            get
            {
                return stackTrace;
            }
        }

        /// <param name="kind">ExceptionKind of this packet</param>
        /// <param name="message">Human-readable message</param>
        public GraceExceptionPacket(string kind, string message)
        {
            this.message = message;
            KindName = kind;
            initialise();
        }

        /// <param name="kind">ExceptionKind of this packet</param>
        /// <param name="message">Human-readable message</param>
        /// <param name="stackTrace">List of human-readable strings
        /// describing the stack at the point this exception was
        /// created</param>
        public GraceExceptionPacket(string kind, string message,
                List<string> stackTrace)
        {
            this.message = message;
            KindName = kind;
            this.stackTrace = stackTrace;
            initialise();
        }

        /// <param name="kind">ExceptionKind of this packet</param>
        /// <param name="message">Human-readable message</param>
        public GraceExceptionPacket(GraceExceptionKind kind, string message)
        {
            this.message = message;
            KindName = kind.Name;
            initialise();
        }

        /// <param name="kind">ExceptionKind of this packet</param>
        /// <param name="message">Human-readable message</param>
        /// <param name="stackTrace">List of human-readable strings
        /// describing the stack at the point this exception was
        /// created</param>
        public GraceExceptionPacket(GraceExceptionKind kind, string message,
                List<string> stackTrace)
        {
            this.message = message;
            KindName = kind.Name;
            this.stackTrace = stackTrace;
            initialise();
        }

        /// <param name="message">Human-readable message</param>
        public GraceExceptionPacket(string message)
        {
            this.message = message;
            initialise();
        }

        /// <summary>Shared initialisation code for multiple
        /// constructors</summary>
        private void initialise()
        {
            AddMethod("asString",
                new DelegateMethodReceiver0Ctx(AsString));
            AddMethod("message",
                new DelegateMethod0(
                    new NativeMethod0(this.Message)));
        }

        /// <summary>Native method for Grace asString</summary>
        new public GraceObject AsString(EvaluationContext ctx,
                GraceObject self)
        {
            return GraceString.Create(KindName + ": " + message);
        }

        /// <summary>Native method for Grace message</summary>
        public GraceObject Message()
        {
            return GraceString.Create(message);
        }

        /// <summary>Create a new exception with a given message
        /// and throw a wrapped version of it</summary>
        /// <param name="message">Human-readable message for exception</param>
        public static void Throw(string message)
        {
            var gep = new GraceExceptionPacket(message);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        /// <summary>Create a new exception with a given message, kind,
        /// and stack trace, and throw a wrapped version of it</summary>
        /// <param name="kind">Exception kind</param>
        /// <param name="message">Human-readable message for exception</param>
        /// <param name="stackTrace">Human-readable stack trace</param>
        public static void Throw(GraceExceptionKind kind, string message,
                List<string> stackTrace)
        {
            var gep = new GraceExceptionPacket(kind, message, stackTrace);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        /// <summary>Create a new exception with a given message, kind,
        /// and stack trace, and throw a wrapped version of it</summary>
        /// <param name="kind">Exception kind</param>
        /// <param name="message">Human-readable message for exception</param>
        public static void Throw(GraceExceptionKind kind, string message)
        {
            var gep = new GraceExceptionPacket(kind, message);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        /// <summary>Create a new exception with a given message, kind,
        /// and stack trace, and throw a wrapped version of it</summary>
        /// <param name="kind">Exception kind</param>
        /// <param name="message">Human-readable message for exception</param>
        public static void Throw(string kind, string message)
        {
            var gep = new GraceExceptionPacket(kind, message);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }

        /// <summary>Create a new exception with a given message, kind,
        /// and stack trace, and throw a wrapped version of it</summary>
        /// <param name="kind">Exception kind</param>
        /// <param name="message">Human-readable message for exception</param>
        /// <param name="stackTrace">Human-readable stack trace</param>
        public static void Throw(string kind, string message,
                List<string> stackTrace)
        {
            var gep = new GraceExceptionPacket(kind, message, stackTrace);
            var gepe = new GraceExceptionPacketException(gep);
            throw gepe;
        }
    }

    /// <summary>Grace exception kind</summary>
    public class GraceExceptionKind : GraceObject
    {
        /// <summary>Name of this exception kind</summary>
        public string Name { get; private set; }
        /// <summary>Parent of this exception kind</summary>
        public GraceExceptionKind Parent { get; private set; }
        /// <summary>Descendants of this exception kind</summary>
        public HashSet<string> Children;

        /// <param name="name">Name of this exception kind</param>
        public GraceExceptionKind(string name)
        {
            Name = name;
            initialise();
        }

        /// <param name="parent">Parent of this exception kind</param>
        /// <param name="name">Name of this exception kind</param>
        public GraceExceptionKind(GraceExceptionKind parent, string name)
        {
            Parent = parent;
            Name = name;
            initialise();
        }

        /// <summary>Shared code used by multiple constructors</summary>
        private void initialise()
        {
            Children = new HashSet<string>();
            Children.Add(Name);
            AddMethod("asString",
                new DelegateMethodReceiver0Ctx(
                    new NativeMethodReceiver0Ctx(this.AsString)));
            AddMethod("match",
                new DelegateMethodReq(
                    new NativeMethodReq(this.Match)));
            AddMethod("raise",
                new DelegateMethod1Ctx(
                    new NativeMethod1Ctx(this.Raise)));
            AddMethod("refine",
                new DelegateMethod1Ctx(
                    new NativeMethod1Ctx(this.Refine)));
        }

        /// <inheritdoc/>
        public GraceObject AsString(EvaluationContext ctx)
        {
            return GraceString.Create(Name);
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request that resolved to this method</param>
        public GraceObject Match(EvaluationContext ctx, MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 1);
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

        /// <summary>Native method for Grace refine</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="name">Name of the sub-exception</param>
        public GraceObject Refine(EvaluationContext ctx, GraceObject name)
        {
            var asGraceString = name.Request(ctx,
                    MethodRequest.Nullary("asString"))
                .FindNativeParent<GraceString>();
            var nameStr = asGraceString.Value;
            AddDescendant(nameStr);
            return new GraceExceptionKind(this, nameStr);
        }

        /// <summary>Add a new descendant for matching as a parent of</summary>
        /// <param name="nameStr">Name of the descendant</param>
        private void AddDescendant(string nameStr)
        {
            Children.Add(nameStr);
            if (Parent != null)
                Parent.AddDescendant(nameStr);
        }

        /// <summary>Native method for Grace raise</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="message">Message string</param>
        public GraceObject Raise(EvaluationContext ctx, GraceObject message)
        {
            var msg = "<<No message>>";
            var asGraceString = message.Request(ctx,
                    MethodRequest.Nullary("asString"))
                .FindNativeParent<GraceString>();
            if (asGraceString != null)
            {
                msg = asGraceString.Value;
            }
            GraceExceptionPacket.Throw(this, msg, ctx.GetStackTrace());
            return GraceObject.Done;
        }
    }

}
