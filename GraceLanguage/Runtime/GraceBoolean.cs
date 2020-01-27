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
        private static Dictionary<string, Method> sharedMethods;

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
            : base(createSharedMethods())
        {
            Boolean = val;
        }

        private static Dictionary<string, Method> createSharedMethods()
        {
            if (sharedMethods != null)
                return sharedMethods;
            sharedMethods = new Dictionary<string, Method>
            {
                { "prefix!", new DelegateMethodTyped0<GraceBoolean>(Negate) },
                { "not", new DelegateMethodTyped0<GraceBoolean>(Negate) },
                { "hash", new DelegateMethodTyped0<GraceBoolean>(mHash) },
                { "&&(_)", new DelegateMethodTyped1Ctx<GraceBoolean>(AndAnd) },
                { "||(_)", new DelegateMethodTyped1Ctx<GraceBoolean>(OrOr) },
                { "ifTrue(_)", new DelegateMethodTyped1Ctx<GraceBoolean>(IfTrue) },
                { "ifFalse(_)", new DelegateMethodTyped1Ctx<GraceBoolean>(IfFalse) },
                { "ifTrue(_) ifFalse(_)", new DelegateMethodTyped<GraceBoolean>(IfTrueIfFalse) },
                { "andAlso(_)", new DelegateMethodTyped1Ctx<GraceBoolean>(AndAlso) },
                { "orElse(_)", new DelegateMethodTyped1Ctx<GraceBoolean>(OrElse) },
                { "match(_)", new DelegateMethodTyped1Ctx<GraceBoolean>(Match) },
                { "asString", new DelegateMethodTyped0<GraceBoolean>(AsString) },
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

        /// <summary>Native method for Grace !</summary>
        public static GraceObject Negate(GraceBoolean self)
        {
            if (self.Boolean)
                return GraceBoolean.False;
            return GraceBoolean.True;
        }

        private static GraceObject mHash(GraceBoolean self)
        {
            return GraceNumber.Create(self.Boolean.GetHashCode());
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="target">Target of the match</param>
        public static GraceObject Match(EvaluationContext ctx, GraceBoolean self, GraceObject target)
        {
            var b = target as GraceBoolean;
            if (b != null && b.Boolean == self.Boolean)
                return Matching.SuccessfulMatch(ctx, target);
            return Matching.FailedMatch(ctx, target);
        }

        /// <summary>Native method for Grace asString</summary>
        public static GraceObject AsString(GraceBoolean self)
        {
            if (self.Boolean)
                return GraceString.Create("true");
            return GraceString.Create("false");
        }

        /// <summary>Native method for Grace &amp;&amp;</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        public static GraceObject AndAnd(EvaluationContext ctx, GraceBoolean self, GraceObject other)
        {
            GraceBoolean oth = other as GraceBoolean;
            if (oth != null)
                return GraceBoolean.Create(self.Boolean && oth.Boolean);
            GraceObjectProxy op = other as GraceObjectProxy;
            if (op != null)
                return GraceBoolean.Create(self.Boolean && (dynamic)op.Object);
            ErrorReporting.RaiseError(ctx, "R2001",
                    new Dictionary<string, string>() {
                        { "method", "&&" },
                        { "index", "1" },
                        { "part", "&&" },
                        { "required", "Boolean" }
                    },
                    "ArgumentTypeError: && requires a Boolean argument"
            );
            return GraceBoolean.False;
        }

        /// <summary>Native method for Grace ||</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Argument to the method</param>
        public static GraceObject OrOr(EvaluationContext ctx, GraceBoolean self, GraceObject other)
        {
            GraceBoolean oth = other as GraceBoolean;
            if (oth != null)
                return GraceBoolean.Create(self.Boolean || oth.Boolean);
            GraceObjectProxy op = other as GraceObjectProxy;
            if (op != null)
                return GraceBoolean.Create(self.Boolean || (dynamic)op.Object);
            ErrorReporting.RaiseError(ctx, "R2001",
                    new Dictionary<string, string>() {
                        { "method", "||" },
                        { "index", "1" },
                        { "part", "||" },
                        { "required", "Boolean" }
                    },
                    "ArgumentTypeError: || requires a Boolean argument"
            );
            return GraceBoolean.False;
        }

        /// <summary>Native method for Grace ifTrue</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Block to apply if true</param>
        public static GraceObject IfTrue(EvaluationContext ctx, GraceBoolean self, GraceObject other)
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
            if (self.Boolean)
            {
                var req = MethodRequest.Nullary("apply");
                other.Request(ctx, req);
            }
            return GraceObject.Done;
        }

        /// <summary>Native method for Grace ifFalse</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Block to apply if false</param>
        public static GraceObject IfFalse(EvaluationContext ctx, GraceBoolean self, GraceObject other)
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
            if (!self.Boolean)
            {
                var req = MethodRequest.Nullary("apply");
                other.Request(ctx, req);
            }
            return GraceObject.Done;
        }

        /// <summary>Native method for Grace ifTrue ifFalse</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="req">Method request that gave rise to this method
        /// execution</param>
        public static GraceObject IfTrueIfFalse(EvaluationContext ctx,
                MethodRequest req, GraceBoolean self)
        {
            MethodHelper.CheckArity(ctx, req, 1, 1);
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
            if (self.Boolean)
            {
                return trueBlock.Request(ctx, apply);
            }
            return falseBlock.Request(ctx, apply);
        }

        /// <summary>Native method for Grace andAlso</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Block to apply if true</param>
        public static GraceObject AndAlso(EvaluationContext ctx, GraceBoolean self, GraceObject other)
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
            if (self.Boolean)
            {
                var req = MethodRequest.Nullary("apply");
                return other.Request(ctx, req);
            }
            return False;
        }

        /// <summary>Native method for Grace orElse</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the method</param>
        /// <param name="other">Block to apply if false</param>
        public static GraceObject OrElse(EvaluationContext ctx, GraceBoolean self, GraceObject other)
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
            if (!self.Boolean)
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

        private static GraceObject _trueBlock
            = GraceRequestBlock.Create(null, False,
                    MethodRequest.Nullary("prefix!"));
        private static GraceObject _falseBlock
            = GraceRequestBlock.Create(null, True,
                    MethodRequest.Nullary("prefix!"));

        /// <summary>
        /// Determine whether a Grace object is truthy or not.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="val">Grace object to test for truthiness</param>
        /// <remarks>
        /// If the value is the true or false boolean literal, this
        /// method returns the appropriate value immediately. If it is
        /// an object conforming to the boolean type, this method requests
        /// ifTrue ifFalse on the object with suitable blocks and returns
        /// the result. If it is neither, an error will be reported.
        /// </remarks>
        public static bool IsTrue(EvaluationContext ctx, GraceObject val)
        {
            if (val == True)
                return true;
            if (val == False)
                return false;
            var req = new MethodRequest();
            req.AddPart(new RequestPart("ifTrue",
                        new List<GraceObject> { _trueBlock },
                        RequestPart.EmptyList
                        )
                    );
            req.AddPart(new RequestPart("ifFalse",
                        new List<GraceObject> { _falseBlock },
                        RequestPart.EmptyList
                        )
                    );
            return (val.Request(ctx, req) == True);
        }

    }
}
