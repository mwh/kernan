using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Grace.Execution;

namespace Grace.Runtime
{
    class GraceString : GraceObject
    {
        private string nfc;
        private int[] graphemeIndices;
        public string Value
        {
            get;
            set;
        }
        private GraceString(string val)
        {
            Interpreter.Debug("made new string " + val);
            Value = val;
            nfc = val.Normalize();
            if (val.Length > 0)
                graphemeIndices = StringInfo.ParseCombiningCharacters(val);
            else
                graphemeIndices = new int[0];
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
                return GraceString.Create(this.Value + oth.Value);
            GraceObjectProxy op = other as GraceObjectProxy;
            if (op != null)
                return GraceString.Create(this.Value + op.Object.ToString());
            other = other.Request(ctx, MethodRequest.Nullary("asString"));
            oth = other as GraceString;
            return GraceString.Create(this.Value + oth.Value);
        }

        new public GraceObject EqualsEquals(GraceObject other)
        {
            var oth = other as GraceString;
            if (oth == null)
                return GraceBoolean.False;
            return GraceBoolean.Create(this.nfc == oth.nfc);
        }

        new public GraceObject NotEquals(GraceObject other)
        {
            var oth = other as GraceString;
            if (oth == null)
                return GraceBoolean.True;
            return GraceBoolean.Create(this.nfc != oth.nfc);
        }

        public GraceObject At(GraceObject other)
        {
            var oth = other as GraceNumber;
            if (oth == null)
                return GraceString.Create("bad index");
            int idx = oth.GetInt() - 1;
            int start = graphemeIndices[idx];
            return GraceString.Create(StringInfo.GetNextTextElement(Value, start));
        }

        public GraceObject Size()
        {
            return GraceNumber.Create(graphemeIndices.Length);
        }

        public GraceObject Match(EvaluationContext ctx, GraceObject target)
        {
            if (this.EqualsEquals(target) == GraceBoolean.True)
            {
                return Matching.SuccessfulMatch(ctx, target);
            }
            return Matching.FailedMatch(ctx, target);
        }

        new public GraceObject AsString()
        {
            return this;
        }

        public override string ToString()
        {
            return "GraceString[\"" + Value + "\"]";
        }

        public static GraceObject Create(string val)
        {
            return new GraceString(val);
        }

    }

}
