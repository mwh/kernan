using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{

    /// <summary>Represents a single word of a request, including its
    /// arguments.</summary>
    public class RequestPart
    {
        /// <summary>Empty immutable list for unspecified arguments.</summary>
        public static readonly IList<GraceObject> EmptyList = new GraceObject[0];

        private string name;
        private IList<GraceObject> generics;
        private IList<GraceObject> arguments;

        /// <summary>Create a new part with empty arguments</summary>
        /// <param name="name">Name of this part</param>
        internal static RequestPart Nullary(string name)
        {
            return new RequestPart(name, new List<GraceObject>(),
                    new List<GraceObject>());
        }

        /// <summary>Create a new part with a single argument</summary>
        /// <param name="name">Name of this part</param>
        /// <param name="arg">Value of the lone argument</param>
        internal static RequestPart Single(string name, GraceObject arg)
        {
            return new RequestPart(name, EmptyList,
                    new List<GraceObject>() { arg });
        }

        /// <param name="name">Name of this part</param>
        /// <param name="generics">List of generic (type) arguments</param>
        /// <param name="arguments">List of ordinary arguments</param>
        internal RequestPart(string name, IList<GraceObject> generics,
                IList<GraceObject> arguments)
        {
            this.name = name;
            this.generics = generics;
            this.arguments = arguments;
        }

        /// <summary>The name of this part</summary>
        /// <value>This property gets the value of the string field name</value>
        public string Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>The generic arguments to this part</summary>
        /// <value>This property gets the value of the field generics</value>
        public IList<GraceObject> GenericArguments
        {
            get
            {
                return generics;
            }
        }

        /// <summary>The ordinary arguments to this part</summary>
        /// <value>This property gets the value of the field arguments</value>
        public IList<GraceObject> Arguments
        {
            get
            {
                return arguments;
            }
        }
    }

    /// <summary>An encapsulated method request</summary>
    public class MethodRequest : IEnumerable<RequestPart>
    {
        private string name = "";
        private bool nameComplete;
        private List<RequestPart> parts = new List<RequestPart>();

        /// <summary>Creates an empty method request</summary>
        public MethodRequest()
        {

        }

        /// <summary>
        /// Creates a method request with the full name preset.
        /// </summary>
        /// <param name="n">Full name of method</param>
        public MethodRequest(string n)
        {
            name = n;
            nameComplete = true;
        }

        /// <summary>Add a part to this request</summary>
        /// <param name="part">Request part to add to the request</param>
        /// <remarks>This method also updates the internal store of the
        /// overall method name.</remarks>
        public void AddPart(RequestPart part)
        {
            if (!nameComplete)
            {
                if (name != "")
                    name += " ";
                name += part.Name;
            }
            parts.Add(part);
        }

        /// <summary>Get a particular part of the request</summary>
        /// <param name="i">Zero-indexed part to retrieve</param>
        /// <value>This indexer accesses the field "parts", which
        /// is a List</value>
        public RequestPart this[int i]
        {
            get
            {
                return parts[i];
            }
        }

        /// <summary>Overall name of this method</summary>
        /// <remarks>Part names are separated by spaces.</remarks>
        /// <value>This property gets the value of the string field name</value>
        public string Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>Is this method request an interior (receiverless)
        /// request?</summary>
        public bool IsInterior { get; set; }

        /// <summary>Is this method request being inherited from?</summary>
        public bool IsInherits { get; set; }

        /// <summary>Part-object performing inheritance, or null</summary>
        public GraceObject InheritingObject { get; set; }

        /// <summary>Object identity during inheritance, or null</summary>
        public GraceObject InheritingSelf { get; set; }

        /// <summary>"as" name of inherits clause, or null</summary>
        public string InheritingName { get; set; }

        /// <summary>Get an enumerator giving each part of the request
        /// in turn</summary>
        public IEnumerator<RequestPart> GetEnumerator()
        {
            foreach (RequestPart p in parts)
            {
                yield return p;
            }
        }

        /// <summary>Get an enumerator giving each part of the request
        /// in turn</summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Create a method request with a single part and
        /// no arguments of any kind</summary>
        /// <param name="name">Name of method</param>
        public static MethodRequest Nullary(string name)
        {
            var ret = new MethodRequest();
            ret.AddPart(RequestPart.Nullary(name));
            return ret;
        }

        /// <summary>Create a method request with a single part and
        /// a lone ordinary argument</summary>
        /// <param name="name">Name of method</param>
        /// <param name="arg">Value of lone argument</param>
        public static MethodRequest Single(string name, GraceObject arg)
        {
            var ret = new MethodRequest();
            ret.AddPart(RequestPart.Single(name, arg));
            return ret;
        }
    }
}
