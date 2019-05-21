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

        private static Dictionary<string, Method> sharedMethods;

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
                    if (Value.Length == 0)
                    {
                        _decomposedGraphemeClusters = new int[0][];
                        return _decomposedGraphemeClusters;
                    }
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
            : base(createSharedMethods())
        {
            Value = val;
        }

        private static Dictionary<string, Method> createSharedMethods()
        {
            if (sharedMethods != null)
                return sharedMethods;
            sharedMethods = new Dictionary<string, Method>
            {
                { "==(_)", new DelegateMethodTyped1<GraceString>(mEqualsEquals) },
                { "!=(_)", new DelegateMethodTyped1<GraceString>(mNotEquals) },
                { "++(_)",
                    new DelegateMethodTyped1Ctx<GraceString>(mConcatenate) },
                { ">(_)", new DelegateMethodTyped1<GraceString>(mGreaterThan) },
                { ">=(_)",
                    new DelegateMethodTyped1<GraceString>(mGreaterThanEqual) },
                { "<(_)", new DelegateMethodTyped1<GraceString>(mLessThan) },
                { "<=(_)", new DelegateMethodTyped1<GraceString>(mLessThanEqual) },
                { "at(_)", new DelegateMethodTyped1Ctx<GraceString>(mAt) },
                { "size", new DelegateMethodTyped0<GraceString>(mSize) },
                { "do(_)", new DelegateMethodTyped1Ctx<GraceString>(mDo) },
                { "asString",
                    new DelegateMethodTyped0<GraceString>(mAsString) },
                { "substringFrom(_) to(_)",
                    new DelegateMethodTyped<GraceString>(substringFromTo) },
                { "codepoints",
                    new DelegateMethodTyped0<GraceString>(mCodepoints) },
                { "match(_)", new DelegateMethodTyped1Ctx<GraceString>(mMatch) },
                { "hash", new DelegateMethodTyped0<GraceString>(mHash) },
                { "|(_)", Matching.OrMethod },
                { "&(_)", Matching.AndMethod },
                { "|>(_)", Matching.ChainMethod },
            };
            return sharedMethods;
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

        private static GraceObject mConcatenate(EvaluationContext ctx,
                GraceString self,
                GraceObject other)
        {
            var oth = other.FindNativeParent<GraceString>();
            if (oth != null)
                return GraceString.Create(self.Value + oth.Value);
            var op = other as GraceObjectProxy;
            if (op != null)
                return GraceString.Create(self.Value + op.Object);
            other = other.Request(ctx, MethodRequest.Nullary("asString"));
            oth = other.FindNativeParent<GraceString>();
            return GraceString.Create(self.Value + oth.Value);
        }

        private static GraceObject mEqualsEquals(
                GraceString self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(self.nfc == oth.nfc);
        }

        private static GraceObject mNotEquals(
                GraceString self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.True
                                 : GraceBoolean.Create(self.nfc != oth.nfc);
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

        private static GraceObject mLessThan(
                GraceString self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(self, oth) < 0
                                         );
        }

        private static GraceObject mGreaterThan(
                GraceString self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(self, oth) > 0
                                         );
        }

        private static GraceObject mLessThanEqual(
                GraceString self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(self, oth) <= 0
                                         );
        }

        private static GraceObject mGreaterThanEqual(
                GraceString self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceString>();
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(
                                             compare(self, oth) >= 0
                                         );
        }

        private static GraceObject mAt(
                EvaluationContext ctx,
                GraceString self,
                GraceObject other
                )
        {
            var oth = other.FindNativeParent<GraceNumber>();
            if (oth == null)
                return GraceString.Create("bad index");
            int idx = oth.GetInt() - 1;
            if (idx >= self.graphemeIndices.Length || idx < 0)
                ErrorReporting.RaiseError(ctx, "R2013",
                        new Dictionary<string, string> {
                            { "index", "" + (idx + 1) },
                            { "valid", self.graphemeIndices.Length > 0 ?
                                "1 .. " + self.graphemeIndices.Length
                                : "none (empty)" }
                        }, "Index must be a number");
            int start = self.graphemeIndices[idx];
            return GraceString.Create(
                    StringInfo.GetNextTextElement(self.Value, start));
        }

        private static GraceObject mDo(
                EvaluationContext ctx,
                GraceString self,
                GraceObject blk
                )
        {
            var req = MethodRequest.Single("apply", null);
            for (var i = 0; i < self.graphemeIndices.Length; i++)
            {
                int start = self.graphemeIndices[i];
                string c = StringInfo.GetNextTextElement(self.Value, start);
                req[0].Arguments[0] = GraceString.Create(c);
                blk.Request(ctx, req);
            }
            return GraceObject.Done;
        }

        private static GraceObject mHash(GraceString self)
        {
            return GraceNumber.Create(self.nfc.GetHashCode());
        }

        private static GraceObject mCodepoints(GraceString self)
        {
            if (self.codepointsObject == null)
                self.codepointsObject = new StringCodepoints(self.Value);
            return self.codepointsObject;
        }

        private static GraceObject substringFromTo(
                EvaluationContext ctx,
                MethodRequest req,
                GraceString self
                )
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
            if (enInd >= self.graphemeIndices.Length)
                enInd = self.graphemeIndices.Length;
            int endIndex = enInd < self.graphemeIndices.Length
                ? self.graphemeIndices[enInd]
                : self.Value.Length;
            stInd = self.graphemeIndices[stInd];
            return GraceString.Create(self.Value.Substring(stInd,
                        endIndex - stInd));
        }

        private static GraceObject mSize(GraceString self)
        {
            return GraceNumber.Create(self.graphemeIndices.Length);
        }

        private static GraceObject mMatch(
                EvaluationContext ctx,
                GraceString self,
                GraceObject target)
        {
            return (mEqualsEquals(self, target) == GraceBoolean.True)
                ? Matching.SuccessfulMatch(ctx, target)
                : Matching.FailedMatch(ctx, target);
        }

        private static GraceObject mAsString(GraceString self)
        {
            return self;
        }

        /// <summary>
        /// Convert any GraceObject into a CLI string.
        /// </summary>
        /// <param name="ctx">Interpreter to call asString under</param>
        /// <param name="o">Object to convert</param>
        public static string AsNativeString(EvaluationContext ctx,
                GraceObject o)
        {
            var s = o as GraceString;
            if (s != null)
                return s.Value;
            s = o.Request(ctx, MethodRequest.Nullary("asString"))
                as GraceString;
            if (s != null)
                return s.Value;
            return "not a string, nor stringifiable";
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "GraceString[\"" + Value + "\"]";
        }

        /// <summary>Create a Grace string</summary>
        /// <param name="val">Value of string to create</param>
        public static GraceString Create(string val)
        {
            return new GraceString(val);
        }

    }

}
