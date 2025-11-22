using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema_Suporte.Data;
using Sistema_Suporte.Models;
using System.Text.RegularExpressions;

namespace Sistema_Suporte.Controllers
{
    public class TecnicoController : Controller
    {
        private readonly BancoContext _context;

        public TecnicoController(BancoContext context)
        {
            _context = context;
        }

        // ===========================================================
        // 🔹 TELA PRINCIPAL DO TÉCNICO – LISTA DE CHAMADOS EM ANDAMENTO
        // ===========================================================
        public async Task<IActionResult> Index()
        {
            int? tecnicoId = HttpContext.Session.GetInt32("UserId");

            if (tecnicoId == null)
                return RedirectToAction("Login", "Account");

            var technicianName = HttpContext.Session.GetString("UserName") ?? "Técnico";
            ViewBag.TechnicianName = technicianName;

            var chamados = await _context.Chamados
                .Include(c => c.Usuario)
                .Where(c => c.TecnicoId == tecnicoId && c.Status == "Em Andamento")
                .OrderByDescending(c => c.DataAtualizacao ?? c.DataAbertura)
                .ToListAsync();

            return View(chamados);
        }

        // ===========================================================
        // 🔹 LISTA DE CHAMADOS RESOLVIDOS
        // ===========================================================
        public async Task<IActionResult> Resolvidos()
        {
            int? tecnicoId = HttpContext.Session.GetInt32("UserId");
            if (tecnicoId == null)
                return RedirectToAction("Login", "Account");

            ViewBag.TechnicianName = HttpContext.Session.GetString("UserName") ?? "Técnico";

            // Trazer os campos necessários do banco (sem usar métodos C# no Select)
            var raw = await _context.Chamados
                .Include(c => c.Usuario)
                .Include(c => c.Tecnico)
                .Where(c => c.TecnicoId == tecnicoId &&
                           (c.Status == "Resolvido" || c.Status == "Finalizado"))
                .OrderByDescending(c => c.DataAtualizacao ?? c.DataAbertura)
                .Select(c => new
                {
                    c.IdChamado,
                    c.Titulo,
                    c.Descricao,
                    NomeCliente = c.Usuario.Nome,
                    NomeTecnico = c.Tecnico != null ? c.Tecnico.Nome : null,
                    c.Status,
                    c.DataAbertura,
                    DataAtualizacao = c.DataAtualizacao ?? c.DataAbertura
                })
                .ToListAsync();

            // Mapear em memória e usar ExtrairConversationId(...) tranquilamente
            var chamadosResolvidos = raw
                .Select(c => new ChamadoResolvidoViewModel
                {
                    IdChamado = c.IdChamado,
                    Titulo = c.Titulo,
                    Descricao = c.Descricao,
                    NomeCliente = c.NomeCliente,
                    NomeTecnico = string.IsNullOrEmpty(c.NomeTecnico) ? "Não atribuído" : c.NomeTecnico,
                    Status = c.Status,
                    DataAbertura = c.DataAbertura,
                    DataAtualizacao = c.DataAtualizacao,
                    ConversationId = ExtrairConversationId(c.Titulo)
                })
                .ToList();

            return View(chamadosResolvidos);
        }

        [HttpGet]
        public async Task<IActionResult> DetalhesChamado(int id)
        {
            // Buscar dados do chamado
            var chamado = await _context.Chamados
                .Include(c => c.Usuario)
                .Include(c => c.Tecnico)
                .FirstOrDefaultAsync(c => c.IdChamado == id);

            if (chamado == null)
            {
                return Json(new { erro = "Chamado não encontrado." });
            }

            // Buscar mensagens associadas ao chamado
            var mensagens = await _context.ChatMessages
                .Where(m => m.ChamadoId == id)
                .OrderBy(m => m.DataEnvio)
                .Select(m => new
                {
                    Texto = m.Conteudo,
                    Remetente = m.TipoRemetente,  // Cliente / Tecnico / IA
                    Data = m.DataEnvio.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            // Retorno completo para preencher o modal
            return Json(new
            {
                Id = chamado.IdChamado,
                Titulo = chamado.Titulo,
                Descricao = chamado.Descricao,
                Cliente = chamado.Usuario?.Nome ?? "Desconhecido",
                Tecnico = chamado.Tecnico?.Nome ?? "Não atribuído",
                Status = chamado.Status,
                Abertura = chamado.DataAbertura.ToString("dd/MM/yyyy HH:mm"),
                Atualizacao = (chamado.DataAtualizacao ?? chamado.DataAbertura).ToString("dd/MM/yyyy HH:mm"),
                Mensagens = mensagens
            });
        }



        // ===========================================================
        // 🔹 MARCAR UM CHAMADO COMO RESOLVIDO
        // ===========================================================
        [HttpPost]
        public async Task<IActionResult> ResolverChamado(int id)
        {
            var chamado = await _context.Chamados.FirstOrDefaultAsync(c => c.IdChamado == id);

            if (chamado == null)
            {
                TempData["Erro"] = "Chamado não encontrado.";
                return RedirectToAction("Index");
            }

            chamado.Status = "Resolvido";
            chamado.DataAtualizacao = DateTime.Now;

            _context.Update(chamado);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Chamado marcado como resolvido!";
            return RedirectToAction("Resolvidos");
        }

        // ===========================================================
        // 🔹 EXTRAIR ID DA CONVERSA DO TÍTULO (#001)
        // ===========================================================
        private int? ExtrairConversationId(string titulo)
        {
            if (string.IsNullOrWhiteSpace(titulo))
                return null;

            var match = Regex.Match(titulo, @"#(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : (int?)null;
        }
    }

    // ===========================================================
    // 🔹 VIEW MODEL
    // ===========================================================
    public class ChamadoResolvidoViewModel
    {
        public int IdChamado { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public string NomeCliente { get; set; }
        public string NomeTecnico { get; set; }
        public string Status { get; set; }
        public DateTime DataAbertura { get; set; }
        public DateTime DataAtualizacao { get; set; }
        public int? ConversationId { get; set; }
    }
}





//using Microsoft.AspNetCore.Mvc;

//namespace Sistema_Suporte.Controllers
//{
//    public class TecnicoController : Controller
//    {
//        public IActionResult Index()
//        {
//            var technicianName = HttpContext.Session.GetString("UserName") ?? "Técnico";
//            ViewBag.TechnicianName = technicianName;
//            return View();
//        }


//        public IActionResult Resolvidos()
//        {
//            return View();
//        }




//    }
//}
