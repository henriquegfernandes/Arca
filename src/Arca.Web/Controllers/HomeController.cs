using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();
}
