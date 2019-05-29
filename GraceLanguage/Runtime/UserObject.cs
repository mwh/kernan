using System.Collections.Generic;
using Grace.Execution;
using Grace.Parsing;

namespace Grace.Runtime
{
    /// <summary>
    /// A user-space Grace object arising from an object
    /// constructor expression.
    /// </summary>
    public class UserObject : GraceObject
    {

        private List<UserObjectInitialiser> initialisers
            = new List<UserObjectInitialiser>();

        private List<Cell> cells = new List<Cell>();

        /// <summary>
        /// Creates a basic user object.
        /// </summary>
        public UserObject()
        {
            SetFlag(Flags.UserspaceObject);
        }

        /// <param name="omitDefaultMethods">
        /// True to leave out the default object protocol elements
        /// such as ==; false to keep them.
        /// </param>
        public UserObject(bool omitDefaultMethods)
            : base(omitDefaultMethods)
        {
            SetFlag(Flags.UserspaceObject);
        }

        /// <summary>
        /// Add an initialiser to run when this object is complete.
        /// </summary>
        /// <param name="i">Initialiser to add</param>
        public void AddInitialiser(UserObjectInitialiser i)
        {
            initialisers.Add(i);
        }

        /// <summary>
        /// Run the initialisers of this object.
        /// </summary>
        public void RunInitialisers(EvaluationContext ctx)
        {
            foreach (var i in initialisers)
                i.Run(ctx, this);
            initialisers.Clear();
        }

        /// <summary>
        /// Add methods to this object representing a var
        /// declaration
        /// </summary>
        /// <param name="name">Name of the var to add</param>
        /// <param name="val">Initial value of this var</param>
        /// <returns>Object encapsulating the added methods</returns>
        public virtual ReaderWriterPair AddLocalVar(string name,
                GraceObject val)
        {
            return AddLocalVar(name, val, null);
        }

        /// <summary>
        /// Add methods to this object representing a var
        /// declaration
        /// </summary>
        /// <param name="name">Name of the var to add</param>
        /// <param name="val">Initial value of this var</param>
        /// <param name="pattern">Pattern to check values against</param>
        /// <returns>Object encapsulating the added methods</returns>
        public virtual ReaderWriterPair AddLocalVar(string name,
                GraceObject val, Node pattern)
        {
            var c = new Cell( val == null ? GraceObject.Uninitialised : val,
                    pattern);
            cells.Add(c);
            var reader = new FieldReaderMethod(c);
            var writer = new FieldWriterMethod(c, pattern);
            AddMethod(name, reader);
            AddMethod(name + " :=(_)", writer);
            return new ReaderWriterPair { Read = reader, Write = writer };
        }

        /// <summary>
        /// Add method to this object representing a def
        /// declaration
        /// </summary>
        /// <param name="name">Name of the def to add</param>
        /// <param name="val">Value of this def</param>
        /// <returns>Added method</returns>
        public virtual Method AddLocalDef(string name, GraceObject val)
        {
            var c = new Cell(val, null);
            cells.Add(c);
            var read = new FieldReaderMethod(c);
            AddMethod(name, read);
            return read;
        }

        /// <summary>
        /// Create a method corresponding to a def declaration,
        /// backed by a cell, and store that method under a given
        /// name.
        /// </summary>
        /// <param name="name">Name of def</param>
        /// <param name="methods">
        /// Mapping of names to methods to update
        /// </param>
        /// <param name="readable">
        /// True if this def is public.
        /// </param>
        /// <param name="pattern">
        /// Pattern the value of this def should match
        /// </param>
        /// <param name="cell">
        /// Cell storing the value of this def
        /// </param>
        public virtual void CreateDef(
                string name,
                Dictionary<string, Method> methods,
                bool readable,
                Node pattern,
                out Cell cell
                )
        {
            cell = new Cell(GraceObject.Uninitialised, pattern);
            var m = new FieldReaderMethod(cell);
            methods[name] = m;
            if (!readable)
                m.Confidential = true;
        }

        /// <summary>
        /// Create methods corresponding to a var declaration,
        /// backed by a cell, and store those methods under a
        /// given name.
        /// </summary>
        /// <param name="name">Name of var</param>
        /// <param name="methods">
        /// Mapping of names to methods to update
        /// </param>
        /// <param name="readable">
        /// True if this var is readable.
        /// </param>
        /// <param name="writable">
        /// True if this var is writable.
        /// </param>
        /// <param name="pattern">
        /// Pattern to check values against
        /// </param>
        /// <param name="cell">
        /// Cell storing the value of this var
        /// </param>
        public virtual void CreateVar(
                string name,
                Dictionary<string, Method> methods,
                bool readable, bool writable,
                Node pattern,
                out Cell cell
                )
        {
            cell = new Cell(GraceObject.Uninitialised, pattern);
            var r = new FieldReaderMethod(cell);
            methods[name] = r;
            var w = new FieldWriterMethod(cell, pattern);
            methods[name + " :=(_)"] = w;
            if (!readable)
                r.Confidential = true;
            if (!writable)
                w.Confidential = true;
        }


    }

    /// <summary>
    /// Encapsulates the data needed for final-phase initialisation
    /// of a user object from a particular object constructor.
    /// </summary>
    public class UserObjectInitialiser
    {

        private Dictionary<Node, Cell> cellMapping;

        private ObjectConstructorNode constructor;

        private Interpreter.ScopeMemo scope;

