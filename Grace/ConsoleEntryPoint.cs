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
            foreach (string arg in args)
            {
                if (arg == "--parse-tree")
                    mode = "parse-tree";
                else if (arg == "--execution-tree")
                    mode = "execution-tree";
                else if (arg == "--no-run")
                    mode = "no-run";
                else if (arg == "--verbose")
                {
                    Interpreter.ActivateDebuggingMessages();
                    verbose = true;
                }
                else
                    filename = arg;
            }
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
    }
}
