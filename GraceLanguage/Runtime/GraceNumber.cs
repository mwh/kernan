using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Globalization;
using Grace.Execution;
using Grace.Utility;

namespace Grace.Runtime
{
    /// <summary>A Grace number object</summary>
    public class GraceNumber : GraceObject
    {
        /// <summary>Value of this number as a double</summary>
        public double Double
        {
            get
            {
                return Value.AsDouble;
            }
        }

        /// <summary>
        /// Value of this number as a Rational
        /// </summary>
        public Rational Value
        {
            get;
            private set;
        }

        private GraceNumber(Rational val)
            : base(true)
        {
            Value = val;
            addMethods();
        }

        private GraceNumber(int val)
            : base(true)
        {
            Value = Rational.Create(val);
            addMethods();
        }

        private GraceNumber(double val)
            : base(true)
        {
            Value = Rational.Create(val);
            addMethods();
        }

        private void addMethods()
        {
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
            AddMethod("hash", null);
            // These methods are extensions and should not be used:
            AddMethod("numerator", null);
            AddMethod("denominator", null);
            AddMethod("integral", null);
        }

        /// <inheritdoc />
        protected override Method getLazyMethod(string name)
        {
            switch(name)
            {
                case "==": return new DelegateMethod1(EqualsEquals);
                case "!=": return new DelegateMethod1(NotEquals);
                case "+": return new DelegateMethod1(Add);
                case "*": return new DelegateMethod1(Multiply);
                case "-": return new DelegateMethod1(Subtract);
                case "/": return new DelegateMethod1Ctx(Divide);
                case "%": return new DelegateMethod1(Modulus);
                case "^": return new DelegateMethod1(Exponentiate);
                case ">": return new DelegateMethod1(GreaterThan);
                case ">=": return new DelegateMethod1(GreaterEqual);
                case "<": return new DelegateMethod1(LessThan);
                case "<=": return new DelegateMethod1(LessEqual);
                case "asString": return new DelegateMethod0(AsString);
                case "prefix-": return new DelegateMethod0(Negate);
                case "..": return new DelegateMethod1Ctx(DotDot);
                case "match": return new DelegateMethod1Ctx(Match);
                case "hash": return new DelegateMethod0(mHash);
                case "numerator": return new DelegateMethod0(mNumerator);
                case "denominator":
                                  return new DelegateMethod0(mDenominator);
                case "integral": return new DelegateMethod0(mIntegral);
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
            return "Number[" + Value + "]";
        }

        /// <summary>Native method for Grace ==</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject EqualsEquals(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(this.Value == oth.Value);
        }

        /// <summary>Native method for Grace !=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject NotEquals(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceBoolean.True;
            return GraceBoolean.Create(this.Value != oth.Value);
        }

        /// <summary>Native method for Grace ..</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Argument to the method</param>
        public GraceObject DotDot(EvaluationContext ctx, GraceObject other)
        {
            var n = other.FindNativeParent<GraceNumber>();
            if (n == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string> {
                            { "method", ".." },
                            { "index", "1" },
                            { "part", ".." },
                            { "required", "Number" }
                        },
                        "ArgumentTypeError: .. requires a Number argument"
                );
            return new GraceRange(Value, n.Value, 1);
        }

