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

    /// <summary>
    /// A native method given a receiver object, and an interpreter
    /// </summary>
    public delegate GraceObject NativeMethodReceiver0Ctx(EvaluationContext ctx,
            GraceObject self);

    /// <summary>A native method accepting a single Grace argument,
    /// a receiver object, and an interpreter</summary>
    public delegate GraceObject NativeMethodReceiver1Ctx(EvaluationContext ctx,
            GraceObject self,
            GraceObject other);

    /// <summary>
    /// A native method that can be inherited, with no arguments.
    /// </summary>
    public delegate GraceObject NativeMethodInheritable(
            EvaluationContext ctx,
            MethodRequest req,
            GraceObject self);

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// the interpreter, and the request.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public delegate GraceObject NativeMethodTyped<T>(
            EvaluationContext ctx,
            MethodRequest req,
            T self);

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// and no other arguments.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public delegate GraceObject NativeMethodTyped0<T>(T self);

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// and a single argument.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public delegate GraceObject NativeMethodTyped1<T>(T self,
            GraceObject other);

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// and a single argument, also given the context.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public delegate GraceObject NativeMethodTyped1Ctx<T>(
            EvaluationContext ctx,
            T self,
            GraceObject other);

    /// <summary>
    /// A dynamic method that may be attached to an object and requested.
    /// </summary>
    public class Method
    {
        private MethodNode code;
        private Interpreter.ScopeMemo lexicalScope;

        /// <summary>
        ///  True if this method is confidential and may only be requested
        ///  on self.
        /// </summary>
        public bool Confidential { get; set; }

        /// <summary>
        ///  True if this method is abstract and may not be requested.
        /// </summary>
        public bool Abstract { get; set; }

        /// <summary>
        ///  True if this method is a conflict and may not be requested.
        /// </summary>
        public bool Conflict { get; set; }

        public GraceObject Type { get; set; } = null;

        /// <summary>
        /// Create an ordinary method node.
        /// </summary>
        /// <param name="c">
        /// Method node with code to run for this method
        /// </param>
        /// <param name="scope">
        /// Captured lexical scope
        /// </param>
        public Method(MethodNode c, Interpreter.ScopeMemo scope)
        {
            code = c;
            if (c != null)
            {
                Confidential = c.Confidential;
                Abstract = c.Abstract;
            }
            lexicalScope = scope;
        }

        /// <summary>
        /// A default unconfigured method for inheritors.
        /// </summary>
        protected Method() {}

        /// <summary>
        /// Create a copy of this method with the same code
        /// and scope associated.
        /// </summary>
        public Method Copy()
        {
            var m = new Method(code, lexicalScope);
            m.Confidential = Confidential;
            m.Abstract = Abstract;
            return m;
        }

        /// <summary>
        /// Respond to a request of this method.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="receiver">Self-binding of this request</param>
        /// <param name="req">Method request being responded to</param>
        public virtual GraceObject Respond(EvaluationContext ctx,
            GraceObject receiver,
            MethodRequest req)
        {
            checkAccessibility(ctx, req);
            if (lexicalScope != null)
                ctx.Remember(lexicalScope);
            var ret = code.Respond(ctx, receiver, req);
            if (lexicalScope != null)
                ctx.Forget(lexicalScope);
            return ret;
        }

        /// <summary>Confirm that this method can be accessed through
        /// the given request in this context</summary>
        /// <remarks>If this method is confidential and the request is
        /// not an interior one with privileged access, this method
        /// will raise a Grace exception reporting an accessibility
        /// violation.</remarks>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request to check</param>
        protected virtual void checkAccessibility(EvaluationContext ctx,
                MethodRequest req)
        {
            if (Conflict)
            {
                ErrorReporting.RaiseError(ctx, "R2022",
                        new Dictionary<string, string>() {
                            { "method", req.Name }
                        },
                        "InheritanceError: Method ${method} is a conflict."
                );
            }
            if (Abstract)
            {
                ErrorReporting.RaiseError(ctx, "R2021",
                        new Dictionary<string, string>() {
                            { "method", req.Name }
                        },
                        "InheritanceError: Method ${method} is abstract."
                );
            }
            if (Confidential && !req.IsInterior)
            {
                ErrorReporting.RaiseError(ctx, "R2003",
                        new Dictionary<string, string>() {
                            { "method", req.Name }
                        },
                        "AccessibilityError: Method ${method} is confidential"
                );
            }
        }

        /// <summary>
        /// A singleton abstract method.
        /// </summary>
        public static readonly Method AbstractMethod = new Method();

        /// <summary>
        /// A singleton conflicted method.
        /// </summary>
        public static readonly Method ConflictMethod = new Method();

        static Method()
        {
            AbstractMethod.Abstract = true;
            ConflictMethod.Conflict = true;
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument and an interpreter</summary>
    public class DelegateMethod0Ctx : Method
    {
        readonly NativeMethod0Ctx method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethod0Ctx(NativeMethod0Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckNoInherits(ctx, req);
            return method(ctx);
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument and an interpreter</summary>
    public class DelegateMethod1Ctx : Method
    {
        readonly NativeMethod1Ctx method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethod1Ctx(NativeMethod1Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 1);
            GraceObject arg = req[0].Arguments[0];
            return method(ctx, arg);
        }
    }

    /// <summary>
    /// A Grace method wrapping a native method to be given
    /// the receiver and an interpreter.
    /// </summary>
    public class DelegateMethodReceiver0Ctx : Method
    {
        readonly NativeMethodReceiver0Ctx method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodReceiver0Ctx(NativeMethodReceiver0Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 0);
            return method(ctx, self);
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument, the receiver, and an interpreter</summary>
    public class DelegateMethodReceiver1Ctx : Method
    {
        readonly NativeMethodReceiver1Ctx method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodReceiver1Ctx(NativeMethodReceiver1Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 1);
            GraceObject arg = req[0].Arguments[0];
            return method(ctx, self, arg);
        }
    }

    /// <summary>
    /// A Grace method that can be inherited from.
    /// </summary>
    public class DelegateMethodInheritable : Method
    {
        readonly NativeMethodInheritable method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodInheritable(NativeMethodInheritable rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self,
                MethodRequest req)
        {
            return method(ctx, req, self);
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// a single Grace argument</summary>
    public class DelegateMethod1 : Method
    {
        readonly NativeMethod1 method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethod1(NativeMethod1 rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 1);
            GraceObject arg = req[0].Arguments[0];
            return method(arg);
        }
    }


    /// <summary>A Grace method wrapping a native method accepting
    /// no arguments</summary>
    public class DelegateMethod0 : Method
    {
        readonly NativeMethod0 method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethod0(NativeMethod0 rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 0);
            return method();
        }
    }

    /// <summary>A Grace method wrapping a native method accepting
    /// an interpreter and the raw method request</summary>
    public class DelegateMethodReq : Method
    {
        readonly NativeMethodReq method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodReq(NativeMethodReq rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckNoInherits(ctx, req);
            return method(ctx, req);
        }
    }

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// the interpreter, and the request.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public class DelegateMethodTyped<T> : Method
        where T : GraceObject
    {
        readonly NativeMethodTyped<T> method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodTyped(NativeMethodTyped<T> rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self,
                MethodRequest req)
        {
            MethodHelper.CheckNoInherits(ctx, req);
            return method(ctx, req, (T)self);
        }
    }

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// and no further arguments.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public class DelegateMethodTyped0<T> : Method
        where T : GraceObject
    {
        readonly NativeMethodTyped0<T> method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodTyped0(NativeMethodTyped0<T> rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self,
                MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 0);
            return method((T)self);
        }
    }

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// and a single arguments.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public class DelegateMethodTyped1<T> : Method
        where T : GraceObject
    {
        readonly NativeMethodTyped1<T> method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodTyped1(NativeMethodTyped1<T> rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self,
                MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 1);
            return method((T)self, req[0].Arguments[0]);
        }
    }

    /// <summary>
    /// A native method accepting the receiver, with its type,
    /// and a single arguments.
    /// </summary>
    /// <remarks>
    /// This type of method is reusable between instances.
    /// </remarks>
    public class DelegateMethodTyped1Ctx<T> : Method
        where T : GraceObject
    {
        readonly NativeMethodTyped1Ctx<T> method;

        /// <param name="rm">Native method to wrap</param>
        public DelegateMethodTyped1Ctx(NativeMethodTyped1Ctx<T> rm)
            : base(null, null)
        {
            method = rm;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self,
                MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 1);
            return method(ctx, (T)self, req[0].Arguments[0]);
        }
    }

    /// <summary>A Grace method giving a simple constant value</summary>
    public class ConstantMethod : Method
    {
        private GraceObject returnValue;

        /// <param name="ret">Value to return</param>
        public ConstantMethod(GraceObject ret)
            : base(null, null)
        {
            returnValue = ret;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            MethodHelper.CheckNoInherits(ctx, req);
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

    /// <summary>
    /// Encapsulates helper methods for implementations of
    /// native methods.
    /// </summary>
    public static class MethodHelper
    {
        /// <summary>
        /// Raise an error if too many or too few arguments
        /// provided to a part of a method.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Method request to check</param>
        /// <param name="counts">
        /// Number of arguments required in each part
        /// </param>
        public static void CheckArity(EvaluationContext ctx,
                MethodRequest req, params int[] counts)
        {
            for (int i = 0; i < counts.Length; i++)
            {
                var got = req[i].Arguments.Count;
                var want = counts[i];
                MethodNode.CheckArgCount(ctx, req.Name, req[i].Name,
                        want, false, got);
            }
            CheckNoInherits(ctx, req);
        }

        /// <summary>
        /// Raise an error if too many or too few arguments
        /// provided to a method.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Method request to check</param>
        /// <param name="want">
        /// Number of arguments this method wants.
        /// </param>
        public static void CheckArity(EvaluationContext ctx,
                MethodRequest req, int want)
        {
            var got = req[0].Arguments.Count;
            MethodNode.CheckArgCount(ctx, req.Name, req[0].Name,
                    want, false, got);
            CheckNoInherits(ctx, req);
        }

        /// <summary>
        /// Raise an error if the user is trying to inherit from
        /// this method.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Method request to check</param>
        public static void CheckNoInherits(EvaluationContext ctx,
                MethodRequest req)
        {
            if (req.IsInherits)
                ErrorReporting.RaiseError(ctx, "R2017",
                        new Dictionary<string,string> {
                            { "method", req.Name }
                        },
                        "InheritanceError: Invalid inheritance"
                    );
        }

        /// <summary>
        /// Return an arity-formatted version of a part name.
        /// </summary>
        /// <param name="name">Part name</param>
        /// <param name="count">Number of parameters/arguments</param>
        public static string ArityNamePart(string name, int count)
        {
            switch(count)
            {
                case 0:
                    return name;
                case 1:
                    return name + "(_)";
                case 2:
                    return name + "(_,_)";
                case 3:
                    return name + "(_,_,_)";
                default:
                    var ret = name + "(_";
                    for (int i = 1; i < count; i++)
                        ret += ",_";
                    return ret + ")";
            }
        }
    }

}
