using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Grace.Execution;
using Grace.Parsing;

namespace Grace.Runtime
{
    /// <summary>A Grace object</summary>
    public class GraceObject
    {
        /// <summary>Certain indicators that can be embedded in an
        /// object</summary>
        public enum Flags
        {
            /// <summary>Is a user-space object</summary>
            UserspaceObject = 1,
            /// <summary>Dialect should run atModuleEnd method</summary>
            RunAtModuleEnd = 2
        }
        private Dictionary<string, Method> methods = new Dictionary<string, Method>();
        private Dictionary<string, GraceObject> fields = new Dictionary<string, GraceObject>();

        /// <summary>
        /// Gives the names of all non-operator methods on
        /// this object, including inherited methods.
        /// </summary>
        public virtual IEnumerable<string> DotMethods
        {
            get
            {
                var names = new HashSet<string>();
                foreach (var m in methods.Keys)
                    if (Lexer.IsIdentifier(m))
                        names.Add(m);
                return names;
            }
        }

        private Interpreter.ScopeMemo lexicalScope;
        private Flags flags;

        private string name;

        /// <summary>Part-object with built-in default methods</summary>
        public static readonly GraceObject DefaultMethods = new GraceObject();

        /// <summary>Name of this object for debugging</summary>
        /// <value>This property gets/sets the value of the field name</value>
        public string TagName
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }

        /// <summary>
        /// The final self-identity of this object.
        /// </summary>
        /// <remarks>
        /// In the case of inheritance, this value will be different
        /// than "this" in the parent part-objects. Methods such as
        /// the built-in == need to use this value to get correct
        /// reference semantics.
        /// </remarks>
        public GraceObject Identity { get; set; }

        /// <summary>A default object</summary>
        public GraceObject()
        {
            initialise(true);
        }

        /// <summary>An object with or without default methods</summary>
        /// <param name="omitDefaultMethods">
        /// Leave out the default methods ==, asString, etc,
        /// from this (part-)object.
        /// </param>
        public GraceObject(bool omitDefaultMethods)
        {
            initialise(!omitDefaultMethods);
        }

        /// <summary>An object with a debugging name</summary>
        /// <param name="name">Debugging name of this object</param>
        public GraceObject(string name)
        {
            initialise(true);
            this.name = name;
        }

        /// <summary>An object with a debugging name</summary>
        /// <param name="name">Debugging name of this object</param>
        /// <param name="omitDefaultMethods">
        /// Leave out the default methods ==, asString, etc,
        /// from this (part-)object.
        /// </param>
        public GraceObject(string name, bool omitDefaultMethods)
        {
            initialise(!omitDefaultMethods);
            this.name = name;
        }

        /// <summary>An object with an internal scope</summary>
        /// <param name="scope">Internal scope of this object</param>
        /// <param name="omitDefaultMethods">
        /// Leave out the default methods ==, asString, etc,
        /// from this (part-)object.
        /// </param>
        public GraceObject(LocalScope scope, bool omitDefaultMethods)
        {
            initialise(!omitDefaultMethods);
        }

        /// <summary>Initialisation code used by multiple constructors</summary>
        private void initialise(bool defaults)
        {
            Identity = this;
            if (defaults)
            {
                // The default methods are found on all objects,
                // but should not be defined on all part-objects.
                // User objects obtain them by inheritance, but
                // others will have them directly.
                AddMethod("asString", null);
                AddMethod("==", null);
                AddMethod("!=", null);
            }
        }

        /// <summary>
        /// Find a Grace superobject that is an instance of the native
        /// type T.
        /// </summary>
        /// <returns>The parent of that type, or null</returns>
        /// <typeparam name="T">Type to find</typeparam>
        public T FindNativeParent<T>() where T : GraceObject
        {
            var s = this as T;
            return s;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (name != null)
                return string.Format("{0}[{1:X}]",
                        name,
                        RuntimeHelpers.GetHashCode(this));
            return string.Format("GraceObject[{0:X}]",
                    RuntimeHelpers.GetHashCode(this));
        }

        /// <summary>Native method supporting Grace AsString</summary>
        public virtual GraceObject AsString(EvaluationContext ctx,
                GraceObject self)
        {
            return GraceString.Create(self.ToString());
        }

