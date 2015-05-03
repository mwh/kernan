using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Grace.Execution;
using Grace.Parsing;
using Grace.Runtime;

namespace Grace
{
    class ConsoleEntryPoint
    {
        static int Main(string[] args)
        {
            ParseNode module;
            string filename = null;
            string mode = "run";
            bool verbose = false;
            string errorCodeTarget = null;
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
                else
                    filename = arg;
            }
            if (mode == "repl")
                return repl(filename);
            if (filename == null) {
                System.Console.Error.WriteLine("Required filename argument missing.");
                return 1;
            }
            var interp = new Interpreter();
            interp.LoadPrelude();
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
                    //eModule.DebugPrint(Console.Out, "T>    ");
                    //Console.WriteLine("========== EVALUATING ==========");
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

        private static int repl(string filename)
        {
            Console.WriteLine("* Grace REPL with runtime "
                    + Interpreter.GetRuntimeVersion());
            ParseNode module;
            var interp = new Interpreter();
            interp.LoadPrelude();
            var obj = new GraceObject();
            if (filename != null)
            {
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
            while (line != null)
            {
                accum += line.Replace("\u0000", "") + "\n";
                ObjectConstructorNode mod = null;
                try {
                    var p = new Parser("source code", accum);
                    module = p.Parse();
                    var trans = new ExecutionTreeTranslator();
                    mod = (ObjectConstructorNode)trans.Translate((ObjectParseNode)module);
                }
                catch (StaticErrorException ex)
                {
                    if (ex.Code == "P1001")
                    {
                        // "Unexpected end of file" is expected here
                        // for unfinished statements.
                        Console.Write("... ");
                    }
                    else
                    {
                        // All other errors are errors, and should
                        // clear the accumulated buffer and let the
                        // user start again.
                        Console.Write(">>> ");
                        accum = String.Empty;
                    }
                }
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
                    }
                    // No matter what happened, restore the interpreter
                    // to as pristine a state as we can manage before
                    // the next time.
                    interp.RestoreExactly(memo);
                    interp.PopCallStackTo(0);
                    accum = String.Empty;
                    mod = null;
                    Console.Write(">>> ");
                }
                line = Console.ReadLine();
            }
            return 0;
        }
    }

}
