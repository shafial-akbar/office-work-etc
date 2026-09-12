using ETCAdminUI.Helper;
using ETCAdminUI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Security.Cryptography;

namespace ETCAdminUI.Controllers
{    
    public class AccountController : Controller
    {
        private readonly ConfigValue _ConfigValue;
        APIProcessHelper aPIProcessHelper;

        public AccountController(IOptions<ConfigValue> configValue)
        {
            _ConfigValue = configValue.Value;
            aPIProcessHelper = new APIProcessHelper(_ConfigValue);
        }
        public bool NotLogin()
        {
            return string.IsNullOrEmpty(HttpContext.Session.GetString("loginStatus"));
        }
        // GET: Account
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Login()
        {
            var json = HttpContext.Session.GetString("loginStatus");
            if (!string.IsNullOrEmpty(json))
            {
                return RedirectToAction("Index", "Home");
            }
            else return View();
        }

        [HttpPost]
        public ActionResult Login(LoginModel viewModel)
        {
            UserInfoModel objUserInfo = new UserInfoModel();
            if (ModelState.IsValid)
            {
                objUserInfo.UserId = viewModel.UserId;
                if (viewModel.Password != null)
                {
                    objUserInfo.UserId = viewModel.UserId.ToUpper();
                    objUserInfo.Password = EncryptionBySHA256(viewModel.Password.ToString());
                }
                else objUserInfo.Password = "";
                UserInfoModel daoUserInfo = new UserInfoModel();
                daoUserInfo = aPIProcessHelper.CheckLogin(objUserInfo);

                if (daoUserInfo.IsActive)
                {
                    HttpContext.Session.SetString("temploginStatus", JsonSerializer.Serialize(daoUserInfo));
                    RoleResponse roleResponse = aPIProcessHelper.GetRoleList(daoUserInfo);
                    if(roleResponse.role_list != null)
                    {
                        HttpContext.Session.SetString("roleList", JsonSerializer.Serialize(roleResponse.role_list));                        
                    }
                    return RedirectToAction("BranchCheck", "Account");
                }
                else if(daoUserInfo.IsPasswordExpire)
                {
                    HttpContext.Session.SetString("temploginStatus", JsonSerializer.Serialize(daoUserInfo));
                    return RedirectToAction("ForcePasswordChange", "Account");
                }
                else
                {
                    ViewBag.LoginStat = false;
                    ViewBag.Message = daoUserInfo.Message;
                }
            }
            return View();
        }

        public ActionResult BranchCheck()
        {
            return View();
        }
        [HttpPost]
        public ActionResult BranchCheck(UserInfoModel viewModel)
        {
            var json = HttpContext.Session.GetString("temploginStatus");

            UserInfoModel objUserInfo = new UserInfoModel();
            if (!string.IsNullOrEmpty(json))
            {
                objUserInfo = JsonSerializer.Deserialize<UserInfoModel>(json);
                if(objUserInfo.UserBRCode == viewModel.UserBRCode)
                {
                    HttpContext.Session.SetString("loginStatus", JsonSerializer.Serialize(objUserInfo));
                    HttpContext.Session.Remove("temploginStatus");

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.BranchStat = false;
                }
            }
            
            return View();
        }

