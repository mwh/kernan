using System;
using System.Collections.Generic;
using System.Linq;
using Grace.Runtime;
using System.IO;
using Grace.Parsing;
using Grace.Execution;

namespace Grace.Utility
{
    /// <summary>
    /// Static methods for enabling REPL-like functionality.
    /// </summary>
    public class REPL
    {

        /// <summary>
        /// Create a REPL-ready interpreter using a given
        /// module object and interior scope.
        /// </summary>
        /// <param name="obj">
        /// Module object
        /// </param>
        /// <param name="localScope">
        /// Interior scope which holds (at least) "self".
        /// </param>
        public static Interpreter CreateInterpreter(UserObject obj,
                LocalScope localScope)
        {
            var interp = new Interpreter();
            configureInterpreter(interp, obj, localScope);
            return interp;
        }

        /// <summary>
        /// Create a REPL-ready interpreter using a given
        /// module object, interior scope, and sink.
        /// </summary>
        /// <param name="obj">
        /// Module object
        /// </param>
        /// <param name="localScope">
        /// Interior scope which holds (at least) "self".
        /// </param>
        /// <param name="sink">
        /// Output sink for this interpreter.
        /// </param>
        public static Interpreter CreateInterpreter(UserObject obj,
                LocalScope localScope,
                OutputSink sink)
        {
            var interp = new Interpreter(sink);
            configureInterpreter(interp, obj, localScope);
            return interp;
        }

        private static void configureInterpreter(Interpreter interp,
                UserObject obj, LocalScope localScope)
        {
            interp.LoadPrelude();
            interp.Extend(obj);
            localScope.AddLocalDef("self", obj);
            localScope.AddLocalDef("LAST", GraceObject.Done);
            interp.ExtendMinor(localScope);
        }

        /// <summary>
        /// REPL-execute a given line in an interpreter,
        /// indicating whether it was incomplete, and
        /// restoring the interpreter afterwards.
        /// </summary>
        /// <param name="interp">
        /// Interpreter to use
        /// </param>
        /// <param name="obj">
        /// Module object where method declarations will be added.
        /// </param>
        /// <param name="memo">
        /// Restoration point for the interpreter context.
        /// </param>
        /// <param name="line">
        /// Line of code to execute.
        /// </param>
        /// <param name="unfinished">
        /// Set to true if this line was incomplete and could not
        /// be executed for that reason.
        /// </param>
        /// <param name="result">
        /// Result of the executed expression.
        /// </param>
        public static int RunLine(Interpreter interp,
                UserObject obj,
                Interpreter.ScopeMemo memo,
                string line,
                out bool unfinished, out GraceObject result)
        {
            result = null;
            ParseNode module;
            ObjectConstructorNode mod = null;
            var isExtensionTrait = false;
            try {
                var p = new Parser("source code", line);
                module = p.Parse();
                var opm = (ObjectParseNode)module;
                if (opm.Body.Count == 1)
                {
                    // Tricky hack to let extensions be defined interactively.
                    var trait = opm.Body[0] as TraitDeclarationParseNode;
                    isExtensionTrait = (trait != null
                            && trait.Signature.Name.EndsWith("Extension"));
                }
                var trans = new ExecutionTreeTranslator();
                mod = (ObjectConstructorNode)trans.Translate(opm);
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
                        obj.AddMethod(meth.Name,
                                new Method(meth, memo));
                    }
                    foreach (var node in mod.Body)
                    {
                        var inherits = node as InheritsNode;
                        if (inherits != null)
                        {
                            var ms = inherits.Inherit(interp, obj);
                            obj.AddMethods(ms);
                            obj.RunInitialisers(interp);
                        }
                        var v = node as VarDeclarationNode;
                        var d = node as DefDeclarationNode;
                        Cell cell;
                        var meths = new Dictionary<string, Method>();
                        if (v != null)
                        {
                            var p = obj.CreateVar(v.Name, meths,
                                v.Readable, v.Writable, out cell);
                            obj.AddMethods(meths);
                            var t = v.Type?.Evaluate(interp);
                            p.Write.Type = t;
                            if (v.Value != null)
                            {
                                var val = Matching.TypeMatch(interp, t, v.Value.Evaluate(interp), v.Name);
                                cell.Value = val;
                            }
                            result = GraceObject.Done;
                            continue;
                        }
                        if (d != null)
                        {
                            var t = d.Type?.Evaluate(interp);
                            obj.CreateDef(d.Name, meths,
                                    d.Public, out cell);
                            obj.AddMethods(meths);
                            var val = Matching.TypeMatch(interp, t, d.Value.Evaluate(interp), d.Name);
                            cell.Value = val;
                            result = GraceObject.Done;
                            continue;
                        }
                        var ret = node.Evaluate(interp);
                        if (ret != null
                                && ret != GraceObject.Done
                                && ret != GraceObject.Uninitialised)
                        {
                            interp.Print(interp, ret);
                        }
                        result = ret;
                    }
                    if (isExtensionTrait)
                        interp.LoadExtensionsFromObject(obj);
                }
                catch (GraceExceptionPacketException e)
                {
                    ErrorReporting.WriteLine("Uncaught exception:");
                    ErrorReporting.WriteException(e.ExceptionPacket);
                    if (e.ExceptionPacket.StackTrace != null)
                    {
                        foreach (var l in e.ExceptionPacket.StackTrace)
                        {
                            ErrorReporting.WriteLine("    from "
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
            return 0;
        }
    }
}
