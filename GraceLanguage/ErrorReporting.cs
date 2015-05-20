using System;
using System.Collections.Generic;
using System.IO;
using Grace.Execution;
using Grace.Runtime;
using Grace.Parsing;

namespace Grace
{
    /// <summary>Encapsulates behaviour relating to error reporting</summary>
    public static class ErrorReporting
    {
        private static OutputSink sink;
        private static HashSet<string> SilencedErrors = new HashSet<string>();

        /// <summary>
        /// Retrieve the matching error message for a given code
        /// from the highest-priority error message source.
        /// </summary>
        /// <param name="code">Error code</param>
        /// <param name="data">Data to be used for matching messages</param>
        /// <returns>
        /// The error string corresponding to the code and conditions,
        /// or null
        /// </returns>
        public static string GetMessage(string code,
                Dictionary<string, string> data)
        {
            string localGrace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "grace");
            var msg = GetMessageFromFile(code, localGrace, data);
            if (msg != null)
                return msg;
            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return GetMessageFromFile(code, dir, data);
        }

        /// <summary>
        /// Retrieve the error message for a given code from the
        /// message database in a given directory, using the
        /// provided data to select from multiple options.
        /// </summary>
        /// <param name="code">Error code</param>
        /// <param name="dir">
        /// Path to directory containing messages file to use
        /// </param>
        /// <param name="data">
        /// Dictionary of proposed substitute values to be used
        /// for winnowing
        /// </param>
        /// <returns>
        /// The error string corresponding to the code, or null
        /// </returns>
        /// <seealso cref="ErrorReporting.GetMessage" />
        public static string GetMessageFromFile(string code, string dir,
                Dictionary<string, string> data)
        {
            string fp = Path.Combine(dir, "DefaultErrorMessages.txt");
            if (!File.Exists(fp))
                return null;
            using (StreamReader reader = File.OpenText(fp))
            {
                string codeSpace = code + " ";
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line.StartsWith(codeSpace,
                                StringComparison.InvariantCulture))
                    {
                        var l = line.Substring(codeSpace.Length).Trim();
                        if (l.StartsWith("|["))
                        {
                            if (conditionsMet(l, data))
                                return l.Substring(l.LastIndexOf("]|") + 3);
                            continue;
                        }
                        return line.Substring(codeSpace.Length).Trim();
                    }
                }
            }
            return null;
        }

        private static bool conditionsMet(
                string l,
                Dictionary<string, string> data
                )
        {
            var interpreter = new Interpreter();
            interpreter.LoadPrelude();
            var ls = new LocalScope();
            foreach (var k in data.Keys)
                switch(k)
                {
                    case "method":
                    case "type":
                    case "class":
                    case "object":
                    case "def":
                    case "var":
                    case "dialect":
                    case "import":
                    case "return":
                    case "is":
                    case "where":
                        ls.AddLocalDef(k + "_", GraceString.Create(data[k]));
                        break;
                    default:
                        ls.AddLocalDef(k, GraceString.Create(data[k]));
                        break;
                }
            interpreter.Extend(ls);
            while (l.StartsWith("|["))
            {
                int end = l.IndexOf("]|");
                var condition = l.Substring(2, end - 2);
                if (!conditionMet(condition, data, interpreter))
                    return false;
                l = l.Substring(end + 2);
            }
            return true;
        }

        private static bool conditionMet(
                string condition,
                Dictionary<string, string> data,
                Interpreter interpreter
                )
        {
            var p = (ObjectParseNode)new Parser(condition).Parse();
            var e = new ExecutionTreeTranslator();
            var t = p.Body[0].Visit(e);
            var b = t.Evaluate(interpreter);
            return GraceBoolean.IsTrue(interpreter, b);
        }

        /// <summary>
        /// Substitute variables into an error message string.
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="vars">Dictionary from variable names to
        /// values to insert in their place.</param>
        /// <remarks>
        /// The error string can contain substitution marks written in
        /// ${...} that will be replaced by the variable value from
        /// the <paramref name="vars" /> parameter.
        /// </remarks>
        /// <returns>The <paramref name="message" /> string with
        /// any substitutions made.</returns>
        /// <seealso cref="ErrorReporting.GetMessage" />
        public static string FormatMessage(string message,
                IDictionary<string, string> vars)
        {
            string ret = message;
            foreach (string k in vars.Keys)
            {
                ret = ret.Replace("${" + k + "}", vars[k]);
            }
            return ret;
        }

        /// <summary>
        /// Report a static error to the user
        /// </summary>
        /// <param name="module">Module name where error found</param>
        /// <param name="line">Line number where error found</param>
        /// <param name="code">Error code</param>
        /// <param name="vars">Dictionary from variable names to
        /// values to insert in the message in their place.</param>
        /// <param name="localDescription">A description of the error
        /// given at the site of generation, which will be used if no
        /// user error message for <paramref name="code" /> is found.
        /// </param>
        /// <remarks>
        /// The error code will be translated into a message and
        /// formatted, then displayed to the user using WriteError
        /// as configured by the front end.
        /// </remarks>
        /// <exception cref="StaticErrorException">Always thrown to
        /// allow front-end code to handle a static failure.</exception>
        /// <seealso cref="WriteError" />
        /// <seealso cref="ErrorReporting.GetMessage" />
        public static void ReportStaticError(string module, int line, string code, Dictionary<string, string> vars, string localDescription)
        {
            string baseMessage = GetMessage(code, vars) ?? localDescription;
            string formattedMessage = FormatMessage(baseMessage, vars);
            if (!SilencedErrors.Contains(code))
                WriteError(module, line, code, formattedMessage);
            throw new StaticErrorException(code);
        }

        /// <summary>
        /// Report a static error to the user
        /// </summary>
        /// <param name="module">Module name where error found</param>
        /// <param name="line">Line number where error found</param>
        /// <param name="code">Error code</param>
        /// <param name="localDescription">A description of the error
        /// given at the site of generation, which will be used if no
        /// user error message for <paramref name="code" /> is found.
        /// </param>
        /// <remarks>
        /// The error code will be translated into a message, then
        /// displayed to the user using WriteError as configured by
        /// the front end.
        /// </remarks>
        /// <exception cref="StaticErrorException">Always thrown to
        /// allow front-end code to handle a static failure.</exception>
        /// <seealso cref="WriteError" />
        /// <seealso cref="ErrorReporting.GetMessage" />
        public static void ReportStaticError(string module, int line, string code, string localDescription)
        {
            string baseMessage = GetMessage(code,
                    new Dictionary<string, string>()) ?? localDescription;
            WriteError(module, line, code, baseMessage);
        }

        /// <summary>
        /// Write out a static error message according to the
        /// configuration provided by the front end.
        /// </summary>
        /// <param name="module">Module name where error found</param>
        /// <param name="line">Line number where error found</param>
        /// <param name="code">Error code</param>
        /// <param name="message">Formatted error message</param>
        /// <remarks>
        /// The message is written to the <c cref="OutputSink">
        /// OutputSink</c> configured by the front end.
        /// </remarks>
        /// <exception cref="StaticErrorException">Always thrown to
        /// allow front-end code to handle a static failure.</exception>
        /// <seealso cref="WriteError" />
        /// <seealso cref="ErrorReporting.GetMessage" />
        public static void WriteError(string module, int line, string code, string message)
        {
            if (!Console.IsErrorRedirected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            sink.WriteLine(module + ":" + line + ": " + code + ": " + message);
            if (!Console.IsErrorRedirected)
            {
                Console.ResetColor();
            }
            throw new StaticErrorException(code);
        }

        /// <summary>
        /// Write out a runtime error message for a Grace exception
        /// according to the configuration provided by the front end.
        /// </summary>
        /// <param name="gep">Exception packet to print</param>
        /// <remarks>
        /// The message is written to the <c cref="OutputSink">
        /// OutputSink</c> configured by the front end.
        /// </remarks>
        /// <seealso cref="WriteError" />
        public static void WriteException(GraceExceptionPacket gep)
        {
            if (!Console.IsErrorRedirected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            sink.WriteLine(gep.Description);
            if (!Console.IsErrorRedirected)
            {
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Raise a Grace exception for a particular error.
        /// </summary>
        /// <param name="ctx">Evaluation context to raise from</param>
        /// <param name="code">Error code</param>
        /// <param name="vars">Dictionary from variable names to
        /// values to insert in the message in their place.</param>
        /// <param name="localDescription">A description of the error
        /// given at the site of generation, which will be used if no
        /// user error message for <paramref name="code" /> is found.
        /// </param>
        /// <remarks>
        /// A new <c cref="GraceExceptionPacket">GraceExceptionPacket
        /// </c> is created and thrown with the retrieved method and
        /// current call stack.
        /// </remarks>
        /// <seealso cref="WriteException" />
        public static void RaiseError(EvaluationContext ctx,
                string code, Dictionary<string, string> vars,
                string localDescription)
        {
            string baseMessage = GetMessage(code, vars) ?? localDescription;
            var parts = baseMessage.Split(new[] { ": " }, 2,
                    StringSplitOptions.None);
            var kind = parts[0];
            var msg = parts[1];
            msg = FormatMessage(msg, vars);
            GraceExceptionPacket.Throw(kind, code + ": " + msg,
                    ctx.GetStackTrace());
        }

        /// <summary>
        /// Set the sink where error messages will be written.
        /// </summary>
        /// <param name="s">Destination for error messages</param>
        public static void SetSink(OutputSink s)
        {
            sink = s;
        }

        /// <summary>
        /// Suppress printing of a particular static error.
        /// </summary>
        /// <param name="code">Error to silence</param>
        public static void SilenceError(string code)
        {
            SilencedErrors.Add(code);
        }
    }

    /// <summary>Represents the fact that a static error occurred</summary>
    public class StaticErrorException : Exception
    {
        /// <summary>Error code (X####) of this error</summary>
        public string Code { get; private set; }

        /// <param name="code">Error code (X####) of this error</param>
        public StaticErrorException(string code)
        {
            Code = code;
        }
    }

}
