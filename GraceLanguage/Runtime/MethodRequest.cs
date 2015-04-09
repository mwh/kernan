using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grace.Execution;

namespace Grace.Runtime
{
    public class RequestPart
    {
        private string name;
        private List<GraceObject> generics;
        private List<GraceObject> arguments;

        internal static RequestPart Nullary(string name)
        {
            return new RequestPart(name, new List<GraceObject>(),
                    new List<GraceObject>());
        }

        internal static RequestPart Single(string name, GraceObject arg)
        {
            return new RequestPart(name, new List<GraceObject>(),
                    new List<GraceObject>() { arg });
        }

        internal RequestPart(string name, List<GraceObject> generics, List<GraceObject> arguments)
        {
            this.name = name;
            this.generics = generics;
            this.arguments = arguments;
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public List<GraceObject> GenericArguments
        {
            get
            {
                return generics;
            }
        }

        public List<GraceObject> Arguments
        {
            get
            {
                return arguments;
            }
        }
    }

    public class MethodRequest : IEnumerable<RequestPart>
    {
        private string name = "";
        private List<RequestPart> parts = new List<RequestPart>();

        public MethodRequest()
        {

        }

        public void AddPart(RequestPart part)
        {
            if (name != "")
                name += " ";
            name += part.Name;
            parts.Add(part);
            Interpreter.Debug("Added part to name. Name now " + name);
        }

        public RequestPart this[int i]
        {
            get
            {
                return parts[i];
            }
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public bool IsInterior { get; set; }

        public IEnumerator<RequestPart> GetEnumerator()
        {
            foreach (RequestPart p in parts)
            {
                yield return p;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static MethodRequest Nullary(string name)
        {
            var ret = new MethodRequest();
            ret.AddPart(RequestPart.Nullary(name));
            return ret;
        }

        public static MethodRequest Single(string name, GraceObject arg)
        {
            var ret = new MethodRequest();
            ret.AddPart(RequestPart.Single(name, arg));
            return ret;
        }
    }
}
