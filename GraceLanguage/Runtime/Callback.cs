using System;
using System.Collections.Generic;
using Grace.Execution;

namespace Grace.Runtime
{
    // A new version of "Method.cs" that's much easier to use!
    public static class Callback
    {
        /// <summary> A callback for a method on a receiver with type T0, and taking any number of arguments </summary>
        public static Method Nary<T0>(Func<EvaluationContext, T0, MethodRequest, GraceObject> m) where T0 : GraceObject
            => new DelegateMethodInheritable((ctx, req, self) => m(ctx, (T0)self, req));

        /// <summary> A callback for a method on a receiver with type T0, and taking no arguments </summar>
        public static Method Nullary<T0>(Func<EvaluationContext, T0, GraceObject> m) where T0 : GraceObject 
            => CustomArity<T0>((ctx, self, req) => m(ctx, self));

        /// <summary> A callback for a method on a receiver with type T0, and taking one argument of type T1 </summar>
        public static Method Unary<T0, T1>(Func<EvaluationContext, T0, T1, GraceObject> m) where T0 : GraceObject where T1 : GraceObject
            => CustomArity<T0>((ctx, self, req) => m(ctx, self, GetArg<T1>(ctx, req, 0, 0)), 1);

        /// <summary> A callback for a method on a receiver with type T0, and taking two arguments of type T1 and T2 in one part </summar>
        public static Method Binary<T0, T1, T2>(Func<EvaluationContext, T0, T1, T2, GraceObject> m) where T0 : GraceObject where T1 : GraceObject where T2 : GraceObject
            => CustomArity<T0>((ctx, self, req) => m(ctx, self, GetArg<T1>(ctx, req, 0, 0), GetArg<T2>(ctx, req, 0, 1)), 2);

        /// <summary> A callback for a method on a receiver with type T0, and taking one argument of type T1 in the first part, and one of type T2 in the second part </summar>
        public static Method UnaryUnary<T0, T1, T2>(Func<EvaluationContext, T0, T1, T2, GraceObject> m) where T0 : GraceObject where T1 : GraceObject where T2 : GraceObject
            => CustomArity<T0>((ctx, self, req) => m(ctx, self, GetArg<T1>(ctx, req, 0, 0), GetArg<T2>(ctx, req, 1, 0)), 1, 1);

        // Utility method to check the arity before calling "m"
        private static Method CustomArity<T0>(Func<EvaluationContext, T0, MethodRequest, GraceObject> m, params int[] arities) where T0 : GraceObject
            => Nary<T0>((ctx, self, req) => {
                MethodHelper.CheckArity(ctx, req, arities);
                return m(ctx, self, req);});

        // A utility method to convert types accordingly
        private static T GetArg<T>(EvaluationContext ctx, MethodRequest req, int part, int index) where T : GraceObject {
            var arg = req[part].Arguments[index].FindNativeParent<T>();
            if (arg == null) ErrorReporting.RaiseError(ctx, "R2001",
                new Dictionary<string, string> {
                    { "method", req.Name },
                    { "index", index.ToString() },
                    { "part", req[part].Name }
                }, "must be a " + typeof(T).Name.Replace("Grace", ""));
            return arg; }
    }
}
