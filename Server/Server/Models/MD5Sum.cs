using System;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Server.Utilities;

namespace Server.Models
{
	[JsonConverter(typeof(IdentifierConverter))]
    public class MD5Sum
    {
        readonly byte[] body;
        readonly int hashCode;

        public MD5Sum(byte[] body)
        {
            System.Diagnostics.Debug.Assert(body.Length == 16);
            this.body = body;
            this.hashCode = ComputeHash(body);
        }

        public Guid ToGuid() {
            return new Guid(body);
        }

        public override int GetHashCode()
        {
            return hashCode;
        }

        public override string ToString()
        {
            return WebEncoders.Base64UrlEncode(body);
        }

        public override bool Equals(object obj)
        {
            if (obj is MD5Sum)
            {
                var other = (MD5Sum)obj;
                return body.SequenceEqual(other.body);
            }
            if(obj is byte[]) 
            {
                var other = (byte[])obj;
                return body.SequenceEqual(other);
            }
            return false;
        }

        //https://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c/16381
        public static int ComputeHash(byte[] data)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < data.Length; i++)
                    hash = (hash ^ data[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
    }
}
