using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;
using Grace.Parsing;

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
        private BlockNode node;
        private GraceString stringification;

        /// <summary>Whether this block is variadic or not</summary>
        public bool Variadic { get; set; }

        private readonly string applyName;

        private GraceBlock(EvaluationContext ctx, List<Node> parameters,
                List<Node> body, BlockNode node)
        {
            this.parameters = parameters;
            this.body = body;
            this.node = node;
            lexicalScope = ctx.Memorise();
            applyName = MethodHelper.ArityNamePart("apply", parameters.Count);
            AddMethod(applyName, null);
            AddMethod("spawn", null);
            AddMethod("asString", null);
            if (parameters.Count == 1)
            {
                AddMethod("match(_)", null);
                AddMethod("|(_)", Matching.OrMethod);
                AddMethod("&(_)", Matching.AndMethod);
                var par = parameters[0];
                var first = par as ParameterNode;
                if (first != null && first.Type != null)
                {
                    Pattern = first.Type.Evaluate(ctx);
                }
                else if (par is NumberLiteralNode || par is StringLiteralNode
                        || par is ExplicitReceiverRequestNode)
                {
                    Pattern = par.Evaluate(ctx);
                    explicitPattern = true;
                    var t = par.Location;
                    var id = new IdentifierParseNode(
                            new IdentifierToken(t.module, t.line, t.column,
                                "_"));
                    this.parameters = new List<Node> {
                        new ParameterNode(t, id)
                    };
                }
            }
        }

        private GraceBlock(Action<GraceObject> act)
        {
            AddMethod("apply(_)", null);
            AddMethod("spawn", null);
            AddMethod("asString", null);
            stringification = GraceString.Create("{ _ -> native code }");
        }

        /// <inheritdoc />
        protected override Method getLazyMethod(string name)
        {
            if (name == applyName)
                return new DelegateMethodReq(Apply);
            switch(name) {
                case "match(_)": return new DelegateMethodReq(Match);
                case "spawn": return new DelegateMethod0Ctx(mSpawn);
                case "asString": return new DelegateMethod0Ctx(mAsString);
            }
            return base.getLazyMethod(name);
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
            AddMethod("match(_)",
                new DelegateMethodReq(
                    new NativeMethodReq(this.Match)));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
            Pattern = pattern;
        }

        /// <summary>Make a new block</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="parameters">Parameter list</param>
        /// <param name="body">Nodes in the body of the block</param>
        /// <param name="node">AST node of this block</param>
        public static GraceBlock Create(EvaluationContext ctx,
                List<Node> parameters,
                List<Node> body,
                BlockNode node)
        {
            return new GraceBlock(ctx, parameters, body, node);
        }

        private GraceObject mAsString(EvaluationContext ctx)
        {
            if (stringification != null)
                return stringification;
            var p = ParseNodeMeta.PrettyPrint(ctx, node.Origin);
            if (p.LastIndexOf(Environment.NewLine) != -1
                    && node.Body.Count != 1)
            {
                var last = "...";
                if (node.Body.Count > 0)
                {
                    last = "... " + ParseNodeMeta.PrettyPrint(ctx,
                            node.Body.Last().Origin);
                }
                var bpn = node.Origin as BlockParseNode;
                if (bpn == null)
                    p = String.Join(", ",
                            from x in parameters
                            select ParseNodeMeta.PrettyPrint(ctx, x.Origin))
                        + " -> " + last;
                else
                    p = String.Join(", ",
                            from x in bpn.Parameters
                            select ParseNodeMeta.PrettyPrint(ctx, x))
                        + " -> " + last;
            }
            stringification = GraceString.Create("Block["
                    + p
                    + "]");
            return stringification;
        }

        private GraceObject mSpawn(EvaluationContext ctx)
        {
            return new GraceThread(ctx, this);
        }
        /// <summary>Native method representing the apply method</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request that obtained this method</param>
        public GraceObject Apply(EvaluationContext ctx, MethodRequest req)
        {
            GraceObject ret = GraceObject.Done;
            MethodNode.CheckArgCount(ctx, "apply", "apply",
                    parameters.Count, Variadic,
                    req[0].Arguments.Count);
            ctx.Remember(lexicalScope);
            var myScope = new LocalScope(req.Name);
            // Bind any local methods (types) on the scope
            foreach (var localMeth in body.OfType<MethodNode>())
            {
                myScope.AddMethod(localMeth.Name, new Method(localMeth,
                            lexicalScope));
            }
            // Bind parameters and arguments
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
            MethodHelper.CheckArity(ctx, req, 1);
            var target = req[0].Arguments[0];
            if (Pattern == null)
            {
                var rrr = this.Apply(ctx,
                        MethodRequest.Single("apply(_)", target));
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
                        MethodRequest.Single("apply(_)", result));
                return Matching.SuccessfulMatch(ctx, res);
            }
            var res2 = this.Apply(ctx,
                    MethodRequest.Single("apply(_)", result));
            return Matching.SuccessfulMatch(ctx, res2);
        }

        private class AddToListBlock : GraceObject
        {
            private List<GraceObject> _dest;

            public AddToListBlock(List<GraceObject> dest)
            {
                _dest = dest;
                AddMethod("apply(_)",
                    new DelegateMethod1Ctx(
                        new NativeMethod1Ctx(this.apply)));
            }

            private GraceObject apply(EvaluationContext ctx,
                    GraceObject arg)
            {
                _dest.Add(arg);
                return GraceObject.Done;
            }
        }

        /// <summary>
        /// Create a one-parameter block from an action.
        /// </summary>
        /// <param name="act">Action to wrap</param>
        public static NativeBlock_1d Create(Action<GraceObject> act)
        {
            return act;
        }

    }

    /// <summary>
    /// Wraps an Action&lt;GraceObject&gt; into a GraceObject block.
    /// </summary>
    public class NativeBlock_1d : GraceObject
    {
        private GraceString stringification;
        private Action<GraceObject> action;

        private NativeBlock_1d(Action<GraceObject> act)
        {
            action = act;
            AddMethod("apply(_)", null);
            AddMethod("asString", null);
            stringification = GraceString.Create("{ _ -> native code }");
        }

        /// <summary>
        /// Convert a lambda with a single argument to a block.
        /// </summary>
        /// <param name="act">Lambda/action to wrap</param>
        public static implicit operator NativeBlock_1d(Action<GraceObject> act)
        {
            return new NativeBlock_1d(act);
        }

        private GraceObject mApply(GraceObject arg)
        {
            action(arg);
            return GraceObject.Done;
        }

        /// <inheritdoc />
        protected override Method getLazyMethod(string name)
        {
            if (name == "apply(_)")
                return new DelegateMethod1(mApply);
            switch(name) {
                case "asString": return new DelegateMethod0Ctx(mAsString);
            }
            return base.getLazyMethod(name);
        }

        private GraceObject mAsString(EvaluationContext ctx)
        {
            return stringification;
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
                    new DelegateMethodReq(new NativeMethodReq(this.Apply)));
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
}
