namespace ETCAdminUI.Models
{
    public class ConfigValue
    {
        public string ServiceCode { get; set; }
        public string LoginAPIBaseUrl { get; set; }
    }
    public class LoginRequest
    {
        public string username { get; set; }
        public string password { get; set; }
        public string serviceCode { get; set; }
    }

    public class LoginResponse
    {
        public string routingNo { get; set; }
        public string name { get; set; }
        public string index { get; set; }
        public string message { get; set; }
        public string brCode { get; set; }
        public string brName { get; set; }
        public string email { get; set; }
        public string status { get; set; }
        public string username { get; set; }
        public string contactNo { get; set; }
        public List<Role> role_list { get; set; }
    }

    public class Role
    {
        public int ROLE_ID { get; set; }
        public string ROLE_NAME { get; set; }
    }

    public class RoleRequest
    {
        public string username { get; set; }
        public string password { get; set; }
    }

    public class RoleResponse
    {
        public List<Role> role_list { get; set; }
    }

    public class UpdatePasswordRequest
    {
        public string username { get; set; }
        public string password { get; set; }
        public string serviceCode { get; set; }
        public string newPassword { get; set; }
    }

    public class UpdatePasswordResponse
    {
        public string message { get; set; }
        public string status { get; set; }
    }
}
