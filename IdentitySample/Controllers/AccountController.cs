using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using IdentitySample.Models;
using IdentitySample.Repositories;
using IdentitySample.Security.PhoneTotp;
using IdentitySample.Security.PhoneTotp.Providers;
using IdentitySample.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentitySample.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IMessageSender _messageSender;
        private readonly IPhoneTotpProvider _phoneTotpProvider;
        private readonly PhoneTotpOptions _phoneTotpOptions;

        public AccountController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, IMessageSender messageSender, IPhoneTotpProvider phoneTotpProvider,
            IOptions<PhoneTotpOptions> phoneTotpOptions)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _messageSender = messageSender;
            _phoneTotpProvider = phoneTotpProvider;
            _phoneTotpOptions = phoneTotpOptions?.Value ?? new PhoneTotpOptions();
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser()
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    City = "Tehran"
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    var emailConfirmationToken =
                        await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var emailMessage =
                        Url.Action("ConfirmEmail", "Account",
                            new { username = user.UserName, token = emailConfirmationToken },
                            Request.Scheme);
                    await _messageSender.SendEmailAsync(model.Email, "Email confirmation", emailMessage);

                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Index", "Home");

            var model = new LoginViewModel()
            {
                ReturnUrl = returnUrl,
                ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
            };

            ViewData["returnUrl"] = returnUrl;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Index", "Home");

            model.ReturnUrl = returnUrl;
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ViewData["returnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                //var result = await _signInManager.PasswordSignInAsync(
                //    model.UserName, model.Password, model.RememberMe, true);

                var user = await _userManager.FindByNameAsync(model.UserName);
                if (user == null)
                {
                    ModelState.AddModelError("", "رمزعبور یا نام کاربری اشتباه است");
                    return View(model);
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, true);
                if (result.Succeeded)
                {
                    await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, new List<Claim>()
                    {
                        new Claim("UserCity",user.City ?? "")
                    });
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    ViewData["ErrorMessage"] = "اکانت شما به دلیل پنج بار ورود ناموفق به مدت پنج دقیقه قفل شده است";
                    return View(model);
                }

                ModelState.AddModelError("", "رمزعبور یا نام کاربری اشتباه است");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOut()
        {
            // HttpContext.Response.Cookies.Delete("RVG");
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IsEmailInUse(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return Json(true);
            return Json("ایمیل وارد شده از قبل موجود است");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IsUserNameInUse(string userName)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return Json(true);
            return Json("نام کاربری وارد شده از قبل موجود است");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userName, string token)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(token))
                return NotFound();
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return NotFound();
            var result = await _userManager.ConfirmEmailAsync(user, token);

            return Content(result.Succeeded ? "Email Confirmed" : "Email Not Confirmed");
        }

        [HttpPost]
        public IActionResult ExternalLogin(string provider, string returnUrl)
        {
            var redirectUrl = Url.Action("ExternalLoginCallBack", "Account",
                new { ReturnUrl = returnUrl });

            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallBack(string returnUrl = null, string remoteError = null)
        {
            ViewData["returnUrl"] = returnUrl;
            returnUrl = (returnUrl != null && Url.IsLocalUrl(returnUrl)) ? returnUrl : Url.Content("~/");

            var loginViewModel = new LoginViewModel()
            {
                ReturnUrl = returnUrl,
                ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
            };

            if (remoteError != null)
            {
                ModelState.AddModelError("", $"Error : {remoteError}");
                return View("Login", loginViewModel);
            }

            var externalLoginInfo = await _signInManager.GetExternalLoginInfoAsync();
            if (externalLoginInfo == null)
            {
                ModelState.AddModelError("ErrorLoadingExternalLoginInfo", $"مشکلی پیش آمد");
                return View("Login", loginViewModel);
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(externalLoginInfo.LoginProvider,
                externalLoginInfo.ProviderKey, false, true);

            if (signInResult.Succeeded)
            {
                return Redirect(returnUrl);
            }

            var email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email);

            if (email != null)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null) return View();

                await _userManager.AddLoginAsync(user, externalLoginInfo);
                await _signInManager.SignInAsync(user, false);

                return Redirect(returnUrl);
            }

            ViewData["ErrorMessage"] = $"دریافت کرد {externalLoginInfo.LoginProvider} نمیتوان اطلاعاتی از";
            return View("Login", loginViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ExternalLoginCallBack(ExternalLoginCallBackViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var loginViewModel = new LoginViewModel()
                {
                    ReturnUrl = returnUrl,
                    ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
                };

                var externalLoginInfo = await _signInManager.GetExternalLoginInfoAsync();
                if (externalLoginInfo?.Principal.FindFirstValue(ClaimTypes.Email) == null)
                {
                    ModelState.AddModelError("ErrorLoadingExternalLoginInfo", $"مشکلی پیش آمد");
                    return View("Login", loginViewModel);
                }

                var email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email);

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    IdentityResult result;
                    user = new ApplicationUser()
                    {
                        Email = email,
                        UserName = model.UserName,
                        EmailConfirmed = true
                    };

                    if (!string.IsNullOrWhiteSpace(model.Password))
                        result = await _userManager.CreateAsync(user, model.Password);
                    else
                        result = await _userManager.CreateAsync(user);


                    if (result.Succeeded)
                    {
                        await _userManager.AddLoginAsync(user, externalLoginInfo);
                        await _signInManager.SignInAsync(user, false);

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                            return Redirect(returnUrl);

                        return RedirectToAction("Index", "Home");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError("", $"مشکلی پیش آمد");
                    return View("Login", loginViewModel);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var loginViewModel = new LoginViewModel()
                {
                    ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
                };

                ViewData["ErrorMessage"] = "اگر ایمیل وارد معتبر باشد، لینک فراموشی رمزعبور به ایمیل شما ارسال شد";

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null) return View("Login", loginViewModel);

                var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetPasswordUrl = Url.Action("ResetPassword", "Account",
                    new { email = user.Email, token = resetPasswordToken }, Request.Scheme);

                // await _messageSender.SendEmailAsync(user.Email, "reset password link", resetPasswordUrl);

                return View("Login", loginViewModel);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction("Index", "Home");

            var model = new ResetPasswordViewModel()
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var loginViewModel = new LoginViewModel()
                {
                    ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
                };

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null) return RedirectToAction("Login", loginViewModel);
                var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
                if (result.Succeeded)
                {
                    ViewData["ErrorMessage"] = "رمزعبور شما با موفقیت تغییر یافت";
                    return View("Login", loginViewModel);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult SendTotpCode()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            if (TempData.ContainsKey("PTC"))
            {
                var totpTempDataModel = JsonSerializer.Deserialize<PhoneTotpTempDataModel>(TempData["PTC"].ToString()!);
                if (totpTempDataModel.ExpirationTime >= DateTime.Now)
                {
                    var differenceInSeconds = (int)(totpTempDataModel.ExpirationTime - DateTime.Now).TotalSeconds;
                    ModelState.AddModelError("", $"برای ارسال دوباره کد، لطفا {differenceInSeconds} ثانیه صبر کنید.");
                    TempData.Keep("PTC");
                    return View();
                }
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendTotpCode(SendTotpCodeViewModel model)
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            if (ModelState.IsValid)
            {
                if (TempData.ContainsKey("PTC"))
                {
                    var totpTempDataModel = JsonSerializer.Deserialize<PhoneTotpTempDataModel>(TempData["PTC"].ToString()!);
                    if (totpTempDataModel.ExpirationTime >= DateTime.Now)
                    {
                        var differenceInSeconds = (int)(totpTempDataModel.ExpirationTime - DateTime.Now).TotalSeconds;
                        ModelState.AddModelError("", $"برای ارسال دوباره کد، لطفا {differenceInSeconds} ثانیه صبر کنید.");
                        TempData.Keep("PTC");
                        return View();
                    }
                }

                var secretKey = Guid.NewGuid().ToString();
                var totpCode = _phoneTotpProvider.GenerateTotp(secretKey);

                var userExists = await _userManager.Users
                    .AnyAsync(user => user.PhoneNumber == model.PhoneNumber);
                if (userExists)
                {
                    //TODO send totpCode to user.
                }

                TempData["PTC"] = JsonSerializer.Serialize(new PhoneTotpTempDataModel()
                {
                    SecretKey = secretKey,
                    PhoneNumber = model.PhoneNumber,
                    ExpirationTime = DateTime.Now.AddSeconds(_phoneTotpOptions.StepInSeconds)
                });

                //RedirectToAction("VerifyTotpCode");
                return Content(totpCode);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyTotpCode()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            if (!TempData.ContainsKey("PTC")) return NotFound();

            var totpTempDataModel = JsonSerializer.Deserialize<PhoneTotpTempDataModel>(TempData["PTC"].ToString()!);
            if (totpTempDataModel.ExpirationTime <= DateTime.Now)
            {
                TempData["SendTotpCodeErrorMessage"] = "کد ارسال شده منقضی شده، لطفا کد جدیدی دریافت کنید.";
                return RedirectToAction("SendTotpCode");
            }

            TempData.Keep("PTC");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyTotpCode(VerifyTotpCodeViewModel model)
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            if (!TempData.ContainsKey("PTC")) return NotFound();
            if (ModelState.IsValid)
            {
                var totpTempDataModel = JsonSerializer.Deserialize<PhoneTotpTempDataModel>(TempData["PTC"].ToString()!);
                if (totpTempDataModel.ExpirationTime <= DateTime.Now)
                {
                    TempData["SendTotpCodeErrorMessage"] = "کد ارسال شده منقضی شده، لطفا کد جدیدی دریافت کنید.";
                    return RedirectToAction("SendTotpCode");
                }

                var user = await _userManager.Users
                    .Where(u => u.PhoneNumber == totpTempDataModel.PhoneNumber)
                    .FirstOrDefaultAsync();

                var result = _phoneTotpProvider.VerifyTotp(totpTempDataModel.SecretKey, model.TotpCode);
                if (result.Succeeded)
                {
                    if (user == null)
                    {
                        TempData["SendTotpCodeErrorMessage"] = "کاربری با شماره موبایل وارد شده یافت نشد.";
                        return RedirectToAction("SendTotpCode");
                    }

                    if (!user.PhoneNumberConfirmed)
                    {
                        TempData["SendTotpCodeErrorMessage"] = "شماره موبایل شما تایید نشده است.";
                        return RedirectToAction("SendTotpCode");
                    }

                    if (!await _userManager.IsLockedOutAsync(user))
                    {
                        await _userManager.ResetAccessFailedCountAsync(user);
                        await _signInManager.SignInWithClaimsAsync(user, false, new List<Claim>()
                        {
                            new Claim("UserCity",user.City ?? "")
                        });

                        return RedirectToAction("Index", "Home");
                    }

                    TempData["SendTotpCodeErrorMessage"] = "اکانت شما به دلیل ورود ناموفق تا مدت زمان معینی قفل شده است.";
                    return RedirectToAction("SendTotpCode");
                }

                if (user != null && user.PhoneNumberConfirmed && !await _userManager.IsLockedOutAsync(user))
                {
                    await _userManager.AccessFailedAsync(user);
                }

                TempData["SendTotpCodeErrorMessage"] = "کد ارسال شده معتبر نمی باشد، لطفا کد جدیدی دریافت کنید.";
                return RedirectToAction("SendTotpCode");
            }

            return View(model);
        }
    }
}