using System;
using System.Collections.Generic;
using System.Linq;
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
        private static bool debugMessagesActive;
        private Dictionary<string, GraceObject> modules = new Dictionary<string, GraceObject>();
        private HashSet<string> importedPaths = new HashSet<string>();
        private Stack<string> importStack = new Stack<string>();

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
            private readonly ScopeLink link;
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
        private Stack<string> callStackMethod = new Stack<string>();
        private Stack<string> callStackModule = new Stack<string>();
        private Stack<int> callStackLine = new Stack<int>();
        private ScopeLink scope = new ScopeLink();
        private GraceObject majorScope;
        private OutputSink sink;

        /// <summary>
        /// Sink used for output from this interpreter.
        /// </summary>
        /// <value>Retrieves the value of the field "sink".</value>
        public OutputSink Sink {
            get {
                return sink;
            }
        }

        /// <summary>
        /// Sink used for RPC from this interpreter. Usually null.
        /// </summary>
        public RPCSink RPCSink { get; set; }

        private List<string> additionalModuleRoots =
            new List<string>();

        /// <summary>
        /// Hook function called when an import fails
        /// </summary>
        /// <returns>
        /// True if the import should be retried.
        /// </returns>
        public Func<string, Interpreter, bool> FailedImportHook
        {
            get;
            set;
        }

        /// <summary>
        /// Set to true if the code is running on JSIL.
        /// </summary>
        public static bool JSIL { get; set; }

        /// <summary>A default interpreter</summary>
        public Interpreter()
        {
            sink = new StdoutOutputSinkWrapper();
            ErrorReporting.SetSink(new StderrOutputSinkWrapper());
            if (JSIL)
                ErrorReporting.SetSink(sink);
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
            var s = new LocalScope();
            scope.scope = s;
            majorScope = s;
            s.AddMethod("print(_)",
                    new DelegateMethod1Ctx(Print));
            s.AddLocalDef("true", GraceBoolean.True);
            s.AddLocalDef("false", GraceBoolean.False);
            s.AddMethod("_base_while_do(_,_)",
                    new DelegateMethodReq(BaseWhileDo));
            s.AddMethod("_base_try_catch_finally(_,_)",
                    new DelegateMethodReq(BaseTryCatchFinally));
            s.AddMethod("_base_try_catch_finally(_,_,_)",
                    new DelegateMethodReq(BaseTryCatchFinally));
            s.AddMethod("_base_try_catch_finally(_,_,_,_)",
                    new DelegateMethodReq(BaseTryCatchFinally));
            s.AddMethod("_base_try_catch_finally(_,_,_,_,_)",
                    new DelegateMethodReq(BaseTryCatchFinally));
            s.AddMethod("_base_try_catch_finally(_,_,_,_,_,_)",
                    new DelegateMethodReq(BaseTryCatchFinally));
            s.AddMethod("_base_try_catch_finally(_,_,_,_,_,_,_)",
                    new DelegateMethodReq(BaseTryCatchFinally));
            s.AddMethod("Exception",
                    new ConstantMethod(
                        new GraceExceptionKind("Exception")));
        }

        private bool loadedPrelude;
        private static GraceObject loadedPreludeObject;

        /// <summary>Finds the standard prelude file, loads and
        /// interprets it, and places the created module in scope</summary>
        public void LoadPrelude()
        {
            if (loadedPrelude)
                return;
            if (JSIL && loadedPreludeObject != null)
            {
                Extend(loadedPreludeObject);
                return;
            }
            string dir = Path.GetDirectoryName(typeof(Interpreter).Assembly.Location);
            string preludePath = Path.Combine(dir, "prelude.grace");
            using (StreamReader preludeReader = File.OpenText(preludePath))
            {
                var parser = new Parser("prelude", preludeReader.ReadToEnd());
                var pt = parser.Parse() as ObjectParseNode;
                var eMod = new ExecutionTreeTranslator().Translate(pt);
                prelude = eMod.Evaluate(this);
                Extend(prelude);
                loadedPrelude = true;
                loadedPreludeObject = prelude;
                Interpreter.Debug("========== END PRELUDE ==========");
            }
        }

        /// <summary>
        /// Load the builtin-extending module file, if one exists
        /// in any of the known locations.
        /// </summary>
        public void LoadExtensionFile()
        {
            string localGrace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "grace");
            var p = Path.Combine(localGrace, "BuiltinsExtension.grace");
            if (File.Exists(p))
            {
                LoadExtensionFile(p);
                return;
            }
            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            p = Path.Combine(dir, "BuiltinsExtension.grace");
            if (File.Exists(p))
            {
                LoadExtensionFile(p);
                return;
            }
        }
        /// <summary>
        /// Load a file as the builtin-extending module.
        /// </summary>
        /// <param name="path">Path to file to load</param>
        public void LoadExtensionFile(string path)
        {
            GraceObject ext;
            using (StreamReader reader = File.OpenText(path))
            {
                var parser = new Parser("extension", reader.ReadToEnd());
                var pt = parser.Parse() as ObjectParseNode;
                var eMod = new ExecutionTreeTranslator().Translate(pt);
                ext = eMod.Evaluate(this);
            }
            LoadExtensionsFromObject(ext);
        }

        /// <summary>
        /// Load extensions to builtins from traits in an
        /// existing object.
        /// </summary>
        /// <param name="ext">Object containing extension traits.</param>
        public void LoadExtensionsFromObject(GraceObject ext)
        {
            var req = MethodRequest.Nullary("ObjectExtension");
            req.IsInherits = true;
            if (ext.RespondsTo(req))
            {
                var uo = new UserObject();
                req.InheritingObject = uo;
                ext.Request(this, req);
                GraceObject.ExtendDefaultMethods(req.InheritedMethods);
            }
            req = MethodRequest.Nullary("NumberExtension");
            req.IsInherits = true;
            if (ext.RespondsTo(req))
            {
                var uo = new UserObject();
                req.InheritingObject = uo;
                ext.Request(this, req);
                GraceNumber.ExtendWith(req.InheritedMethods);
            }
            req = MethodRequest.Nullary("StringExtension");
            req.IsInherits = true;
            if (ext.RespondsTo(req))
            {
                var uo = new UserObject();
                req.InheritingObject = uo;
                ext.Request(this, req);
                GraceString.ExtendWith(req.InheritedMethods);
            }
        }

        /// <summary>
        /// Copy this interpreter to make a new independent one starting
        /// with the same state, configuration, and call history.
        /// </summary>
        public Interpreter Copy()
        {
            var ret = new Interpreter();
            ret.prelude = prelude;
            ret.modules = modules;
            ret.importedPaths = importedPaths;
            ret.dynamics = new Stack<ScopeLink>(dynamics);
            ret.callStackMethod = new Stack<string>(callStackMethod);
            ret.callStackModule = new Stack<string>(callStackModule);
            ret.callStackLine = new Stack<int>(callStackLine);
            ret.scope = scope;
            ret.majorScope = majorScope;
            ret.sink = sink;
            foreach (var r in additionalModuleRoots)
                ret.AddModuleRoot(r);
            return ret;
        }

        /// <summary>
        /// Gives the directory paths searched for imports independently
        /// of the executing program
        /// </summary>
        public static List<string> GetStaticModulePaths()
        {
            string execDir = Path.GetDirectoryName(typeof(Interpreter).Assembly.Location);
            string localGrace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "grace");
            var bases = new List<string> {
                Path.Combine(localGrace, "modules"),
                Path.Combine(execDir, "modules")
            };
            return bases;
        }

        /// <summary>Gives the directory paths searched for imports</summary>
        public List<string> GetModulePaths()
        {
            return additionalModuleRoots.Concat(GetStaticModulePaths()).ToList();
        }

        /// <summary>
        /// Add an additional path to be searched for imported modules
        /// </summary>
        /// <param name="path">Absolute path to search inside</param>
        public void AddModuleRoot(string path)
        {
            additionalModuleRoots.Add(path);
        }

        /// <summary>
        /// Register a module path in the chain of imported modules
        /// that will be reported in any errors.
        /// </summary>
        /// <param name="importPath">Import path to insert</param>
        public void EnterModule(string importPath)
        {
            importedPaths.Add(importPath);
            importStack.Push(importPath);
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
            importStack.Push(path);
            if (importedPaths.Contains(path))
            {
                var chain = String.Join(" -> ", importStack.Reverse());
                ErrorReporting.RaiseError(this, "R2011",
                    new Dictionary<string, string> {
                        { "path", path },
                        { "chain", chain }
                    },
                    "CyclicImportError: Module ${path} imports itself.");
            }
            importedPaths.Add(path);
            var name = Path.GetFileName(path);
            var isResource = name.Contains('.');
            var bases = GetModulePaths();
            try
            {
                foreach (var p in bases)
                {
                    string filePath;
                    GraceObject mod;
                    if (isResource)
                    {
                        filePath = Path.Combine(p, path);
                        mod = tryLoadResource(filePath, path);
                        if (mod != null)
                        {
                            modules[path] = mod;
                            return mod;
                        }
                        continue;
                    }
                    filePath = Path.Combine(p, path + ".grace");
                    mod = tryLoadModuleFile(filePath, path);
                    if (mod != null)
                    {
                        modules[path] = mod;
                        return mod;
                    }
                    filePath = Path.Combine(p, path + ".dll");
                    mod = tryLoadNativeModule(filePath);
                    if (mod != null)
                    {
                        modules[path] = mod;
                        return mod;
                    }
                }
                if (FailedImportHook != null)
                {
                    // Optionally, the host program can try to satisfy a module
                    // and indicate that we should retry the import.
                    if (FailedImportHook(path, this))
                        return LoadModule(path);
                }
            }
            finally
            {
                importStack.Pop();
            }
            ErrorReporting.RaiseError(this, "R2005",
                new Dictionary<string, string> { { "path", path } },
                "LookupError: Could not find module ${path}");
            return null;
        }

        /// <summary>Load a module file if it exists</summary>
        private GraceObject tryLoadModuleFile(string filePath,
                string importPath)
        {
            return (File.Exists(filePath))
                ? loadModuleFile(filePath, importPath)
                : null;
        }

        /// <summary>Load a native module file if it exists</summary>
        private GraceObject tryLoadNativeModule(string path)
        {
            return (File.Exists(path))
                ? loadNativeModule(path)
                : null;
        }

        /// <summary>Load a resource file if it exists</summary>
        /// <param name="filePath">Filesystem path to resource</param>
        /// <param name="importPath">Import path used to reach resource</param>
        private GraceObject tryLoadResource(string filePath, string importPath)
        {
            return (File.Exists(filePath))
                ? loadResource(filePath, importPath)
                : null;
        }

        /// <summary>Load a module file</summary>
        private GraceObject loadModuleFile(string filePath, string importPath)
        {
            Interpreter.Debug("========== LOAD " + filePath + " ==========");
            using (StreamReader reader = File.OpenText(filePath))
            {
                return LoadModuleString(importPath, reader.ReadToEnd());
            }
        }

        /// <summary>
        /// Load a string containing Grace code under a given
        /// module name.
        /// </summary>
        /// <param name="importPath">
        /// Module name to treat this code as belonging to.
        /// </param>
        /// <param name="code">
        /// Source code to load.
        /// </param>
        public GraceObject LoadModuleString(string importPath,
                string code)
        {
            var parser = new Parser(importPath, code);
            var pt = parser.Parse() as ObjectParseNode;
            var eMod = new ExecutionTreeTranslator().Translate(pt);
            var ret = eMod.Evaluate(this);
            modules[importPath] = ret;
            return ret;
        }

        /// <summary>Load a module file</summary>
        private GraceObject loadNativeModule(string path)
        {
            Interpreter.Debug("========== LOAD " + path + " ==========");
            var dll = Assembly.LoadFile(path);
            foreach (var t in dll.GetExportedTypes())
            {
                foreach (var a in t.GetCustomAttributes(false))
                {
                    if (a is ModuleEntryPoint)
                    {
                        var m = t.GetMethod("Instantiate");
                        return (GraceObject)m.Invoke(null, new Object[1]{this});
                    }
                }
            }
            ErrorReporting.RaiseError(this, "R2005",
                new Dictionary<string, string> { { "path", path } },
                "LookupError: Could not find module ${path}");
            return null;
        }

        /// <summary>
        /// Load a resource file
        /// </summary>
        /// <param name="filePath">Filesystem path to resource</param>
        /// <param name="importPath">Import path used to reach resource</param>
        private GraceObject loadResource(string filePath, string importPath)
        {
            var ext = Path.GetExtension(importPath);
            if (ext == ".txt")
            {
                return GraceString.Create(File.OpenText(filePath).ReadToEnd());
            }
            ErrorReporting.RaiseError(this, "R2010",
                new Dictionary<string, string> {
                    { "path", importPath },
                    { "extension", ext }
                },
                "LookupError: No resource handler for ${importPath}");
            return null;
        }

        /// <inheritdoc />
        public void InsertOuter(GraceObject obj)
        {
            var s = scope;
            while (s != null)
            {
                if (s.scope.HasFlag(GraceObject.Flags.ObjectConstructor))
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

        /// <inheritdoc/>
        public GraceObject Prelude {
            get {
                return prelude;
            }
        }

        /// <inheritdoc />
        public List<string> GetStackTrace()
        {
            if (JSIL)
            {
                var ret = new List<string>();
                while (callStackMethod.Count > 0)
                {
                    var name = callStackMethod.Pop();
                    var line = callStackLine.Pop();
                    var module = callStackModule.Pop();
                    ret.Add("«" + name + "», at line " + line + " of "
                            + module);
                }
                return ret;
            }
            var tmp = callStackMethod.Zip(callStackLine,
                    (name, line) =>
                        "«" + name + "», at line " + line + " of ");
            return new List<string>(tmp.Zip(callStackModule,
                        (start, module) => start + module));
        }

        /// <summary>The built-in Grace "print" method</summary>
        public GraceObject Print(EvaluationContext ctx, GraceObject arg)
        {
            Object obj = arg;
            var gop = arg as GraceObjectProxy;
            if (gop != null)
            {
                obj = gop.Object;
            }
            var go = obj as GraceObject;
            if (go != null)
            {
                var req = new MethodRequest();
                var part = RequestPart.Nullary("asString");
                req.AddPart(part);
                if (go.RespondsTo(req))
                {
                    obj = go.Request(ctx, req);
                }
            }
            GraceString gs = null;
            go = obj as GraceObject;
            if (go != null)
                gs = go.FindNativeParent<GraceString>();
            if (gs != null)
                obj = gs.Value.Replace("\u2028", Environment.NewLine);
            sink.WriteLine("" + obj);
            return GraceObject.Done;
        }

        /// <summary>Native version of the built-in Grace "while-do" method
        /// </summary>
        /// <remarks>This while-do is more efficient than is possible to
        /// implement in bare Grace without it.</remarks>
        public static GraceObject BaseWhileDo(EvaluationContext ctx,
                MethodRequest req)
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
        public static GraceObject BaseTryCatchFinally(EvaluationContext ctx,
                MethodRequest req)
        {
            Interpreter.ScopeMemo memo = ctx.Memorise();
            GraceObject tryBlock;
            GraceObject finallyBlock;
            var catchBlocks = new List<GraceObject>();
            tryBlock = req[0].Arguments[0];
            finallyBlock = req[0].Arguments[1];
            for (int i = 2; i < req[0].Arguments.Count; i++)
                catchBlocks.Add(req[0].Arguments[i]);
            var ret = GraceObject.Done;
            var caught = true;
            try
            {
                ret = tryBlock.Request(ctx, MethodRequest.Nullary("apply"));
            }
            catch (GraceExceptionPacketException e)
            {
                ctx.RestoreExactly(memo);
                GraceObject gep = e.ExceptionPacket;
                MethodRequest matchReq = MethodRequest.Single("match",
                        gep);
                caught = false;
                foreach (var cb in catchBlocks)
                {
                    var mr = cb.Request(ctx, matchReq);
                    if (Matching.Succeeded(ctx, mr))
                    {
                        caught = true;
                        ret = mr.Request(ctx,
                                MethodRequest.Nullary("result"));
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
                    if (caught)
                    {
                        finallyBlock.Request(ctx,
                                MethodRequest.Nullary("apply"));
                    }
                    else
                    {
                        try {
                            finallyBlock.Request(ctx,
                                    MethodRequest.Nullary("apply"));
                        }
                        catch (ReturnException)
                        {
                            ErrorReporting.RaiseError(ctx, "R2018",
                                new Dictionary<string, string>(),
                                "IllegalReturnError: From completed finally");
                        }
                    }
            }
            return ret;
        }

        /// <inheritdoc />
        public int NestRequest(string module, int line, string name)
        {
            callStackMethod.Push(name);
            callStackModule.Push(module);
            callStackLine.Push(line);
            return callStackMethod.Count - 1;
        }

        /// <inheritdoc />
        public void PopCallStackTo(int depth)
        {
            while (callStackMethod.Count > depth)
            {
                callStackMethod.Pop();
                callStackModule.Pop();
                callStackLine.Pop();
            }
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
            return FindReceiver(req, 0);
        }

        /// <inheritdoc />
        public GraceObject FindReceiver(MethodRequest req, int skipRedirects)
        {
            ScopeLink sl = scope;
            GraceObject capture = null;
            while (sl != null && sl.scope != null)
            {
                if (skipRedirects > 0 &&
                        sl.scope.HasFlag(GraceObject.Flags.ObjectConstructor))
                {
                    sl = sl.next;
                    skipRedirects--;
                    continue;
                }
                if (sl.scope.RespondsTo(req))
                {
                    return capture ?? sl.scope;
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
                var ms = sl.scope as MethodScope;
                if (ms != null)
                    return ms;
                sl = sl.next;
            }
            return null;
        }

        /// <inheritdoc />
        public ReaderWriterPair AddVar(string name, GraceObject val)
        {
            var m = majorScope as LocalScope;
            if (m == null)
                return new ReaderWriterPair();
            var pair = m.AddLocalVar(name, val);
            return pair;
        }

        /// <inheritdoc />
        public Method AddDef(string name, GraceObject val)
        {
            var m = majorScope as LocalScope;
            if (m == null)
                return null;
            return m.AddLocalDef(name, val);
        }

        /// <inheritdoc />
        public Method AddMinorDef(string name, GraceObject val)
        {
            var m = scope.scope as LocalScope;
            if (m == null) {
                var s = scope.next;
                while (s != null) {
                    if (s.minor)
                        m = s.scope as LocalScope;
                    if (m != null)
                        break;
                    s = s.next;
                }
                if (m == null)
                    return null;
            }
            return m.AddLocalDef(name, val);
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
            if (!Console.IsErrorRedirected)
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
            }
            Console.Error.WriteLine(message);
            if (!Console.IsErrorRedirected)
            {
                Console.ResetColor();
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
        public Method Read;
        /// <summary>Writer method</summary>
        public Method Write;
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
        /// <summary>prelude.grace module object</summary>
        GraceObject Prelude { get; }
        /// <summary>Find a surrounding object able to process a given
        /// request</summary>
        /// <param name="req">Method request that must be accepted</param>
        /// <returns>An object that responds to <paramref name="req"/>,
        /// or null</returns>
        GraceObject FindReceiver(MethodRequest req);
        /// <summary>Find a surrounding object able to process a given
        /// request</summary>
        /// <param name="req">Method request that must be accepted</param>
        /// <param name="skipRedirects">Number of surrounding "self"
        /// objects to skip over</param>
        /// <returns>An object that responds to <paramref name="req"/>,
        /// or null</returns>
        GraceObject FindReceiver(MethodRequest req, int skipRedirects);
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
        Method AddDef(string name, GraceObject val);
        /// <summary>Add a def field to the nearest scope</summary>
        /// <param name="name">Variable name to use</param>
        /// <param name="val">Value of the field</param>
        /// <returns>The method added to the object</returns>
        Method AddMinorDef(string name, GraceObject val);

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

    class StdoutOutputSinkWrapper : OutputSink
    {
        /// <inheritdoc/>
        public void WriteLine(string s)
        {
            Console.WriteLine(s);
        }
    }

    class StderrOutputSinkWrapper : OutputSink
    {
        /// <inheritdoc/>
        public void WriteLine(string s)
        {
            Console.Error.WriteLine(s);
        }
    }

    /// <summary>Wraps a <c cref="System.IO.TextWriter">TextWriter</c>
    /// instance into an output sink</summary>
    public class OutputSinkWrapper : OutputSink
    {
        readonly TextWriter writer;

        /// <param name="w">TextWriter to wrap</param>
        public OutputSinkWrapper(TextWriter w)
        {
            writer = w;
        }

        /// <inheritdoc />
        public void WriteLine(string s)
        {
            writer.WriteLine(s);
        }
    }

    /// <summary>
    /// Attribute to be applied to the single entry point class of
    /// a native module DLL.
    /// </summary>
    public class ModuleEntryPoint : System.Attribute
    {
        /// <summary>Default entry point</summary>
        public ModuleEntryPoint() {}
    }
}
