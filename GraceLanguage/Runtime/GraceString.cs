using System.Globalization;
using System.Collections.Generic;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>A Grace string object</summary>
    public class GraceString : GraceObject
    {
        /// <summary>
        /// User code to extend all builtin numbers.
        /// </summary>
        public static ObjectConstructorNode Extension { get ; set; }

        /// <summary>
        /// Interpreter to use for creating the extension objects.
        /// </summary>
        public static EvaluationContext ExtensionInterpreter { get ; set; }

        /// <summary>This string in normalization form C</summary>
        private string nfc {
            get {
                if (_nfc == null)
                    _nfc = Value.Normalize();
                return _nfc;
            }
        }
        private string _nfc;

        /// <summary>Indices of the start of each grapheme cluster
        /// in this string</summary>
        private int[] graphemeIndices {
            get {
                if (_graphemeIndices == null)
                    _graphemeIndices = (Value.Length > 0)
                        ? StringInfo.ParseCombiningCharacters(Value)
                        : emptyIntArray;
                return _graphemeIndices;
            }
        }
        private int[] _graphemeIndices;

        /// <summary>Value of this string</summary>
        public string Value
        {
            get;
            set;
        }

        private static int[] emptyIntArray = new int[0];
        private GraceString(string val)
            : base(true)
        {
            Value = val;
            AddMethod("++", null);
            AddMethod("==", null);
            AddMethod("!=", null);
            AddMethod("at", null);
            AddMethod("size", null);
            AddMethod("match", null);
            AddMethod("asString", null);
            AddMethod("substringFrom to", null);
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
        }

        /// <inheritdoc />
        protected override MethodNode getLazyMethod(string name)
        {
            switch(name) {
                case "++":
                    return new DelegateMethodNode1Ctx(mConcatenate);
                case "==": return new DelegateMethodNode1(EqualsEquals);
                case "!=": return new DelegateMethodNode1(NotEquals);
                case "at": return new DelegateMethodNode1(At);
                case "size": return new DelegateMethodNode0(Size);
                case "match": return new DelegateMethodNode1Ctx(Match);
                case "asString": return new DelegateMethodNode0(AsString);
                case "substringFrom to":
                                 return new DelegateMethodNodeReq(
                                         substringFromTo);
            }
            return base.getLazyMethod(name);
        }

        private GraceObject mConcatenate(EvaluationContext ctx,
                GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            if (oth != null)
                return GraceString.Create(Value + oth.Value);
            var op = other as GraceObjectProxy;
            if (op != null)
                return GraceString.Create(Value + op.Object);
            other = other.Request(ctx, MethodRequest.Nullary("asString"));
            oth = other.FindNativeParent<GraceString>();
            return GraceString.Create(Value + oth.Value);
        }

        /// <summary>Native method for Grace ==</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject EqualsEquals(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(nfc == oth.nfc);
        }

        /// <summary>Native method for Grace !=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject NotEquals(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.True
                                 : GraceBoolean.Create(nfc != oth.nfc);
        }

        /// <summary>Native method for Grace at</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject At(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceString.Create("bad index");
            int idx = oth.GetInt() - 1;
            int start = graphemeIndices[idx];
            return GraceString.Create(StringInfo.GetNextTextElement(Value, start));
        }

        /// <summary>Native method for Grace substringFrom To</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request arriving at this method</param>
        private GraceObject substringFromTo(EvaluationContext ctx,
                MethodRequest req)
        {
            // Index of first grapheme to include.
            var start = req[0].Arguments[0];
            // Index of last grapheme to include.
            var end = req[1].Arguments[0];
            var st = start.FindNativeParent<GraceNumber>();
            if (st == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "index", "1" },
                            { "part", "substringFrom" }
                        }, "Start must be a number");
            var en = end.FindNativeParent<GraceNumber>();
            if (en == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "index", "1" },
                            { "part", "to" }
                        }, "End must be a number");
            // Because, e.g., substringFrom(1) to(1) should return the
            // first grapheme, the start value must be adjusted for
            // base-one indexing, but the end value must not be.
            int stInd = st.GetInt() - 1;
            int enInd = en.GetInt();
            if (stInd < 0)
                stInd = 0;
            if (enInd < 0)
                enInd = 0;
            if (enInd >= graphemeIndices.Length)
                enInd = graphemeIndices.Length;
            int endIndex = enInd < graphemeIndices.Length
                ? graphemeIndices[enInd]
                : Value.Length;
            return GraceString.Create(Value.Substring(stInd,
                        endIndex - stInd));
        }

        /// <summary>Native method for Grace size</summary>
        public GraceObject Size()
        {
            return GraceNumber.Create(graphemeIndices.Length);
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="target">Target of the match</param>
        public GraceObject Match(EvaluationContext ctx, GraceObject target)
        {
            return (EqualsEquals(target) == GraceBoolean.True)
                ? Matching.SuccessfulMatch(ctx, target)
                : Matching.FailedMatch(ctx, target);
        }

        /// <summary>Native method for Grace asString</summary>
        new public GraceObject AsString()
        {
            return this;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "GraceString[\"" + Value + "\"]";
        }

        /// <summary>Create a Grace string</summary>
        /// <param name="val">Value of string to create</param>
        public static GraceObject Create(string val)
        {
            if (Extension == null)
                return new GraceString(val);
            var str = new GraceString(val);
            var o = Extension.Evaluate(ExtensionInterpreter);
            o.AddParent("builtin", str);
            return o;
        }

    }

}
