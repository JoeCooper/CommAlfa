using System;
using System.Collections;
using System.Collections.Generic;

namespace Server.UnitTests.Fodder
{
    public class BadIds: IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { "" };
            yield return new object[] { ";DELETE FROM document;" };
            yield return new object[] { "adsfjaj5k0ttv#52554gk5" };
            yield return new object[] { "fasdfasg" };
            yield return new object[] { "AGKji4jtijijt" };
            yield return new object[] { "29tk95i9354g9k59gk409gk0495kg" };
            yield return new object[] { null };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
