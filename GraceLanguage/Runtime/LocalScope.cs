using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{
    public class LocalScope : GraceObject
    {
        public static readonly LocalReaderMethod Reader = new LocalReaderMethod();
        public static readonly LocalWriterMethod Writer = new LocalWriterMethod();

        public Dictionary<string, GraceObject> locals = new Dictionary<string, GraceObject>();

        private string name = "<anon>";

        public LocalScope() { }

        public LocalScope(string name)
        {
            this.name = name;
        }

        public override string ToString()
        {
            if (name != null)
                return "GraceObject[" + name + "]";
            return "GraceObject";
        }

        public void AddLocalDef(string name)
        {
            AddLocalDef(name, GraceObject.Uninitialised);
        }

        public override MethodNode AddLocalDef(string name, GraceObject val)
        {
            locals[name] = val;
            AddMethod(name, Reader);
            return Reader;
        }

        public void AddLocalVar(string name)
        {
            AddLocalVar(name, GraceObject.Uninitialised);
        }

        public override ReaderWriterPair AddLocalVar(string name, GraceObject val)
        {
            locals[name] = val;
            AddMethod(name, Reader);
            AddMethod(name + ":=", Writer);
            return new ReaderWriterPair { Read = Reader, Write = Writer };
        }

        public GraceObject this[string s]
        {
            get
            {
                return locals[s];
            }
            set
            {
                locals[s] = value;
            }
        }
    }

    public class LocalReaderMethod : MethodNode
    {
        public LocalReaderMethod()
            : base(null, null)
        {
        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            LocalScope s = self as LocalScope;
            string name = req.Name;
            Interpreter.Debug("local '" + name + "' is " + s[name]);
            return s[name];
        }
    }

    public class LocalWriterMethod : MethodNode
    {
        public LocalWriterMethod()
            : base(null, null)
        {

        }

        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            LocalScope s = self as LocalScope;
            string name = req.Name.Substring(0, req.Name.Length - 2);
            s[name] = req[0].Arguments[0];
            Interpreter.Debug("local '" + name + "' set to " + s[name]);
            return GraceObject.Uninitialised;
        }
    }

}
