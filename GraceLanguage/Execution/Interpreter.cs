using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Runtime;
using System.IO;
using System.Reflection;
using Grace.Parsing;

namespace Grace.Execution
{
    /// <summary>An interpreter tracking the state of the world in an
    /// execution of a Grace program.</summary>
    public class Interpreter : EvaluationContext
    {
        private static bool debugMessagesActive = false;
        private Dictionary<string, GraceObject> modules = new Dictionary<string, GraceObject>();

        /// <summary>Linked-list node for a stack of object scopes</summary>
        internal class ScopeLink
        {
            public ScopeLink next;
            public GraceObject scope;
            public bool minor;

            public ScopeLink() { }

            /// <param name="next">Next element in the list, or null</param>
            /// <param name="scope">Grace object to be used as a scope</param>
            public ScopeLink(ScopeLink next, GraceObject scope)
            {
                this.next = next;
                this.scope = scope;
            }

            /// <param name="next">Next element in the list, or null</param>
            /// <param name="scope">Grace object to be used as a scope</param>
            /// <param name="minor">True if this link represents a "minor"
            /// scope that is used for internal names</param>
            public ScopeLink(ScopeLink next, GraceObject scope,
                    bool minor)
            {
                this.next = next;
                this.scope = scope;
                this.minor = minor;
            }
        }

        /// <summary>A rememberable Memo that allows restoring interpreter
        /// state at a later point</summary>
        public class ScopeMemo
        {
            private ScopeLink link;
            internal ScopeLink Link { get { return link; } }
            internal int dynamicsSize;
            /// <param name="link">Top of the static scope stack</param>
            /// <param name="dynamics">Size of the dynamic stack</param>
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
        private OutputSink sink;

        /// <summary>A default interpreter</summary>
        public Interpreter()
        {
            sink = new OutputSinkWrapper(System.Console.Out);
            ErrorReporting.SetSink(new OutputSinkWrapper(System.Console.Error));
            initialise();
        }

        /// <param name="s">Destination for error messages</param>
        public Interpreter(OutputSink s)
        {
            sink = s;
            ErrorReporting.SetSink(s);
            initialise();
        }

        /// <summary>Performs set-up behaviour shared by multiple
        /// constructors</summary>
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

        /// <summary>Finds the standard prelude file, loads and
        /// interprets it, and places the created module in scope</summary>
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

        /// <summary>Gives the directory paths searched for imports</summary>
        public static List<string> GetModulePaths()
        {
            string execDir = System.IO.Path.GetDirectoryName(typeof(Interpreter).Assembly.Location);
            string localGrace = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "grace");
            var bases = new List<string>() { localGrace, execDir };
            return bases;
        }

        /// <inheritdoc />
        /// <remarks>The import path will be resolved according to
        /// the directories given by
        /// <c cref="GetModulePaths">GetModulePaths</c>. If this import
        /// path has been loaded previously, the existing module object
        /// is returned.</remarks>
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

        /// <summary>Load a module file if it exists</summary>
        private GraceObject tryLoadModuleFile(string path)
        {
            if (System.IO.File.Exists(path))
            {
                return loadModuleFile(path);
            }
            return null;
        }

        /// <summary>Load a module file</summary>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public GraceObject PreludeRequest(MethodRequest req)
        {
            return prelude.Request(this, req);
        }

        /// <inheritdoc />
        public List<string> GetStackTrace()
        {
            return new List<string>(callStack);
        }

        /// <summary>The built-in Grace "print" method</summary>
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

        /// <summary>Native version of the built-in Grace "while-do" method
        /// </summary>
        /// <remarks>This while-do is more efficient than is possible to
        /// implement in bare Grace without it.</remarks>
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

        /// <summary>Native version of the built-in Grace
        /// "try-*catch-?finally" method
        /// </summary>
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

        /// <inheritdoc />
        public int NestRequest(string module, int line, string name)
        {
            callStack.Push("«" + name + "», at line " + line + " of " + module);
            return callStack.Count - 1;
        }

        /// <inheritdoc />
        public void PopCallStackTo(int depth)
        {
            while (callStack.Count > depth)
                callStack.Pop();
        }

        /// <inheritdoc />
        public void Extend(GraceObject o)
        {
            scope = new ScopeLink(scope, o);
            majorScope = scope.scope;
        }

