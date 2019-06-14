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

        /// <param name="name">Name of this type for debugging
        /// and reporting purposes</param>
        public GraceType(string name)
        {
            this.name = name;
            AddMethod("match(_)", Callback.Unary<GraceType, GraceObject>((ctx, self, target) =>
                self.DoesMatch(ctx, target) ? Matching.SuccessfulMatch(ctx, target) : Matching.FailedMatch(ctx, target)));
            AddMethod("matches(_)", Callback.Unary<GraceType, GraceObject>((ctx, self, target) =>
                GraceBoolean.Create(self.DoesMatch(ctx, target))));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
        }

        /// <summary>Add a method type entry to this type</summary>
        /// <param name="n">Method entry to add</param>
        public void Add(SignatureNode n)
        {
            methods.Add(n);
        }

        /// <summary>method used for both the .matches and .match grace methods</summary>
        /// <remarks>At present, this matching uses only the method
        /// names in both the object and the type.</remarks>
        public bool DoesMatch(EvaluationContext ctx, GraceObject target)
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
                    return false;
            }
            return true;
        }

        /// <summary>Native method implementing the .match Grace method
        /// for types</summary>
        public GraceObject Matches(EvaluationContext ctx, GraceObject target)
        {
            return DoesMatch(ctx, target) ? Matching.SuccessfulMatch(ctx, target) : Matching.FailedMatch(ctx, target);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "GraceType[" + name + "]";
        }

        /// <summary>Unknown (dynamic) type</summary>
        public static GraceType Unknown = new GraceType("Unknown");
    }

}