        /// <summary>Native method supporting Grace ==</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver</param>
        /// <param name="other">Object to compare</param>
        private static GraceObject mEqualsEquals(EvaluationContext ctx,
                GraceObject self,
                GraceObject other)
        {
            return GraceBoolean.Create(object.ReferenceEquals(self, other));
        }

        /// <summary>Native method supporting Grace !=</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver</param>
        /// <param name="other">Object to compare</param>
        private static GraceObject mNotEquals(EvaluationContext ctx,
                GraceObject self,
                GraceObject other)
        {
            return GraceBoolean.Create(!object.ReferenceEquals(self, other));
        }

        /// <summary>Save the current scope of this interpreter into
        /// the object</summary>
        /// <param name="ctx">Current interpreter</param>
        public void RememberScope(EvaluationContext ctx)
        {
            lexicalScope = ctx.Memorise();
        }

        /// <summary>Remove a method from this object</summary>
        /// <param name="m">Method name to remove</param>
        public virtual void RemoveMethod(string m)
        {
            methods.Remove(m);
        }

        /// <summary>Add a method to this object</summary>
        /// <param name="m">Method to add</param>
        public void AddMethod(MethodNode m)
        {
            methods[m.Name] = new Method(m, lexicalScope);
        }

        /// <summary>Add a method to this object with a custom name</summary>
        /// <param name="name">Name to give this method</param>
        /// <param name="m">Method to add</param>
        public void AddMethod(string name, Method m)
        {
            methods[name] = m;
        }

        /// <summary>
        /// Add several methods to this object with given names.
        /// </summary>
        /// <param name="meths">
        /// Dictionary of method names=&gt;methods to add
        /// </param>
        public void AddMethods(IDictionary<string, Method> meths)
        {
            foreach (var kvp in meths)
                methods[kvp.Key] = kvp.Value;
        }

        /// <summary>Add methods to this object representing a var
        /// declaration</summary>
        /// <param name="name">Name of the var to add</param>
        /// <param name="val">Initial value of this var</param>
        /// <returns>Object encapsulating the added methods</returns>
        public virtual ReaderWriterPair AddLocalVar(string name,
                GraceObject val)
        {
            if (fields.ContainsKey(name))
            {
                if (val != GraceObject.Uninitialised)
                    fields[name] = val;
                return new ReaderWriterPair
                {
                    Read = methods[name],
                    Write = methods[name + " :="]
                };
            }
            fields[name] = val == null ? GraceObject.Uninitialised : val;
            var reader = new FieldReaderMethod(fields);
            var writer = new FieldWriterMethod(fields);
            AddMethod(name, reader);
            AddMethod(name + " :=", writer);
            return new ReaderWriterPair { Read = reader, Write = writer };
        }

        /// <summary>Add method to this object representing a def
        /// declaration</summary>
        /// <param name="name">Name of the def to add</param>
        /// <param name="val">Value of this def</param>
        /// <returns>Added method</returns>
        public virtual Method AddLocalDef(string name, GraceObject val)
        {
            fields[name] = val;
            var read = new FieldReaderMethod(fields);
            AddMethod(name, read);
            return read;
        }

        /// <summary>Get all method names in this object</summary>
        public List<string> MethodNames()
        {
            return new List<string>(methods.Keys);
        }

        /// <summary>Determines whether this object can respond to
        /// the given request</summary>
        /// <param name="req">Request to check</param>
        /// <returns>True if this object can process the request;
        /// false otherwise</returns>
        public virtual bool RespondsTo(MethodRequest req)
        {
            if (methods.ContainsKey(req.Name))
                return true;
            return false;
        }

        /// <summary>
        /// Get a method by a given name on an object that was
        /// not allocated in advance.
        /// </summary>
        /// <remarks>
        /// A method can be added with a name, but a null MethodNode.
        /// Such a method is "lazy" and will be computed (and cached)
        /// when first accessed, if ever.
        /// </remarks>
        /// <param name="name">Name of method to create</param>
        protected virtual Method getLazyMethod(string name)
        {
            Method m;
            switch(name)
            {
                case "asString":
                    m = new DelegateMethodReceiver0Ctx(AsString);
                    return m;
                case "==":
                    m = new DelegateMethodReceiver1Ctx(mEqualsEquals);
                    return m;
                case "!=":
                    m = new DelegateMethodReceiver1Ctx(mNotEquals);
                    return m;
            }
            return null;
        }