        /// <param name="ocn">
        /// Object constructor node with the initialisation code
        /// for this layer of initialisation.
        /// </param>
        /// <param name="s">
        /// Lexical scope the initialisation code will be executed in.
        /// </param>
        /// <param name="map">
        /// Mapping of declaration nodes to storage cells.
        /// </param>
        public UserObjectInitialiser(
                ObjectConstructorNode ocn,
                Interpreter.ScopeMemo s,
                Dictionary<Node, Cell> map
                )
        {
            constructor = ocn;
            scope = s;
            cellMapping = map;
        }

        /// <summary>
        /// Execute the initialisation code.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="obj">User object to initialise inside</param>
        public void Run(
                EvaluationContext ctx,
                UserObject obj)
        {
            ctx.Remember(scope);
            constructor.Initialise(ctx, obj, cellMapping);
            ctx.Forget(scope);
        }
    }

    /// <summary>
    /// Reader method for a particular field of an object.
    /// </summary>
    class FieldReaderMethod : Method
    {
        Cell cell;

        /// <param name="c">Cell holding the value to access.</param>
        public FieldReaderMethod(Cell c)
        {
            cell = c;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(
                EvaluationContext ctx,
                GraceObject self,
                MethodRequest req
                )
        {
            checkAccessibility(ctx, req);
            MethodHelper.CheckNoInherits(ctx, req);
            MethodNode.CheckArgCount(ctx, req.Name, req.Name,
                    0, false,
                    req[0].Arguments.Count);
            if (cell.Value == GraceObject.Uninitialised)
                ErrorReporting.RaiseError(ctx, "R2008",
                    new Dictionary<string, string> {
                        { "name", req.Name },
                        { "receiver", self.ToString() }
                    },
                    "UninitialisedReadError: Cannot read from " + req.Name
                );
            return cell.Value;
        }

    }

    /// <summary>
    /// Writer method for a particular field of an object.
    /// </summary>
    class FieldWriterMethod : Method
    {
        Cell cell;
        Node pattern;

        /// <param name="c">Cell holding the value to access.</param>
        /// <param name="p">Pattern to check values against</param>
        public FieldWriterMethod(Cell c, Node p)
        {
            cell = c;
            pattern = p;
        }

        /// <inheritdoc/>
        public override GraceObject Respond(
                EvaluationContext ctx,
                GraceObject self,
                MethodRequest req
                )
        {
            checkAccessibility(ctx, req);
            var val = req[1].Arguments[0];
            if (pattern != null)
            {
                var pat = pattern.Evaluate(ctx);
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
                                    GraceString.AsNativeString(ctx, val)},
                                { "required",
                                    ParseNodeMeta.PrettyPrint(ctx,
                                            pattern.Origin) }
                            },
                            "ArgumentTypeError: value assigned to variable did"
                            + " not match type"
                            );
                }
            }
            cell.Value = val;
            return GraceObject.Done;
        }

    }

    /// <summary>
    /// Encapsulates a mutable GraceObject location.
    /// </summary>
    public class Cell
    {
        /// <summary>
        /// Object held by this cell.
        /// </summary>
        public GraceObject Value { get; set; }

        /// <summary>
        /// Pattern expression for values in this cell.
        /// </summary>
        public Node Pattern { get; set; }

        /// <param name="v">Initial value of this cell</param>
        /// <param name="p">Pattern for objects in this cell</param>
        public Cell(GraceObject v, Node p)
        {
            Value = v;
            Pattern = p;
        }

        /// <summary>Check that the value in this cell meets the
        /// pattern</summary>
        /// <param name="ctx">Interpreter</param>
        public void Check(EvaluationContext ctx)
        {
            if (Value == GraceObject.Uninitialised)
                return;
            if (Pattern == null)
                return;
            var pat = Pattern.Evaluate(ctx);
            var mr = Matching.Match(ctx, pat, Value);
            if (Matching.Succeeded(ctx, mr))
            {
                Value = Matching.GetResult(ctx, mr);
            }
            else
            {
                ErrorReporting.RaiseError(ctx, "R2025",
                        new Dictionary<string, string> {
                            { "value",
                                GraceString.AsNativeString(ctx, Value)},
                            { "type",
                                ParseNodeMeta.PrettyPrint(ctx,
                                        Pattern.Origin) }
                        },
                        "ArgumentTypeError: value assigned to var did not "
                        + "match type"
                        );
            }
        }
    }

    /// <summary>
    /// An artificial object to be inserted into scope stacks and
    /// capture passing requests, redirecting them to "self". These
    /// objects ensure that only locally-known names are sent to self,
    /// preventing unanticipated downcalls.
    /// </summary>
    public class SelfRedirectorObject : GraceObject
    {
        GraceObject target;
        HashSet<string> redirectedMethods = new HashSet<string>();

        /// <param name="self">Target of redirections</param>
        public SelfRedirectorObject(GraceObject self)
        {
            target = self;
            SetFlag(GraceObject.Flags.ObjectConstructor);
        }

        /// <summary>
        /// Set the methods this redirector will capture
        /// and respond to.
        /// </summary>
        /// <param name="names">Enumerable of method names</param>
        public void SetMethods(IEnumerable<string> names)
        {
            redirectedMethods.UnionWith(names);
        }

        /// <inheritdoc/>
        public override GraceObject Request(EvaluationContext ctx,
                MethodRequest req,
                GraceObject receiver)
        {
            return target.Request(ctx, req, target);
        }

        /// <inheritsdoc/>
        public override bool RespondsTo(MethodRequest req)
        {
            return redirectedMethods.Contains(req.Name);
        }
    }
}
