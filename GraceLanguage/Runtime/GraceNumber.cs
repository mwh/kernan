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
    /// <summary>A Grace number object</summary>
    class GraceNumber : GraceObject
    {
        /// <summary>Value of this number as a double</summary>
        public double Double
        {
            get;
            set;
        }

        /// <summary>
        /// User code to extend all builtin numbers.
        /// </summary>
        public static ObjectConstructorNode Extension { get ; set; }

        /// <summary>
        /// Interpreter to use for creating the extension objects.
        /// </summary>
        public static EvaluationContext ExtensionInterpreter { get ; set; }

        private GraceNumber(double val)
            : base(true)
        {
            Double = val;
            AddMethod("==", null);
            AddMethod("!=", null);
            AddMethod("+", null);
            AddMethod("*", null);
            AddMethod("-", null);
            AddMethod("/", null);
            AddMethod("%", null);
            AddMethod("^", null);
            AddMethod(">", null);
            AddMethod(">=", null);
            AddMethod("<", null);
            AddMethod("<=", null);
            AddMethod("asString", null);
            AddMethod("prefix-", null);
            AddMethod("..", null);
            AddMethod("match", null);
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
        }

        protected override MethodNode getLazyMethod(string name)
        {
            switch(name)
            {
                case "==": return new DelegateMethodNode1(EqualsEquals);
                case "!=": return new DelegateMethodNode1(NotEquals);
                case "+": return new DelegateMethodNode1(Add);
                case "*": return new DelegateMethodNode1(Multiply);
                case "-": return new DelegateMethodNode1(Subtract);
                case "/": return new DelegateMethodNode1(Divide);
                case "%": return new DelegateMethodNode1(Modulus);
                case "^": return new DelegateMethodNode1(Exponentiate);
                case ">": return new DelegateMethodNode1(GreaterThan);
                case ">=": return new DelegateMethodNode1(GreaterEqual);
                case "<": return new DelegateMethodNode1(LessThan);
                case "<=": return new DelegateMethodNode1(LessEqual);
                case "asString": return new DelegateMethodNode0(AsString);
                case "prefix-": return new DelegateMethodNode0(Negate);
                case "..": return new DelegateMethodNode1Ctx(DotDot);
                case "match": return new DelegateMethodNode1Ctx(Match);
            }
            return base.getLazyMethod(name);
        }

        /// <summary>Get the value of this number as an int</summary>
        public int GetInt()
        {
            return (int)Double;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Number[" + Double + "]";
        }

        /// <summary>Native method for Grace ==</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject EqualsEquals(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(this.Double == oth.Double);
        }

        /// <summary>Native method for Grace !=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject NotEquals(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceBoolean.True;
            return GraceBoolean.Create(this.Double != oth.Double);
        }

        /// <summary>Native method for Grace ..</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Argument to the method</param>
        public GraceObject DotDot(EvaluationContext ctx, GraceObject other)
        {
            MethodRequest req = new MethodRequest();
            RequestPart rpn = new RequestPart("_range", new List<GraceObject>(),
                    new List<GraceObject>() { this, other });
            req.AddPart(rpn);
            GraceObject rec = ctx.FindReceiver(req);
            return rec.Request(ctx, req);
        }

        /// <summary>Native method for Grace +</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Add(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Double + oth.Double);
        }

        /// <summary>Native method for Grace *</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Multiply(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Double * oth.Double);
        }

        /// <summary>Native method for Grace -</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Subtract(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Double - oth.Double);
        }

        /// <summary>Native method for Grace /</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Divide(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Double / oth.Double);
        }

        /// <summary>Native method for Grace %</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Modulus(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Double % oth.Double);
        }

        /// <summary>Native method for Grace ^</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Exponentiate(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(Math.Pow(this.Double, oth.Double));
        }

        /// <summary>Native method for Grace &gt;</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject GreaterThan(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Double > oth.Double);
        }

        /// <summary>Native method for Grace &gt;=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject GreaterEqual(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Double >= oth.Double);
        }

        /// <summary>Native method for Grace &lt;</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject LessThan(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Double < oth.Double);
        }

        /// <summary>Native method for Grace &lt;=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject LessEqual(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Double <= oth.Double);
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="target">Target of the match</param>
        public GraceObject Match(EvaluationContext ctx, GraceObject target)
        {
            if (this.EqualsEquals(target) == GraceBoolean.True)
            {
                return Matching.SuccessfulMatch(ctx, target);
            }
            return Matching.FailedMatch(ctx, target);
        }

        /// <summary>Native method for Grace unary negation</summary>
        public GraceObject Negate()
        {
            return GraceNumber.Create(-Double);
        }

        /// <summary>Native method for Grace asString</summary>
        public new GraceObject AsString()
        {
            return GraceString.Create("" + Double);
        }

        /// <summary>Make a Grace number</summary>
        /// <param name="val">Number to create</param>
        public static GraceObject Create(double val)
        {
            if (Extension == null)
                return new GraceNumber(val);
            var num = new GraceNumber(val);
            var o = Extension.Evaluate(ExtensionInterpreter);
            o.AddParent("builtin", num);
            return o;
        }

    }
}
