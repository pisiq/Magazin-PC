using Microsoft.AspNetCore.Mvc;

namespace Recomandare_PC.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
