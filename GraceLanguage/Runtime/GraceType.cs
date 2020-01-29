using System.Collections.Generic;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>A Grace type literal</summary>
    public class GraceType : GraceObject
    {
        internal static Dictionary<string, Method> sharedMethods = new Dictionary<string, Method> { };

        private readonly string name;
        private List<SignatureNode> methods = new List<SignatureNode>();
        private List<MethodRequest> requests;

        /// <param name="name">Name of this type for debugging
        /// and reporting purposes</param>
        public GraceType(string name)
        {
            this.name = name;
            AddMethod("match(_)", new DelegateMethod1Ctx(
                        new NativeMethod1Ctx(this.Match)));
            AddMethod("|(_)", Matching.OrMethod);
            AddMethod("&(_)", Matching.AndMethod);
            AddMethods(GraceType.sharedMethods);
        }

        /// <summary>
        /// Apply an extension trait to all future instances of this type.
        /// </summary>
        /// <param name="meths">
        /// Dictionary of methods to add.
        /// </param>
        public static void ExtendWith(IDictionary<string, Method> meths)
        {
            foreach (var m in meths)
                sharedMethods[m.Key] = m.Value;
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

        /// <inheritdoc />
        public override string ToString()
        {
            return "GraceType[" + name + "]";
        }

        /// <summary>Unknown (dynamic) type</summary>
        public static GraceType Unknown = new GraceType("Unknown");
    }

}
