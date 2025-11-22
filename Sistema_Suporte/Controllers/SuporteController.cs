using Microsoft.AspNetCore.Mvc;
using Sistema_Suporte.Services;

namespace Sistema_Suporte.Controllers
{
    public class SuporteController : Controller
    {
        private readonly IGeminiAIService _geminiAI;

        public SuporteController(IGeminiAIService geminiAI)
        {
            _geminiAI = geminiAI; // agora funciona
        }
        public IActionResult ChatIA()
        {
            return View();
        }




        public IActionResult Chat_Tecnico()
        {
            var userName = HttpContext.Session.GetString("UserName") ?? "Cliente";
            ViewBag.UserName = userName;
            return View();
        }


    }
}
