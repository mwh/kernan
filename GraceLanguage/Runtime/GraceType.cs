using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{
    public class GraceType : GraceObject
    {
        private string name;
        private List<MethodTypeNode> methods = new List<MethodTypeNode>();
        private List<MethodRequest> requests;

        public GraceType(string name)
            : base()
        {
            this.name = name;
            AddMethod("match", new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.Match)));
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
        }

        public void Add(MethodTypeNode n)
        {
            methods.Add(n);
        }

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

        public override string ToString()
        {
            return "GraceType[" + name + "]";
        }

        public static GraceType Unknown = new GraceType("Unknown");
    }

}
