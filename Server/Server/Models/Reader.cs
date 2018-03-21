using System;
using System.Threading.Tasks;
using System.Data.Common;

namespace Server.Models
{
    public class Reader<TResult>: IDisposable
    {
        public delegate Task<bool> AdvancementFunction();
        public delegate TResult ReadFunction();

        readonly IDisposable disposableDependency;
        readonly ReadFunction readFunction;
        readonly AdvancementFunction advancementFunction;

        public Reader(IDisposable disposableDependency, AdvancementFunction advancementFunction, ReadFunction readFunction)
        {
            this.advancementFunction = advancementFunction;
            this.disposableDependency = disposableDependency;
            this.readFunction = readFunction;
        }

        public void Dispose()
        {
            disposableDependency?.Dispose();
        }

        public async Task<bool> MoveNextAsync() {
            return await advancementFunction();
        }

        public TResult Current { get => readFunction(); }
    }
}
