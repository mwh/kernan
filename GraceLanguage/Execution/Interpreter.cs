using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Runtime;
using System.IO;
using Grace.Parsing;

namespace Grace.Execution
{
    public class Interpreter : EvaluationContext
    {
        private static bool debugMessagesActive = false;
        private Dictionary<string, GraceObject> modules = new Dictionary<string, GraceObject>();

        internal class ScopeLink
        {
            public ScopeLink next;
            public GraceObject scope;
            public bool minor;

            public ScopeLink() { }

            public ScopeLink(ScopeLink next, GraceObject scope)
            {
                this.next = next;
                this.scope = scope;
            }

            public ScopeLink(ScopeLink next, GraceObject scope,
                    bool minor)
            {
                this.next = next;
                this.scope = scope;
                this.minor = minor;
            }
        }

        public class ScopeMemo
        {
            private ScopeLink link;
            internal ScopeLink Link { get { return link; } }
            internal int dynamicsSize;
            internal ScopeMemo(ScopeLink link, int dynamics)
            {
                this.link = link;
                this.dynamicsSize = dynamics;
            }
        }

        private GraceObject prelude;
        private Stack<ScopeLink> dynamics = new Stack<ScopeLink>();
        private Stack<string> callStack = new Stack<string>();
        private ScopeLink scope = new ScopeLink();
        private GraceObject majorScope;
        OutputSink sink;

        public Interpreter()
        {
            sink = new OutputSinkWrapper(System.Console.Out);
            ErrorReporting.SetSink(new OutputSinkWrapper(System.Console.Error));
            initialise();
        }

        public Interpreter(OutputSink s)
        {
            sink = s;
            ErrorReporting.SetSink(s);
            initialise();
        }

        private void initialise()
        {
            scope.scope = new GraceObject();
            majorScope = scope.scope;
            scope.scope.AddMethod("print", new DelegateMethodNode1Ctx(this.Print));
            scope.scope.AddLocalDef("true", GraceBoolean.True);
            scope.scope.AddLocalDef("false", GraceBoolean.False);
            scope.scope.AddMethod("_base_while_do", new DelegateMethodNodeReq(this.BaseWhileDo));
            scope.scope.AddMethod("_base_try_catch_finally", new DelegateMethodNodeReq(this.BaseTryCatchFinally));
            scope.scope.AddMethod("Exception",
                    new ConstantMethodNode(
                        new GraceExceptionKind("Exception")));
        }

        public void LoadPrelude()
        {
            string dir = System.IO.Path.GetDirectoryName(typeof(Interpreter).Assembly.Location);
            string preludePath = Path.Combine(dir, "prelude.grace");
            using (StreamReader preludeReader = File.OpenText(preludePath))
            {
                var parser = new Parser(preludeReader.ReadToEnd());
                var pt = parser.Parse() as ObjectParseNode;
                var eMod = new ExecutionTreeTranslator().Translate(pt);
                prelude = eMod.Evaluate(this);
                Extend(prelude);
                Interpreter.Debug("========== END PRELUDE ==========");
            }
        }

        public static List<string> GetModulePaths()
        {
            string execDir = System.IO.Path.GetDirectoryName(typeof(Interpreter).Assembly.Location);
            string localGrace = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "grace");
            var bases = new List<string>() { localGrace, execDir };
            return bases;
        }

        public GraceObject LoadModule(string path)
        {
            if (modules.ContainsKey(path))
                return modules[path];
            var bases = GetModulePaths();
            foreach (var p in bases)
            {
                var filePath = Path.Combine(Path.Combine(p, "modules"), path + ".grace");
                var mod = tryLoadModuleFile(filePath);
                if (mod != null)
                {
                    modules[path] = mod;
                    return mod;
                }
            }
            ErrorReporting.RaiseError(this, "R2005", new Dictionary<string, string>() { { "path", path } },
                "LookupError: Could not find module ${path}");
            return null;
        }

        private GraceObject tryLoadModuleFile(string path)
        {
            if (System.IO.File.Exists(path))
            {
                return loadModuleFile(path);
            }
            return null;
        }

