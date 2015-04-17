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
    /// <summary>Encapsulates behaviour relating to error reporting</summary>
    public class ErrorReporting
    {
        private static OutputSink sink;

        /// <summary>
        /// Retrieve the error message for a given code from the
        /// highest-priority error message source.
        /// </summary>
        /// <param name="code">Error code</param>
        /// <returns>The error string corresponding to the code,
        /// or null</returns>
        public static string GetMessage(string code)
        {
            string localGrace = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "grace");
            var msg = GetMessageFromFile(code, localGrace);
            if (msg != null)
                return msg;
            string dir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return GetMessageFromFile(code, dir);
        }

        /// <summary>
        /// Retrieve the error message for a given code from the
        /// message database in a given directory.
        /// </summary>
        /// <param name="code">Error code</param>
        /// <param name="dir">Path to directory containing messages
        /// file to use</param>
        /// <returns>The error string corresponding to the code,
        /// or null</returns>
        /// <seealso cref="ErrorReporting.GetMessage" />
        public static string GetMessageFromFile(string code, string dir)
        {
            string fp = Path.Combine(dir, "DefaultErrorMessages.txt");
            if (!System.IO.File.Exists(fp))
                return null;
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
        public static string FormatMessage(string message, Dictionary<string, string> vars)
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
            string baseMessage = GetMessage(code);
            if (baseMessage == null)
                baseMessage = localDescription;
            string formattedMessage = FormatMessage(baseMessage, vars);
            WriteError(module, line, code, formattedMessage);
            throw new StaticErrorException();
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
            string baseMessage = GetMessage(code);
            if (baseMessage == null)
                baseMessage = localDescription;
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

        /// <summary>
        /// Set the sink where error messages will be written.
        /// </summary>
        /// <param name="s">Destination for error messages</param>
        public static void SetSink(OutputSink s)
        {
            sink = s;
        }

    }

    /// <summary>Represents the fact that a static error occurred</summary>
    public class StaticErrorException : Exception
    {

    }

}
