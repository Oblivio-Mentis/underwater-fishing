using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace Raid
{
    // A confirmation identifies the exact fish and their real yields, not just a count.
    public static class FishRouteQuote
    {
        public static long Compute(IEnumerable<CatchItem> stock,int requiredFood)
        {
            using(var bytes=new MemoryStream())
            {
                using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
                {
                    writer.Write(requiredFood);
                    foreach(var fish in stock.Where(x=>x.offering>0).OrderBy(x=>x.id))
                    {
                        writer.Write(fish.id);writer.Write(fish.biomass);writer.Write(fish.chitin);
                        writer.Write(fish.offering);writer.Write(fish.scrap);writer.Write(fish.electronics);
                        writer.Write(fish.cargoSize);writer.Write(fish.depthTier);
                    }
                }
                using(var sha=SHA256.Create())
                {
                    long hash=System.BitConverter.ToInt64(sha.ComputeHash(bytes.ToArray()),0)&long.MaxValue;
                    return hash==0?1:hash;
                }
            }
        }
    }
}
