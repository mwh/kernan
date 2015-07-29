using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Grace.Execution;
using Grace.Parsing;
using Grace.Runtime;

namespace Grace
{
    class ConsoleEntryPoint
    {
        static string builtinsFile;
        static int Main(string[] args)
        {
            ParseNode module;
            string filename = null;
            string mode = "run";
            bool verbose = false;
            string errorCodeTarget = null;
            var lines = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--parse-tree")
                    mode = "parse-tree";
                else if (arg == "--execution-tree")
                    mode = "execution-tree";
                else if (arg == "--no-run")
                    mode = "no-run";
                else if (arg == "--repl")
                    mode = "repl";
                else if (arg == "--verbose")
                {
                    Interpreter.ActivateDebuggingMessages();
                    verbose = true;
                }
                else if (arg == "--errors-to-file")
                {
                    errorCodeTarget = args[++i];
                }
                else if (arg == "--builtins-override")
                {
                    builtinsFile = args[++i];
                }
                else if (arg == "-c")
                {
                    mode = "line";
                    if (i >= args.Length - 1)
                        return error("Expected code argument after `-c`.");
                    lines.Add(args[++i]);
                }
                else if (arg == "--")
                {
                    if (i < args.Length - 1)
                        filename = args[++i];
                    i++;
                    break;
                }
                else if (arg.StartsWith("-"))
                {
                    return error("Unknown option `" + arg + "`.");
                }
                else
                    filename = arg;
            }
            if (mode == "repl" || (mode == "run" && filename == null))
                return repl(filename);
            if (filename == null && lines.Count == 0) {
                return error("Required filename argument missing.");
            }
            if (!File.Exists(filename) && lines.Count == 0)
            {
                return error("File `" + filename + "` does not exist.");
            }
            var interp = new Interpreter();
            if (filename != null)
                interp.AddModuleRoot(
                        Path.GetDirectoryName(Path.GetFullPath(filename)));
            else
                interp.AddModuleRoot(Path.GetFullPath("."));
            interp.FailedImportHook = promptInstallModule;
            interp.LoadPrelude();
            if (builtinsFile != null)
                interp.LoadBuiltins(builtinsFile);
            if (runLines(interp, lines) != 0)
                return 1;
            if (filename == null)
                return 0;
            using (StreamReader reader = File.OpenText(filename))
            {
                Parser parser = new Parser(
                        Path.GetFileNameWithoutExtension(filename),
                        reader.ReadToEnd());
                try
                {
                    //Console.WriteLine("========== PARSING ==========");
                    module = parser.Parse();
                    if (mode == "parse-tree")
                    {
                        module.DebugPrint(System.Console.Out, "");
                        return 0;
                    }
                    //Console.WriteLine("========== TRANSLATING ==========");
                    ExecutionTreeTranslator ett = new ExecutionTreeTranslator();
                    Node eModule = ett.Translate(module as ObjectParseNode);
                    if (mode == "execution-tree")
                    {
                        eModule.DebugPrint(Console.Out, "");
                        return 0;
                    }
                    if (mode == "no-run")
                        return 0;
                    interp.EnterModule(
                            Path.GetFileNameWithoutExtension(filename));
                    try
                    {
                        eModule.Evaluate(interp);
                    }
                    catch (GraceExceptionPacketException e)
                    {
                        System.Console.Error.WriteLine("Uncaught exception:");
                        ErrorReporting.WriteException(e.ExceptionPacket);
                        if (e.ExceptionPacket.StackTrace != null)
                        {
                            foreach (var l in e.ExceptionPacket.StackTrace)
                            {
                                System.Console.Error.WriteLine("    from "
                                        + l);
                            }
                        }
                        return 1;
                    }
                }
                catch (StaticErrorException e)
                {
                    if (verbose)
                        System.Console.WriteLine(e.StackTrace);
                    if (errorCodeTarget != null)
                    {
                        File.WriteAllText(errorCodeTarget, e.Code);
                    }
                    return 1;
                }
                catch (Exception e)
                {
                    System.Console.Error.WriteLine(
                            "An internal error occurred. "
                            + "Debugging information follows.");
                    System.Console.Error.WriteLine("Runtime version: "
                            + Interpreter.GetRuntimeVersion());
                    System.Console.Error.WriteLine(e);
                    System.Console.Error.WriteLine(e.StackTrace);
                    System.Console.Error.WriteLine(
                            "\nAn internal error occurred. "
                            + "This is a bug in the implementation.");
                    return 1;
                }
            }
            return 0;
        }

