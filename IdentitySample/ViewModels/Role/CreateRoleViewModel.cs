using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace IdentitySample.ViewModels.Role
{
    public class CreateRoleViewModel
    {
        public CreateRoleViewModel()
        {
            ActionAndControllerNames = new List<ActionAndControllerName>();
        }

        [Required()]
        [Display(Name = "نام مقام")]
        public string RoleName { get; set; }
        public IList<ActionAndControllerName> ActionAndControllerNames { get; set; }
    }

    public class ActionAndControllerName
    {
        public string AreaName { get; set; }
        public string ActionName { get; set; }
        public string ControllerName { get; set; }
        public bool IsSelected { get; set; }
    }
}
