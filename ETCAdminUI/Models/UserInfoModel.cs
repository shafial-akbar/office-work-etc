using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ETCAdminUI.Models
{
    public class UserInfoModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }        
        public string UserName { get; set; }       
        public string UserMobileNo { get; set; }
        public string UserRole { get; set; }
        public string UserRoleName { get; set; }
        public int MenuRetrive { get; set; }
        public bool LoginStat { get; set; }
        public bool IsGTSBranch { get; set; }
        public bool IsActive { get; set; }
        public string UserBRCode { get; set; }
        public string BranchName { get; set; }
        public string RoutingNo { get; set; }
        public bool IsPasswordExpire { get; set; }
        public string Message { get; set; }
        public SelectList UserRoleList { get; set; }
        public SelectList BranchList { get; set; }
    }
}
