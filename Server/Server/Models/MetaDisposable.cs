using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Server.Models
{
    public class MetaDisposable: IDisposable
    {
        readonly IEnumerable<IDisposable> disposables;

        public MetaDisposable(params IDisposable[] disposables)
        {
            this.disposables = ImmutableArray.Create(disposables);
        }

        public MetaDisposable(IEnumerable<IDisposable> disposables)
        {
            this.disposables = ImmutableArray.CreateRange(disposables);
        }

        public void Dispose()
        {
            foreach(var disposable in disposables){
                disposable.Dispose();
            }
        }
    }
}
