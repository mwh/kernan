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
        [System.FlagsAttribute]
        public enum Flags
        {
            /// <summary>Is a user-space object</summary>
            UserspaceObject = 1,
            /// <summary>Dialect should run atModuleEnd method</summary>
            RunAtModuleEnd = 2,
            /// <summary>Came from an object constructor</summary>
            ObjectConstructor = 4
        }
        private Dictionary<string, Method> objectMethods;

        /// <summary>
        /// Gives the names of all non-operator methods on
        /// this object, including inherited methods.
        /// </summary>
        public virtual IEnumerable<string> DotMethods
        {
            get
            {
                var names = new HashSet<string>();
                foreach (var m in objectMethods.Keys)
                    if (Lexer.IsIdentifier(m))
                        names.Add(m);
                return names;
            }
        }

        private Flags flags;

        private string tagName;

        /// <summary>Part-object with built-in default methods</summary>
        public static readonly GraceObject DefaultMethods = new GraceObject();

        private static Dictionary<string, Method> defaultExtensions =
            new Dictionary<string, Method>();

        private bool includeDefaults;

        /// <summary>
        /// Apply an extension trait to all future objects.
        /// </summary>
        /// <param name="meths">
        /// Dictionary of methods to add.
        /// </param>
        public static void ExtendDefaultMethods(
                IDictionary<string, Method> meths
                )
        {
            foreach (var m in meths)
                defaultExtensions[m.Key] = m.Value;
        }

        /// <summary>Name of this object for debugging</summary>
        /// <value>This property gets/sets the value of the field name</value>
        public string TagName
        {
            get
            {
                return tagName;
            }
            set
            {
                tagName = value;
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
            tagName = name;
        }

        /// <summary>An object with a provided method dictionary</summary>
        /// <param name="methodDict">
        /// Dictionary to be used as the actual methods of this object.
        /// </param>
        public GraceObject(Dictionary<string, Method> methodDict)
        {
            objectMethods = methodDict;
            initialise(true);
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
            tagName = name;
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
            includeDefaults = defaults;
            if (objectMethods != null)
                return;
            objectMethods = new Dictionary<string, Method>();
            if (defaults)
            {
                // The default methods are found on all objects,
                // but should not be defined on all part-objects.
                // User objects obtain them by inheritance, but
                // others will have them directly.
                AddMethod("asString", null);
                AddMethod("==(_)", null);
                AddMethod("!=(_)", null);
                AddMethod("hash", null);
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
            if (tagName != null)
                return string.Format("{0}[{1:X}]",
                        tagName,
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

        /// <summary>Native method supporting Grace hash</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver</param>
        private static GraceObject mHash(EvaluationContext ctx,
                GraceObject self)
        {
            return GraceNumber.Create(RuntimeHelpers.GetHashCode(self));
        }

        /// <summary>Remove a method from this object</summary>
        /// <param name="m">Method name to remove</param>
        public virtual void RemoveMethod(string m)
        {
            objectMethods.Remove(m);
        }

        /// <summary>Add a method to this object with a custom name</summary>
        /// <param name="name">Name to give this method</param>
        /// <param name="m">Method to add</param>
        public void AddMethod(string name, Method m)
        {
            objectMethods[name] = m;
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
                objectMethods[kvp.Key] = kvp.Value;
        }

        /// <summary>Get all method names in this object</summary>
        public List<string> MethodNames()
        {
            return new List<string>(objectMethods.Keys);
        }

        /// <summary>Determines whether this object can respond to
        /// the given request</summary>
        /// <param name="req">Request to check</param>
        /// <returns>True if this object can process the request;
        /// false otherwise</returns>
        public virtual bool RespondsTo(MethodRequest req)
        {
            if (objectMethods.ContainsKey(req.Name))
                return true;
            if (includeDefaults && defaultExtensions.ContainsKey(req.Name))
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
                case "==(_)":
                    m = new DelegateMethodReceiver1Ctx(mEqualsEquals);
                    return m;
                case "!=(_)":
                    m = new DelegateMethodReceiver1Ctx(mNotEquals);
                    return m;
                case "hash":
                    m = new DelegateMethodReceiver0Ctx(mHash);
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
            if (objectMethods.ContainsKey(name))
            {
                if (objectMethods[name] == null)
                    objectMethods[name] = getLazyMethod(name);
                return objectMethods[name];
            }
            if (includeDefaults && defaultExtensions.ContainsKey(name))
                return defaultExtensions[name];
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
            return (flags & f) == f;
        }

        /// <summary>The uninitialised variable object</summary>
        public static readonly GraceObject Uninitialised = new GraceObject();

        /// <summary>The singleton done object</summary>
        public static readonly GraceObject Done = new GraceObject("Done");

        /// <summary>The singleton uninherited parent object</summary>
        public static readonly GraceObject UninheritedParent =
            new GraceObject("ParentNotInheritedYet", true);
    }

}
