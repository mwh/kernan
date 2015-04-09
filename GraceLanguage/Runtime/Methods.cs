using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{
    public delegate GraceObject NativeMethodReq(EvaluationContext ctx,
        MethodRequest req);
    public delegate GraceObject NativeMethod0();
    public delegate GraceObject NativeMethod1(GraceObject other);
    public delegate GraceObject NativeMethod1Ctx(EvaluationContext ctx,
            GraceObject other);
    public delegate GraceObject NativeMethodReceiver1Ctx(EvaluationContext ctx,
            GraceObject self,
            GraceObject other);

    public class DelegateMethodNode1Ctx : MethodNode
    {
        readonly NativeMethod1Ctx method;
        public DelegateMethodNode1Ctx(NativeMethod1Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            GraceObject arg = req[0].Arguments[0];
            return method(ctx, arg);
        }
    }

    public class DelegateMethodNodeReceiver1Ctx : MethodNode
    {
        readonly NativeMethodReceiver1Ctx method;
        public DelegateMethodNodeReceiver1Ctx(NativeMethodReceiver1Ctx rm)
            : base(null, null)
        {
            method = rm;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            GraceObject arg = req[0].Arguments[0];
            return method(ctx, self, arg);
        }
    }

    public class DelegateMethodNode1 : MethodNode
    {
        readonly NativeMethod1 method;
        public DelegateMethodNode1(NativeMethod1 rm)
            : base(null, null)
        {
            method = rm;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            GraceObject arg = req[0].Arguments[0];
            return method(arg);
        }
    }

    public class DelegateMethodNode0 : MethodNode
    {
        readonly NativeMethod0 method;
        public DelegateMethodNode0(NativeMethod0 rm)
            : base(null, null)
        {
            method = rm;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            return method();
        }
    }

    public class DelegateMethodNodeReq : MethodNode
    {
        readonly NativeMethodReq method;
        public DelegateMethodNodeReq(NativeMethodReq rm)
            : base(null, null)
        {
            method = rm;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            return method(ctx, req);
        }
    }

    public class ConstantMethodNode : MethodNode
    {
        private GraceObject returnValue;
        public ConstantMethodNode(GraceObject ret)
            : base(null, null)
        {
            returnValue = ret;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            return returnValue;
        }
    }

    public class MethodScope : LocalScope
    {

        public MethodScope(string name) : base(name) { }

        public void Return(GraceObject val)
        {
            throw new ReturnException(this, val);
        }
    }

    public class ReturnException : Exception
    {

        private MethodScope scope;
        public GraceObject Value { get; set; }

        public ReturnException(MethodScope scope, GraceObject val)
        {
            this.scope = scope;
            Value = val;
        }

        public bool IsFromScope(MethodScope s)
        {
            return s == scope;
        }
    }
}