        /// <summary>
        /// Find a method node with a given name in this object
        /// or its parents.
        /// </summary>
        /// <param name="name">Method name to find</param>
        protected virtual Method FindMethod(string name)
        {
            if (methods.ContainsKey(name))
            {
                if (methods[name] == null)
                    methods[name] = getLazyMethod(name);
                return methods[name];
            }
            return null;
        }

        /// <summary>
        /// Find a method node with a given name in a given object
        /// or its parents.
        /// </summary>
        /// <param name="obj">Object to search</param>
        /// <param name="name">Method name to find</param>
        protected Method FindMethod(GraceObject obj, string name)
        {
            return obj.FindMethod(name);
        }

        /// <summary>Request a method of this object in a given
        /// context</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request</param>
        /// <returns>Return value of the resolved method</returns>
        public virtual GraceObject Request(EvaluationContext ctx,
                MethodRequest req)
        {
            return Request(ctx, req, this.Identity);
        }

        /// <summary>
        /// Request a method of this object in a given
        /// context with a particular receiver identity
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request</param>
        /// <param name="receiver">Receiver identity</param>
        /// <returns>Return value of the resolved method</returns>
        public virtual GraceObject Request(EvaluationContext ctx,
                MethodRequest req,
                GraceObject receiver)
        {
            var m = FindMethod(req.Name);
            if (m == null)
            {
                ErrorReporting.RaiseError(ctx, "R2000",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "receiver", ToString() }
                        },
                        "LookupError: Method «" + req.Name +
                            "» not found.");
            }
            var ret = m.Respond(ctx, receiver, req);
            return ret;
        }

        /// <summary>Mark this object with a particular flag</summary>
        /// <param name="f">Flag to enable</param>
        public void SetFlag(Flags f)
        {
            this.flags |= f;
        }

        /// <summary>Check if this object has a particular flag</summary>
        /// <param name="f">Flag to check</param>
        public bool HasFlag(Flags f)
        {
            return flags.HasFlag(f);
        }

        /// <summary>The uninitialised variable object</summary>
        public static readonly GraceObject Uninitialised = new GraceObject();

        /// <summary>The singleton done object</summary>
        public static readonly GraceObject Done = new GraceObject("Done");

        /// <summary>The singleton uninherited parent object</summary>
        public static readonly GraceObject UninheritedParent =
            new GraceObject("ParentNotInheritedYet", true);
    }

    /// <summary>Reusable method reading a field of an object</summary>
    public class FieldReaderMethod : Method
    {
        private Dictionary<string, GraceObject> fields;

        /// <param name="fields">Field dictionary of the object</param>
        public FieldReaderMethod(Dictionary<string, GraceObject> fields)
            : base(null, null)
        {
            this.fields = fields;
            Confidential = true;
        }

        /// <inheritdoc/>
        /// <remarks>This method determines the field to access by the
        /// contents of the request.</remarks>
        public override GraceObject Respond(EvaluationContext ctx,
                GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            MethodHelper.CheckNoInherits(ctx, req);
            MethodNode.CheckArgCount(ctx, req.Name, req.Name,
                    0, false,
                    req[0].Arguments.Count);
            string name = req.Name;
            if (fields[name] == GraceObject.Uninitialised
                    || fields[name] == null)
            {
                ErrorReporting.RaiseError(ctx, "R2008",
                    new Dictionary<string, string> {
                        { "name", name },
                        { "receiver", ToString() }
                    },
                    "UninitialisedReadError: Cannot read from «" + name + "»"
                );
            }
            return fields[name];
        }
    }

    /// <summary>Reusable method writing a field of an object</summary>
    public class FieldWriterMethod : Method
    {
        private Dictionary<string, GraceObject> fields;

        /// <param name="fields">Field dictionary of the object</param>
        public FieldWriterMethod(Dictionary<string, GraceObject> fields)
            : base(null, null)
        {
            this.fields = fields;
            Confidential = true;
        }

        /// <inheritdoc/>
        /// <remarks>This method determines the field to access by the
        /// contents of the request.</remarks>
        public override GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            string name = req[0].Name;
            fields[name] = req[1].Arguments[0];
            return GraceObject.Done;
        }
    }

}
