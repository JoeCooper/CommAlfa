using System;
using System.Collections;
using System.Collections.Generic;
using Server.Models;

namespace Server.UnitTests.Fodder
{
    public class AccountMetadataData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] {new AccountMetadata(
                    new Guid("652724b0-7a6c-4f90-8d78-822ecd788558"),
                    "Alfa",
                    "virginia.woolf@authors.com")};
            yield return new object[] {new AccountMetadata(
                new Guid("57b866b5-8495-40da-92f9-a00bd2ed002a"),
                    "Bravo",
                    "oscar.wilde@authors.com")};
            yield return new object[] {new AccountMetadata(
                new Guid("d1dad60f-4d1d-41ef-aa7e-134ab3cb09fd"),
                    "Charlie",
                    "aristotle@authors.com")};
            yield return new object[] {new AccountMetadata(
                new Guid("3942fe99-0fab-44d8-ac2d-6ad88602bc5d"),
                    "Delta",
                    "scott.adams@authors.com")};
            yield return new object[] {new AccountMetadata(
                new Guid("f63eba74-c7c5-4e50-9cac-b2a287081dd7"),
                    "Echo",
                    "hotel@ibsen.com")};
            yield return new object[] {new AccountMetadata(
                new Guid("e209af2c-1005-4f8b-a216-304177f7d4ce"),
                    "Foxtrot",
                    "echo@foxtrot.com")};
            yield return new object[] {new AccountMetadata(
                new Guid("b3f0289a-b040-4130-999f-f5f698de761f"),
                    "Hotel",
                    "charlie@delta.com")};
            yield return new object[] {new AccountMetadata(
                new Guid("5e3d402c-97e8-4294-a52e-3bdf12aba4e8"),
                    "Ibsen",
                    "alfa@bravo.com")};
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
