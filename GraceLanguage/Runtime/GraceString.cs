using System;
using System.Globalization;
using System.Text;
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

        /// <summary>
        /// An array containing one array of codepoints for each
        /// grapheme cluster in the fully decomposed version of this
        /// string.
        /// </summary>
        private int[][] decomposedGraphemeClusters
        {
            get {
                if (_decomposedGraphemeClusters == null)
                {
                    var s = Value.Normalize(NormalizationForm.FormD);
                    var cs = StringInfo.ParseCombiningCharacters(s);
                    _decomposedGraphemeClusters = new int[cs.Length][];
                    for (int i = 0; i < cs.Length; i++)
                    {
                        int start = cs[i];
                        int end = i + 1 < cs.Length ? cs[i + 1] : s.Length;
                        int len = 0;
                        for (int j = start; j < end; j++)
                        {
                            if (Char.IsHighSurrogate(s[j]))
                                j++;
                            len++;
                        }
                        var a = new int[len];
                        int offset = start;
                        for (int j = 0; j < a.Length; j++)
                        {
                            a[j] = Char.ConvertToUtf32(s, offset);
                            if (Char.IsHighSurrogate(s[offset]))
                                offset++;
                            offset++;
                        }
                        _decomposedGraphemeClusters[i] = a;
                    }
                }
                return _decomposedGraphemeClusters;
            }
        }
        private int[][] _decomposedGraphemeClusters;

        /// <summary>Value of this string</summary>
        public string Value
        {
            get;
            set;
        }

        private StringCodepoints codepointsObject;

        private static int[] emptyIntArray = new int[0];
        private GraceString(string val)
            : base(true)
        {
            Value = val;
            AddMethod("++", null);
            AddMethod("==", null);
            AddMethod("!=", null);
            AddMethod("<", null);
            AddMethod(">", null);
            AddMethod("<=", null);
            AddMethod(">=", null);
            AddMethod("at", null);
            AddMethod("[]", null);
            AddMethod("size", null);
            AddMethod("match", null);
            AddMethod("asString", null);
            AddMethod("substringFrom to", null);
            AddMethod("codepoints", null);
            AddMethod("hash", null);
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
        }

        /// <inheritdoc />
        protected override Method getLazyMethod(string name)
        {
            switch(name) {
                case "++":
                    return new DelegateMethod1Ctx(mConcatenate);
                case "==": return new DelegateMethod1(EqualsEquals);
                case "!=": return new DelegateMethod1(NotEquals);
                case "<": return new DelegateMethod1(mLessThan);
                case ">": return new DelegateMethod1(mGreaterThan);
                case "<=": return new DelegateMethod1(mLessThanEqual);
                case ">=": return new DelegateMethod1(mGreaterThanEqual);
                case "at": return new DelegateMethod1Ctx(At);
                case "[]": return new DelegateMethod1Ctx(At);
                case "size": return new DelegateMethod0(Size);
                case "match": return new DelegateMethod1Ctx(Match);
                case "asString": return new DelegateMethod0(AsString);
                case "substringFrom to":
                                 return new DelegateMethodReq(
                                         substringFromTo);
                case "codepoints": return new DelegateMethod0(mCodepoints);
                case "hash": return new DelegateMethod0(mHash);
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

        /// <summary>
        /// Compare two GraceStrings, respecting grapheme cluster
        /// boundaries, using the fully-decomposed version of the
        /// string.
        /// </summary>
        private static int compare(GraceString a, GraceString b)
        {
            if (a.nfc == b.nfc)
                return 0;
            var ad = a.decomposedGraphemeClusters;
            var bd = b.decomposedGraphemeClusters;
            for (int i = 0, j = 0; i < ad.Length && j < bd.Length; i++, j++)
            {
                var ga = ad[i];
                var gb = bd[i];
                var len = ga.Length < gb.Length ? ga.Length : gb.Length;
                for (int k = 0; k < len; k++)
                {
                    if (ga[k] < gb[k])
                        return -1;
                    if (ga[k] > gb[k])
                        return 1;
                }
                // If the clusters are the same as far as they go,
                // but one is longer, it comes afterwards.
                if (ga.Length > gb.Length)
                    return 1;
                if (gb.Length > ga.Length)
                    return -1;
            }
            // If the strings are the same as far as they go, but
            // one is longer, it comes afterwards.
            if (ad.Length > bd.Length)
                return 1;
            if (bd.Length > ad.Length)
                return -1;
            return 0;
        }

        /// <summary>Native method for Grace &lt;</summary>
        /// <param name="other">Argument to the method</param>
        /// <remarks>
        /// This compares strings according to the UTS #10 collation
        /// algorithm, using the current culture taken from the
        /// environment.
        /// </remarks>
        private GraceObject mLessThan(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(this, oth) < 0
                                         );
        }

        /// <summary>Native method for Grace &gt;</summary>
        /// <param name="other">Argument to the method</param>
        /// <remarks>
        /// This compares strings according to the UTS #10 collation
        /// algorithm, using the current culture taken from the
        /// environment.
        /// </remarks>
        private GraceObject mGreaterThan(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(this, oth) > 0
                                         );
        }

        /// <summary>Native method for Grace &lt;</summary>
        /// <param name="other">Argument to the method</param>
        /// <remarks>
        /// This compares strings according to the UTS #10 collation
        /// algorithm, using the current culture taken from the
        /// environment.
        /// </remarks>
        private GraceObject mLessThanEqual(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(this, oth) <= 0
                                         );
        }

        /// <summary>Native method for Grace &gt;</summary>
        /// <param name="other">Argument to the method</param>
        /// <remarks>
        /// This compares strings according to the UTS #10 collation
        /// algorithm, using the current culture taken from the
        /// environment.
        /// </remarks>
        private GraceObject mGreaterThanEqual(GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(this, oth) >= 0
                                         );
        }

        /// <summary>Native method for Grace at</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Argument to the method</param>
        public GraceObject At(EvaluationContext ctx, GraceObject other)
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceString.Create("bad index");
            int idx = oth.GetInt() - 1;
            if (idx >= graphemeIndices.Length || idx < 0)
                ErrorReporting.RaiseError(ctx, "R2013",
                        new Dictionary<string, string> {
                            { "index", "" + (idx + 1) },
                            { "valid", graphemeIndices.Length > 0 ?
                                "1 .. " + graphemeIndices.Length
                                : "none (empty)" }
                        }, "Index must be a number");
            int start = graphemeIndices[idx];
            return GraceString.Create(StringInfo.GetNextTextElement(Value, start));
        }

        private GraceObject mHash()
        {
            return GraceNumber.Create(nfc.GetHashCode());
        }

        private GraceObject mCodepoints()
        {
            if (codepointsObject == null)
                codepointsObject = new StringCodepoints(Value);
            return codepointsObject;
        }

        /// <summary>Native method for Grace substringFrom To</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request arriving at this method</param>
        private GraceObject substringFromTo(EvaluationContext ctx,
                MethodRequest req)
        {
            MethodHelper.CheckArity(ctx, req, 1, 1);
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
        public GraceObject AsString()
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
