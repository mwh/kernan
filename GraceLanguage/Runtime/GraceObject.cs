using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{
    public class GraceObject
    {
        public enum Flags
        {
            UserspaceObject = 1,
            RunAtModuleEnd = 2
        }
        private Dictionary<string, MethodNode> methods = new Dictionary<string, MethodNode>();
        private Dictionary<string, GraceObject> fields = new Dictionary<string, GraceObject>();

        private FieldReaderMethod Reader;
        private FieldWriterMethod Writer;

        private Interpreter.ScopeMemo lexicalScope;
        private Flags flags;

        private string name;
        public string TagName
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }

        public GraceObject()
        {
            initialise();
        }

        public GraceObject(string name)
        {
            initialise();
            this.name = name;
        }

        private void initialise()
        {
            Reader = new FieldReaderMethod(fields);
            Writer = new FieldWriterMethod(fields);
            AddMethod("asString", new DelegateMethodNode0(
                        new NativeMethod0(this.AsString)));
            AddMethod("==", new DelegateMethodNode1(new NativeMethod1(this.EqualsEquals)));
            AddMethod("!=", new DelegateMethodNode1(new NativeMethod1(this.NotEquals)));
        }

        public override string ToString()
        {
            if (name != null)
                return "GraceObject[" + name + "]";
            return "GraceObject";
        }

        public virtual GraceObject AsString()
        {
            return GraceString.Create(this.ToString());
        }

        public GraceObject EqualsEquals(GraceObject other)
        {
            return GraceBoolean.Create(this == other);
        }

        public GraceObject NotEquals(GraceObject other)
        {
            return GraceBoolean.Create(this != other);
        }

        public void RememberScope(EvaluationContext ctx)
        {
            lexicalScope = ctx.Memorise();
        }

        public virtual void AddMethod(MethodNode m)
        {
            methods[m.Name] = m;
        }

        public virtual void AddMethod(string name, MethodNode m)
        {
            methods[name] = m;
        }

        public virtual ReaderWriterPair AddLocalVar(string name, GraceObject val)
        {
            fields[name] = val;
            AddMethod(name, Reader);
            AddMethod(name + ":=", Writer);
            return new ReaderWriterPair { Read = Reader, Write = Writer };
        }

        public virtual MethodNode AddLocalDef(string name, GraceObject val)
        {
            fields[name] = val;
            var read = new FieldReaderMethod(fields);
            AddMethod(name, read);
            return read;
        }

        public List<string> MethodNames()
        {
            return new List<string>(methods.Keys);
        }

        public virtual bool RespondsTo(MethodRequest req)
        {
            return methods.ContainsKey(req.Name);
        }

        public virtual GraceObject Request(EvaluationContext ctx, MethodRequest req)
        {
            if (lexicalScope != null)
                ctx.Remember(lexicalScope);
            if (!methods.ContainsKey(req.Name))
            {
                ErrorReporting.RaiseError(ctx, "R2000",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "receiver", ToString() }
                        },
                        "LookupError: Method «" + req.Name
                            + "» not found in object «" + ToString() + "»"
                );
            }
            MethodNode m = methods[req.Name];
            Interpreter.Debug("calling method " + req.Name);
            var ret = m.Respond(ctx, this, req);
            if (lexicalScope != null)
                ctx.Forget(lexicalScope);
            return ret;
        }

        public void SetFlag(Flags f)
        {
            this.flags |= f;
        }

        public bool HasFlag(Flags f)
        {
            return flags.HasFlag(f);
        }

        public static readonly GraceObject Uninitialised = new GraceObject();
        public static readonly GraceObject Done = new GraceObject("Done");
    }

    public class FieldReaderMethod : MethodNode
    {
        private Dictionary<string, GraceObject> fields;

        public FieldReaderMethod(Dictionary<string, GraceObject> fields)
            : base(null, null)
        {
            this.fields = fields;
            Confidential = true;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            string name = req.Name;
            Interpreter.Debug("field '" + name + "' is " + fields[name]);
            return fields[name];
        }
    }

    public class FieldWriterMethod : MethodNode
    {
        private Dictionary<string, GraceObject> fields;

        public FieldWriterMethod(Dictionary<string, GraceObject> fields)
            : base(null, null)
        {
            this.fields = fields;
            Confidential = true;
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            string name = req.Name.Substring(0, req.Name.Length - 2);
            fields[name] = req[0].Arguments[0];
            Interpreter.Debug("field '" + name + "' set to " + fields[name]);
            return GraceObject.Uninitialised;
        }
    }

}
