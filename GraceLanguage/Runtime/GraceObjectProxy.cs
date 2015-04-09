using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Grace.Execution;

namespace Grace.Runtime
{
    public class GraceObjectProxy : GraceObject
    {
        Object obj;
        Type type;

        public object Object
        {
            get
            {
                return obj;
            }
        }

        public GraceObjectProxy(Object obj)
        {
            Interpreter.Debug("created proxy object for: " + obj.GetType() + " " + obj);
            this.obj = obj;
            this.type = obj.GetType();
        }

        public override bool RespondsTo(MethodRequest req)
        {
            MethodInfo meth = type.GetMethod(req.Name);
            if (meth != null)
                return true;
            return base.RespondsTo(req);
        }

        public override GraceObject Request(EvaluationContext ctx, MethodRequest req)
        {
            string name = req.Name;
            switch (name)
            {
                case "+":
                    return GraceObjectProxy.Create((dynamic)obj + (dynamic)viewAsNative(req[0].Arguments[0]));
                case "-":
                    return GraceObjectProxy.Create((dynamic)obj - (dynamic)viewAsNative(req[0].Arguments[0]));
                case "*":
                    return GraceObjectProxy.Create((dynamic)obj * (dynamic)viewAsNative(req[0].Arguments[0]));
                case "/":
                    return GraceObjectProxy.Create((dynamic)obj / (dynamic)viewAsNative(req[0].Arguments[0]));
                case "<":
                    return GraceObjectProxy.Create((dynamic)obj < (dynamic)viewAsNative(req[0].Arguments[0]));
                case "<=":
                    return GraceObjectProxy.Create((dynamic)obj <= (dynamic)viewAsNative(req[0].Arguments[0]));
                case ">":
                    return GraceObjectProxy.Create((dynamic)obj > (dynamic)viewAsNative(req[0].Arguments[0]));
                case ">=":
                    return GraceObjectProxy.Create((dynamic)obj >= (dynamic)viewAsNative(req[0].Arguments[0]));
                case "==":
                    return GraceObjectProxy.Create((dynamic)obj == (dynamic)viewAsNative(req[0].Arguments[0]));
                case "!=":
                    return GraceObjectProxy.Create((dynamic)obj != (dynamic)viewAsNative(req[0].Arguments[0]));
                case "%":
                    return GraceObjectProxy.Create((dynamic)obj % (dynamic)viewAsNative(req[0].Arguments[0]));
                case "^":
                    return GraceObjectProxy.Create(Math.Pow((dynamic)obj, (dynamic)viewAsNative(req[0].Arguments[0])));
                case "asString":
                    return GraceString.Create(obj.ToString());
                case "prefix!":
                    return GraceObjectProxy.Create(!(dynamic)obj);
            }
            object[] args = new object[req[0].Arguments.Count];
            for (int i = 0; i < req[0].Arguments.Count; i++)
                args[i] = viewAsNative(req[0].Arguments[i]);
            Type[] types = new Type[args.Length];
            for (int i = 0; i < types.Length; i++)
                types[i] = args[i].GetType();
            MethodInfo meth = type.GetMethod(name, types);
            if (meth == null)
                ErrorReporting.RaiseError(ctx, "R2000",
                        new Dictionary<string, string>() {
                            { "method", req.Name },
                            { "receiver", "Native Proxy" }
                        },
                        "LookupError: Native proxy failed to find method «${method}»"
                );
            return GraceObjectProxy.Create(meth.Invoke(obj, args));
        }

        private static object viewAsNative(Object obj)
        {
            if (obj is GraceObjectProxy)
                return viewAsNative(((GraceObjectProxy)obj).Object);
            if (obj is GraceNumber)
                return (obj as GraceNumber).Double;
            return obj;
        }

        public static GraceObject Create(Object o)
        {
            if (o is bool)
            {
                return GraceBoolean.Create((dynamic)o);
            }
            return new GraceObjectProxy(o);
        }
    }
}
