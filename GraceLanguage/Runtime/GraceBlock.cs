using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>A runtime Grace block</summary>
    public class GraceBlock : GraceObject
    {
        private readonly List<Node> parameters;
        private readonly List<Node> body;
        private readonly Interpreter.ScopeMemo lexicalScope;
        private GraceObject Pattern;
        private bool explicitPattern;

        /// <summary>Whether this block is variadic or not</summary>
        public bool Variadic { get; set; }

        private GraceBlock(EvaluationContext ctx, List<Node> parameters,
                List<Node> body)
        {
            this.parameters = parameters;
            this.body = body;
            lexicalScope = ctx.Memorise();
            AddMethod("apply",
                    new DelegateMethodNodeReq(new NativeMethodReq(this.Apply)));
            if (parameters.Count == 1)
            {
                AddMethod("match",
                    new DelegateMethodNodeReq(
                        new NativeMethodReq(this.Match)));
                AddMethod("|", Matching.OrMethod);
                AddMethod("&", Matching.AndMethod);
                var par = parameters[0];
                var first = par as ParameterNode;
                if (first != null && first.Type != null)
                {
                    Pattern = first.Type.Evaluate(ctx);
                }
                else if (par is NumberNode || par is StringLiteralNode
                        || par is ExplicitReceiverRequestNode)
                {
                    Pattern = par.Evaluate(ctx);
                    explicitPattern = true;
                    this.parameters = new List<Node>(parameters.Skip<Node>(1));
                }
            }
        }

        /// <summary>
        /// Enable this block as a matching block using a specified pattern
        /// </summary>
        /// <param name="pattern">Pattern to use</param>
        public void ForcePattern(GraceObject pattern)
        {
            if (Pattern != null)
                return;
            explicitPattern = true;
            AddMethod("match",
                new DelegateMethodNodeReq(
                    new NativeMethodReq(this.Match)));
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
            Pattern = pattern;
        }

        /// <summary>Make a new block</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="parameters">Parameter list</param>
        /// <param name="body">Nodes in the body of the block</param>
        public static GraceBlock Create(EvaluationContext ctx,
                List<Node> parameters,
                List<Node> body)
        {
            return new GraceBlock(ctx, parameters, body);
        }

        /// <summary>Native method representing the apply method</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request that obtained this method</param>
        public GraceObject Apply(EvaluationContext ctx, MethodRequest req)
        {
            GraceObject ret = null;
            MethodNode.CheckArgCount(ctx, "apply", "apply",
                    parameters.Count, Variadic,
                    req[0].Arguments.Count);
            ctx.Remember(lexicalScope);
            var myScope = new LocalScope(req.Name);
            foreach (var arg in parameters.Zip(req[0].Arguments, (a, b) => new { name = a, val = b }))
            {
                var id = arg.name as ParameterNode;
                if (id != null && id.Variadic)
                {
                    // Populate variadic parameter with all remaining
                    // arguments.
                    var gvl = new GraceVariadicList();
                    for (var i = parameters.Count - 1;
                            i < req[0].Arguments.Count;
                            i++)
                    {
                        gvl.Add(req[0].Arguments[i]);
                    }
                    myScope.AddLocalDef(id.Name, gvl);
                } else {
                    string name = ((IdentifierNode)arg.name).Name;
                    myScope.AddLocalDef(name, arg.val);
                }
            }
            if (Variadic && parameters.Count > req[0].Arguments.Count)
            {
                // Empty variadic parameter.
                var param = parameters.Last();
                var idNode = param as ParameterNode;
                if (idNode != null && idNode.Variadic)
                {
                    var gvl = new GraceVariadicList();
                    myScope.AddLocalDef(idNode.Name, gvl);
                }
            }
            ctx.Extend(myScope);
            foreach (Node n in body)
            {
                ret = n.Evaluate(ctx);
            }
            ctx.Unextend(myScope);
            ctx.Forget(lexicalScope);
            return ret;
        }

        /// <summary>Native method representing the match method</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request that obtained this method</param>
        public GraceObject Match(EvaluationContext ctx, MethodRequest req)
        {
            var target = req[0].Arguments[0];
            if (Pattern == null)
            {
                var rrr = this.Apply(ctx,
                        MethodRequest.Single("apply", target));
                return Matching.SuccessfulMatch(ctx, rrr);
            }
            var matchResult = Matching.Match(ctx, Pattern, target);
            if (!Matching.Succeeded(ctx, matchResult))
            {
                return Matching.FailedMatch(ctx, target);
            }
            var result = matchResult.Request(ctx,
                    MethodRequest.Nullary("result"));
            if (!explicitPattern)
            {
                var res = this.Apply(ctx,
                        MethodRequest.Single("apply", result));
                return Matching.SuccessfulMatch(ctx, res);
            }
            var args = new List<GraceObject>();
            //args.Add(result);
            var doReq = MethodRequest.Single("do", new AddToListBlock(args));
            var bindings = matchResult.Request(ctx,
                    MethodRequest.Nullary("bindings"));
            bindings.Request(ctx, doReq);
            var mReq = new MethodRequest();
            var rpn = new RequestPart("apply", new List<GraceObject>(),
                    args);
            mReq.AddPart(rpn);
            var myResult = this.Apply(ctx, mReq);
            //var myResult = this.Apply(ctx,
            //        MethodRequest.Single("apply", result));
            return Matching.SuccessfulMatch(ctx, myResult);
        }

        private class AddToListBlock : GraceObject
        {
            private List<GraceObject> _dest;

            public AddToListBlock(List<GraceObject> dest)
            {
                _dest = dest;
                AddMethod("apply",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.apply)));
            }

            private GraceObject apply(EvaluationContext ctx,
                    GraceObject arg)
            {
                _dest.Add(arg);
                return GraceObject.Done;
            }
        }

    }

    /// <summary>Object with the Block interface wrapping a Grace
    /// method</summary>
    public class GraceRequestBlock : GraceObject
    {
        private GraceObject receiver;
        private MethodRequest request;

        private GraceRequestBlock(EvaluationContext ctx, GraceObject receiver,
                MethodRequest req)
        {
            AddMethod("apply",
                    new DelegateMethodNodeReq(new NativeMethodReq(this.Apply)));
            this.receiver = receiver;
            this.request = req;
        }

        /// <summary>Make a request block</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="receiver">Receiver to dispatch the method on</param>
        /// <param name="req">Request to send</param>
        public static GraceRequestBlock Create(EvaluationContext ctx,
                GraceObject receiver, MethodRequest req)
        {
            return new GraceRequestBlock(ctx, receiver, req);
        }

        /// <summary>Native method for the Grace apply method</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request that reached this method</param>
        public GraceObject Apply(EvaluationContext ctx, MethodRequest req)
        {
            return receiver.Request(ctx, request);
        }

    }

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
        public static readonly MethodNode AndMethod = new DelegateMethodNodeReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Matching.AndCombinator)
                );

        /// <summary>Reusable method for the | combinator</summary>
        public static readonly MethodNode OrMethod = new DelegateMethodNodeReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Matching.OrCombinator)
                );
    }
}
