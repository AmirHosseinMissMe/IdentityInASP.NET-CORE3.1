using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentitySample.Models;
using IdentitySample.Models.Context;
using IdentitySample.ViewModels.SiteSetting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace IdentitySample.Controllers
{
    public class SiteSettingController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly IMemoryCache _memoryCache;

        public SiteSettingController(AppDbContext dbContext, IMemoryCache memoryCache)
        {
            _dbContext = dbContext;
            _memoryCache = memoryCache;
        }


        public IActionResult Index()
        {
            var model = _dbContext.SiteSettings.ToList();
            return View(model);
        }

        [HttpGet]
        public IActionResult RoleValidationGuid()
        {
            var roleValidationGuidSiteSetting =
                _dbContext.SiteSettings.FirstOrDefault(t => t.Key == "RoleValidationGuid");

            var model = new RoleValidationGuidViewModel()
            {
                Value = roleValidationGuidSiteSetting?.Value,
                LastTimeChanged = roleValidationGuidSiteSetting?.LastTimeChanged
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult RoleValidationGuid(RoleValidationGuidViewModel model)
        {
            var roleValidationGuidSiteSetting =
                _dbContext.SiteSettings.FirstOrDefault(t => t.Key == "RoleValidationGuid");

            if (roleValidationGuidSiteSetting == null)
            {
                _dbContext.SiteSettings.Add(new SiteSetting()
                {
                    Key = "RoleValidationGuid",
                    Value = Guid.NewGuid().ToString(),
                    LastTimeChanged = DateTime.Now
                });
            }
            else
            {
                roleValidationGuidSiteSetting.Value = Guid.NewGuid().ToString();
                roleValidationGuidSiteSetting.LastTimeChanged = DateTime.Now;
                _dbContext.Update(roleValidationGuidSiteSetting);
            }
            _dbContext.SaveChanges();
            _memoryCache.Remove("RoleValidationGuid");

            return RedirectToAction("Index");
        }

    }
}