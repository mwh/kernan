using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Grace.Execution;

namespace Grace.Runtime
{
    /// <summary>Grace object providing access to a native object</summary>
    public class GraceObjectProxy : GraceObject
    {
        Object obj;
        Type type;

        /// <summary>The wrapped object</summary>
        /// <value>This property gets the value of the field obj</value>
        public object Object
        {
            get
            {
                return obj;
            }
        }

        /// <param name="obj">Object to proxy</param>
        public GraceObjectProxy(Object obj)
        {
            this.obj = obj;
            if (obj != null)
                this.type = obj.GetType();
        }

        /// <inheritsdoc/>
        /// <remarks>Uses reflection to determine whether the native
        /// object handles this method</remarks>
        public override bool RespondsTo(MethodRequest req)
        {
            MethodInfo meth = type.GetMethod(req.Name);
            if (meth != null)
                return true;
            return base.RespondsTo(req);
        }

        /// <inheritsdoc/>
        /// <remarks>Uses reflection to access the method, or the
        /// dynamic type to access operators.</remarks>
        public override GraceObject Request(EvaluationContext ctx, MethodRequest req)
        {
            string name = req.Name;
            switch (name)
            {
                case "isNull":
                    if (obj == null)
                        return GraceBoolean.True;
                    return GraceBoolean.False;
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
                    if (obj == null)
                        return GraceString.Create("(null)");
                    return GraceString.Create(obj.ToString());
                case "prefix!":
                    return GraceObjectProxy.Create(!(dynamic)obj);
                case "at":
                    if (Interpreter.JSIL)
                        // Calling get_Item directly is iffy, but
                        // works on JSIL where accessing Item fails,
                        // and [] uses native (the wrong) [].
                        return GraceObjectProxy.Create(
                            ((dynamic)obj)
                                .get_Item(
                                    (dynamic)viewAsNative(req[0].Arguments[0]))
                            );
                    return GraceObjectProxy.Create(
                            ((dynamic)obj)[
                                (dynamic)viewAsNative(req[0].Arguments[0])]);
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
            {
                var d = ((GraceNumber)obj).Double;
                if (d == (int)d)
                    return (int)d;
                return d;
            }
            if (obj is GraceString)
            {
                return ((GraceString)obj).Value;
            }
            return obj;
        }

        /// <summary>Make a proxy for an object</summary>
        /// <param name="o">Object to proxy</param>
        public static GraceObject Create(Object o)
        {
            if (o is bool)
            {
                return GraceBoolean.Create((dynamic)o);
            }
            if (o is int)
                return GraceNumber.Create((dynamic)o);
            var s = o as string;
            if (s != null)
                return GraceString.Create(s);
            return new GraceObjectProxy(o);
        }
    }
}
