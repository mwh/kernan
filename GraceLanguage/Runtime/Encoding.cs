using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Grace.Utility;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>
    /// Represents a sequence of Unicode codepoints, indexed individually.
    /// </summary>
    class StringCodepoints : GraceObject
    {
        private List<int> utf32 = new List<int>();
        private CodepointObject[] codepoints;

        private static Dictionary<string, MethodNode> sharedMethods =
            new Dictionary<string, MethodNode> {
                { "<", new DelegateMethodNodeTyped<StringCodepoints>(mLT) },
                { "<=", new DelegateMethodNodeTyped<StringCodepoints>(mLTE) },
                { ">", new DelegateMethodNodeTyped<StringCodepoints>(mGT) },
                { ">=", new DelegateMethodNodeTyped<StringCodepoints>(mGTE) },
                { "==", new DelegateMethodNodeTyped<StringCodepoints>(mEQ) },
                { "!=", new DelegateMethodNodeTyped<StringCodepoints>(mNE) },
                { "[]",
                    new DelegateMethodNodeTyped<StringCodepoints>(mAt) },
                { "at",
                    new DelegateMethodNodeTyped<StringCodepoints>(mAt) },
                { "size",
                    new DelegateMethodNodeTyped<StringCodepoints>(mSize) },
                { "string",
                    new DelegateMethodNodeTyped<StringCodepoints>(mString) },
                { "nfc",
                    new DelegateMethodNodeTyped<StringCodepoints>(mNFC) },
                { "nfd",
                    new DelegateMethodNodeTyped<StringCodepoints>(mNFD) },
                { "utf8",
                    new DelegateMethodNodeTyped<StringCodepoints>(mUTF8) },
                { "utf16",
                    new DelegateMethodNodeTyped<StringCodepoints>(mUTF16) },
                { "utf32",
                    new DelegateMethodNodeTyped<StringCodepoints>(mUTF32) },
            };

        /// <summary>
        /// Create a StringCodepoints from a literal string.
        /// </summary>
        /// <param name="data">String to include codepoints from</param>
        public StringCodepoints(string data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                utf32.Add(Char.ConvertToUtf32(data, i));
                char c = data[i];
                if (c >= 0xD800 && c <= 0xDBFF)
                    // Leading surrogate
                    i++;
            }
            codepoints = new CodepointObject[utf32.Count];
            addMethods();
        }

        private StringCodepoints(IEnumerable<int> cps)
        {
            utf32.AddRange(cps);
            codepoints = new CodepointObject[utf32.Count];
            addMethods();
        }

        private void addMethods()
        {
            AddMethods(sharedMethods);
        }

        private static GraceObject mAt(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var oth = req[0].Arguments[0].FindNativeParent<GraceNumber>();
            if (oth == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "index", "1" },
                            { "part", req.Name }
                        }, "IndexError: Index must be a number");
            int idx = oth.GetInt() - 1;
            if (idx >= self.codepoints.Length || idx < 0)
                ErrorReporting.RaiseError(ctx, "R2013",
                        new Dictionary<string, string> {
                            { "index", "" + (idx + 1) },
                            { "valid", self.codepoints.Length > 0 ?
                                "1 .. " + self.codepoints.Length
                                : "none (empty)" }
                        }, "IndexError: Index out of range");
            if (self.codepoints[idx] == null)
            {
                self.codepoints[idx] = CodepointObject.Create(self.utf32[idx]);
            }
            return self.codepoints[idx];
        }

        private static GraceObject mSize(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            return GraceNumber.Create(self.utf32.Count);
        }

        private static string makeString(StringCodepoints self)
        {
            return String.Join("",
                    from c in self.utf32
                    select Char.ConvertFromUtf32(c));
        }

        private static GraceObject mString(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            return GraceString.Create(makeString(self));
        }

        private static int cmp(StringCodepoints a, StringCodepoints b)
        {
            var l = Math.Min(a.utf32.Count, b.utf32.Count);
            for (var i = 0; i < l; i++)
            {
                if (a.utf32[i] < b.utf32[i])
                    return -1;
                if (a.utf32[i] > b.utf32[i])
                    return 1;
            }
            if (a.utf32.Count > b.utf32.Count)
                return 1;
            if (b.utf32.Count > a.utf32.Count)
                return -1;
            return 0;
        }

        private static GraceObject mLT(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var other = req[0].Arguments[0] as StringCodepoints;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(cmp(self, other) < 0);
        }

        private static GraceObject mLTE(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var other = req[0].Arguments[0] as StringCodepoints;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(cmp(self, other) <= 0);
        }

        private static GraceObject mGT(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var other = req[0].Arguments[0] as StringCodepoints;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(cmp(self, other) > 0);
        }

        private static GraceObject mGTE(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var other = req[0].Arguments[0] as StringCodepoints;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(cmp(self, other) >= 0);
        }

        private static GraceObject mEQ(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var other = req[0].Arguments[0] as StringCodepoints;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(cmp(self, other) == 0);
        }

        private static GraceObject mNE(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var other = req[0].Arguments[0] as StringCodepoints;
            if (other == null)
                return GraceBoolean.True;
            return GraceBoolean.Create(cmp(self, other) != 0);
        }

        private static GraceObject mNFC(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var str = makeString(self);
            str = str.Normalize();
            return new StringCodepoints(str);
        }


        private static GraceObject mNFD(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var str = makeString(self);
            str = str.Normalize(NormalizationForm.FormD);
            return new StringCodepoints(str);
        }

        private static GraceObject mUTF8(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var str = makeString(self);
            return new ByteString(Encoding.UTF8.GetBytes(str));
        }

        private static GraceObject mUTF16(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var str = makeString(self);
            return new UTF16CodepointsView(str);
        }

        private static GraceObject mUTF32(EvaluationContext ctx,
                MethodRequest req,
                StringCodepoints self)
        {
            var str = makeString(self);
            return new UTF32CodepointsView(str, self.utf32);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Codepoints[" + String.Join(", ", from c in utf32
                    select "U+" + c.ToString("X4")) + "]";
        }

        /// <summary>
        /// Create a StringCodepoints from an enumerable of
        /// codepoint ints.
        /// </summary>
        /// <param name="cps">Sequence of codepoints to represent</param>
        public static StringCodepoints Create(IEnumerable<int> cps)
        {
            return new StringCodepoints(cps);
        }
    }

    /// <summary>
    /// Represents a Unicode codepoint, including its associated
    /// properties.
    /// </summary>
    class CodepointObject : GraceObject
    {
        private int codepoint;
        private string[] parts;

        private static Dictionary<string, MethodNode> sharedMethods;
        private CodepointObject(int cp)
        {
            codepoint = cp;
            parts = UnicodeLookup.GetCodepointParts(cp);
            if (sharedMethods == null)
                createSharedMethods();
            AddMethods(sharedMethods);
        }

        private static void createSharedMethods()
        {
            sharedMethods = new Dictionary<string, MethodNode> {
                { "<", new DelegateMethodNodeTyped<CodepointObject>(mLT) },
                { "<=", new DelegateMethodNodeTyped<CodepointObject>(mLTE) },
                { ">", new DelegateMethodNodeTyped<CodepointObject>(mGT) },
                { ">=", new DelegateMethodNodeTyped<CodepointObject>(mGTE) },
                { "asString",
                    new DelegateMethodNodeTyped<CodepointObject>(mAsString) },
                { "codepoint",
                    new DelegateMethodNodeTyped<CodepointObject>(mCodepoint) },
                { "name",
                    new DelegateMethodNodeTyped<CodepointObject>(mName) },
                { "category",
                    new DelegateMethodNodeTyped<CodepointObject>(mCategory) },
                { "combining",
                    new DelegateMethodNodeTyped<CodepointObject>(mCombining) },
                { "bidirectional",
                    new DelegateMethodNodeTyped<CodepointObject>(mBidirectional)
                },
                { "decomposition",
                    new DelegateMethodNodeTyped<CodepointObject>(mDecomposition)
                },
                { "decimalDigit",
                    new DelegateMethodNodeTyped<CodepointObject>(mDecimalDigit)
                },
                { "digit",
                    new DelegateMethodNodeTyped<CodepointObject>(mDigit)
                },
                { "numeric",
                    new DelegateMethodNodeTyped<CodepointObject>(mNumeric)
                },
                { "mirrored",
                    new DelegateMethodNodeTyped<CodepointObject>(mMirrored)
                },
                { "uppercase",
                    new DelegateMethodNodeTyped<CodepointObject>(mUppercase) },
                { "lowercase",
                    new DelegateMethodNodeTyped<CodepointObject>(mLowercase) },
                { "titlecase",
                    new DelegateMethodNodeTyped<CodepointObject>(mTitlecase) },
                { "string",
                    new DelegateMethodNodeTyped<CodepointObject>(mString) },
            };
        }

        private static GraceObject mAsString(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceString.Create("U+" + self.codepoint.ToString("X4")
                    + " " + self.parts[0]);
        }

        private static GraceObject mCodepoint(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceNumber.Create(self.codepoint);
        }

        private static GraceObject mName(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceString.Create(self.parts[0]);
        }

        private static GraceObject mCategory(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceString.Create(self.parts[1]);
        }

        private static GraceObject mCombining(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceNumber.Create(int.Parse(self.parts[2]));
        }

        private static GraceObject mBidirectional(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceString.Create(self.parts[3]);
        }

        private static GraceObject mDecomposition(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return mapCodepointSequenceString(self.parts[4]);
        }

        private static GraceObject mDecimalDigit(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            // By convention, return -1 for non-numeric codepoints.
            if (self.parts[5] == "")
                return GraceNumber.Create(-1);
            return GraceNumber.Create(int.Parse(self.parts[5]));
        }

        private static GraceObject mDigit(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            // By convention, return -1 for non-numeric codepoints.
            if (self.parts[6] == "")
                return GraceNumber.Create(-1);
            return GraceNumber.Create(int.Parse(self.parts[6]));
        }

        private static GraceObject mNumeric(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            // By convention, return -1 for non-numeric codepoints.
            if (self.parts[7] == "")
                return GraceNumber.Create(-1);
            int val;
            if (int.TryParse(self.parts[7], out val))
                return GraceNumber.Create(val);
            // At this point, it must be a fraction n/m.
            var bits = self.parts[7].Split('/');
            var rat = Rational.Create(int.Parse(bits[0]), int.Parse(bits[1]));
            return GraceNumber.Create(rat);
        }

        private static GraceObject mMirrored(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceBoolean.Create(self.parts[8] == "Y");
        }

        private static GraceObject mUppercase(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return mapCodepointSequenceString(self.parts[11]);
        }

        private static GraceObject mLowercase(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return mapCodepointSequenceString(self.parts[12]);
        }

        private static GraceObject mTitlecase(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return mapCodepointSequenceString(self.parts[13]);
        }

        private static GraceObject mString(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            return GraceString.Create(Char.ConvertFromUtf32(self.codepoint));
        }

        private static GraceObject mLT(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            var other = req[0].Arguments[0] as CodepointObject;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(self.codepoint < other.codepoint);
        }

        private static GraceObject mLTE(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            var other = req[0].Arguments[0] as CodepointObject;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(self.codepoint <= other.codepoint);
        }

        private static GraceObject mGT(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            var other = req[0].Arguments[0] as CodepointObject;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(self.codepoint > other.codepoint);
        }

        private static GraceObject mGTE(EvaluationContext ctx,
                MethodRequest req,
                CodepointObject self)
        {
            var other = req[0].Arguments[0] as CodepointObject;
            if (other == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(self.codepoint >= other.codepoint);
        }

        private static StringCodepoints mapCodepointSequenceString(string s)
        {
            var bits = s.Split(' ');
            // Skip compatibility & formatting decompositions
            if (bits.Length >= 1 && (bits[0].Length == 0 || bits[0][0] == '<'))
                bits = new String[0];
            return StringCodepoints.Create(from b in bits select
                    Convert.ToInt32(b, 16));
        }

        private static Dictionary<int, CodepointObject> cache
            = new Dictionary<int, CodepointObject>();
        /// <summary>
        /// Create a codepoint object for a given codepoint.
        /// </summary>
        /// <param name="cp">Codepoint to represent</param>
        /// <remarks>
        /// These objects are cached, so that only one object for
        /// a given codepoint is created.
        /// </remarks>
        public static CodepointObject Create(int cp)
        {
            if (!cache.ContainsKey(cp))
                cache[cp] = new CodepointObject(cp);
            return cache[cp];
        }

    }

    /// <summary>
    /// Represents the UTF-16 encoding of codepoints, indexable
    /// by code unit.
    /// </summary>
    class UTF16CodepointsView : GraceObject
    {
        private string data;

        private static Dictionary<string, MethodNode> sharedMethods;

        public UTF16CodepointsView(string s)
        {
            data = s;
            addMethods();
        }

        private static void createSharedMethods()
        {
            sharedMethods = new Dictionary<string, MethodNode> {
                { "at", new DelegateMethodNodeTyped<UTF16CodepointsView>(mAt) },
                { "[]", new DelegateMethodNodeTyped<UTF16CodepointsView>(mAt) },
                { "le",
                    new DelegateMethodNodeTyped<UTF16CodepointsView>(mLEndian)
                },
                { "be",
                    new DelegateMethodNodeTyped<UTF16CodepointsView>(mBEndian)
                },
            };
        }

        private static GraceObject mAt(EvaluationContext ctx,
                MethodRequest req,
                UTF16CodepointsView self)
        {
            var arg = req[0].Arguments[0];
            var index = arg.FindNativeParent<GraceNumber>();
            var idx = index.GetInt() - 1;
            if (idx < 0 || idx >= self.data.Length)
                ErrorReporting.RaiseError(ctx, "R2013",
                        new Dictionary<string, string> {
                            { "index", "" + (idx + 1) },
                            { "valid", self.data.Length > 0 ?
                                "1 .. " + self.data.Length
                                : "none (empty)" }
                        }, "IndexError: Index out of range");
            return GraceNumber.Create(self.data[idx]);
        }

        private static GraceObject mLEndian(EvaluationContext ctx,
                MethodRequest req,
                UTF16CodepointsView self)
        {
            return new ByteString(Encoding.Unicode.GetBytes(self.data));
        }

        private static GraceObject mBEndian(EvaluationContext ctx,
                MethodRequest req,
                UTF16CodepointsView self)
        {
            var enc = new UnicodeEncoding(true, false);
            return new ByteString(enc.GetBytes(self.data));
        }

        public override string ToString()
        {
            return "UTF16View[" + String.Join(" ",
                        from b in data
                        select ((int)b).ToString("X4")
                    ) + "]";
        }

        private void addMethods()
        {
            if (sharedMethods == null)
                createSharedMethods();
            AddMethods(sharedMethods);
        }
    }


    /// <summary>
    /// Represents the UTF-32 encoding of codepoints, indexable
    /// by code unit.
    /// </summary>
    class UTF32CodepointsView : GraceObject
    {
        private string stringData;
        private List<int> utf32;

        private static Dictionary<string, MethodNode> sharedMethods;

        public UTF32CodepointsView(string s, List<int> u)
        {
            stringData = s;
            utf32 = u;
            addMethods();
        }

        private static void createSharedMethods()
        {
            sharedMethods = new Dictionary<string, MethodNode> {
                { "at", new DelegateMethodNodeTyped<UTF32CodepointsView>(mAt) },
                { "[]", new DelegateMethodNodeTyped<UTF32CodepointsView>(mAt) },
                { "le",
                    new DelegateMethodNodeTyped<UTF32CodepointsView>(mLEndian)
                },
                { "be",
                    new DelegateMethodNodeTyped<UTF32CodepointsView>(mBEndian)
                },
            };
        }

        private static GraceObject mAt(EvaluationContext ctx,
                MethodRequest req,
                UTF32CodepointsView self)
        {
            var arg = req[0].Arguments[0];
            var index = arg.FindNativeParent<GraceNumber>();
            var idx = index.GetInt() - 1;
            if (idx < 0 || idx >= self.utf32.Count)
                ErrorReporting.RaiseError(ctx, "R2013",
                        new Dictionary<string, string> {
                            { "index", "" + (idx + 1) },
                            { "valid", self.utf32.Count > 0 ?
                                "1 .. " + self.utf32.Count
                                : "none (empty)" }
                        }, "IndexError: Index out of range");
            return GraceNumber.Create(self.utf32[idx]);
        }

        private static GraceObject mLEndian(EvaluationContext ctx,
                MethodRequest req,
                UTF32CodepointsView self)
        {
            return new ByteString(Encoding.UTF32.GetBytes(self.stringData));
        }

        private static GraceObject mBEndian(EvaluationContext ctx,
                MethodRequest req,
                UTF32CodepointsView self)
        {
            var enc = new UTF32Encoding(true, false);
            return new ByteString(enc.GetBytes(self.stringData));
        }

        public override string ToString()
        {
            return "UTF32View[" + String.Join(" ",
                        from b in utf32
                        select b.ToString("X8")
                    ) + "]";
        }

        private void addMethods()
        {
            if (sharedMethods == null)
                createSharedMethods();
            AddMethods(sharedMethods);
        }
    }

}
