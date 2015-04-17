using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private FieldReaderMethod Reader;
        private FieldWriterMethod Writer;

        private Interpreter.ScopeMemo lexicalScope;
        private Flags flags;

        private string name;

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
            initialise();
        }

        /// <summary>An object with a debugging name</summary>
        /// <param name="name">Debugging name of this object</param>
        public GraceObject(string name)
        {
            initialise();
            this.name = name;
        }

        /// <summary>Initialisation code used by multiple constructors</summary>
        private void initialise()
        {
            Reader = new FieldReaderMethod(fields);
            Writer = new FieldWriterMethod(fields);
            AddMethod("asString", new DelegateMethodNode0(
                        new NativeMethod0(this.AsString)));
            AddMethod("==", new DelegateMethodNode1(new NativeMethod1(this.EqualsEquals)));
            AddMethod("!=", new DelegateMethodNode1(new NativeMethod1(this.NotEquals)));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (name != null)
                return "GraceObject[" + name + "]";
            return "GraceObject";
        }

        /// <summary>Native method supporting Grace AsString</summary>
        public virtual GraceObject AsString()
        {
            return GraceString.Create(this.ToString());
        }

        /// <summary>Native method supporting Grace ==</summary>
        /// <param name="other">Object to compare</param>
        public GraceObject EqualsEquals(GraceObject other)
        {
            return GraceBoolean.Create(this == other);
        }

        /// <summary>Native method supporting Grace !=</summary>
        /// <param name="other">Object to compare</param>
        public GraceObject NotEquals(GraceObject other)
        {
            return GraceBoolean.Create(this != other);
        }

        /// <summary>Save the current scope of this interpreter into
        /// the object</summary>
        /// <param name="ctx">Current interpreter</param>
        public void RememberScope(EvaluationContext ctx)
        {
            lexicalScope = ctx.Memorise();
        }

        /// <summary>Add a method to this object</summary>
        /// <param name="m">Method to add</param>
        public virtual void AddMethod(MethodNode m)
        {
            methods[m.Name] = m;
        }

        /// <summary>Add a method to this object with a custom name</summary>
        /// <param name="name">Name to give this method</param>
        /// <param name="m">Method to add</param>
        public virtual void AddMethod(string name, MethodNode m)
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
            AddMethod(name + ":=", Writer);
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
            return methods.ContainsKey(req.Name);
        }

        /// <summary>Request a method of this object in a given
        /// context</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request</param>
        /// <returns>Return value of the resolved method</returns>
        public virtual GraceObject Request(EvaluationContext ctx,
                MethodRequest req)
        {
            if (lexicalScope != null)
                ctx.Remember(lexicalScope);
            if (!methods.ContainsKey(req.Name))
            {
                ErrorReporting.RaiseError(ctx, "R2000",
                        new Dictionary<string, string> {
                            { "method", req.Name },
                            { "receiver", ToString() }
                        },
                        "LookupError: Method «" + req.Name
                            + "» not found in object «" + ToString() + "»"
                );
            }
            MethodNode m = methods[req.Name];
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
            string name = req.Name.Substring(0, req.Name.Length - 2);
            fields[name] = req[0].Arguments[0];
            Interpreter.Debug("field '" + name + "' set to " + fields[name]);
            return GraceObject.Uninitialised;
        }
    }

}