        /// <inheritdoc />
        public void ExtendMinor(GraceObject o)
        {
            scope = new ScopeLink(scope, o, true);
        }

        /// <inheritdoc />
        public void Unextend(GraceObject o)
        {
            scope = scope.next;
            restoreMajor();
        }

        /// <inheritdoc />
        public ScopeMemo Memorise()
        {
            return new ScopeMemo(scope, dynamics.Count);
        }

        /// <inheritdoc />
        public void RestoreExactly(ScopeMemo sm)
        {
            scope = sm.Link;
            while (dynamics.Count > sm.dynamicsSize)
                dynamics.Pop();
            restoreMajor();
        }

        /// <inheritdoc />
        public void Remember(ScopeMemo sm)
        {
            dynamics.Push(scope);
            scope = sm.Link;
            restoreMajor();
        }

        /// <inheritdoc />
        public void Forget(ScopeMemo sm)
        {
            scope = dynamics.Pop();
            restoreMajor();
        }

        /// <summary>Set the majorScope field to the closest non-minor
        /// scope</summary>
        private void restoreMajor()
        {
            ScopeLink s = scope;
            while (s.minor)
            {
                s = s.next;
            }
            majorScope = s.scope;
        }

        /// <inheritdoc />
        public GraceObject FindReceiver(MethodRequest req)
        {
            ScopeLink sl = scope;
            GraceObject capture = null;
            while (sl != null && sl.scope != null)
            {
                if (sl.scope.RespondsTo(req))
                {
                    if (capture != null)
                        return capture;
                    return sl.scope;
                }
                capture = null;
                var ls = sl.scope as LocalScope;
                if (ls != null)
                {
                    if (ls.RedirectSurrounding != null)
                        capture = ls.RedirectSurrounding;
                }
                sl = sl.next;
            }
            return null;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public ReaderWriterPair AddVar(string name, GraceObject val)
        {
            var pair = majorScope.AddLocalVar(name, val);
            return pair;
        }

        /// <inheritdoc />
        public MethodNode AddDef(string name, GraceObject val)
        {
            return majorScope.AddLocalDef(name, val);
        }

        /// <inheritdoc />
        public MethodNode AddMinorDef(string name, GraceObject val)
        {
            return scope.scope.AddLocalDef(name, val);
        }

        /// <inheritdoc />
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

        /// <summary>Enable interpreter debugging messages to the
        /// console</summary>
        public static void ActivateDebuggingMessages()
        {
            debugMessagesActive = true;
        }

        /// <inheritdoc />
        /// <seealso cref="ActivateDebuggingMessages" />
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

        /// <summary>Log a debugging message, if enabled</summary>
        /// <param name="message">Message to log</param>
        /// <seealso cref="ActivateDebuggingMessages" />
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

        /// <summary>Get the identifying version of the language
        /// runtime</summary>
        /// <remarks>This version will generally include the version
        /// control revision, but may not always be available. When
        /// the version is unavailable, the method will return
        /// <c>"(unknown!)"</c>.</remarks>
        public static string GetRuntimeVersion()
        {
            var ver = Assembly.GetExecutingAssembly().
                GetCustomAttributes(
                        typeof(AssemblyInformationalVersionAttribute),
                        false
                ).FirstOrDefault();
            if (ver == null)
                return "(unknown!)";
            return ((AssemblyInformationalVersionAttribute)ver)
                .InformationalVersion;
        }
    }

    /// <summary>Encapsulates a pair of methods corresponding to a
    /// var declaration.</summary>
    public struct ReaderWriterPair
    {
        /// <summary>Reader method</summary>
        public MethodNode Read;
        /// <summary>Writer method</summary>
        public MethodNode Write;
    }

    /// <summary>Represents the current status of a program evaluation</summary>
    public interface EvaluationContext
    {
        /// <summary>Add a new child scope</summary>
        /// <param name="o">Object to place in scope</param>
        void Extend(GraceObject o);
        /// <summary>Add a new child scope for internal use</summary>
        /// <param name="o">Object to place in scope</param>
        void ExtendMinor(GraceObject o);
        /// <summary>Remove the innermost scope object</summary>
        /// <param name="o">Currently ignored</param>
        void Unextend(GraceObject o);
        /// <summary>Add an object scope outside the current "self"</summary>
        /// <param name="o">Object to place in scope</param>
        void InsertOuter(GraceObject o);

