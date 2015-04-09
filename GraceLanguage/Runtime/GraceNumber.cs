using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Globalization;
using Grace.Execution;

namespace Grace.Runtime
{

    class GraceNumber : GraceObject
    {
        public double Double
        {
            get;
            set;
        }
        private GraceNumber(double val)
        {
            Interpreter.Debug("made new number " + val);
            Double = val;
            AddMethod("==", new DelegateMethodNode1(new NativeMethod1(this.EqualsEquals)));
            AddMethod("!=", new DelegateMethodNode1(new NativeMethod1(this.NotEquals)));
            AddMethod("+", new DelegateMethodNode1(new NativeMethod1(this.Add)));
            AddMethod("*", new DelegateMethodNode1(new NativeMethod1(this.Multiply)));
            AddMethod("-", new DelegateMethodNode1(new NativeMethod1(this.Subtract)));
            AddMethod("/", new DelegateMethodNode1(new NativeMethod1(this.Divide)));
            AddMethod("%", new DelegateMethodNode1(new NativeMethod1(this.Modulus)));
            AddMethod("^", new DelegateMethodNode1(new NativeMethod1(this.Exponentiate)));
            AddMethod(">", new DelegateMethodNode1(new NativeMethod1(this.GreaterThan)));
            AddMethod(">=", new DelegateMethodNode1(new NativeMethod1(this.GreaterEqual)));
            AddMethod("<", new DelegateMethodNode1(new NativeMethod1(this.LessThan)));
            AddMethod("<=", new DelegateMethodNode1(new NativeMethod1(this.LessEqual)));
            AddMethod("asString", new DelegateMethodNode0(new NativeMethod0(this.AsString)));
            AddMethod("prefix-", new DelegateMethodNode0(new NativeMethod0(this.Negate)));
            AddMethod("..", new DelegateMethodNode1Ctx(new NativeMethod1Ctx(this.DotDot)));
            AddMethod("match", new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.Match)));
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
        }

        public int GetInt()
        {
            return (int)Double;
        }

        public override string ToString()
        {
            return "Number[" + Double + "]";
        }

        new public GraceObject EqualsEquals(GraceObject other)
        {
            var oth = other as GraceNumber;
            if (oth == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(this.Double == oth.Double);
        }

        new public GraceObject NotEquals(GraceObject other)
        {
            var oth = other as GraceNumber;
            if (oth == null)
                return GraceBoolean.True;
            return GraceBoolean.Create(this.Double != oth.Double);
        }

        public GraceObject DotDot(EvaluationContext ctx, GraceObject other)
        {
            MethodRequest req = new MethodRequest();
            RequestPart rpn = new RequestPart("_range", new List<GraceObject>(),
                    new List<GraceObject>() { this, other });
            req.AddPart(rpn);
            GraceObject rec = ctx.FindReceiver(req);
            return rec.Request(ctx, req);
        }

        public GraceObject Add(GraceObject other)
        {
            Interpreter.Debug("called +");
            GraceNumber oth = other as GraceNumber;
            return GraceNumber.Create(this.Double + oth.Double);
        }

        public GraceObject Multiply(GraceObject other)
        {
            Interpreter.Debug("called *");
            GraceNumber oth = other as GraceNumber;
            return GraceNumber.Create(this.Double * oth.Double);
        }

        public GraceObject Subtract(GraceObject other)
        {
            Interpreter.Debug("called -");
            GraceNumber oth = other as GraceNumber;
            return GraceNumber.Create(this.Double - oth.Double);
        }

        public GraceObject Divide(GraceObject other)
        {
            Interpreter.Debug("called /");
            GraceNumber oth = other as GraceNumber;
            return GraceNumber.Create(this.Double / oth.Double);
        }

        public GraceObject Modulus(GraceObject other)
        {
            Interpreter.Debug("called %");
            GraceNumber oth = other as GraceNumber;
            return GraceNumber.Create(this.Double % oth.Double);
        }

        public GraceObject Exponentiate(GraceObject other)
        {
            Interpreter.Debug("called ^");
            GraceNumber oth = other as GraceNumber;
            return GraceNumber.Create(Math.Pow(this.Double, oth.Double));
        }

        public GraceObject GreaterThan(GraceObject other)
        {
            GraceNumber oth = other as GraceNumber;
            return GraceBoolean.Create(this.Double > oth.Double);
        }

        public GraceObject GreaterEqual(GraceObject other)
        {
            GraceNumber oth = other as GraceNumber;
            return GraceBoolean.Create(this.Double >= oth.Double);
        }

        public GraceObject LessThan(GraceObject other)
        {
            GraceNumber oth = other as GraceNumber;
            return GraceBoolean.Create(this.Double < oth.Double);
        }

        public GraceObject LessEqual(GraceObject other)
        {
            GraceNumber oth = other as GraceNumber;
            return GraceBoolean.Create(this.Double <= oth.Double);
        }

        public GraceObject Match(EvaluationContext ctx, GraceObject target)
        {
            if (this.EqualsEquals(target) == GraceBoolean.True)
            {
                return Matching.SuccessfulMatch(ctx, target);
            }
            return Matching.FailedMatch(ctx, target);
        }

        public GraceObject Negate()
        {
            return GraceNumber.Create(-Double);
        }

        public new GraceObject AsString()
        {
            return GraceString.Create("" + Double);
        }

        public static GraceObject Create(double val)
        {
            return new GraceNumber(val);
        }

    }
}
