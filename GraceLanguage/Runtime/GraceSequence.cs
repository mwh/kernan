using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;

namespace Grace.Runtime
{
   public class GraceSequence : GraceObject
   {
       /// <summary>
       /// Execute a block of native code for each element
       /// of an iterable.
       /// </summary>
       /// <param name="ctx">Interpreter to use</param>
       /// <param name="iterable">Iterable to loop over</param>
       /// <param name="block">
       /// Block of code to execute.
       /// </param>
       public static void ForEach(
               EvaluationContext ctx,
               GraceObject iterable,
               GraceObject block
               )
       {
           var req = MethodRequest.Single("do", block);
           iterable.Request(ctx, req);
       }

       private List<GraceObject> elements = new List<GraceObject>();

       /// <summary>Empty list</summary>
       public GraceSequence()
       {
           AddMethod("==(_)", Callback.Unary<GraceSequence, GraceObject>(mEquals));
           AddMethod("indexOf(_)", Callback.Unary<GraceSequence, GraceObject>(mIndexOf));
           AddMethod("do(_)", Callback.Unary<GraceSequence, GraceObject>(mDo));
           AddMethod("do(_) separatedBy(_)", Callback.UnaryUnary<GraceSequence, GraceObject, GraceObject>(mDoSeparatedBy));
           AddMethod("map(_)", Callback.Unary<GraceSequence, GraceObject>(mMap));
           AddMethod("++(_)", Callback.Unary<GraceSequence, GraceObject>(mConcat));
           AddMethod("with(_) do(_)", Callback.UnaryUnary<GraceSequence, GraceObject, GraceObject>(mWithDo));
           AddMethod("size", Callback.Nullary<GraceSequence>((ctx, self) => GraceNumber.Create(self.elements.Count)));
           AddMethod("sizeIfUnknown(_)", Callback.Unary<GraceSequence, GraceObject>((ctx, self, arg) => GraceNumber.Create(self.elements.Count)));
           AddMethod("first", Callback.Nullary<GraceSequence>((ctx, self) => self.elements.First()));
           AddMethod("last", Callback.Nullary<GraceSequence>((ctx, self) => self.elements.Last()));
           AddMethod("indices", Callback.Nullary<GraceSequence>((ctx, self) => Of(Enumerable.Range(1, self.elements.Count).Select((x) => GraceNumber.Create(x)))));
           AddMethod("hash", Callback.Nullary<GraceSequence>((ctx, self) =>
                   // This is the same algorithm as minigrace's standard grace
                   GraceNumber.Create(self.elements.Aggregate(0x5E0EACE, (result, next) => (result * 2) ^ next.GetHashCode(ctx)))));
           AddMethod("at(_)", Callback.Unary<GraceSequence, GraceNumber>(mAt));
           AddMethod("contains(_)", Callback.Unary<GraceSequence, GraceObject>((ctx, self, val) => GraceBoolean.Create(self.elements.Contains(val, new EqualityComparer(ctx)))));
           TagName = "Sequence";
       }
       // Mostly Copied from GraceString
       private static GraceObject mAt(EvaluationContext ctx, GraceSequence self, GraceNumber index)
       {
           int idx = index.GetInt() - 1;
           if (idx >= self.elements.Count || idx < 0)
               ErrorReporting.RaiseError(ctx, "R2013",
                       new Dictionary<string, string> {
                           { "index", "" + (idx + 1) },
                           { "valid", self.elements.Count > 0 ?
                               "1 .. " + self.elements.Count
                               : "none (empty)" }
                       }, "Index must be a number");
           return self.elements[idx];
       }

       /// <summary>
       /// List of particular items.
       /// </summary>
       /// <param name="items">Enumerable of items to use</param>
       public static GraceSequence Of(IEnumerable<GraceObject> items)
       {
           var ret = new GraceSequence();
           foreach (var it in items)
               ret.Add(it);
           return ret;
       }

       /// <summary>Add an object to the list</summary>
       /// <param name="obj">Object to add</param>
       public void Add(GraceObject obj)
       {
           elements.Add(obj);
       }

       /// <summary>Native method for Grace ++</summary>
       /// <param name="ctx">Current interpreter</param>
       /// <param name="other">Second iterable to concatenate</param>
       public static GraceObject mConcat(EvaluationContext ctx, GraceSequence self, GraceObject other)
       {
           var res = new GraceSequence();
           res.elements = new List<GraceObject>(self.elements);
           // TODO: Something more efficient?
           ForEach(ctx, other, GraceBlock.Create((elem) => res.elements.Add(elem)));
           return res;
       }

