using System.Collections.Generic;
using System.IO;
using Grace.Parsing;
using Grace.Execution;
using Grace.Runtime;

namespace Grace.Platform.Mirrors
{
    [ModuleEntryPoint]
    public class PlatformMirrors : GraceObject
    {

        public static Dictionary<GraceObject, Dictionary<GraceObject, IList<GraceObject>>> MetadataTable = new Dictionary<GraceObject, Dictionary<GraceObject, IList<GraceObject>>>();

        public static GraceObject Instantiate(
            EvaluationContext ctx)
        {
            return new PlatformMirrors();
        }

        public PlatformMirrors() : base("platform/mirrors")
        {
            AddMethod("reflect(_)", new DelegateMethod1(mReflect));
            AddMethod("mutable(_)", new DelegateMethod1(mMutable));
        }

        private GraceObject mReflect(GraceObject arg)
        {
            return new Mirror(arg);
        }

        private GraceObject mMutable(GraceObject arg)
        {
            return new Mirror(arg);
        }

    }

    class GraceEqualityComparer : IEqualityComparer<GraceObject>
    {
        private EvaluationContext ctx;
        private MethodRequest hash = MethodRequest.Nullary("hash");

        public GraceEqualityComparer(EvaluationContext ctx)
        {
            this.ctx = ctx;
        }

        public bool Equals(GraceObject g1, GraceObject g2)
        {
            if (g1 == null && g2 == null)
                return true;
            if (g1 == null || g2 == null)
                return false;
            var req = MethodRequest.Single("==", g2);
            return GraceBoolean.IsTrue(ctx, g1.Request(ctx, req));
        }

        public int GetHashCode(GraceObject o) {
            if (o.RespondsTo(hash)) {
                return o.Request(ctx, hash)
                    .FindNativeParent<GraceNumber>().GetInt();
            }
            return 7;
        }
    }

    class Mirror : GraceObject
    {

        private GraceObject target;

        public Mirror(GraceObject o)
        {
            target = o;
            var respondsTo = new DelegateMethod1Ctx(mRespondsTo);
            AddMethod("respondsTo(_)", respondsTo);
            AddMethod("addMetadata(_,_)", new DelegateMethodReq(mAddMetadata));
            AddMethod("getMetadata(_)", new DelegateMethodReq(mGetMetadata));
        }

        private GraceObject mRespondsTo(EvaluationContext ctx,
                GraceObject name)
        {
            var n = name.FindNativeParent<GraceString>().Value;
            return GraceBoolean.Create(target.RespondsTo(
                        new MethodRequest(n)));
        }

        private GraceObject mAddMetadata(EvaluationContext ctx,
                MethodRequest req)
        {
            var key = req[0].Arguments[0];
            var val = req[0].Arguments[1];
            var mt = PlatformMirrors.MetadataTable;
            if (!mt.ContainsKey(target))
                mt.Add(target, new Dictionary<GraceObject, IList<GraceObject>>(new GraceEqualityComparer(ctx)));
            var table = mt[target];
            if (!table.ContainsKey(key))
                table[key] = new List<GraceObject>();
            table[key].Add(val);
            return GraceObject.Done;
        }


        private GraceObject mGetMetadata(EvaluationContext ctx,
                MethodRequest req)
        {
            var key = req[0].Arguments[0];
            var mt = PlatformMirrors.MetadataTable;
            if (!mt.ContainsKey(target))
                mt.Add(target, new Dictionary<GraceObject, IList<GraceObject>>(new GraceEqualityComparer(ctx)));
            var table = mt[target];
            if (!table.ContainsKey(key))
                table[key] = new List<GraceObject>();
            return new MetadataList(table[key]);
        }

    }

    internal class MetadataList : GraceObject
    {
        private IList<GraceObject> list;

        public MetadataList(IList<GraceObject> l)
        {
            list = l;
            var contains = new DelegateMethod1Ctx(mContains);
            AddMethod("contains(_)", contains);
            AddMethod("size", new DelegateMethod0(mSize));
        }

        private GraceObject mSize()
        {
            return GraceNumber.Create(list.Count);
        }

        private GraceObject mContains(EvaluationContext ctx, GraceObject needle)
        {
            var mr = MethodRequest.Single("==", needle);
            foreach (var o in list)
            {
                var r = o.Request(ctx, mr);
                if (GraceBoolean.IsTrue(ctx, r))
                    return r;
            }
            return GraceBoolean.False;
        }
    }
}

