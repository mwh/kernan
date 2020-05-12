using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{

    /// <summary>Static class encapsulating various matching
    /// functionality</summary>
    public class Matching
    {
        private Matching() { }

        /// <summary>Match a pattern against a target</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="pattern">Pattern to match</param>
        /// <param name="target">Target of the match</param>
        public static GraceObject Match(EvaluationContext ctx,
                GraceObject pattern,
                GraceObject target)
        {
            var matchReq = new MethodRequest();
            var rp = new RequestPart("match", new List<GraceObject>(),
                    new List<GraceObject>() { target });
            matchReq.AddPart(rp);
            return pattern.Request(ctx, matchReq);
        }

        /// <summary>Create a successful match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="obj">Result value</param>
        public static GraceObject SuccessfulMatch(EvaluationContext ctx,
                GraceObject obj)
        {
            return new MatchResult(true, obj);
        }

        /// <summary>
        /// Attempt to match a pattern against an object, and output the result as well when it succeeds.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="pattern">Pattern to test against</param>
        /// <param name="target">Object to examine</param>
        /// <param name="result">pattern.match(target).result, or null if match failed</param>
        /// <returns>True if match succeeded</returns>
        public static bool TryMatch(EvaluationContext ctx, GraceObject pattern, GraceObject target,
            out GraceObject result)
        {
            if (pattern == null)
            {
                result = target;
                return true;
            }
            var m = Match(ctx, pattern, target);
            if (Succeeded(ctx, m))
            {
                result = GetResult(ctx, m);
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>
        /// Attempt to match a pattern, raising R2025 TypeError on failure.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="pattern">Pattern to match against</param>
        /// <param name="target">Object to examine</param>
        /// <param name="name">Name to report in error (e.g. field or parameter name)</param>
        /// <returns></returns>
        public static GraceObject TypeMatch(EvaluationContext ctx, GraceObject pattern, GraceObject target, string name)
        {
            if (Matching.TryMatch(ctx, pattern, target, out var result))
            {
                return result;
            }
            else
            {
                ErrorReporting.RaiseError(ctx, "R2025",
                    new Dictionary<string, string> { { "field", name },
                                    { "required", GraceString.AsNativeString(ctx, pattern) } },
                    "TypeError: argument type mismatch");
                return null;
            }
        }

        /// <summary>Create a failed match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="obj">Result value</param>
        public static GraceObject FailedMatch(EvaluationContext ctx,
                GraceObject obj)
        {
            return new MatchResult(false, obj);
        }

        /// <summary>Get the result of a MatchResult</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="matchResult">MatchResult to examine</param>
        public static GraceObject GetResult(EvaluationContext ctx,
                GraceObject matchResult)
        {
            return matchResult.Request(ctx, MethodRequest.Nullary("result"));
        }

        /// <summary>Determine whether MatchResult succeeded</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="matchResult">MatchResult to examine</param>
        public static bool Succeeded(EvaluationContext ctx,
                GraceObject matchResult)
        {
            return GraceBoolean.True == matchResult.Request(ctx, MethodRequest.Nullary("succeeded"));
        }

        /// <summary>Determine whether MatchResult failed</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="matchResult">MatchResult to examine</param>
        public static bool Failed(EvaluationContext ctx,
                GraceObject matchResult)
        {
            return GraceBoolean.False == matchResult.Request(ctx, MethodRequest.Nullary("succeeded"));
        }

        /// <summary>Native method for the | combinator</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Left-hand side</param>
        /// <param name="other">Right-hand side</param>
        public static GraceObject OrCombinator(EvaluationContext ctx,
                GraceObject self, GraceObject other)
        {
            return new OrPattern(self, other);
        }

        /// <summary>Native method for the &amp; combinator</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Left-hand side</param>
        /// <param name="other">Right-hand side</param>
        public static GraceObject AndCombinator(EvaluationContext ctx,
                GraceObject self, GraceObject other)
        {
            return new AndPattern(self, other);
        }

        /// <summary>Reusable method for the &amp; combinator</summary>
        public static readonly Method AndMethod = new DelegateMethodReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Matching.AndCombinator)
                );

        /// <summary>Reusable method for the | combinator</summary>
        public static readonly Method OrMethod = new DelegateMethodReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Matching.OrCombinator)
                );
    }

    /// <summary>
    /// Either a Successful or Failed MatchResult.
    /// </summary>
    public class MatchResult : GraceObject
    {
        /// <summary>
        /// True if this is a SuccessfulMatch.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// The result (output) object of the match.
        /// </summary>
        public GraceObject Result { get; set; }

        /// <summary>
        /// Create a new MatchResult with given success state and result.
        /// </summary>
        /// <param name="success">True for successful, false for failed.</param>
        /// <param name="result">Match result</param>
        public MatchResult(bool success, GraceObject result)
        {
            Success = success;
            Result = result;
            if (result == null)
                throw new Exception();
            AddMethod("succeeded", new DelegateMethod0(mSucceeded));
            AddMethod("result", new DelegateMethod0(mResult));
            AddMethod("ifTrue(_)", new DelegateMethod1Ctx(mIfTrue));
            AddMethod("ifFalse(_)", new DelegateMethod1Ctx(mIfFalse));
            AddMethod("ifTrue(_) ifFalse(_)", new DelegateMethodReq(mIfTrueIfFalse));
            AddMethod("asString", new DelegateMethod0Ctx(mAsString));
        }

        private GraceObject mSucceeded()
        {
            return GraceBoolean.Create(Success);
        }

        private GraceObject mResult()
        {
            return Result;
        }

        private GraceObject mAsString(EvaluationContext ctx)
        {
            return GraceString.Create((Success ? "Successful" : "Failed") +"Match["
                + GraceString.AsNativeString(ctx, Result) + "]");
        }

        private GraceObject mIfTrue(EvaluationContext ctx, GraceObject b)
        {
            if (Success)
                return b.Request(ctx, MethodRequest.Nullary("apply"));
            return GraceObject.Done;
        }

        private GraceObject mIfFalse(EvaluationContext ctx, GraceObject b)
        {
            if (!Success)
                return b.Request(ctx, MethodRequest.Nullary("apply"));
            return GraceObject.Done;
        }

        private GraceObject mIfTrueIfFalse(EvaluationContext ctx, MethodRequest req)
        {
            if (Success)
                return req[0].Arguments[0].Request(ctx, MethodRequest.Nullary("apply"));
            return req[1].Arguments[0].Request(ctx, MethodRequest.Nullary("apply"));
        }

    }

    /// <summary>
    /// A pattern that matches when two other patterns do.
    /// </summary>
    class AndPattern : GraceObject
    {
        private GraceObject lhs;
        private GraceObject rhs;

        public AndPattern(GraceObject l, GraceObject r)
        {
            lhs = l;
            rhs = r;
            AddMethod("match(_)", new DelegateMethod1Ctx(mMatch));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
            AddMethod("asString", new DelegateMethod0Ctx(mAsString));
        }

        private GraceObject mMatch(EvaluationContext ctx, GraceObject target)
        {
            if (Matching.TryMatch(ctx, lhs, target, out _))
                return new MatchResult(Matching.TryMatch(ctx, rhs, target, out _), target);
            return new MatchResult(false, target);
        }

        private GraceObject mAsString(EvaluationContext ctx)
        {
            return GraceString.Create(GraceString.AsNativeString(ctx, lhs) +
                " & " + GraceString.AsNativeString(ctx, rhs));
        }
    }

    /// <summary>
    /// A pattern that matches when either of two other patterns does.
    /// </summary>
    class OrPattern : GraceObject
    {
        private GraceObject lhs;
        private GraceObject rhs;

        public OrPattern(GraceObject l, GraceObject r)
        {
            lhs = l;
            rhs = r;
            AddMethod("match(_)", new DelegateMethod1Ctx(mMatch));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
            AddMethod("asString", new DelegateMethod0Ctx(mAsString));
        }

        private GraceObject mMatch(EvaluationContext ctx, GraceObject target)
        {
            if (Matching.TryMatch(ctx, lhs, target, out _))
                return new MatchResult(true, target);
            return new MatchResult(Matching.TryMatch(ctx, rhs, target, out _), target);
        }

        private GraceObject mAsString(EvaluationContext ctx)
        {
            return GraceString.Create(GraceString.AsNativeString(ctx, lhs) +
                " | " + GraceString.AsNativeString(ctx, rhs));
        }
    }

    class NativeTypePattern<T> : GraceObject {
        public NativeTypePattern()
        {
            AddMethod("match(_)", new DelegateMethod1Ctx(mMatch));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="target">Target of the match</param>
        private GraceObject mMatch(EvaluationContext ctx, GraceObject target)
        {
            var gop = target as GraceObjectProxy;
            if (gop == null)
                return Matching.FailedMatch(ctx, target);
            if (gop.Object is T)
                return Matching.SuccessfulMatch(ctx, target);
            return Matching.FailedMatch(ctx, target);
        }
    }

    /// <summary>
    /// A pattern that matches when a CLI predicate is true.
    /// </summary>
    public class PredicatePattern : GraceObject
    {
        private Func<GraceObject, bool> predicate;

        private string desc = "PredicatePattern";

	/// <summary>Create a new PredicatePattern based on pred</summary>
	/// <param name="pred">Predicate function returning true for success</param>
        public PredicatePattern(Func<GraceObject, bool> pred)
        {
            predicate = pred;
            AddMethod("match(_)", new DelegateMethod1Ctx(mMatch));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
            AddMethod("asString", new DelegateMethod0(mAsString));
        }

	/// <summary>Create a new PredicatePattern based on pred</summary>
	/// <param name="pred">Predicate function returning true for success</param>
	/// <param name="d">Description of the pattern object to use in asString</param>
        public PredicatePattern(Func<GraceObject, bool> pred, string d)
        {
            predicate = pred;
            desc = d;
            AddMethod("match(_)", new DelegateMethod1Ctx(mMatch));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
            AddMethod("asString", new DelegateMethod0(mAsString));
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="target">Target of the match</param>
        private GraceObject mMatch(EvaluationContext ctx, GraceObject target)
        {
            if (predicate(target))
                return Matching.SuccessfulMatch(ctx, target);
            return Matching.FailedMatch(ctx, target);
        }

        private GraceObject mAsString()
        {
            return GraceString.Create(desc);
        }
    }
}
