using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentitySample.ViewModels.Role;

namespace IdentitySample.Repositories
{
    public interface IUtilities
    {
        public IList<ActionAndControllerName> ActionAndControllerNamesList();
        public IList<string> GetAllAreasNames();
        public string DataBaseRoleValidationGuid();
    }
}
