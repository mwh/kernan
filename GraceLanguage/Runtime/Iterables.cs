using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{
    public static class Iterables
    {
        public static GraceObject Concatenate(GraceObject left,
                GraceObject right)
        {
            return new Concatenated(left, right);
        }

        public class Concatenated : GraceObject
        {
            private GraceObject left, right;
            public Concatenated(GraceObject l, GraceObject r)
            {
                left = l;
                right = r;
                AddMethod("do",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.Do)));
                AddMethod("++", Iterables.ConcatMethod);
            }

            public GraceObject Concat(EvaluationContext ctx, GraceObject other)
            {
                return Iterables.Concatenate(this, other);
            }

            public GraceObject Do(EvaluationContext ctx, GraceObject block)
            {
                var req = MethodRequest.Single("do", block);
                left.Request(ctx, req);
                right.Request(ctx, req);
                return GraceObject.Done;
            }
        }

        public static GraceObject MConcat(EvaluationContext ctx,
                GraceObject receiver,
                GraceObject other)
        {
            return Iterables.Concatenate(receiver, other);
        }

        public static readonly MethodNode ConcatMethod = new DelegateMethodNodeReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Iterables.MConcat)
                );
    }
    public class GraceVariadicList : GraceObject
    {
        private List<GraceObject> elements = new List<GraceObject>();
        public GraceVariadicList()
        {
            AddMethod("do",
                new DelegateMethodNode1Ctx(
                    new NativeMethod1Ctx(this.Do)));
            AddMethod("++", Iterables.ConcatMethod);
        }

        public void Add(GraceObject obj)
        {
            elements.Add(obj);
        }

        public GraceObject Concat(EvaluationContext ctx, GraceObject other)
        {
            return Iterables.Concatenate(this, other);
        }

        public GraceObject Do(EvaluationContext ctx, GraceObject block)
        {
            var req = MethodRequest.Single("apply", null);
            foreach (var o in elements)
            {
                req[0].Arguments[0] = o;
                block.Request(ctx, req);
            }
            return GraceObject.Done;
        }
    }

}
