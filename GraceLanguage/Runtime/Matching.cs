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
            var successfulMatchReq = MethodRequest.Single("_SuccessfulMatch",
                    obj);
            GraceObject smRec = ctx.FindReceiver(successfulMatchReq);
            return smRec.Request(ctx, successfulMatchReq);
        }

        /// <summary>Create a failed match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="obj">Result value</param>
        public static GraceObject FailedMatch(EvaluationContext ctx,
                GraceObject obj)
        {
            var successfulMatchReq = MethodRequest.Single("_FailedMatch",
                    obj);
            GraceObject smRec = ctx.FindReceiver(successfulMatchReq);
            return smRec.Request(ctx, successfulMatchReq);
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
            var patReq = MethodRequest.Single("_OrPattern",
                    self);
            patReq[0].Arguments.Add(other);
            GraceObject patRec = ctx.FindReceiver(patReq);
            return patRec.Request(ctx, patReq);
        }

        /// <summary>Native method for the &amp; combinator</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Left-hand side</param>
        /// <param name="other">Right-hand side</param>
        public static GraceObject AndCombinator(EvaluationContext ctx,
                GraceObject self, GraceObject other)
        {
            var patReq = MethodRequest.Single("_AndPattern",
                    self);
            patReq[0].Arguments.Add(other);
            GraceObject patRec = ctx.FindReceiver(patReq);
            return patRec.Request(ctx, patReq);
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

    class NativeTypePattern<T> : GraceObject {
        public NativeTypePattern()
        {
            AddMethod("match", new DelegateMethod1Ctx(mMatch));
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
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
}
