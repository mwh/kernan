using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{
    public class GraceBlock : GraceObject
    {
        private readonly List<Node> parameters;
        private readonly List<Node> body;
        private readonly Interpreter.ScopeMemo lexicalScope;
        private GraceObject Pattern;

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
                    this.parameters = new List<Node>(parameters.Skip<Node>(1));
                }
            }
        }

        public static GraceBlock Create(EvaluationContext ctx, List<Node> parameters,
                List<Node> body)
        {
            return new GraceBlock(ctx, parameters, body);
        }

        public GraceObject Apply(EvaluationContext ctx, MethodRequest req)
        {
            GraceObject ret = null;
            if (parameters.Count > req[0].Arguments.Count)
            {
                ErrorReporting.RaiseError(ctx, "R2004",
                        new Dictionary<string, string>() {
                            { "method", req.Name },
                            { "part", "apply" },
                            { "need", parameters.Count.ToString() },
                            { "have", req[0].Arguments.Count.ToString() }
                        },
                        "InsufficientArgumentsError: Insufficient arguments for block"
                );
            }
            ctx.Remember(lexicalScope);
            var myScope = new LocalScope(req.Name);
            foreach (var arg in parameters.Zip(req[0].Arguments, (a, b) => new { name = a, val = b }))
            {
                string name = ((IdentifierNode)arg.name).Name;
                myScope.AddLocalDef(name, arg.val);
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
            var myResult = this.Apply(ctx,
                    MethodRequest.Single("apply", result));
            return Matching.SuccessfulMatch(ctx, myResult);
        }
    }

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

        public static GraceRequestBlock Create(EvaluationContext ctx,
                GraceObject receiver, MethodRequest req)
        {
            return new GraceRequestBlock(ctx, receiver, req);
        }

        public GraceObject Apply(EvaluationContext ctx, MethodRequest req)
        {
            return receiver.Request(ctx, request);
        }

    }

    public class Matching
    {
        private Matching() { }

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

        public static GraceObject SuccessfulMatch(EvaluationContext ctx,
                GraceObject obj)
        {
            var successfulMatchReq = MethodRequest.Single("_SuccessfulMatch",
                    obj);
            GraceObject smRec = ctx.FindReceiver(successfulMatchReq);
            return smRec.Request(ctx, successfulMatchReq);
        }

        public static GraceObject FailedMatch(EvaluationContext ctx,
                GraceObject obj)
        {
            var successfulMatchReq = MethodRequest.Single("_FailedMatch",
                    obj);
            GraceObject smRec = ctx.FindReceiver(successfulMatchReq);
            return smRec.Request(ctx, successfulMatchReq);
        }

        public static GraceObject GetResult(EvaluationContext ctx,
                GraceObject matchResult)
        {
            return matchResult.Request(ctx, MethodRequest.Nullary("result"));
        }

        public static bool Succeeded(EvaluationContext ctx,
                GraceObject matchResult)
        {
            return GraceBoolean.True == matchResult.Request(ctx, MethodRequest.Nullary("succeeded"));
        }

        public static bool Failed(EvaluationContext ctx,
                GraceObject matchResult)
        {
            return GraceBoolean.False == matchResult.Request(ctx, MethodRequest.Nullary("succeeded"));
        }

        public static GraceObject OrCombinator(EvaluationContext ctx,
                GraceObject self, GraceObject other)
        {
            var patReq = MethodRequest.Single("_OrPattern",
                    self);
            patReq[0].Arguments.Add(other);
            GraceObject patRec = ctx.FindReceiver(patReq);
            return patRec.Request(ctx, patReq);
        }

        public static GraceObject AndCombinator(EvaluationContext ctx,
                GraceObject self, GraceObject other)
        {
            var patReq = MethodRequest.Single("_AndPattern",
                    self);
            patReq[0].Arguments.Add(other);
            GraceObject patRec = ctx.FindReceiver(patReq);
            return patRec.Request(ctx, patReq);
        }

        public static readonly MethodNode AndMethod = new DelegateMethodNodeReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Matching.AndCombinator)
                );
        public static readonly MethodNode OrMethod = new DelegateMethodNodeReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Matching.OrCombinator)
                );
    }
}
