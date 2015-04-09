using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Grace.Execution;
using Grace.Runtime;

namespace Grace
{
    public class ErrorReporting
    {
        static OutputSink sink;
        public static string GetMessage(string code)
        {
            string dir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string fp = Path.Combine(dir, "DefaultErrorMessages.txt");
            string codeSpace = code + " ";
            using (StreamReader reader = File.OpenText(fp))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line.StartsWith(codeSpace))
                    {
                        return line.Substring(codeSpace.Length).Trim();
                    }
                }
            }
            return null;
        }

        public static string FormatMessage(string message, Dictionary<string, string> vars)
        {
            string ret = message;
            foreach (string k in vars.Keys)
            {
                ret = ret.Replace("${" + k + "}", vars[k]);
            }
            return ret;
        }

        public static void ReportStaticError(string module, int line, string code, Dictionary<string, string> vars, string localDescription)
        {
            string baseMessage = GetMessage(code);
            if (baseMessage == null)
                baseMessage = localDescription;
            string formattedMessage = FormatMessage(baseMessage, vars);
            WriteError(module, line, code, formattedMessage);
            throw new StaticErrorException();
        }

        public static void ReportStaticError(string module, int line, string code, string localDescription)
        {
            string baseMessage = GetMessage(code);
            if (baseMessage == null)
                baseMessage = localDescription;
            WriteError(module, line, code, baseMessage);
        }

        public static void WriteError(string module, int line, string code, string message)
        {
            if (!System.Console.IsErrorRedirected)
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
            }
            sink.WriteLine(module + ":" + line + ": " + code + ": " + message);
            if (!System.Console.IsErrorRedirected)
            {
                System.Console.ResetColor();
            }
            throw new StaticErrorException();
        }

        public static void WriteException(GraceExceptionPacket gep)
        {
            if (!System.Console.IsErrorRedirected)
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
            }
            sink.WriteLine(gep.Description);
            if (!System.Console.IsErrorRedirected)
            {
                System.Console.ResetColor();
            }
        }

        public static void RaiseError(EvaluationContext ctx,
                string code, Dictionary<string, string> vars,
                string localDescription)
        {
            string baseMessage = GetMessage(code);
            if (baseMessage == null)
                baseMessage = localDescription;
            var parts = baseMessage.Split(new string[] { ": " }, 2,
                    StringSplitOptions.None);
            var kind = parts[0];
            var msg = parts[1];
            msg = FormatMessage(msg, vars);
            GraceExceptionPacket.Throw(kind, code + ": " + msg,
                    ctx.GetStackTrace());
        }

        public static void SetSink(OutputSink s)
        {
            sink = s;
        }

    }

    public class StaticErrorException : Exception
    {

    }

}