        private static bool doInstallModule(string path,
                Interpreter interp)
        {
            var parts = path.Split('/');
            try
            {
                // The first fetch must be a Grace file, so we can always
                // treat it as a string. If it delegates to another (native)
                // module, we will make a second request to get that file.
                System.Console.WriteLine("Fetching " + "https://" + path
                    + ".grace");
                var request = WebRequest.Create("https://" + path + ".grace");
                var response = request.GetResponse();
                var http = (HttpWebResponse)response;
                var stream = http.GetResponseStream();
                var readStream = new StreamReader(stream,
                    System.Text.Encoding.UTF8);
                var code = readStream.ReadToEnd();
                var dest = Interpreter.GetStaticModulePaths()[0];
                // Build up the directory path, ignoring the extension
                // for the moment.
                foreach (var p in parts)
                {
                    dest = Path.Combine(dest, p);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (code.StartsWith("#kernan:"))
                {
                    // This is a placeholder redirecting to a native module
                    response.Close();
                    var lines = code.Split('\n');
                    var native = lines[0].Trim().Substring(8);
                    System.Console.WriteLine("Fetching " + "https://"
                        + path.Substring(0, path.LastIndexOf('/')) + "/"
                        + native);
                    request = WebRequest.Create("https://"
                        + path.Substring(0, path.LastIndexOf('/')) + "/"
                        + native);
                    response = request.GetResponse();
                    http = (HttpWebResponse)response;
                    stream = http.GetResponseStream();
                    var array = new byte[2048];
                    dest += ".dll";
                    var fpn = File.OpenWrite(dest);
                    var len = 0;
                    while ((len = stream.Read(array, 0, array.Length)) > 0)
                    {
                        fpn.Write(array, 0, len);
                    }
                    fpn.Close();
                }
                else
                {
                    // Ordinary code, to be saved to a grace file as text.
                    dest += ".grace";
                    var fp = File.OpenWrite(dest);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(code);
                    fp.Write(bytes, 0, bytes.Length);
                    fp.Close();
                }
                response.Close();
                System.Console.WriteLine("Saved module to " + dest);
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.SendFailure)
                {
                    System.Console.WriteLine(
                            "There was an error making the HTTPS connection;"
                            + " this may be because the remote\nserver is "
                            + "currently inaccessible.");
                    if (System.Environment.OSVersion.Platform
                            == System.PlatformID.Unix)
                    {
                        System.Console.WriteLine();
                        System.Console.WriteLine(
                                "Alternatively, it may mean that your system "
                                + "is not set up to trust certificates.\nIf "
                                + "the location is generally reachable, you "
                                + "can probably install the\ncertificates "
                                + "accepted by Mozilla using the mozroots "
                                + "tool:\n");
                        System.Console.WriteLine(
                                "        mozroots --import --ask-remove");
                        System.Console.WriteLine(
                                "\nThis tool is included with Mono.");
                        System.Console.WriteLine(
                                "\nIf this was the cause of the problem, "
                                + "it will not occur again after importing"
                                + "\nthe certificates.");
                    }
                    return false;
                }
                else if (ex.Status == WebExceptionStatus.ConnectFailure)
                {
                    System.Console.WriteLine(
                            "There was an error making the HTTPS connection;"
                            + " this may be because the remote\nserver is "
                            + "currently inaccessible, or because "
                            + "local firewall or proxy rules do\nnot "
                            + "permit access to it.");
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }

        private static bool promptInstallModule(string path,
            Interpreter interp)
        {
            // If it doesn't look like it can be mapped to a URL,
            // don't bother trying.
            var parts = path.Split('/');
            if (!parts[0].Contains('.'))
                return false;
            // Ask the user what to do. Any input other than
            // those listed is treated as a no.
            System.Console.WriteLine("Imported module `" + path
                + "` is not installed on this system.");
            System.Console.WriteLine("Would you like to install it?");
            System.Console.WriteLine("[I]nstall [T]erminate");
            var k = System.Console.ReadKey();
            System.Console.WriteLine();
            if (k.KeyChar == 'i' || k.KeyChar == 'I' || k.KeyChar == 'y'
                || k.KeyChar == 'Y')
            {
                return doInstallModule(path, interp);
            }
            return false;
        }

        private static int runLines(Interpreter interp,
                IEnumerable<string> lines)
        {
            var obj = new GraceObject();
            interp.Extend(obj);
            var ls = new LocalScope("code-inner");
            ls.AddLocalDef("self", obj);
            interp.ExtendMinor(ls);
            var memo = interp.Memorise();
            bool unfinished;
            foreach (var line in lines)
            {
                int r = runLine(interp, obj, memo, line, out unfinished);
                if (r != 0)
                    return r;
                if (unfinished)
                    return 1;
            }
            return 0;
        }

        private static int runLine(Interpreter interp,
                GraceObject obj,
                Interpreter.ScopeMemo memo,
                string line,
                out bool unfinished)
        {
            if (true)
            {
                ParseNode module;
                ObjectConstructorNode mod = null;
                try {
                    var p = new Parser("source code", line);
                    module = p.Parse();
                    var trans = new ExecutionTreeTranslator();
                    mod = (ObjectConstructorNode)trans.Translate((ObjectParseNode)module);
                }
                catch (StaticErrorException ex)
                {
                    if (ex.Code == "P1001")
                    {
                        // "Unexpected end of file" is expected in the
                        // repl for unfinished statements.
                        unfinished = true;
                        return 1;
                    }
                    else
                    {
                        // All other errors are errors.
                        unfinished = false;
                        return 1;
                    }
                }
                unfinished = false;
                if (mod != null)
                {
                    try
                    {
                        // The "module" object can only really have
                        // a single element, but we don't know whether
                        // it's a method, statement, or expression yet.
                        foreach (var meth in mod.Methods.Values)
                        {
                            obj.AddMethod(meth);
                        }
                        foreach (var node in mod.Body)
                        {
                            var ret = node.Evaluate(interp);
                            if (ret != null
                                    && ret != GraceObject.Done
                                    && ret != GraceObject.Uninitialised)
                            {
                                interp.Print(interp, ret);
                            }
                        }
                    }
                    catch (GraceExceptionPacketException e)
                    {
                        Console.Error.WriteLine("Uncaught exception:");
                        ErrorReporting.WriteException(e.ExceptionPacket);
                        if (e.ExceptionPacket.StackTrace != null)
                        {
                            foreach (var l in e.ExceptionPacket.StackTrace)
                            {
                                Console.Error.WriteLine("    from "
                                        + l);
                            }
                        }
                        return 1;
                    }
                    finally
                    {
                        // No matter what happened, restore the interpreter
                        // to as pristine a state as we can manage before
                        // the next time.
                        interp.RestoreExactly(memo);
                        interp.PopCallStackTo(0);
                        mod = null;
                    }
                }
            }
            return 0;
        }

        private static int repl(string filename)
        {
            Console.WriteLine("* Grace REPL with runtime "
                    + Interpreter.GetRuntimeVersion());
            ParseNode module;
            var interp = new Interpreter();
            interp.LoadPrelude();
            if (builtinsFile != null)
                interp.LoadBuiltins(builtinsFile);
            var obj = new GraceObject();
            if (filename != null)
            {
                if (!File.Exists(filename))
                {
                    return error("File `" + filename + "` does not exist.");
                }
                var dir = Path.GetDirectoryName(Path.GetFullPath(filename));
                interp.AddModuleRoot(dir);
                Console.WriteLine("* Loading " + filename + "...");
                using (StreamReader reader = File.OpenText(filename))
                {
                    var parser = new Parser(
                            Path.GetFileNameWithoutExtension(filename),
                            reader.ReadToEnd());
                    module = parser.Parse();
                    ExecutionTreeTranslator ett = new ExecutionTreeTranslator();
                    Node eModule = ett.Translate(module as ObjectParseNode);
                    try
                    {
                        obj = eModule.Evaluate(interp);
                    }
                    catch (GraceExceptionPacketException e)
                    {
                        Console.Error.WriteLine("Uncaught exception:");
                        ErrorReporting.WriteException(e.ExceptionPacket);
                        if (e.ExceptionPacket.StackTrace != null)
                        {
                            foreach (var l in e.ExceptionPacket.StackTrace)
                            {
                                Console.Error.WriteLine("    from "
                                        + l);
                            }
                        }
                        return 1;
                    }
                }
                Console.WriteLine("* Loaded.");
            }
            else
            {
                var dir = Path.GetFullPath(".");
                interp.AddModuleRoot(dir);
            }
            Console.WriteLine("* Enter code at the prompt.\n");
            ErrorReporting.SilenceError("P1001");
            interp.Extend(obj);
            var ls = new LocalScope("repl-inner");
            ls.AddLocalDef("self", obj);
            interp.ExtendMinor(ls);
            var memo = interp.Memorise();
            Console.Write(">>> ");
            string line = Console.ReadLine();
            string accum = String.Empty;
            bool unfinished;
            while (line != null)
            {
                accum += line.Replace("\u0000", "") + "\n";
                var r = runLine(interp, obj, memo, accum, out unfinished);
                if (unfinished)
                {
                    // "Unexpected end of file" is expected here
                    // for unfinished statements.
                    Console.Write("... ");
                    unfinished = false;
                }
                else if (r != 0)
                {
                    // All other errors are errors, and should
                    // clear the accumulated buffer and let the
                    // user start again.
                    Console.Write(">>> ");
                    accum = String.Empty;
                }
                else
                {
                    accum = String.Empty;
                    Console.Write(">>> ");
                }
                line = Console.ReadLine();
            }
            return 0;
        }

        private static int error(string msg)
        {
            Console.Error.WriteLine("grace: Error: " + msg);
            return 1;
        }
    }

}
