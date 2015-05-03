using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Grace.Execution;

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
        private Dictionary<string, MethodNode> methods = new Dictionary<string, MethodNode>();
        private Dictionary<string, GraceObject> fields = new Dictionary<string, GraceObject>();

        private Stack<GraceObject> parents = new Stack<GraceObject>();

        private FieldReaderMethod Reader;
        private FieldWriterMethod Writer;

        private Interpreter.ScopeMemo lexicalScope;
        private Flags flags;

        private string name;
        private LocalScope _internalScope;

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
            this._internalScope = scope;
        }

        /// <summary>Initialisation code used by multiple constructors</summary>
        private void initialise(bool defaults)
        {
            Reader = new FieldReaderMethod(fields);
            Writer = new FieldWriterMethod(fields);
            if (defaults)
            {
                // The default methods are found on all objects,
                // but should not be defined on all part-objects.
                // User objects obtain them by inheritance, but
                // others will have them directly. These methods
                // should be given the final object's identity as
                // their receiver, as otherwise equality will
                // always fail. The UseRealReceiver flag on
                // a method node ensures this.
                MethodNode m = new DelegateMethodNode0(
                            new NativeMethod0(AsString));
                m.UseRealReceiver = true;
                AddMethod("asString", m);
                m = new DelegateMethodNodeReceiver1Ctx(
                            new NativeMethodReceiver1Ctx(mEqualsEquals));
                m.UseRealReceiver = true;
                AddMethod("==", m);
                m = new DelegateMethodNodeReceiver1Ctx(
                            new NativeMethodReceiver1Ctx(mNotEquals));
                m.UseRealReceiver = true;
                AddMethod("!=", m);
            }
        }

        /// <summary>Add a named superobject to this object</summary>
        /// <param name="name">Name of parent</param>
        /// <param name="obj">Parent part-object</param>
        public void AddParent(string name, GraceObject obj)
        {
            parents.Push(obj);
            if (_internalScope != null && name != null)
                _internalScope.AddLocalDef(name, obj);
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
            if (s != null)
                return s;
            foreach (var p in parents)
            {
                s = p as T;
                if (s != null)
                    return s;
            }
            foreach (var p in parents)
            {
                s = p.FindNativeParent<T>();
                if (s != null)
                    return s;
            }
            return null;
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
        public virtual GraceObject AsString()
        {
            return GraceString.Create(this.ToString());
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
            methods[m.Name] = m;
        }

        /// <summary>Add a method to this object with a custom name</summary>
        /// <param name="name">Name to give this method</param>
        /// <param name="m">Method to add</param>
        public void AddMethod(string name, MethodNode m)
        {
            methods[name] = m;
        }

        /// <summary>Add methods to this object representing a var
        /// declaration</summary>
        /// <param name="name">Name of the var to add</param>
        /// <param name="val">Initial value of this var</param>
        /// <returns>Object encapsulating the added methods</returns>
        public virtual ReaderWriterPair AddLocalVar(string name,
                GraceObject val)
        {
            fields[name] = val;
            AddMethod(name, Reader);
            AddMethod(name + " :=", Writer);
            return new ReaderWriterPair { Read = Reader, Write = Writer };
        }

        /// <summary>Add method to this object representing a def
        /// declaration</summary>
        /// <param name="name">Name of the def to add</param>
        /// <param name="val">Value of this def</param>
        /// <returns>Added method</returns>
        public virtual MethodNode AddLocalDef(string name, GraceObject val)
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
            foreach (var o in parents)
                if (o.RespondsTo(req))
                    return true;
            return false;
        }

        private MethodNode findMethod(string name)
        {
            if (methods.ContainsKey(name))
                return methods[name];
            foreach (var o in parents)
            {
                var m = o.findMethod(name);
                if (m != null)
                    return m;
            }
            return null;
        }

        /// <summary>Request a method of this object in a given
        /// context</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request</param>
        /// <returns>Return value of the resolved method</returns>
        public virtual GraceObject Request(EvaluationContext ctx,
                MethodRequest req)
        {
            var m = findMethod(req.Name);
            if (!methods.ContainsKey(req.Name))
            {
                bool found = false;
                foreach (var o in parents)
                {
                    m = o.findMethod(req.Name);
                    if (m != null)
                    {
                        if (m.UseRealReceiver)
                        {
                            found = true;
                            break;
                        }
                        return o.Request(ctx, req);
                    }
                }
                if (!found)
                    ErrorReporting.RaiseError(ctx, "R2000",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "receiver", ToString() }
                        },
                        "LookupError: Method «" + req.Name
                            + "» not found in object «" + ToString() + "»"
                );
            }
            if (lexicalScope != null)
                ctx.Remember(lexicalScope);
            Interpreter.Debug("calling method " + req.Name);
            var ret = m.Respond(ctx, this, req);
            if (lexicalScope != null)
                ctx.Forget(lexicalScope);
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
    }

    /// <summary>Reusable method reading a field of an object</summary>
    public class FieldReaderMethod : MethodNode
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
            CheckArgCount(ctx, req.Name, req.Name,
                    0, false,
                    req[0].Arguments.Count);
            string name = req.Name;
            Interpreter.Debug("field '" + name + "' is " + fields[name]);
            return fields[name];
        }
    }

    /// <summary>Reusable method writing a field of an object</summary>
    public class FieldWriterMethod : MethodNode
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
            Interpreter.Debug("field '" + name + "' set to " + fields[name]);
            return GraceObject.Uninitialised;
        }
    }

}
