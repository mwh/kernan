using System.Collections.Generic;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>A Grace type literal</summary>
    public class GraceType : GraceObject
    {
        private readonly string name;
        private List<SignatureNode> methods = new List<SignatureNode>();
        private List<MethodRequest> requests;
        private readonly Interpreter.ScopeMemo scope;

        /// <param name="name">Name of this type for debugging
        /// and reporting purposes</param>
        /// <param name="scope">Evaluation scope of this interface
        /// literal, for resolving names later on</param>
        public GraceType(string name, Interpreter.ScopeMemo scope)
        {
            this.name = name;
            this.scope = scope;
            AddMethod("match(_)", new DelegateMethod1Ctx(
                        new NativeMethod1Ctx(this.Match)));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
            AddMethod("|>(_)", Matching.ChainMethod);
            AddMethod("signatures", new DelegateMethod0Ctx(
            new NativeMethod0Ctx(this.Signatures)));

        }

        /// <summary>Add a method type entry to this type</summary>
        /// <param name="n">Method entry to add</param>
        public void Add(SignatureNode n)
        {
            methods.Add(n);
        }

        /// <summary>Native method implementing the .match Grace method
        /// for types</summary>
        /// <remarks>At present, this matching uses only the method
        /// names in both the object and the type.</remarks>
        public GraceObject Match(EvaluationContext ctx, GraceObject target)
        {
            if (requests == null)
            {
                var l = new List<MethodRequest>();
                foreach (var m in methods)
                {
                    var req = new MethodRequest();
                    foreach (var p in m)
                    {
                        var rp = RequestPart.Nullary(p.Name);
                        req.AddPart(rp);
                    }
                    l.Add(req);
                }
                requests = l;
            }
            foreach (var req in requests)
            {
                if (!target.RespondsTo(req))
                    return Matching.FailedMatch(ctx, target);
            }
            return Matching.SuccessfulMatch(ctx, target);
        }

        private GraceObject Signatures(EvaluationContext ctx)
        {
            return GraceVariadicList.Of(this.methods);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "GraceType[" + name + "]";
        }

        /// <summary>Unknown (dynamic) type</summary>
        public static GraceType Unknown = new GraceType("Unknown", null);
    }

}
