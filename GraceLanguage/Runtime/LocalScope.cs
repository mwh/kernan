using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;
using Grace.Parsing;

namespace Grace.Runtime
{
    /// <summary>Local scope of a method</summary>
    public class LocalScope : UserObject
    {
        /// <summary>Object to redirect any requests resolved on the
        /// surrounding scope to</summary>
        public GraceObject RedirectSurrounding { get; set; }

        /// <summary>Reusable method for reading a local variable</summary>
        public static readonly LocalReaderMethod Reader = new LocalReaderMethod();

        /// <summary>Reusable method for writing a local variable</summary>
        public static readonly LocalWriterMethod Writer = new LocalWriterMethod();

        /// <summary>Mapping of variable names to values</summary>
        public Dictionary<string, GraceObject> locals = new Dictionary<string, GraceObject>();

        /// <summary>Mapping of variable names to patterns</summary>
        public Dictionary<string, Node> Patterns = new Dictionary<string, Node>();

        private string name = "<anon>";

        /// <summary>The name of this scope</summary>
        public string Name {
            get {
                return name;
            }
        }

        /// <summary>Empty anonymous scope</summary>
        public LocalScope()
            : base(true)
        {
        }

        /// <summary>Empty named scope</summary>
        /// <param name="name">Name of this scope for debugging</param>
        public LocalScope(string name)
            : base(true)
        {
            this.name = name;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (name != null)
                return "LocalScope[" + name + "]";
            return "LocalScope";
        }

        /// <summary>
        /// Check for a named member of this object
        /// </summary>
        /// <param name="name">Name to check</param>
        public bool Contains(string name)
        {
            return locals.ContainsKey(name);
        }

        /// <summary>Add a new def to this scope</summary>
        /// <param name="name">Name of def to create</param>
        public void AddLocalDef(string name)
        {
            AddLocalDef(name, GraceObject.Uninitialised);
        }

        /// <summary>Add a new def to this scope</summary>
        /// <param name="name">Name of def to create</param>
        /// <param name="val">Value to set def to</param>
        /// <returns>Method that was added</returns>
        public override Method AddLocalDef(string name, GraceObject val)
        {
            locals[name] = val;
            AddMethod(name, Reader);
            return Reader;
        }

        /// <summary>Add a new var to this scope</summary>
        /// <param name="name">Name of var to create</param>
        public void AddLocalVar(string name)
        {
            AddLocalVar(name, GraceObject.Uninitialised);
        }

        /// <summary>Add a new var to this scope</summary>
        /// <param name="name">Name of var to create</param>
        /// <param name="val">Value to set var to</param>
        /// <param name="pattern">Pattern to check values against</param>
        /// <returns>Pair of methods that were added</returns>
        public override ReaderWriterPair AddLocalVar(string name,
                GraceObject val, Node pattern)
        {
            locals[name] = val;
            Patterns[name] = pattern;
            AddMethod(name, Reader);
            AddMethod(name + " :=(_)", Writer);
            return new ReaderWriterPair { Read = Reader, Write = Writer };
        }

        /// <summary>Add a new var to this scope</summary>
        /// <param name="name">Name of var to create</param>
        /// <param name="val">Value to set var to</param>
        /// <returns>Pair of methods that were added</returns>
        public override ReaderWriterPair AddLocalVar(string name,
                GraceObject val)
        {
            locals[name] = val;
            AddMethod(name, Reader);
            AddMethod(name + " :=(_)", Writer);
            return new ReaderWriterPair { Read = Reader, Write = Writer };
        }

        /// <summary>Access variables in this scope</summary>
        /// <value>This property accesses the Dictionary field locals</value>
        public GraceObject this[string s]
        {
            get
            {
                return locals[s];
            }
            set
            {
                locals[s] = value;
            }
        }

        private static Dictionary<string, string> names
            = new Dictionary<string, string>();
        private static string getName(string name)
        {
            if (!names.ContainsKey(name))
                names[name] = "LocalScope[" + name + "]";
            return names[name];
        }
    }

    /// <summary>Method to read a local variable</summary>
    public class LocalReaderMethod : Method
    {

        ///
        public LocalReaderMethod()
            : base(null, null)
        {
        }

        /// <inheritdoc/>
        /// <remarks>This method uses the indexer on the LocalScope
        /// object the method was requested on.</remarks>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            MethodHelper.CheckNoInherits(ctx, req);
            MethodNode.CheckArgCount(ctx, req.Name, req.Name,
                    0, false,
                    req[0].Arguments.Count);
            LocalScope s = self as LocalScope;
            string name = req.Name;
            if (s[name] == GraceObject.Uninitialised
                    || s[name] == null)
            {
                ErrorReporting.RaiseError(ctx, "R2008",
                    new Dictionary<string, string> {
                        { "name", name },
                        { "receiver", ToString() }
                    },
                    "UninitialisedReadError: Cannot read from «" + name + "»"
                );
            }
            return s[name];
        }
    }

    /// <summary>Method to write a local variable</summary>
    public class LocalWriterMethod : Method
    {
        ///
        public LocalWriterMethod()
            : base(null, null)
        {

        }

        /// <inheritdoc/>
        /// <remarks>This method uses the indexer on the LocalScope
        /// object the method was requested on.</remarks>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            LocalScope s = self as LocalScope;
            string name = req[0].Name;
            var val = req[1].Arguments[0];
            if (s.Patterns[name] != null
                    && val != GraceObject.Uninitialised)
            {
                var pat = s.Patterns[name].Evaluate(ctx);
                var mr = Matching.Match(ctx, pat, val);
                if (Matching.Succeeded(ctx, mr))
                {
                    val = Matching.GetResult(ctx, mr);
                }
                else
                {
                    ErrorReporting.RaiseError(ctx, "R2001",
                            new Dictionary<string, string> {
                                { "method", req.Name },
                                { "index", "1" },
                                { "part", ":=" },
                                { "argument",
                                    GraceString.AsNativeString(ctx, val) },
                                { "required", ParseNodeMeta.PrettyPrint(ctx,
                                            s.Patterns[name].Origin) }
                            },
                            "ArgumentTypeError: value assigned to var did not "
                            + "match type"
                            );
                }
            }
            s[name] = val;
            return GraceObject.Done;
        }
    }

}
