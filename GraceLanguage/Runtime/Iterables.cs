using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>Encapsulates behaviour relating to iterables</summary>
    public static class Iterables
    {
        /// <summary>Create an iterable from the concatenation of
        /// two existing iterables</summary>
        /// <param name="left">First iterable</param>
        /// <param name="right">Second iterable</param>
        public static GraceObject Concatenate(GraceObject left,
                GraceObject right)
        {
            return new Concatenated(left, right);
        }

        /// <summary>Iterable that iterates through one iterable
        /// followed by another</summary>
        public class Concatenated : GraceObject
        {
            private GraceObject left, right;

            /// <param name="l">First iterable</param>
            /// <param name="r">Second iterable</param>
            public Concatenated(GraceObject l, GraceObject r)
            {
                left = l;
                right = r;
                AddMethod("do",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.Do)));
                AddMethod("++", Iterables.ConcatMethod);
                TagName = "ConcatenatedIterables";
            }

            /// <summary>Native method for Grace ++</summary>
            /// <param name="ctx">Current interpreter</param>
            /// <param name="other">Second iterable to concatenate</param>
            public GraceObject Concat(EvaluationContext ctx, GraceObject other)
            {
                return Iterables.Concatenate(this, other);
            }

            /// <summary>Native method for Grace do</summary>
            /// <param name="ctx">Current interpreter</param>
            /// <param name="block">Block to apply for each element</param>
            public GraceObject Do(EvaluationContext ctx, GraceObject block)
            {
                var req = MethodRequest.Single("do", block);
                left.Request(ctx, req);
                right.Request(ctx, req);
                return GraceObject.Done;
            }
        }

        /// <summary>Reusable native method for concatenation</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="receiver">Object on which the method was
        /// requested</param>
        /// <param name="other">Argument to the method</param>
        public static GraceObject MConcat(EvaluationContext ctx,
                GraceObject receiver,
                GraceObject other)
        {
            return Iterables.Concatenate(receiver, other);
        }

        /// <summary>Reusable method for ++</summary>
        public static readonly MethodNode ConcatMethod = new DelegateMethodNodeReceiver1Ctx(
                    new NativeMethodReceiver1Ctx(Iterables.MConcat)
                );
    }

    /// <summary>The iterable created by *variadic parameters</summary>
    public class GraceVariadicList : GraceObject
    {
        private List<GraceObject> elements = new List<GraceObject>();

        /// <summary>Empty list</summary>
        public GraceVariadicList()
        {
            AddMethod("do",
                new DelegateMethodNode1Ctx(
                    new NativeMethod1Ctx(this.Do)));
            AddMethod("++", Iterables.ConcatMethod);
            AddMethod("with do",
                new DelegateMethodNodeReq(
                    new NativeMethodReq(this.WithDo)));
            TagName = "VariadicList";
        }

        /// <summary>Add an object to the list</summary>
        /// <param name="obj">Object to add</param>
        public void Add(GraceObject obj)
        {
            elements.Add(obj);
        }

        /// <summary>Native method for Grace ++</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="other">Second iterable to concatenate</param>
        public GraceObject Concat(EvaluationContext ctx, GraceObject other)
        {
            return Iterables.Concatenate(this, other);
        }

        /// <summary>Native method for Grace do</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="block">Block to apply for each element</param>
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

        private GraceObject WithDo(EvaluationContext ctx, MethodRequest req)
        {
            var with = req[0].Arguments[0];
            var block = req[1].Arguments[0];
            var withBlock = new WithBlock(elements, block);
            var innerReq = MethodRequest.Single("do", withBlock);
            with.Request(ctx, innerReq);
            return GraceObject.Done;
        }

        private class WithBlock : GraceObject
        {
            private List<GraceObject> _elements;
            private GraceObject _block;
            private int index;

            public WithBlock(List<GraceObject> elements,
                    GraceObject block) {
                _elements = elements;
                _block = block;
                AddMethod("apply",
                    new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(this.apply)));
            }

            private GraceObject apply(EvaluationContext ctx,
                    GraceObject arg)
            {
                if (index >= _elements.Count)
                    return GraceObject.Done;
                var el = _elements[index++];
                var req = new MethodRequest();
                var rpn = new RequestPart("apply",
                    new List<GraceObject>(),
                    new List<GraceObject>() {
                        el, arg
                    }
                );
                req.AddPart(rpn);
                _block.Request(ctx, req);
                return GraceObject.Done;
            }
        }
    }

}
