using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Server.Models
{
    public class Relation
    {
        readonly int hashCode;
        
        public Relation(byte[] antecedentId, byte[] descendantId)
        {
            AntecedentId = antecedentId;
            DescendantId = descendantId;
            hashCode = antecedentId.GetHashCode() ^ descendantId.GetHashCode();
        }

        public byte[] AntecedentId { get; }
        public byte[] DescendantId { get; }

        public override int GetHashCode()
        {
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            if(obj is Relation)
            {
                var other = (Relation)obj;
                return AntecedentId.Equals(other.AntecedentId) && DescendantId.Equals(other.DescendantId);
            }
            return false;
        }
    }
}
