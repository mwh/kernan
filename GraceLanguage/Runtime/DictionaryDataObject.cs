using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Grace.Execution;
using Grace.Parsing;

namespace Grace.Runtime
{
    class DictionaryDataObject : GraceObject,
        IEnumerable<KeyValuePair<string, GraceObject>>
    {
        private Dictionary<string, GraceObject> data;

        public DictionaryDataObject() {
            data = new Dictionary<string, GraceObject>();
        }

        public DictionaryDataObject(Dictionary<string, GraceObject> items)
        {
            data = items;
        }

        public void Add(string key, GraceObject val)
        {
            data.Add(key, val);
        }

        public override GraceObject Request(EvaluationContext ctx,
                MethodRequest req)
        {
            if (data.ContainsKey(req.Name))
                return data[req.Name];
            return base.Request(ctx, req);
        }

        public IEnumerator<KeyValuePair<string, GraceObject>> GetEnumerator()
        {
            return data.GetEnumerator();
        }

        System.Collections.IEnumerator
            System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