        /// <summary>Native method for Grace +</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Add(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Value + oth.Value);
        }

        /// <summary>Native method for Grace *</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Multiply(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Value * oth.Value);
        }

        /// <summary>Native method for Grace -</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Subtract(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Value - oth.Value);
        }

        /// <summary>Native method for Grace /</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Argument to the method</param>
        public GraceObject Divide(EvaluationContext ctx, GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth.Value == Rational.Zero)
            {
                ErrorReporting.RaiseError(ctx, "R2012",
                        new Dictionary<string, string> {
                            { "dividend", Value.ToString() },
                        },
                        "ZeroDivisionError: Division by zero.");
            }
            return GraceNumber.Create(this.Value / oth.Value);
        }

        /// <summary>Native method for Grace %</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Modulus(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Value % oth.Value);
        }

        /// <summary>Native method for Grace ^</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject Exponentiate(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(this.Value.Exponentiate(oth.Value));
        }

        /// <summary>Native method for Grace &gt;</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject GreaterThan(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Value > oth.Value);
        }

        /// <summary>Native method for Grace &gt;=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject GreaterEqual(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Value >= oth.Value);
        }

        /// <summary>Native method for Grace &lt;</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject LessThan(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Value < oth.Value);
        }

        /// <summary>Native method for Grace &lt;=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject LessEqual(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(this.Value <= oth.Value);
        }

        private GraceObject mHash()
        {
            return GraceNumber.Create(Value.GetHashCode());
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

        /// <summary>Native method for Grace numerator</summary>
        public GraceObject mNumerator()
        {
            return Create(Value.Numerator);
        }

        /// <summary>Native method for Grace denominator</summary>
        public GraceObject mDenominator()
        {
            return Create(Value.Denominator);
        }

        /// <summary>Native method for Grace integral</summary>
        public GraceObject mIntegral()
        {
            return Create(Value.Integral);
        }

        /// <summary>Native method for Grace unary negation</summary>
        public GraceObject Negate()
        {
            return GraceNumber.Create(-Value);
        }

        /// <summary>Native method for Grace asString</summary>
        public GraceObject AsString()
        {
            return GraceString.Create("" + Value);
        }

        /// <summary>Make a Grace number</summary>
        /// <param name="val">Number to create</param>
        public static GraceObject Create(double val)
        {
            return new GraceNumber(val);
        }

        /// <summary>Make a Grace number</summary>
        /// <param name="val">Number to create</param>
        public static GraceObject Create(Rational val)
        {
            return new GraceNumber(val);
        }

        /// <summary>Make a Grace number</summary>
        /// <param name="val">Number to create</param>
        public static GraceObject Create(int val)
        {
            return new GraceNumber(val);
        }

    }

    class GraceRange : GraceObject
    {
        private readonly Rational _low;
        private readonly Rational _high;
        private readonly Rational _step;

        public Rational Start {
            get {
                return _low;
            }
        }

        public Rational End
        {
            get {
                return _high;
            }
        }

        public Rational Step
        {
            get {
                return _step;
            }
        }

        public GraceRange(Rational start, Rational end, Rational step)
        {
            _low = start;
            _high = end;
            _step = step;
            AddMethod("..", null);
            AddMethod("asString", null);
            AddMethod("do", new DelegateMethod1Ctx(mDo));
        }

        protected override Method getLazyMethod(string name)
        {
            switch(name)
            {
                case "..": return new DelegateMethod1Ctx(mDotDot);
                case "asString": return new DelegateMethod0Ctx(mAsString);
            }
            return base.getLazyMethod(name);
        }

        private GraceObject mAsString(EvaluationContext ctx)
        {
            return GraceString.Create("Range[" + _low + " .. " + _high
                    + (_step != 1 ? " .. " + _step : "") + "]");
        }

        private GraceObject mDotDot(EvaluationContext ctx, GraceObject step)
        {
            var n = step.FindNativeParent<GraceNumber>();
            if (n == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string> {
                            { "method", ".." },
                            { "index", "1" },
                            { "part", ".." },
                            { "required", "Number" }
                        },
                        "ArgumentTypeError: .. requires a Number argument"
                );
            return new GraceRange(_low, _high, _step * n.Value);
        }

        private GraceObject mDo(EvaluationContext ctx, GraceObject block)
        {
            var apply = MethodRequest.Single("apply", null);
            Rational v = _low;
            if (_step < 0)
            {
                while (v >= _high)
                {
                    apply[0].Arguments[0] = GraceNumber.Create(v);
                    block.Request(ctx, apply);
                    v += _step;
                }
                return GraceObject.Done;
            }
            while (v <= _high)
            {
                apply[0].Arguments[0] = GraceNumber.Create(v);
                block.Request(ctx, apply);
                v += _step;
            }
            return GraceObject.Done;
        }
    }
}
