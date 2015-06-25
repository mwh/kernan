using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>A native method accepting an interpreter and raw
    /// request</summary>
    public delegate GraceObject NativeMethodReq(EvaluationContext ctx,
        MethodRequest req);

    /// <summary>A native method accepting no arguments</summary>
    public delegate GraceObject NativeMethod0();

    /// <summary>A native method accepting no and taking a context</summary>
    public delegate GraceObject NativeMethod0Ctx(EvaluationContext ctx);

    /// <summary>A native method accepting a single Grace argument</summary>
    public delegate GraceObject NativeMethod1(GraceObject other);

    /// <summary>A native method accepting a single Grace argument
    /// and an interpreter</summary>
    public delegate GraceObject NativeMethod1Ctx(EvaluationContext ctx,
            GraceObject other);

    /// <summary>A native method accepting a single Grace argument,
    /// a receiver object, and an interpreter</summary>
    public delegate GraceObject NativeMethodReceiver1Ctx(EvaluationContext ctx,
            GraceObject self,
            GraceObject other);

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument and an interpreter</summary>
    public class DelegateMethodNode0Ctx : MethodNode
    {
        readonly NativeMethod0Ctx method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodNode0Ctx(NativeMethod0Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self, MethodRequest req)
        {
            return method(ctx);
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument and an interpreter</summary>
    public class DelegateMethodNode1Ctx : MethodNode
    {
        readonly NativeMethod1Ctx method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodNode1Ctx(NativeMethod1Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            GraceObject arg = req[0].Arguments[0];
            return method(ctx, arg);
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument, the receiver, and an interpreter</summary>
    public class DelegateMethodNodeReceiver1Ctx : MethodNode
    {
        readonly NativeMethodReceiver1Ctx method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodNodeReceiver1Ctx(NativeMethodReceiver1Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            GraceObject arg = req[0].Arguments[0];
            return method(ctx, self, arg);
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument</summary>
    public class DelegateMethodNode1 : MethodNode
    {
        readonly NativeMethod1 method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodNode1(NativeMethod1 rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            GraceObject arg = req[0].Arguments[0];
            return method(arg);
        }
    }


    /// <summary>A Grace method wrapping a native method accepting
    /// no arguments</summary>
    public class DelegateMethodNode0 : MethodNode
    {
        readonly NativeMethod0 method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodNode0(NativeMethod0 rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            return method();
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// an interpreter and the raw method request</summary>
    public class DelegateMethodNodeReq : MethodNode
    {
        readonly NativeMethodReq method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodNodeReq(NativeMethodReq rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            return method(ctx, req);
        }
    }


    /// <summary>A Grace method giving a simple constant value</summary>
    public class ConstantMethodNode : MethodNode
    {
        private GraceObject returnValue;

        /// <param name="ret">Value to return</param>
        public ConstantMethodNode(GraceObject ret)
            : base(null, null)
        {
            returnValue = ret;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            return returnValue;
        }
    }

    /// <summary>A method body scope</summary>
    public class MethodScope : LocalScope
    {

        private bool completed;

        /// <param name="name">The name of this method for debugging</param>
        public MethodScope(string name) : base(name) { }

        /// <summary>Return a value from this method</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="val">Return value</param>
        /// <param name="ret">Return statement</param>
        /// <exception cref="ReturnException">Always thrown to indicate a
        /// return from a Grace method</exception>
        public void Return(EvaluationContext ctx, GraceObject val,
                Node ret)
        {
            if (completed)
            {
                // Put the return statement into the call stack
                // so that its location is shown to the user, even
                // though it isn't actually a method request.
                ctx.NestRequest(ret.Location.Module,
                        ret.Location.line, "return");
                ErrorReporting.RaiseError(ctx, "R2009",
                    new Dictionary<string, string> {
                        { "method", Name }
                    },
                    "IllegalReturnError: Method «" + Name
                        + "» already returned");
            }
            completed = true;
            throw new ReturnException(this, val);
        }

        /// <summary>
        /// Signal that this method has completed, and may no
        /// longer be returned from.
        /// </summary>
        public void Complete()
        {
            completed = true;
        }
    }

    /// <summary>Represents an explicit return from a Grace method</summary>
    public class ReturnException : Exception
    {

        private MethodScope scope;

        /// <summary>The return value</summary>
        public GraceObject Value { get; set; }

        /// <param name="scope">The method body scope from which this
        /// return originated</param>
        /// <param name="val">The return value</param>
        public ReturnException(MethodScope scope, GraceObject val)
        {
            this.scope = scope;
            Value = val;
        }

        /// <summary>Determines whether this ReturnException came from
        /// a given scope</summary>
        /// <param name="s">Scope to compare against</param>
        /// <returns>True if this ReturnException came from the scope
        /// given in <paramref name="s"/>; false otherwise.</returns>
        public bool IsFromScope(MethodScope s)
        {
            return s == scope;
        }
    }
}