       /// <summary>Native method for Grace do</summary>
       /// <param name="ctx">Current interpreter</param>
       /// <param name="block">Block to apply for each element</param>
       public static GraceObject mDo(EvaluationContext ctx, GraceSequence self, GraceObject block)
       {
           var req = MethodRequest.Single("apply", null);
           foreach (var o in self.elements)
           {
               req[0].Arguments[0] = o;
               block.Request(ctx, req);
           }
           return GraceObject.Done;
       }

       /// <summary>Native method for Grace do separatedBy</summary>
       /// <param name="ctx">Current interpreter</param>
       /// <param name="block">Block to apply for each element</param>
       /// <param name="sep_block">Block to apply between each element</param>
       public static GraceObject mDoSeparatedBy(EvaluationContext ctx, GraceSequence self, GraceObject block, GraceObject sep_block)
       {
           var req = MethodRequest.Single("apply", null);
           var sep_req = MethodRequest.Nullary("apply");
	   bool first = true;
           foreach (var o in self.elements)
           {
               if (first) { first = false; }
               else { sep_block.Request(ctx, sep_req); }
               req[0].Arguments[0] = o;
               block.Request(ctx, req);
           }
           return GraceObject.Done;
       }

       /// <summary>Native method for Grace map (I'm too lazy to make this lazy like minigrace's)</summary>
       /// <param name="ctx">Current interpreter</param>
       /// <param name="block">Block to apply for each element</param>
       public static GraceObject mMap(EvaluationContext ctx, GraceSequence self, GraceObject block)
       {
           var req = MethodRequest.Single("apply", null);
           var res = new GraceSequence();
           res.elements.Capacity = self.elements.Count; // For efficiency
           foreach (var o in self.elements)
           {
               req[0].Arguments[0] = o;
               res.elements.Add(block.Request(ctx, req));
           }
           return res;
       }


       /// <summary>Native method for Grace equals</summary>
       /// <param name="ctx">Current interpreter</param>
       /// <param name="other">Second iterable to compare </param>
       public static GraceObject mEquals(EvaluationContext ctx, GraceSequence self, GraceObject other)
       {
           if (!other.RespondsTo(MethodRequest.Single("do", null)))
               return GraceBoolean.False;
           var others = new List<GraceObject>(); // Compute each element of other
           ForEach(ctx, other, GraceBlock.Create((elem) => others.Add(elem)));
           if (self.elements.Count != others.Count)
               return GraceBoolean.False;

           // Is element-wise equality correct? Or should a sequence only equal another sequence (and not say a mutable list)
           for (int i = 0; i < self.elements.Count; i++)
               if (!self.elements[i].Equals(ctx, others[i]))
                   return GraceBoolean.False;
           return GraceBoolean.True;
       }


       /// <summary>Native method for Grace indexOf</summary>
       /// <param name="ctx">Current interpreter</param>
       /// <param name="other">Element to find </param>
       public static GraceObject mIndexOf(EvaluationContext ctx, GraceSequence self, GraceObject other)
       {
           // Is element-wise equality correct? Or should a sequence only equal another sequence (and not say a mutable list)
           for (int i = 0; i < self.elements.Count; i++)
               if (self.elements[i].Equals(ctx, other))
                   return GraceNumber.Create(i + 1);
	   return GraceNumber.Create(0); // Not found
       }

       private static GraceObject mWithDo(EvaluationContext ctx, GraceSequence self, GraceObject with, GraceObject block)
       {
           var withBlock = new WithBlock(self.elements, block);
           var innerReq = MethodRequest.Single("do", withBlock);
           with.Request(ctx, innerReq);
           return GraceObject.Done;
       }

       private class WithBlock : GraceObject
       {
           private List<GraceObject> _elements;
           private GraceObject _block;
           private int index;

           public WithBlock(List<GraceObject> elements,
                   GraceObject block)
           {
               _elements = elements;
               _block = block;
               AddMethod("apply(_)",
                   new DelegateMethod1Ctx(
                       new NativeMethod1Ctx(this.apply)));
           }

           private GraceObject apply(EvaluationContext ctx,
                   GraceObject arg)
           {
               if (index >= _elements.Count)
                   return GraceObject.Done;
               var el = _elements[index++];
               var req = new MethodRequest();
               var rpn = new RequestPart("apply",
                   new List<GraceObject>(),
                   new List<GraceObject>() {
                       el, arg
                   }
               );
               req.AddPart(rpn);
               _block.Request(ctx, req);
               return GraceObject.Done;
           }
       }
   }
}
