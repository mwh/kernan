using System.Globalization;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>A Grace string object</summary>
    class GraceString : GraceObject
    {
        /// <summary>This string in normalization form C</summary>
        private string nfc;

        /// <summary>Indices of the start of each grapheme cluster
        /// in this string</summary>
        private int[] graphemeIndices;

        /// <summary>Value of this string</summary>
        public string Value
        {
            get;
            set;
        }
        private GraceString(string val)
            : base(true)
        {
            Interpreter.Debug("made new string " + val);
            Value = val;
            nfc = val.Normalize();
            graphemeIndices = (val.Length > 0)
                ? StringInfo.ParseCombiningCharacters(val)
                : new int[0];
            AddMethod("++", new DelegateMethodNode1Ctx(new NativeMethod1Ctx(this.Concatenate)));
            AddMethod("==", new DelegateMethodNode1(new NativeMethod1(this.EqualsEquals)));
            AddMethod("!=", new DelegateMethodNode1(new NativeMethod1(this.NotEquals)));
            AddMethod("at", new DelegateMethodNode1(new NativeMethod1(this.At)));
            AddMethod("size", new DelegateMethodNode0(new NativeMethod0(this.Size)));
            AddMethod("match", new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.Match)));
            AddMethod("asString", new DelegateMethodNode0(new NativeMethod0(this.AsString)));
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
        }

        public GraceObject Concatenate(EvaluationContext ctx,
                GraceObject other)
        {
            Interpreter.Debug("called ++");
            var oth = other as GraceString;
            if (oth != null)
                return GraceString.Create(Value + oth.Value);
            var op = other as GraceObjectProxy;
            if (op != null)
                return GraceString.Create(Value + op.Object);
            other = other.Request(ctx, MethodRequest.Nullary("asString"));
            oth = (GraceString)other;
            return GraceString.Create(Value + oth.Value);
        }

        /// <summary>Native method for Grace ==</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject EqualsEquals(GraceObject other)
        {
            var oth = other as GraceString;
            return (oth == null) ? GraceBoolean.False
                                 : GraceBoolean.Create(nfc == oth.nfc);
        }

        /// <summary>Native method for Grace !=</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject NotEquals(GraceObject other)
        {
            var oth = other as GraceString;
            return (oth == null) ? GraceBoolean.True
                                 : GraceBoolean.Create(nfc != oth.nfc);
        }

        /// <summary>Native method for Grace at</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject At(GraceObject other)
        {
            var oth = other as GraceNumber;
            if (oth == null)
                return GraceString.Create("bad index");
            int idx = oth.GetInt() - 1;
            int start = graphemeIndices[idx];
            return GraceString.Create(StringInfo.GetNextTextElement(Value, start));
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
            return new GraceString(val);
        }

    }

}