        public ActionResult PasswordChange()
        {
            if (NotLogin())
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        public ActionResult PasswordChange(ChangePasswordModel objChangePasswordModel)
        {
            if (NotLogin())
            {
                return RedirectToAction("Login", "Account");
            }

            var json = HttpContext.Session.GetString("loginStatus");
            if (ModelState.IsValid)
            {
                UserInfoModel objUserInfo = new UserInfoModel();
                if (!string.IsNullOrEmpty(json))
                {
                    objUserInfo = JsonSerializer.Deserialize<UserInfoModel>(json);
                }
                if (objChangePasswordModel.NewPassword.Length < 8)
                {
                    ViewBag.ErrorMessage = "Password Length must be at least 8";
                }
                else
                {
                    if (!string.IsNullOrEmpty(objChangePasswordModel.OldPassword) && !string.IsNullOrEmpty(objChangePasswordModel.NewPassword)
                    && !string.IsNullOrEmpty(objChangePasswordModel.ConfirmPassword))
                    {
                        if (objChangePasswordModel.NewPassword == objChangePasswordModel.ConfirmPassword)
                        {
                            string encrypPass = EncryptionBySHA256(objChangePasswordModel.OldPassword);
                            if (objUserInfo.Password == encrypPass)
                            {
                                string newPass = EncryptionBySHA256(objChangePasswordModel.NewPassword);
                                string message_ = "";
                                bool stat = aPIProcessHelper.UpdatePassword(newPass, objUserInfo, out message_);
                                //objUserInfoDAL.FunPasswordChange(newPass, objUserInfo.UserId);
                                if (stat)
                                {
                                    objUserInfo.Password = newPass;
                                    return RedirectToAction("Index", "Home");
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(message_))
                                    {
                                        ViewBag.ErrorMessage = message_;
                                    }
                                    else
                                    {
                                        ViewBag.ErrorMessage = "Failed to update";
                                    }
                                }
                            }
                            else
                            {
                                ViewBag.ErrorMessage = "Old Password is incorrect";
                            }
                        }
                    }
                    else
                    {
                        ViewBag.ErrorMessage = "Please fill necessary data";
                    }
                }
            }
            return View();
        }

        public ActionResult ForcePasswordChange()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ForcePasswordChange(ChangePasswordModel objChangePasswordModel)
        {
            var json = HttpContext.Session.GetString("temploginStatus");
            UserInfoModel objUserInfo = new UserInfoModel();
            if (!string.IsNullOrEmpty(json))
            {
                objUserInfo = JsonSerializer.Deserialize<UserInfoModel>(json);
                objChangePasswordModel.OldPassword = objUserInfo.Password;
                ModelState["OldPassword"].Errors.Clear();

                if (ModelState.IsValid)
                {
                    if (!string.IsNullOrEmpty(objChangePasswordModel.OldPassword) && !string.IsNullOrEmpty(objChangePasswordModel.NewPassword)
                        && !string.IsNullOrEmpty(objChangePasswordModel.ConfirmPassword))
                    {
                        if (objChangePasswordModel.NewPassword == objChangePasswordModel.ConfirmPassword)
                        {
                            string newPass = EncryptionBySHA256(objChangePasswordModel.NewPassword);
                            string message_ = "";
                            bool stat = aPIProcessHelper.UpdatePassword(newPass, objUserInfo, out message_);
                            //objUserInfoDAL.FunPasswordChange(newPass, objUserInfo.UserId);
                            if (stat)
                            {
                                HttpContext.Session.Remove("temploginStatus");
                                return RedirectToAction("Login", "Account");
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(message_))
                                {
                                    ViewBag.ErrorMessage = message_;
                                }
                                else
                                {
                                    ViewBag.ErrorMessage = "Failed to update";
                                }
                            }
                        }
                    }
                }
                else
                {
                    ViewBag.ErrorMessage = "Please fill necessary data";
                }
            }
            else
            {
                ViewBag.ErrorMessage = "Invalid path";
            }           
            return View();
        }

        public ActionResult Logout()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("loginStatus")))
            {
                HttpContext.Session.Remove("loginStatus");
                HttpContext.Session.Remove("TemporaryData");
            }

            return RedirectToAction("", "");
        }

        public string EncryptionBySHA256(string password)
        {
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(password);
                using (SHA256Managed sha256 = new SHA256Managed())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    return string.Join("", hash.Select(b => b.ToString("x2")).ToArray());
                }
            }
            catch (Exception ex)
            {
                //logger.Error("EncryptionBySHA256-" + ex.ToString());
            }
            return string.Empty;
        }
    }
}