        /// <summary>Add method to stack trace</summary>
        /// <param name="module">Module location of request</param>
        /// <param name="line">Line location of request</param>
        /// <param name="name">Name of method</param>
        int NestRequest(string module, int line, string name);
        /// <summary>Remove all elements of call stack above given
        /// depth</summary>
        /// <param name="depth">Target size of call stack</param>
        void PopCallStackTo(int depth);

        /// <summary>Make a method request on the standard prelude
        /// object</summary>
        /// <param name="req">Request to perform</param>
        /// <returns>The return value of the request</returns>
        GraceObject PreludeRequest(MethodRequest req);
        /// <summary>Find a surrounding object able to process a given
        /// request</summary>
        /// <param name="req">Method request that must be accepted</param>
        /// <returns>An object that responds to <paramref name="req"/>,
        /// or null</returns>
        GraceObject FindReceiver(MethodRequest req);
        /// <summary>Find the closest enclosing scope that is a method
        /// body</summary>
        /// <returns>The nearest method scope, or null</returns>
        MethodScope FindNearestMethod();
        /// <summary>Loads and evaluates a module source file</summary>
        /// <param name="path">Import path</param>
        /// <returns>The module object</returns>
        GraceObject LoadModule(string path);


        /// <summary>Add a var field to the nearest major scope</summary>
        /// <param name="name">Variable name to use</param>
        /// <param name="val">Initial value of the field</param>
        /// <returns>An object encapsulating the reader and writer
        /// methods created for the var</returns>
        ReaderWriterPair AddVar(string name, GraceObject val);
        /// <summary>Add a def field to the nearest major scope</summary>
        /// <param name="name">Variable name to use</param>
        /// <param name="val">Value of the field</param>
        /// <returns>The method added to the object</returns>
        MethodNode AddDef(string name, GraceObject val);
        /// <summary>Add a def field to the nearest scope</summary>
        /// <param name="name">Variable name to use</param>
        /// <param name="val">Value of the field</param>
        /// <returns>The method added to the object</returns>
        MethodNode AddMinorDef(string name, GraceObject val);

        /// <summary>Create a Memo that can restore interpreter state</summary>
        Interpreter.ScopeMemo Memorise();
        /// <summary>Restore interpreter state exactly, dropping elements of
        /// the dynamic stack to fit</summary>
        /// <param name="sm">A memo created by <c cref="Memorise">Memorise</c>
        /// </param>
        void RestoreExactly(Interpreter.ScopeMemo sm);
        /// <summary>Restore interpreter state, adding a new dynamic stack
        /// entry</summary>
        /// <param name="sm">A memo created by <c cref="Memorise">Memorise</c>
        /// </param>
        void Remember(Interpreter.ScopeMemo sm);
        /// <summary>Discard a dynamic scope</summary>
        /// <param name="sm">Currently ignored</param>
        void Forget(Interpreter.ScopeMemo sm);

        /// <summary>Get a textual backtrace of the current stack</summary>
        List<string> GetStackTrace();

        /// <summary>List enclosing scopes as strings</summary>
        /// <returns>The stringifications of all surrounding scopes,
        /// separated by commas</returns>
        string ScopeStringList();
        /// <summary>Perform debugging logging of surrounding scopes,
        /// including all their methods</summary>
        void DebugScopes();
    }

    /// <summary>Represents a target that can have text written to it
    /// for error output</summary>
    public interface OutputSink
    {
        /// <summary>Write a line of output</summary>
        /// <param name="s">Line to output</param>
        void WriteLine(string s);

    }

    /// <summary>Wraps a <c cref="System.IO.TextWriter">TextWriter</c>
    /// instance into an output sink</summary>
    public class OutputSinkWrapper : OutputSink
    {
        System.IO.TextWriter writer;

        /// <param name="w">TextWriter to wrap</param>
        public OutputSinkWrapper(System.IO.TextWriter w)
        {
            writer = w;
        }

        /// <inheritdoc />
        public void WriteLine(string s)
        {
            writer.WriteLine(s);
        }
    }
}
