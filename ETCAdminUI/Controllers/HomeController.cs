using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ETCAdminUI.Models;
using System.Text.Json;
using ETCAdminUI.Models;

namespace ETCAdminUI.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public bool NotLogin()
    {
        return string.IsNullOrEmpty(HttpContext.Session.GetString("loginStatus"));
    }

    public IActionResult Index()
    {
        UserInfoModel userInfo = new UserInfoModel();
        if (NotLogin())
        {
            return RedirectToAction("Login", "Account");
        }
        else
        {
            var json = HttpContext.Session.GetString("loginStatus");
            userInfo = JsonSerializer.Deserialize<UserInfoModel>(json);
        }
        return View();
    }

    public IActionResult Help()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
