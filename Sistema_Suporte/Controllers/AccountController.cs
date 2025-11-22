using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Sistema_Suporte.Data;
using Sistema_Suporte.Models;
using Sistema_Suporte.Utils;

namespace Sistema_Suporte.Controllers
{
    public class AccountController : Controller
    {
        private readonly BancoContext _context;

        public AccountController(BancoContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // ... (código anterior)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == model.Email);
                    if (usuario != null && PasswordHasher.VerifyPassword(model.Senha, usuario.Senha))
                    {
                        HttpContext.Session.SetString("UserType", "Cliente");
                        HttpContext.Session.SetString("UserName", usuario.Nome);
                        HttpContext.Session.SetInt32("UserId", usuario.IdUsuario);
                        return RedirectToAction("ChatIA", "Suporte");
                    }

                    var tecnico = await _context.Tecnicos.FirstOrDefaultAsync(t => t.Email == model.Email);
                    if (tecnico != null && PasswordHasher.VerifyPassword(model.Senha, tecnico.Senha))
                    {
                        HttpContext.Session.SetString("UserType", "Tecnico");
                        HttpContext.Session.SetString("UserName", tecnico.Nome);
                        HttpContext.Session.SetInt32("UserId", tecnico.IdTecnico);
                        return RedirectToAction("Index", "Tecnico");
                    }

                    ModelState.AddModelError("", "Email ou senha inválidos");
                }
                catch (Exception ex)
                {
                    TempData["Erro"] = $"Erro detalhado: {ex.Message} - {ex.InnerException?.Message}";
                    return View(model);
                }
            }
            return View(model);
        }

        // Similar para Registrar e RecuperarSenha, adicione try-catch com ex.Message

        // GET: /Account/Registrar
        public IActionResult Registrar()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(RegistroUsuarios model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Console.WriteLine($"=== TENTATIVA DE REGISTRO ===");
                    Console.WriteLine($"Nome: {model.Nome}");
                    Console.WriteLine($"Email: {model.Email}");
                    Console.WriteLine($"Senha: {model.Senha}");

                    // Verificar se email já existe
                    var emailExiste = await _context.Usuarios.AnyAsync(u => u.Email == model.Email) ||
                                     await _context.Tecnicos.AnyAsync(t => t.Email == model.Email);

                    if (emailExiste)
                    {
                        ModelState.AddModelError("Email", "Este email já está cadastrado");
                        return View(model);
                    }

                    // Criar novo usuário
                    var novoUsuario = new RegistroUsuarios
                    {
                        Nome = model.Nome.Trim(),
                        Email = model.Email.Trim().ToLower(),
                        Senha = PasswordHasher.HashPassword(model.Senha),
                        Tipo = "Usuario",
                        DataCadastro = DateTime.Now
                    };

                    Console.WriteLine($"Hash gerado: {novoUsuario.Senha}");

                    _context.Usuarios.Add(novoUsuario);
                    await _context.SaveChangesAsync();

                    Console.WriteLine("USUÁRIO CRIADO COM SUCESSO!");
                    TempData["Sucesso"] = "Conta criada com sucesso! Faça login.";
                    return RedirectToAction("Login");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERRO NO REGISTRO: {ex.Message}");
                    TempData["Erro"] = "Erro ao criar conta. Tente novamente.";
                    return View(model);
                }
            }

            return View(model);
        }

        // GET: /Account/RegistrarTecnico
        public IActionResult RegistrarTecnico()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarTecnico(RegistroTecnico model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Console.WriteLine($"=== TENTATIVA DE REGISTRO TÉCNICO ===");

                    // Verificar se email já existe
                    var emailExiste = await _context.Tecnicos.AnyAsync(t => t.Email == model.Email);

                    if (emailExiste)
                    {
                        ModelState.AddModelError("Email", "Este email já está cadastrado");
                        return View(model);
                    }

                    var novoTecnico = new RegistroTecnico
                    {
                        Nome = model.Nome.Trim(),
                        Email = model.Email.Trim().ToLower(),
                        Senha = PasswordHasher.HashPassword(model.Senha),
                        Tipo = "Tecnico",
                        DataCadastro = DateTime.Now,
                        Disponivel = true
                    };

                    Console.WriteLine($"Hash gerado: {novoTecnico.Senha}");

                    _context.Tecnicos.Add(novoTecnico);
                    await _context.SaveChangesAsync();

                    Console.WriteLine("TÉCNICO CRIADO COM SUCESSO!");
                    TempData["Sucesso"] = "Conta de técnico criada com sucesso!";
                    return RedirectToAction("Login");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERRO NO REGISTRO TÉCNICO: {ex.Message}");
                    TempData["Erro"] = "Erro ao criar conta de técnico. Tente novamente.";
                    return View(model);
                }
            }

            return View(model);
        }

        // GET: /Account/RecuperarSenha
        public IActionResult RecuperarSenha()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecuperarSenha(RecuperarSenhaModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string email = model.Email;

            // Verifica usuário ou técnico
            bool existe = await _context.Usuarios.AnyAsync(u => u.Email == email) ||
                          await _context.Tecnicos.AnyAsync(t => t.Email == email);

            if (existe)
            {
                // Criar token
                string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                var reset = new PasswordResetToken
                {
                    Email = email,
                    Token = token,
                    Expiration = DateTime.Now.AddHours(1) // válido por 1h
                };

                _context.PasswordResetTokens.Add(reset);
                await _context.SaveChangesAsync();

                // Montar link
                string link = Url.Action(
                    "RedefinirSenha",
                    "Account",
                    new { token = token },
                    Request.Scheme
                );

                // Enviar email (função abaixo)
                await EnviarEmailAsync(email, "Recuperação de senha",
                    $"Clique no link para redefinir sua senha:<br><br><a href='{link}'>Redefinir Senha</a>");

                TempData["Sucesso"] = "Se o email existir, você receberá um link de recuperação.";
            }
            else
            {
                TempData["Sucesso"] = "Se o email existir, você receberá um link de recuperação.";
            }

            return RedirectToAction("Login");
        }

        public async Task EnviarEmailAsync(string para, string assunto, string mensagemHtml)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Suporte Técnico", "seuemail@gmail.com"));
            email.To.Add(new MailboxAddress("", para));
            email.Subject = assunto;

            var bodyBuilder = new BodyBuilder { HtmlBody = mensagemHtml };
            email.Body = bodyBuilder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync("kalinybatista08@gmail.com", "jjor evot tivd evud");
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedefinirSenha(RedefinirSenhaModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var reset = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == model.Token && t.Expiration > DateTime.Now);

            if (reset == null)
                return BadRequest("Token expirado ou inválido.");

            // Buscar usuário ou técnico
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == reset.Email);
            var tecnico = await _context.Tecnicos.FirstOrDefaultAsync(t => t.Email == reset.Email);

            string novaSenhaHash = PasswordHasher.HashPassword(model.NovaSenha);

            if (usuario != null)
                usuario.Senha = novaSenhaHash;

            if (tecnico != null)
                tecnico.Senha = novaSenhaHash;

            // Apagar token (evitar uso novamente)
            _context.PasswordResetTokens.Remove(reset);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Senha redefinida com sucesso!";
            return RedirectToAction("Login");
        }


        public async Task<IActionResult> RedefinirSenha(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Token inválido.");

            var reset = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token && t.Expiration > DateTime.Now);

            if (reset == null)
                return BadRequest("Token expirado ou inválido.");

            return View(new RedefinirSenhaModel { Token = token });
        }



        // GET: /Account/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["Sucesso"] = "Logout realizado com sucesso!";
            return RedirectToAction("Login");
        }

        // Método de teste
        public IActionResult Teste()
        {
            var senha = "123456";
            var hash = PasswordHasher.HashPassword(senha);
            var verifica = PasswordHasher.VerifyPassword(senha, hash);

            return Content($"Senha: {senha}, Hash: {hash}, Verificação: {verifica}");
        }

        public IActionResult AlterarEmail()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login");

            ViewBag.UserType = HttpContext.Session.GetString("UserType");
            var model = new AlterarEmailModel();
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarEmail(AlterarEmailModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            int? id = HttpContext.Session.GetInt32("UserId");
            string tipo = HttpContext.Session.GetString("UserType");

            if (id == null || tipo == null)
            {
                TempData["Erro"] = "Sessão expirada. Faça login novamente.";
                return RedirectToAction("Login");
            }

            try
            {
                if (tipo == "Cliente")
                {
                    var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id);

                    if (usuario == null)
                    {
                        TempData["Erro"] = "Usuário não encontrado.";
                        return View(model);
                    }

                    usuario.Email = model.NovoEmail.Trim().ToLower();
                    _context.Update(usuario);
                }
                else if (tipo == "Tecnico")
                {
                    var tecnico = await _context.Tecnicos.FirstOrDefaultAsync(t => t.IdTecnico == id);

                    if (tecnico == null)
                    {
                        TempData["Erro"] = "Técnico não encontrado.";
                        return View(model);
                    }

                    tecnico.Email = model.NovoEmail.Trim().ToLower();
                    _context.Update(tecnico);
                }

                await _context.SaveChangesAsync();

                TempData["Sucesso"] = "Email alterado com sucesso!";
                return RedirectToAction("AlterarEmail");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro ao atualizar email: " + ex.Message;
                return View(model);
            }
        }

        public IActionResult AlterarSenha()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login");

            ViewBag.UserType = HttpContext.Session.GetString("UserType");
            return View(new AlterarSenhaModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarSenha(AlterarSenhaModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            int? id = HttpContext.Session.GetInt32("UserId");
            string tipo = HttpContext.Session.GetString("UserType");

            if (id == null || tipo == null)
            {
                TempData["Erro"] = "Sessão expirada. Faça login novamente.";
                return RedirectToAction("Login");
            }

            try
            {
                // Objeto genérico (usuário ou técnico)
                dynamic usuario = null;

                if (tipo == "Cliente")
                {
                    usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id);
                }
                else if (tipo == "Tecnico")
                {
                    usuario = await _context.Tecnicos
                        .FirstOrDefaultAsync(t => t.IdTecnico == id);
                }

                if (usuario == null)
                {
                    TempData["Erro"] = "Conta não encontrada.";
                    return View(model);
                }

                // Verificar senha atual
                if (!PasswordHasher.VerifyPassword(model.SenhaAtual, usuario.Senha))
                {
                    ModelState.AddModelError("SenhaAtual", "Senha atual incorreta");
                    return View(model);
                }

                // Atualizar senha
                usuario.Senha = PasswordHasher.HashPassword(model.NovaSenha);
                _context.Update(usuario);
                await _context.SaveChangesAsync();

                TempData["Sucesso"] = "Senha alterada com sucesso!";
                return RedirectToAction("AlterarSenha");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro ao atualizar senha: " + ex.Message;
                return View(model);
            }
        }




        // Teste no AccountController
        [HttpGet]
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                var userCount = await _context.Usuarios.CountAsync();
                var techCount = await _context.Tecnicos.CountAsync();

                return Content($"MySQL OK: {canConnect}, Users: {userCount}, Techs: {techCount}");
            }
            catch (Exception ex)
            {
                return Content($"MySQL ERROR: {ex.Message}");
            }
        }
    }
}

        //private async Task LoginTecnico(RegistroTecnico tecnico)
        //{
        //    try
        //    {
        //        // Configura a sessão
        //        HttpContext.Session.SetString("UserType", "Tecnico");
        //        HttpContext.Session.SetString("UserName", tecnico.Nome);
               

              

        //        // Atualiza último acesso no banco
        //        tecnico.UltimoAcesso = DateTime.Now;
        //        tecnico.Disponivel = true; // Marca como disponível para atendimentos

        //        _context.Tecnicos.Update(tecnico);
        //        await _context.SaveChangesAsync();

        //        TempData["Sucesso"] = $"Bem-vindo, {tecnico.Nome}!";
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Erro no login do técnico: {ex.Message}");
        //        // Não lança exceção para não quebrar o login
        //    }
        //}
    