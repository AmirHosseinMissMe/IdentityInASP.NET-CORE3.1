using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentitySample.Security.PhoneTotp
{
    public class PhoneTotpOptions
    {
        public int StepInSeconds { get; set; } = 30;
    }
}