        private GraceObject loadModuleFile(string path)
        {
            Interpreter.Debug("========== LOAD " + path + " ==========");
            using (StreamReader reader = File.OpenText(path))
            {
                var parser = new Parser(reader.ReadToEnd());
                var pt = parser.Parse() as ObjectParseNode;
                var eMod = new ExecutionTreeTranslator().Translate(pt);
                var ret = eMod.Evaluate(this);
                Interpreter.Debug("========== END " + path + " ==========");
                return ret;
            }
        }

        public void InsertOuter(GraceObject obj)
        {
            var s = scope;
            while (s != null)
            {
                if (s.scope.HasFlag(GraceObject.Flags.UserspaceObject))
                    break;
                s = s.next;
            }
            var newScope = new ScopeLink(s.next, obj);
            s.next = newScope;
        }

        public GraceObject PreludeRequest(MethodRequest req)
        {
            return prelude.Request(this, req);
        }

        public List<string> GetStackTrace()
        {
            return new List<string>(callStack);
        }

        public GraceObject Print(EvaluationContext ctx, GraceObject arg)
        {
            Object obj = arg;
            if (arg is GraceObjectProxy)
            {
                obj = ((GraceObjectProxy)arg).Object;
            }
            if (obj is GraceObject)
            {
                var go = (GraceObject)obj;
                var req = new MethodRequest();
                var part = RequestPart.Nullary("asString");
                req.AddPart(part);
                if (go.RespondsTo(req))
                {
                    obj = go.Request(ctx, req);
                }
            }
            if (obj is GraceString)
                obj = ((GraceString)obj).Value.Replace("\u2028", Environment.NewLine);
            sink.WriteLine("" + obj);
            return GraceObject.Done;
        }

        public GraceObject BaseWhileDo(EvaluationContext ctx, MethodRequest req)
        {
            GraceObject cond = req[0].Arguments[0];
            GraceObject block = req[0].Arguments[1];
            MethodRequest apply = MethodRequest.Nullary("apply");
            while (cond.Request(ctx, apply) == GraceBoolean.True)
            {
                block.Request(ctx, apply);
            }
            return GraceObject.Done;
        }

        public GraceObject BaseTryCatchFinally(EvaluationContext ctx,
                MethodRequest req)
        {
            Interpreter.ScopeMemo memo = ctx.Memorise();
            GraceObject tryBlock = null;
            GraceObject finallyBlock = null;
            var catchBlocks = new List<GraceObject>();
            /*foreach (var pt in req)
            {
                if (pt.Name == "try")
                    tryBlock = pt.Arguments[0];
                else if (pt.Name == "catch")
                    catchBlocks.Add(pt.Arguments[0]);
                else if (pt.Name == "finally")
                    finallyBlock = pt.Arguments[0];
            }*/
            tryBlock = req[0].Arguments[0];
            finallyBlock = req[0].Arguments[1];
            for (int i = 2; i < req[0].Arguments.Count; i++)
                catchBlocks.Add(req[0].Arguments[i]);
            try
            {
                tryBlock.Request(ctx, MethodRequest.Nullary("apply"));
            }
            catch (GraceExceptionPacketException e)
            {
                ctx.RestoreExactly(memo);
                GraceObject gep = e.ExceptionPacket;
                MethodRequest matchReq = MethodRequest.Single("match",
                        gep);
                var caught = false;
                foreach (var cb in catchBlocks)
                {
                    var mr = cb.Request(ctx, matchReq);
                    if (Matching.Succeeded(ctx, mr))
                    {
                        caught = true;
                        break;
                    }
                }
                if (!caught)
                    throw;
            }
            finally
            {
                ctx.RestoreExactly(memo);
                if (finallyBlock != null)
                    finallyBlock.Request(ctx, MethodRequest.Nullary("apply"));
            }
            return GraceObject.Done;
        }

        public int NestRequest(string module, int line, string name)
        {
            callStack.Push("«" + name + "», at line " + line + " of " + module);
            return callStack.Count - 1;
        }

        public void PopCallStackTo(int depth)
        {
            while (callStack.Count > depth)
                callStack.Pop();
        }

        public void Extend(GraceObject o)
        {
            scope = new ScopeLink(scope, o);
            majorScope = scope.scope;
        }

        public void ExtendMinor(GraceObject o)
        {
            scope = new ScopeLink(scope, o, true);
        }

