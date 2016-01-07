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
using Grace.Utility;
using ActiveLineEditor;

namespace Grace
{
    class ConsoleEntryPoint
    {
        static int Main(string[] args)
        {
            var enc = new UTF8Encoding(false);
            Console.OutputEncoding = enc;
            Console.InputEncoding = enc;
            ParseNode module;
            string filename = null;
            string mode = "run";
            bool verbose = false;
            string errorCodeTarget = null;
            var lines = new List<string>();
            string builtinsExtensionFile = null;
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
                else if (arg == "--builtins-extension")
                {
                    builtinsExtensionFile = args[++i];
                }
                else if (arg == "--errors-to-file")
                {
                    errorCodeTarget = args[++i];
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
                return repl(filename, builtinsExtensionFile);
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
            if (builtinsExtensionFile != null)
                interp.LoadExtensionFile(builtinsExtensionFile);
            else
                interp.LoadExtensionFile();
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
            var ls = new LocalScope("code-inner");
            var obj = new UserObject();
            interp.Extend(obj);
            ls.AddLocalDef("self", obj);
            interp.ExtendMinor(ls);
            var memo = interp.Memorise();
            bool unfinished;
            GraceObject result;
            foreach (var line in lines)
            {
                int r = REPL.RunLine(interp, obj, memo, line, out unfinished,
                        out result);
                if (r != 0)
                    return r;
                if (unfinished)
                    return 1;
            }
            return 0;
        }

        private static int repl(string filename, string builtinsExtensionFile)
        {
            Console.WriteLine("* Grace REPL with runtime "
                    + Interpreter.GetRuntimeVersion());
            ParseNode module;
            var ls = new LocalScope("repl-inner");
            var obj = new UserObject();
            var interp = REPL.CreateInterpreter(obj, ls);
            if (builtinsExtensionFile != null)
                interp.LoadExtensionFile(builtinsExtensionFile);
            else
                interp.LoadExtensionFile();
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
                        obj = (UserObject)eModule.Evaluate(interp);
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
            var memo = interp.Memorise();
            var edit = new Editor(s => completion(obj, s));
            string accum = String.Empty;
            bool unfinished;
            GraceObject result;
            string line = edit.GetLine(">>> ");
            while (line != null)
            {
                accum += line.Replace("\u0000", "") + "\n";
                var r = REPL.RunLine(interp, obj, memo, accum, out unfinished,
                        out result);
                if (result != null)
                    ls["LAST"] = result;
                if (unfinished)
                {
                    // "Unexpected end of file" is expected here
                    // for unfinished statements.
                    line = edit.GetLine("... ");
                    unfinished = false;
                    continue;
                }
                else if (r != 0)
                {
                    // All other errors are errors, and should
                    // clear the accumulated buffer and let the
                    // user start again.
                    accum = String.Empty;
                }
                else
                {
                    accum = String.Empty;
                }
                line = edit.GetLine(">>> ");
            }
            return 0;
        }

        private static IList<Editor.Completion> completion(
                GraceObject obj,
                string line)
        {
            var ret = new List<Editor.Completion>();
            var lparenIndex = line.LastIndexOf("(");
            var commaIndex = line.LastIndexOf(",");
            var lbraceIndex = line.LastIndexOf("[");
            var spaceIndex = line.LastIndexOf(" ");
            var dotIndex = line.LastIndexOf(".");
            var last = Math.Max(Math.Max(Math.Max(lparenIndex, commaIndex),
                    Math.Max(lbraceIndex, dotIndex)), spaceIndex);
            if (last == -1)
            {
                // Nothing to look at
                foreach (var k in obj.DotMethods)
                {
                    if (k == "asString")
                        continue;
                    if (k.StartsWith(line))
                    {
                        var append = k;
                        var space = k.IndexOf(' ');
                        if (space != -1)
                            append = k.Substring(0, space);
                        append = append.Substring(line.Length);
                        ret.Add(Editor.CreateCompletion(append, k, ""));
                    }
                }
            }
            else if (commaIndex == last || lbraceIndex == last
                    || lparenIndex == last || spaceIndex == last)
            {
                // If we found one of these, ignore everything leading
                // up to it and make base completions for the rest.
                ret.AddRange(completion(obj,
                            line.Substring(last + 1).Trim()));
            }
            else
            {
                // We end with a dot. Check for a preceding
                // bracket, comma, or space, and perform the
                // same truncation as above if applicable.
                var untilDot = dotIndex >= 0 ? line.Substring(0, dotIndex) : "";
                var rbraceIndex = untilDot.LastIndexOf(']');
                var commaIndex2 = untilDot.LastIndexOf(',');
                var spaceIndex2 = untilDot.LastIndexOf(' ');
                var rparenIndex = untilDot.LastIndexOf(')');
                var m = Math.Max(Math.Max(rbraceIndex, spaceIndex2),
                        Math.Max(commaIndex2, rparenIndex));
                if (m != -1 && (m == commaIndex2 || m == spaceIndex2))
                {
                    // Rudimentary quote check - if we find one of these
                    // with an odd number of quotation marks before it,
                    // we retry from before the quote.
                    if (countChars(untilDot.Substring(0, m), '"') % 2 == 1)
                    {
                        // Assume it's inside a quote
                        var qIndex = untilDot.LastIndexOf('"', m);
                        rparenIndex = untilDot.LastIndexOf(")", qIndex);
                        commaIndex2 = untilDot.LastIndexOf(",", qIndex);
                        rbraceIndex = untilDot.LastIndexOf("]", qIndex);
                        spaceIndex2 = untilDot.LastIndexOf(" ", qIndex);
                        m = Math.Max(Math.Max(rbraceIndex, spaceIndex2),
                                Math.Max(commaIndex2, rparenIndex));
                    }
                    // Update untilDot to include only the now-relevant
                    // part of the line.
                    if (m != -1 && (m == commaIndex2 || m == spaceIndex2))
                        untilDot = untilDot.Substring(m + 1);
                    else if (m < lbraceIndex || m < lparenIndex)
                        untilDot = untilDot.Substring(
                                Math.Max(lbraceIndex, lparenIndex) + 1);
                }
                else if (m < lbraceIndex || m < lparenIndex)
                {
                    // Start after a still-open bracket.
                    untilDot = untilDot.Substring(
                            Math.Max(lbraceIndex, lparenIndex) + 1);
                }
                // We will speculatively parse and execute the code,
                // and then examine the actual object that comes of
                // it. The code may have side effects, which will be
                // visible; it would be better to detect such code,
                // but the information is not presently available.
                if (untilDot != "")
                {
                    ErrorReporting.SuppressAllErrors = true;
                    try {
                        var p = new Parser("tab completion", untilDot);
                        var module = p.Parse();
                        var trans = new ExecutionTreeTranslator();
                        var mod = (ObjectConstructorNode)
                            trans.Translate((ObjectParseNode)module);
                        if (mod.Body.Count > 0)
                        {
                            var element = mod.Body[0];
                            var interp = new Interpreter();
                            interp.Extend(obj);
                            var o = element.Evaluate(interp);
                            // Re-run completion with the rest of the
                            // string and the obtained object.
                            ret.AddRange(completion(o,
                                        line.Substring(dotIndex + 1)));
                        }
                    }
                    catch (Exception)
                    {
                        // Eat everything silently - the code isn't meant
                        // to be running, so we don't want to report any
                        // errors.
                    }
                    finally
                    {
                        ErrorReporting.SuppressAllErrors = false;
                    }
                }
            }
            return ret;
        }

        private static int countChars(string s, char c)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == c)
                    count++;
            return count;
        }

        private static int error(string msg)
        {
            Console.Error.WriteLine("grace: Error: " + msg);
            return 1;
        }
    }

}
