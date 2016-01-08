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
        private static Dictionary<string, Method> sharedMethods;

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
        {
            Value = val;
            addMethods();
        }

        private GraceNumber(int val)
        {
            Value = Rational.Create(val);
            addMethods();
        }

        private GraceNumber(double val)
        {
            Value = Rational.Create(val);
            addMethods();
        }

        private void addMethods()
        {
            if (sharedMethods == null)
                createSharedMethods();
            AddMethods(sharedMethods);
        }

        private static void createSharedMethods()
        {
            sharedMethods = new Dictionary<string, Method>
            {
                { "==", new DelegateMethodTyped1<GraceNumber>(mEqualsEquals) },
                { "!=", new DelegateMethodTyped1<GraceNumber>(mNotEquals) },
                { "+", new DelegateMethodTyped1<GraceNumber>(mAdd) },
                { "*", new DelegateMethodTyped1<GraceNumber>(mMultiply) },
                { "-", new DelegateMethodTyped1<GraceNumber>(mSubtract) },
                { "/", new DelegateMethodTyped1Ctx<GraceNumber>(mDivide) },
                { "%", new DelegateMethodTyped1<GraceNumber>(mModulus) },
                { "^", new DelegateMethodTyped1<GraceNumber>(mExponentiate) },
                { ">", new DelegateMethodTyped1<GraceNumber>(mGreaterThan) },
                { ">=", new DelegateMethodTyped1<GraceNumber>(mGreaterEqual) },
                { "<", new DelegateMethodTyped1<GraceNumber>(mLessThan) },
                { "<=", new DelegateMethodTyped1<GraceNumber>(mLessEqual) },
                { "asString",
                    new DelegateMethodTyped0<GraceNumber>(mAsString) },
                { "prefix-", new DelegateMethodTyped0<GraceNumber>(mNegate) },
                { "..", new DelegateMethodTyped1Ctx<GraceNumber>(mDotDot) },
                { "match", new DelegateMethodTyped1Ctx<GraceNumber>(mMatch) },
                { "hash", new DelegateMethodTyped0<GraceNumber>(mHash) },
                // These methods are extensions, and should not be used.
                { "numerator",
                    new DelegateMethodTyped0<GraceNumber>(mNumerator) },
                { "denominator",
                    new DelegateMethodTyped0<GraceNumber>(mDenominator) },
                { "integral",
                    new DelegateMethodTyped0<GraceNumber>(mIntegral) },
            };
        }

        /// <summary>
        /// Apply an extension trait to all future instances of this type.
        /// </summary>
        /// <param name="meths">
        /// Dictionary of methods to add.
        /// </param>
        public static void ExtendWith(IDictionary<string, Method> meths)
        {
            if (sharedMethods == null)
                createSharedMethods();
            foreach (var m in meths)
                sharedMethods[m.Key] = m.Value;
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
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mEqualsEquals(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(self.Value == oth.Value);
        }

        /// <summary>Native method for Grace !=</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mNotEquals(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceBoolean.True;
            return GraceBoolean.Create(self.Value != oth.Value);
        }

        /// <summary>Native method for Grace ..</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mDotDot(
                EvaluationContext ctx,
                GraceNumber self,
                GraceObject other
                )
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
            return new GraceRange(self.Value, n.Value, 1);
        }

        /// <summary>Native method for Grace +</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mAdd(GraceNumber self, GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(self.Value + oth.Value);
        }

        /// <summary>Native method for Grace *</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mMultiply(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(self.Value * oth.Value);
        }

        /// <summary>Native method for Grace -</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mSubtract(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(self.Value - oth.Value);
        }

        /// <summary>Native method for Grace /</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mDivide(
                EvaluationContext ctx,
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth.Value == Rational.Zero)
            {
                ErrorReporting.RaiseError(ctx, "R2012",
                        new Dictionary<string, string> {
                            { "dividend", self.Value.ToString() },
                        },
                        "ZeroDivisionError: Division by zero.");
            }
            return GraceNumber.Create(self.Value / oth.Value);
        }

        /// <summary>Native method for Grace %</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mModulus(GraceNumber self, GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(self.Value % oth.Value);
        }

        /// <summary>Native method for Grace ^</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mExponentiate(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceNumber.Create(self.Value.Exponentiate(oth.Value));
        }

        /// <summary>Native method for Grace &gt;</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mGreaterThan(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(self.Value > oth.Value);
        }

        /// <summary>Native method for Grace &gt;=</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mGreaterEqual(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(self.Value >= oth.Value);
        }

        /// <summary>Native method for Grace &lt;</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mLessThan(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(self.Value < oth.Value);
        }

        /// <summary>Native method for Grace &lt;=</summary>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        private static GraceObject mLessEqual(
                GraceNumber self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            return GraceBoolean.Create(self.Value <= oth.Value);
        }

        private static GraceObject mHash(GraceNumber self)
        {
            return GraceNumber.Create(self.Value.GetHashCode());
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="target">Target of the match</param>
        private static GraceObject mMatch(
                EvaluationContext ctx,
                GraceNumber self,
                GraceObject target
                )
        {
            if (mEqualsEquals(self, target) == GraceBoolean.True)
            {
                return Matching.SuccessfulMatch(ctx, target);
            }
            return Matching.FailedMatch(ctx, target);
        }

        /// <summary>Native method for Grace numerator</summary>
        private static GraceObject mNumerator(GraceNumber self)
        {
            return Create(self.Value.Numerator);
        }

        /// <summary>Native method for Grace denominator</summary>
        private static GraceObject mDenominator(GraceNumber self)
        {
            return Create(self.Value.Denominator);
        }

        /// <summary>Native method for Grace integral</summary>
        private static GraceObject mIntegral(GraceNumber self)
        {
            return Create(self.Value.Integral);
        }

        /// <summary>Native method for Grace unary negation</summary>
        private static GraceObject mNegate(GraceNumber self)
        {
            return GraceNumber.Create(-self.Value);
        }

        /// <summary>Native method for Grace asString</summary>
        private static GraceObject mAsString(GraceNumber self)
        {
            return GraceString.Create("" + self.Value);
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