        public void Unextend(GraceObject o)
        {
            scope = scope.next;
            restoreMajor();
        }

        public ScopeMemo Memorise()
        {
            return new ScopeMemo(scope, dynamics.Count);
        }

        public void RestoreExactly(ScopeMemo sm)
        {
            scope = sm.Link;
            while (dynamics.Count > sm.dynamicsSize)
                dynamics.Pop();
            restoreMajor();
        }

        public void Remember(ScopeMemo sm)
        {
            dynamics.Push(scope);
            scope = sm.Link;
            restoreMajor();
        }

        public void Forget(ScopeMemo sm)
        {
            scope = dynamics.Pop();
            restoreMajor();
        }

        private void restoreMajor()
        {
            ScopeLink s = scope;
            while (s.minor)
            {
                s = s.next;
            }
            majorScope = s.scope;
        }

        public GraceObject FindReceiver(MethodRequest req)
        {
            ScopeLink sl = scope;
            while (sl != null && sl.scope != null)
            {
                if (sl.scope.RespondsTo(req))
                    return sl.scope;
                sl = sl.next;
            }
            return null;
        }

        public MethodScope FindNearestMethod()
        {
            ScopeLink sl = scope;
            while (sl != null && sl.scope != null)
            {
                MethodScope ms = sl.scope as MethodScope;
                if (ms != null)
                    return ms;
                sl = sl.next;
            }
            return null;
        }

        public ReaderWriterPair AddVar(string name, GraceObject val)
        {
            var pair = majorScope.AddLocalVar(name, val);
            return pair;
        }

        public MethodNode AddDef(string name, GraceObject val)
        {
            return majorScope.AddLocalDef(name, val);
        }

        public MethodNode AddMinorDef(string name, GraceObject val)
        {
            return scope.scope.AddLocalDef(name, val);
        }

        public string ScopeStringList()
        {
            string ret = null;
            ScopeLink s = scope;
            while (s != null && s.scope != null)
            {
                if (ret != null)
                    ret += ", " + s.scope;
                else
                    ret = "" + s.scope;
                s = s.next;
            }
            return ret;
        }

        public static void ActivateDebuggingMessages()
        {
            debugMessagesActive = true;
        }

        public void DebugScopes()
        {
            ScopeLink s = scope;
            while (s != null && s.scope != null)
            {
                Debug("Scope " + s.scope);
                foreach (string k in s.scope.MethodNames())
                {
                    Debug("    " + k);
                }
                s = s.next;
            }
        }

        public static void Debug(string message)
        {
            if (!debugMessagesActive)
                return;
            if (!System.Console.IsErrorRedirected)
            {
                System.Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            System.Console.Error.WriteLine(message);
            if (!System.Console.IsErrorRedirected)
            {
                System.Console.ResetColor();
            }
        }
    }

    public struct ReaderWriterPair
    {
        public MethodNode Read;
        public MethodNode Write;
    }

    public interface EvaluationContext
    {
        void Extend(GraceObject o);
        void ExtendMinor(GraceObject o);
        void Unextend(GraceObject o);
        void InsertOuter(GraceObject o);

        int NestRequest(string module, int line, string name);
        void PopCallStackTo(int depth);

        GraceObject PreludeRequest(MethodRequest req);
        GraceObject FindReceiver(MethodRequest req);
        MethodScope FindNearestMethod();
        GraceObject LoadModule(string path);


        ReaderWriterPair AddVar(string name, GraceObject val);
        MethodNode AddDef(string name, GraceObject val);
        MethodNode AddMinorDef(string name, GraceObject val);

        Interpreter.ScopeMemo Memorise();
        void RestoreExactly(Interpreter.ScopeMemo sm);
        void Remember(Interpreter.ScopeMemo sm);
        void Forget(Interpreter.ScopeMemo sm);

        List<string> GetStackTrace();

        string ScopeStringList();
        void DebugScopes();
    }

    public interface OutputSink
    {
        void WriteLine(string s);

    }

    public class OutputSinkWrapper : OutputSink
    {
        System.IO.TextWriter writer;

        public OutputSinkWrapper(System.IO.TextWriter w)
        {
            writer = w;
        }

        public void WriteLine(string s)
        {
            writer.WriteLine(s);
        }
    }
}
