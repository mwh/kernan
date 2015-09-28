using System.Collections.Generic;
using Grace.Execution;

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
            var c = new Cell( val == null ? GraceObject.Uninitialised : val);
            var reader = new FieldReaderMethod(c);
            var writer = new FieldWriterMethod(c);
            AddMethod(name, reader);
            AddMethod(name + " :=", writer);
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
            var c = new Cell(val);
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
        /// <param name="cell">
        /// Cell storing the value of this def
        /// </param>
        public virtual void CreateDef(
                string name,
                Dictionary<string, Method> methods,
                bool readable,
                out Cell cell
                )
        {
            cell = new Cell(GraceObject.Uninitialised);
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
        /// <param name="cell">
        /// Cell storing the value of this var
        /// </param>
        public virtual void CreateVar(
                string name,
                Dictionary<string, Method> methods,
                bool readable, bool writable,
                out Cell cell
                )
        {
            cell = new Cell(GraceObject.Uninitialised);
            var r = new FieldReaderMethod(cell);
            methods[name] = r;
            var w = new FieldWriterMethod(cell);
            methods[name + " :="] = w;
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
            ctx.Extend(obj);
            constructor.Initialise(ctx, obj, cellMapping);
            ctx.Unextend(obj);
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

        /// <param name="c">Cell holding the value to access.</param>
        public FieldWriterMethod(Cell c)
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
            cell.Value = req[1].Arguments[0];
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

        /// <param name="v">Initial value of this cell</param>
        public Cell(GraceObject v)
        {
            Value = v;
        }
    }
}
