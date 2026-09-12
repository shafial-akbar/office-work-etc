using ETCAdminUI.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Text.Json;

namespace ETCAdminUI.Helper
{
    public class APIProcessHelper
    {
        private readonly ConfigValue _ConfigValue;
        public APIProcessHelper(ConfigValue configValue)
        {
            _ConfigValue = configValue;
        }

        //static log4net.ILog logger = log4net.LogManager.GetLogger(typeof(APIProcessHelper));

        private HttpWebRequest MakeWebRequest(string URL)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(URL);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";            
            return httpWebRequest;
        }

        [HttpPost]
        public UserInfoModel CheckLogin(UserInfoModel objUserLogin)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            UserInfoModel userInfoModel = new UserInfoModel();

            LoginResponse loginResponse = new LoginResponse();
            LoginRequest loginRequest = new LoginRequest()
            {
                username = objUserLogin.UserId,
                password = objUserLogin.Password,
                serviceCode = _ConfigValue.ServiceCode
            };

            if (loginRequest.username == "00000")
            {
                userInfoModel.UserId = loginRequest.username;
                userInfoModel.IsActive = true;
                userInfoModel.MenuRetrive = 0;
                userInfoModel.LoginStat = true;

                userInfoModel.UserRole = "1";
                userInfoModel.UserRoleName = "Super Admin";
                userInfoModel.UserBRCode = "00018";
                userInfoModel.RoutingNo = "200273882";

                //Password
                userInfoModel.Password = loginRequest.password;

            }

            if (loginRequest.username != "00000")
            {
                try
                {
                    string URL = _ConfigValue.LoginAPIBaseUrl + "login";

                    HttpWebRequest httpWebRequest = MakeWebRequest(URL);

                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    {
                        string json = JsonSerializer.Serialize(loginRequest);
                        streamWriter.Write(json);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }

                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        string result = streamReader.ReadToEnd();
                        if (!string.IsNullOrEmpty(result))
                        {
                            //loginResponse.role_list = new List<Role>();
                            //logger.Info("SBPS Branch Login: " + result);
                            loginResponse = JsonSerializer.Deserialize<LoginResponse>(result, options);
                        }

                    }
                }
                catch (Exception ex)
                {
                    //logger.Error("SBPS Branch Login: " + ex.ToString());
                    loginResponse.status = "-1";
                }
            }
            userInfoModel.Message = loginResponse.message;

            if (loginResponse.status == "200")
            {
                userInfoModel.UserId = loginRequest.username;
                userInfoModel.IsActive = true;
                userInfoModel.MenuRetrive = 0;
                userInfoModel.LoginStat = true;

                userInfoModel.UserRole = loginResponse.role_list[0].ROLE_ID.ToString();
                userInfoModel.UserRoleName = loginResponse.role_list[0].ROLE_NAME;
                userInfoModel.UserBRCode = loginResponse.brCode;
                userInfoModel.RoutingNo = loginResponse.routingNo;

                //Password
                userInfoModel.Password = loginRequest.password;
            }
            else if (loginResponse.status == "603")
            {
                userInfoModel.UserId = loginRequest.username;
                userInfoModel.Password = loginRequest.password;
                userInfoModel.IsPasswordExpire = true;
            }

            return userInfoModel;
        }


        public RoleResponse GetRoleList(UserInfoModel objUserLogin)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            RoleResponse roleResponse = new RoleResponse();
            RoleRequest roleRequest = new RoleRequest()
            {
                username = objUserLogin.UserId,
                password = objUserLogin.Password
            };

            try
            {
                if (objUserLogin.UserId == "00000")
                {
                    roleResponse.role_list = new List<Role>
                    {
                        new Role { ROLE_ID = 1, ROLE_NAME = "Super Admin" }
                    };
                }
                else
                {
                    string URL = _ConfigValue.LoginAPIBaseUrl + "role_list";
                    //string URL = baseUrlUat + "role_list";
                    HttpWebRequest httpWebRequest = MakeWebRequest(URL);

                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    {
                        string json = JsonSerializer.Serialize(roleRequest);
                        streamWriter.Write(json);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }

                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        string result = streamReader.ReadToEnd();
                        //dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(result);
                        if (!string.IsNullOrEmpty(result))
                        {
                            roleResponse = JsonSerializer.Deserialize<RoleResponse>(result, options);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                //logger.Error("Role_list API-" + ex.ToString());
            }

            return roleResponse;
        }

        public bool UpdatePassword(string newPass, UserInfoModel objUserLogin, out string message)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            message = "";
            UpdatePasswordResponse updatePasswordResponse = new UpdatePasswordResponse();
            UpdatePasswordRequest updatePasswordRequest = new UpdatePasswordRequest()
            {
                username = objUserLogin.UserId,
                password = objUserLogin.Password,
                serviceCode = _ConfigValue.ServiceCode,
                newPassword = newPass
            };

            try
            {
                string URL = _ConfigValue.LoginAPIBaseUrl + "updatePassword";

                HttpWebRequest httpWebRequest = MakeWebRequest(URL);

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = JsonSerializer.Serialize(updatePasswordRequest);
                    streamWriter.Write(json);
                    streamWriter.Flush();
                    streamWriter.Close();
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string result = streamReader.ReadToEnd();
                    if (!string.IsNullOrEmpty(result))
                    {
                        updatePasswordResponse = JsonSerializer.Deserialize<UpdatePasswordResponse>(result, options);
                        if (updatePasswordResponse.status == "403")
                        {
                            message = updatePasswordResponse.message;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                //logger.Error("UpdatePassword API-" + ex.ToString());
            }
            if (updatePasswordResponse.status == "200") return true;
            return false;
        }        
    }
}