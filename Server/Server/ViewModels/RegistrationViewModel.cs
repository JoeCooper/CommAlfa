using Server.Models;
using System.Collections.Generic;
using System.Linq;

namespace Server.ViewModels
{
    public class RegistrationViewModel
    {
        public RegistrationViewModel() : this(Enumerable.Empty<RegistrationFailureReasons>())
        {
        }
        
        public RegistrationViewModel(IEnumerable<RegistrationFailureReasons> reasons)
        {
            Reasons = reasons;
        }

        public IEnumerable<RegistrationFailureReasons> Reasons { get; }

        public bool Failed { get => Reasons.Any(); }
    }
}
