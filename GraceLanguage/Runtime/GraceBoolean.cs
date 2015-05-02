using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;


namespace Grace.Runtime
{
    /// <summary>A Grace boolean object</summary>
    public class GraceBoolean : GraceObject
    {
        /// <summary>The true singleton</summary>
        public static GraceBoolean True = new GraceBoolean(true);

        /// <summary>The false singleton</summary>
        public static GraceBoolean False = new GraceBoolean(false);

        /// <summary>The truth value of this boolean</summary>
        public bool Boolean
        {
            get;
            set;
        }
        private GraceBoolean(bool val)
        {
            Boolean = val;
            AddMethod("prefix!",
                    new DelegateMethodNode0(new NativeMethod0(this.Negate)));
            AddMethod("not",
                    new DelegateMethodNode0(new NativeMethod0(this.Negate)));
            AddMethod("&&",
                    new DelegateMethodNode1(new NativeMethod1(this.AndAnd)));
            AddMethod("||",
                    new DelegateMethodNode1(new NativeMethod1(this.OrOr)));
            AddMethod("ifTrue",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.IfTrue)));
            AddMethod("ifFalse",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.IfFalse)));
            AddMethod("ifTrue ifFalse",
                    new DelegateMethodNodeReq(
                        new NativeMethodReq(this.IfTrueIfFalse)));
            AddMethod("andAlso",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.AndAlso)));
            AddMethod("orElse",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.OrElse)));
            AddMethod("match", new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.Match)));
            AddMethod("asString",
                    new DelegateMethodNode0(new NativeMethod0(this.AsString)));
        }

        /// <summary>Native method for Grace !</summary>
        public GraceObject Negate()
        {
            if (Boolean)
                return GraceBoolean.False;
            return GraceBoolean.True;
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="target">Target of the match</param>
        public GraceObject Match(EvaluationContext ctx, GraceObject target)
        {
            var b = target as GraceBoolean;
            if (b != null && b.Boolean == Boolean)
                return Matching.SuccessfulMatch(ctx, target);
            return Matching.FailedMatch(ctx, target);
        }

        /// <summary>Native method for Grace asString</summary>
        public new GraceObject AsString()
        {
            if (Boolean)
                return GraceString.Create("true");
            return GraceString.Create("false");
        }

        /// <summary>Native method for Grace &amp;&amp;</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject AndAnd(GraceObject other)
        {
            GraceBoolean oth = other as GraceBoolean;
            if (oth != null)
                return GraceBoolean.Create(this.Boolean && oth.Boolean);
            GraceObjectProxy op = other as GraceObjectProxy;
            if (op != null)
                return GraceBoolean.Create(this.Boolean && (dynamic)op.Object);
            return GraceBoolean.False;
        }

        /// <summary>Native method for Grace ||</summary>
        /// <param name="other">Argument to the method</param>
        public GraceObject OrOr(GraceObject other)
        {
            GraceBoolean oth = other as GraceBoolean;
            if (oth != null)
                return GraceBoolean.Create(this.Boolean || oth.Boolean);
            GraceObjectProxy op = other as GraceObjectProxy;
            if (op != null)
                return GraceBoolean.Create(this.Boolean || (dynamic)op.Object);
            return GraceBoolean.False;
        }

        /// <summary>Native method for Grace ifTrue</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Block to apply if true</param>
        public GraceObject IfTrue(EvaluationContext ctx, GraceObject other)
        {
            var oth = other as GraceBlock;
            if (oth == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string>() {
                            { "method", "ifTrue" },
                            { "index", "1" },
                            { "part", "ifTrue" },
                            { "required", "Block" }
                        },
                        "ArgumentTypeError: ifTrue requires a block argument"
                );
            if (Boolean)
            {
                var req = MethodRequest.Nullary("apply");
                other.Request(ctx, req);
            }
            return GraceObject.Done;
        }

        /// <summary>Native method for Grace ifFalse</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Block to apply if false</param>
        public GraceObject IfFalse(EvaluationContext ctx, GraceObject other)
        {
            var oth = other as GraceBlock;
            if (oth == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string>() {
                            { "method", "ifFalse" },
                            { "index", "1" },
                            { "part", "ifFalse" },
                            { "required", "Block" }
                        },
                        "ArgumentTypeError: ifFalse requires a block argument"
                );
            if (!Boolean)
            {
                var req = MethodRequest.Nullary("apply");
                other.Request(ctx, req);
            }
            return GraceObject.Done;
        }

        /// <summary>Native method for Grace ifTrue ifFalse</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Method request that gave rise to this method
        /// execution</param>
        public GraceObject IfTrueIfFalse(EvaluationContext ctx,
                MethodRequest req)
        {
            var trueBlock = req[0].Arguments[0];
            var falseBlock = req[1].Arguments[0];
            if (!(trueBlock is GraceBlock))
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string>() {
                            { "method", req.Name },
                            { "index", "1" },
                            { "part", "ifTrue" },
                            { "required", "Block" }
                        },
                        "ArgumentTypeError: ifTrue ifFalse requires two block arguments"
                );
            if (!(falseBlock is GraceBlock))
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string>() {
                            { "method", req.Name },
                            { "index", "1" },
                            { "part", "ifFalse" },
                            { "required", "Block" }
                        },
                        "ArgumentTypeError: ifTrue ifFalse requires two block arguments"
                );
            var apply = MethodRequest.Nullary("apply");
            if (Boolean)
            {
                return trueBlock.Request(ctx, apply);
            }
            return falseBlock.Request(ctx, apply);
        }

        /// <summary>Native method for Grace andAlso</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Block to apply if true</param>
        public GraceObject AndAlso(EvaluationContext ctx, GraceObject other)
        {
            var oth = other as GraceBlock;
            if (oth == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string>() {
                            { "method", "andAlso" },
                            { "index", "1" },
                            { "part", "ifFalse" },
                            { "required", "Block" }
                        },
                        "ArgumentTypeError: andAlso requires a block argument"
                );
            if (Boolean)
            {
                var req = MethodRequest.Nullary("apply");
                return other.Request(ctx, req);
            }
            return False;
        }

        /// <summary>Native method for Grace orElse</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Block to apply if false</param>
        public GraceObject OrElse(EvaluationContext ctx, GraceObject other)
        {
            var oth = other as GraceBlock;
            if (oth == null)
                ErrorReporting.RaiseError(ctx, "R2001",
                        new Dictionary<string, string>() {
                            { "method", "orElse" },
                            { "index", "1" },
                            { "part", "ifFalse" },
                            { "required", "Block" }
                        },
                        "ArgumentTypeError: orElse requires a block argument"
                );
            if (!Boolean)
            {
                var req = MethodRequest.Nullary("apply");
                return other.Request(ctx, req);
            }
            return True;
        }

        /// <summary>Create a Grace boolean</summary>
        /// <param name="val">Which of true or false to get</param>
        public static GraceObject Create(bool val)
        {
            if (val)
                return GraceBoolean.True;
            return GraceBoolean.False;
        }

    }
}
