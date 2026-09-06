using Microsoft.AspNetCore.Mvc;

namespace FutureTech.StudentManagement.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Students");

        return RedirectToAction("Login", "Auth");
    }
}