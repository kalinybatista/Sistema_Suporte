using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sistema_Suporte.Data;
using Sistema_Suporte.Models;
using Sistema_Suporte.Services;
using System.Collections.Concurrent;
using System.Text;

namespace Sistema_Suporte
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
        private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _chats = new();
        private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _iaChats = new();
        private static int _currentConversationId = 1;        
        private readonly IGeminiAIService _geminiService;
        private readonly IServiceProvider _serviceProvider;

        public ChatHub(IServiceProvider serviceProvider, IGeminiAIService geminiService)
        {
            _serviceProvider = serviceProvider;
        
            _geminiService = geminiService;
        }



        public async Task TestConnection()
        {
            var connectionId = Context.ConnectionId;
            Console.WriteLine($"🎯 TestConnection chamado - ConnectionId: {connectionId}");

            await Clients.Caller.SendAsync("ReceiveTestMessage",
                $"Conexão estabelecida com sucesso! Seu ID: {connectionId}");

            await Clients.Caller.SendAsync("ReceiveSystemMessage",
                "Servidor funcionando perfeitamente!");
        }




        public async Task JoinAsClientIA(string userName)
        {
            var connectionId = Context.ConnectionId;

            _connections[connectionId] = new UserConnection
            {
                UserId = connectionId,
                UserName = userName,
                UserType = "ClienteIA",
                ConnectionTime = DateTime.Now,
                ConversationId = GerarConversationId()
            };

            // ✅ INICIALIZAR HISTÓRICO VAZIO - mensagem inicial será enviada apenas uma vez
            if (!_iaChats.ContainsKey(connectionId))
            {
                _iaChats[connectionId] = new List<ChatMessageDto>();

                // ✅ ENVIAR MENSAGEM INICIAL APENAS UMA VEZ
                var mensagemInicial = new ChatMessageDto
                {
                    UserName = "Assistente IA",
                    Message = "Olá! Sou o assistente virtual de suporte técnico. Como posso ajudar você hoje?",
                    Timestamp = DateTime.Now,
                    IsFromClient = false
                };

                _iaChats[connectionId].Add(mensagemInicial);
            }

            await Groups.AddToGroupAsync(connectionId, "ClientesIA");
            await Clients.Caller.SendAsync("ReceiveIAChatHistory", _iaChats[connectionId]);

            Console.WriteLine($"✅ Cliente IA conectado: {userName} - {connectionId}");
        }


        // ✅ MÉTODO CORRIGIDO COM LOGS DETALHADOS
        public async Task SendMessageToIA(string message)
        {
            var connectionId = Context.ConnectionId;

            Console.WriteLine($"🔍 [SendMessageToIA] Iniciando - Connection: {connectionId}");
            Console.WriteLine($"💬 [SendMessageToIA] Mensagem recebida: {message}");

            if (_connections.TryGetValue(connectionId, out var user) && user.UserType == "ClienteIA")
            {
                Console.WriteLine($"👤 [SendMessageToIA] Usuário válido: {user.UserName}");

                // ✅ ADICIONAR MENSAGEM DO USUÁRIO AO HISTÓRICO
                var userMessage = new ChatMessageDto
                {
                    UserName = user.UserName,
                    Message = message,
                    Timestamp = DateTime.Now,
                    IsFromClient = true
                };

                if (_iaChats.ContainsKey(connectionId))
                {
                    _iaChats[connectionId].Add(userMessage);
                }

                // ✅ ENVIAR CONFIRMAÇÃO
                await Clients.Caller.SendAsync("ReceiveOwnMessage", message, DateTime.Now);
                Console.WriteLine($"📤 [SendMessageToIA] Confirmação enviada para usuário");

                // Mostrar indicador de digitação
                await Clients.Caller.SendAsync("ReceiveIATyping", true);

                try
                {
                    Console.WriteLine($"🤖 [SendMessageToIA] Chamando Gemini Service...");

                    // Chamar a IA Gemini
                    var iaResponse = await _geminiService.SendMessageAsync(message, BuildConversationHistory(connectionId));

                    Console.WriteLine($"✅ [SendMessageToIA] Resposta da IA recebida: {iaResponse?.Substring(0, Math.Min(100, iaResponse.Length))}...");

                    // ✅ ADICIONAR RESPOSTA DA IA AO HISTÓRICO
                    var iaMessage = new ChatMessageDto
                    {
                        UserName = "Assistente IA",
                        Message = iaResponse,
                        Timestamp = DateTime.Now,
                        IsFromClient = false
                    };

                    if (_iaChats.ContainsKey(connectionId))
                    {
                        _iaChats[connectionId].Add(iaMessage);
                    }

                    // ✅ ENVIAR RESPOSTA DA IA
                    await Clients.Caller.SendAsync("ReceiveIAMessage", iaResponse, DateTime.Now);
                    Console.WriteLine($"📨 [SendMessageToIA] Resposta enviada para cliente");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [SendMessageToIA] Erro na IA: {ex.Message}");
                    Console.WriteLine($"❌ [SendMessageToIA] StackTrace: {ex.StackTrace}");

                    var errorMessage = "Desculpe, ocorreu um erro interno ao processar sua solicitação.";
                    await Clients.Caller.SendAsync("ReceiveIAMessage", errorMessage, DateTime.Now);
                }
                finally
                {
                    // Remover indicador de digitação
                    await Clients.Caller.SendAsync("ReceiveIATyping", false);
                    Console.WriteLine($"🔚 [SendMessageToIA] Processamento finalizado");
                }
            }
            else
            {
                Console.WriteLine($"❌ [SendMessageToIA] Usuário não encontrado ou tipo incorreto");
            }
        }

        private string BuildConversationHistory(string connectionId)
        {
            if (!_iaChats.ContainsKey(connectionId)) return string.Empty;

            var history = new StringBuilder();
            foreach (var msg in _iaChats[connectionId])
            {
                var speaker = msg.IsFromClient ? "Usuário" : "Assistente";
                history.AppendLine($"{speaker}: {msg.Message}");
            }
            return history.ToString();
        }

        public class ChatMessageDto
        {
            public string UserName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public bool IsFromClient { get; set; }
        }

        public class UserConnection
        {
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string UserType { get; set; } = string.Empty;
            public DateTime ConnectionTime { get; set; }
            public int? ChamadoId { get; set; }
            public int ConversationId { get; set; }
        }

        // ✅ MÉTODO PARA GERAR ID DE CONVERSA
        private int GerarConversationId()
        {
            lock (this)
            {
                if (_currentConversationId > 999)
                {
                    _currentConversationId = 1;
                }
                return _currentConversationId++;
            }
        }

        // ✅ CLIENTE SE CONECTA
        public async Task JoinAsClient(string userName)
        {
            var connectionId = Context.ConnectionId;
            var conversationId = GerarConversationId();
            var chamadoId = await CriarChamado(userName, conversationId);

            _connections[connectionId] = new UserConnection
            {
                UserId = connectionId,
                UserName = userName,
                UserType = "Cliente",
                ConnectionTime = DateTime.Now,
                ChamadoId = chamadoId,
                ConversationId = conversationId
            };

            if (!_chats.ContainsKey(connectionId))
            {
                _chats[connectionId] = new List<ChatMessageDto>
                {
                    new ChatMessageDto
                    {
                        UserName = "Sistema",
                        Message = "Olá!. Como posso ajudar você hoje?",
                        Timestamp = DateTime.Now,
                        IsFromClient = false
                    }
                };
            }

            await Groups.AddToGroupAsync(connectionId, "Clientes");
            await Clients.Caller.SendAsync("ReceiveChatHistory", _chats[connectionId]);
            await Clients.Group("Tecnicos").SendAsync("NewClientConnected", userName, connectionId, chamadoId, conversationId);
        }

        // ✅ TÉCNICO SE CONECTA
        public async Task JoinAsTechnician(string technicianName)
        {
            var connectionId = Context.ConnectionId;

            _connections[connectionId] = new UserConnection
            {
                UserId = connectionId,
                UserName = technicianName,
                UserType = "Tecnico",
                ConnectionTime = DateTime.Now
            };

            await Groups.AddToGroupAsync(connectionId, "Tecnicos");

            var activeClients = _connections.Values
                .Where(c => c.UserType == "Cliente")
                .Select(c => new {
                    c.UserId,
                    c.UserName,
                    ConnectionTime = c.ConnectionTime,
                    ChamadoId = c.ChamadoId,
                    ConversationId = c.ConversationId
                })
                .ToList();

            await Clients.Caller.SendAsync("ReceiveActiveClients", activeClients);
        }

        // ✅ CLIENTE ENVIA MENSAGEM PARA TÉCNICOS
        public async Task SendMessageToTechnicians(string message)
        {
            var connectionId = Context.ConnectionId;

            if (_connections.TryGetValue(connectionId, out var user) && user.UserType == "Cliente")
            {
                var chatMessage = new ChatMessageDto
                {
                    UserName = user.UserName,
                    Message = message,
                    Timestamp = DateTime.Now,
                    IsFromClient = true
                };

                // Salva no histórico em memória
                if (_chats.ContainsKey(connectionId))
                {
                    _chats[connectionId].Add(chatMessage);
                }

                // Salvar mensagem no banco
                if (user.ChamadoId.HasValue)
                {
                    await SalvarMensagemNoBanco(user.UserName, "Cliente", message, user.ChamadoId.Value);
                }

                // Envia para todos os técnicos
                await Clients.Group("Tecnicos").SendAsync("ReceiveClientMessage",
                    user.UserId, user.UserName, message, DateTime.Now, user.ChamadoId, user.ConversationId);

                // Confirmação para o cliente
                await Clients.Caller.SendAsync("ReceiveOwnMessage", message, DateTime.Now);
            }
        }

        // ✅ TÉCNICO ENVIA MENSAGEM PARA CLIENTE ESPECÍFICO
        public async Task SendMessageToClient(string clientConnectionId, string message)
        {
            var technicianConnectionId = Context.ConnectionId;

            if (_connections.TryGetValue(technicianConnectionId, out var technician) &&
                _connections.TryGetValue(clientConnectionId, out var client))
            {
                var chatMessage = new ChatMessageDto
                {
                    UserName = technician.UserName,
                    Message = message,
                    Timestamp = DateTime.Now,
                    IsFromClient = false
                };

                // Salva no histórico em memória
                if (_chats.ContainsKey(clientConnectionId))
                {
                    _chats[clientConnectionId].Add(chatMessage);
                }

                // Salvar mensagem no banco
                if (client.ChamadoId.HasValue)
                {
                    await SalvarMensagemNoBanco(technician.UserName, "Tecnico", message, client.ChamadoId.Value);
                }

                // Envia para o cliente
                await Clients.Client(clientConnectionId).SendAsync("ReceiveTechnicianMessage",
                    technician.UserName, message, DateTime.Now);

                // Confirmação para o técnico
                await Clients.Caller.SendAsync("ReceiveOwnMessageToClient",
                    clientConnectionId, message, DateTime.Now);
            }
        }

        // ✅ TÉCNICO SOLICITA HISTÓRICO DO CLIENTE
        public async Task RequestClientChatHistory(string clientConnectionId)
        {
            if (_chats.TryGetValue(clientConnectionId, out var chatHistory))
            {
                await Clients.Caller.SendAsync("ReceiveClientChatHistory", clientConnectionId, chatHistory);
            }
        }

        // ✅ MÉTODO PARA SALVAR MENSAGEM NO BANCO
        private async Task SalvarMensagemNoBanco(string remetente, string tipoRemetente, string mensagem, int chamadoId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

            try
            {
                var chatMessage = new ChatMessage
                {
                    Conteudo = mensagem,
                    TipoRemetente = tipoRemetente,
                    ChamadoId = chamadoId,
                    DataEnvio = DateTime.Now
                };

                context.ChatMessages.Add(chatMessage);
                await context.SaveChangesAsync();

                Console.WriteLine($"✅ Mensagem salva no banco: {mensagem.Substring(0, Math.Min(mensagem.Length, 50))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao salvar mensagem no banco: {ex.Message}");
            }
        }

        // ✅ MÉTODO PARA CRIAR CHAMADO NO BANCO
        private async Task<int> CriarChamado(string userName, int conversationId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

            try
            {
                // Buscar usuário pelo nome
                var usuario = await context.Usuarios
                    .FirstOrDefaultAsync(u => u.Nome == userName);

                if (usuario == null)
                {
                    // Se não encontrar, usar o primeiro usuário
                    usuario = await context.Usuarios.FirstOrDefaultAsync();
                    if (usuario == null)
                    {
                        // Criar usuário padrão se não existir
                        usuario = new RegistroUsuarios
                        {
                            Nome = userName,
                            Email = $"{userName.ToLower()}@temp.com",
                            Senha = "temp",
                            Tipo = "Usuario"
                        };
                        context.Usuarios.Add(usuario);
                        await context.SaveChangesAsync();
                    }
                }

                var chamado = new Chamado
                {
                    Titulo = $"Suporte #{conversationId:000}",
                    Descricao = "Chamado aberto via chat online",
                    UsuarioId = usuario.IdUsuario,
                    Status = "Aberto",
                    TipoChat = "Tecnico",
                    DataAbertura = DateTime.Now
                };

                context.Chamados.Add(chamado);
                await context.SaveChangesAsync();

                Console.WriteLine($"✅ Chamado criado: #{conversationId:000} para {userName}");
                return chamado.IdChamado;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao criar chamado: {ex.Message}");
                return 0;
            }
        }

        // ✅ MÉTODO ÚNICO PARA ATUALIZAR STATUS DO CHAMADO
        // ✅ MÉTODO ÚNICO PARA ATUALIZAR STATUS DO CHAMADO (VERSÃO CORRIGIDA)
        private async Task AtualizarStatusChamado(int chamadoId, string status)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

            try
            {
                var chamado = await context.Chamados
                    .FirstOrDefaultAsync(c => c.IdChamado == chamadoId);

                if (chamado != null)
                {
                    chamado.Status = status;
                    chamado.DataAtualizacao = DateTime.Now;

                    // --------------------------------------------------------
                    // CORREÇÃO ESSENCIAL PARA ATRIBUIR O TÉCNICO CORRETAMENTE
                    // --------------------------------------------------------
                    var http = Context.GetHttpContext();
                    int? tecnicoIdSessao = http?.Session.GetInt32("UserId");

                    if (tecnicoIdSessao != null)
                    {
                        chamado.TecnicoId = tecnicoIdSessao.Value;
                        Console.WriteLine($"🔧 Técnico associado ao chamado #{chamadoId}: {tecnicoIdSessao.Value}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠ AVISO: TécnicoId da sessão veio NULO. O chamado ficou sem técnico.");
                    }

                    await context.SaveChangesAsync();

                    Console.WriteLine($"✅ Chamado #{chamadoId} atualizado para status: {status}");
                }
                else
                {
                    Console.WriteLine($"❌ Chamado {chamadoId} não encontrado no banco.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ERRO ao atualizar chamado {chamadoId}: {ex.Message}");
            }
        }



        // ✅ MÉTODO PARA FINALIZAR ATENDIMENTO - ÚNICA VERSÃO
        public async Task FinalizarAtendimento(string clientConnectionId, string motivo = "Atendimento finalizado pelo técnico")
        {
            try
            {
                Console.WriteLine($"🔧 FinalizarAtendimento chamado para: {clientConnectionId}");

                if (_connections.TryGetValue(clientConnectionId, out var client))
                {
                    Console.WriteLine($"✅ Cliente encontrado: {client.UserName}, Chamado: {client.ChamadoId}");

                    // ✅ Enviar mensagem de finalização para o cliente
                    await Clients.Client(clientConnectionId).SendAsync("ReceiveAtendimentoFinalizado", motivo);
                    Console.WriteLine($"✅ Mensagem de finalização enviada para o cliente");

                    // ✅ Salvar mensagem de finalização no banco
                    if (client.ChamadoId.HasValue)
                    {
                        await SalvarMensagemNoBanco("Sistema", "Tecnico", $"Atendimento finalizado: {motivo}", client.ChamadoId.Value);
                        Console.WriteLine($"✅ Mensagem salva no banco");
                    }

                    // ✅ Atualizar status do chamado
                    if (client.ChamadoId.HasValue)
                    {
                        await AtualizarStatusChamado(client.ChamadoId.Value, "Finalizado");
                        Console.WriteLine($"✅ Status do chamado atualizado");
                    }

                    // ✅ Notificar técnicos que o cliente foi desconectado
                    await Clients.Group("Tecnicos").SendAsync("ClientDisconnected", clientConnectionId);
                    Console.WriteLine($"✅ Técnicos notificados sobre desconexão");

                    // ✅ Desconectar o cliente
                    await Clients.Client(clientConnectionId).SendAsync("ForceDisconnect");
                    Console.WriteLine($"✅ Cliente desconectado");

                    // ✅ Remover das conexões ativas
                    _connections.TryRemove(clientConnectionId, out _);
                    _chats.TryRemove(clientConnectionId, out _);
                    Console.WriteLine($"✅ Cliente removido das conexões ativas");

                    await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Atendimento com {client.UserName} finalizado com sucesso.");
                    Console.WriteLine($"✅ Confirmação enviada para o técnico");
                }
                else
                {
                    Console.WriteLine($"❌ Cliente não encontrado: {clientConnectionId}");
                    await Clients.Caller.SendAsync("ReceiveSystemMessage", "Cliente não encontrado ou já desconectado.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ERRO em FinalizarAtendimento: {ex.Message}");
                Console.WriteLine($"💥 StackTrace: {ex.StackTrace}");
                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Erro ao finalizar atendimento: {ex.Message}");
            }
        }

        // ✅ MÉTODO PARA MARCAR COMO RESOLVIDO - ÚNICA VERSÃO
        public async Task MarcarComoResolvido(string clientConnectionId)
        {
            try
            {
                Console.WriteLine($"🔧 MarcarComoResolvido chamado para: {clientConnectionId}");

                if (_connections.TryGetValue(clientConnectionId, out var client))
                {
                    Console.WriteLine($"✅ Cliente encontrado: {client.UserName}, Chamado: {client.ChamadoId}");

                    var mensagemResolvido = "Seu problema foi marcado como RESOLVIDO! Obrigado por entrar em contato.";

                    // ✅ Enviar mensagem de resolução para o cliente
                    await Clients.Client(clientConnectionId).SendAsync("ReceiveProblemaResolvido", mensagemResolvido);
                    Console.WriteLine($"✅ Mensagem de resolução enviada para o cliente");

                    // ✅ Salvar mensagem no banco
                    if (client.ChamadoId.HasValue)
                    {
                        await SalvarMensagemNoBanco("Sistema", "Tecnico", mensagemResolvido, client.ChamadoId.Value);
                        Console.WriteLine($"✅ Mensagem salva no banco");
                    }

                    // ✅ Atualizar status do chamado
                    if (client.ChamadoId.HasValue)
                    {
                        await AtualizarStatusChamado(client.ChamadoId.Value, "Resolvido");
                        Console.WriteLine($"✅ Status do chamado atualizado para Resolvido");
                    }

                    await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Problema de {client.UserName} marcado como resolvido.");
                    Console.WriteLine($"✅ Confirmação enviada para o técnico");
                }
                else
                {
                    Console.WriteLine($"❌ Cliente não encontrado: {clientConnectionId}");
                    await Clients.Caller.SendAsync("ReceiveSystemMessage", "Cliente não encontrado ou já desconectado.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ERRO em MarcarComoResolvido: {ex.Message}");
                Console.WriteLine($"💥 StackTrace: {ex.StackTrace}");
                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Erro ao marcar como resolvido: {ex.Message}");
            }
        }

        // ✅ MÉTODO PARA ENVIAR MENSAGEM DE FINALIZAÇÃO PERSONALIZADA
        public async Task EnviarMensagemFinalizacao(string clientConnectionId, string mensagemPersonalizada)
        {
            if (_connections.TryGetValue(clientConnectionId, out var client))
            {
                // ✅ Enviar mensagem personalizada para o cliente
                await Clients.Client(clientConnectionId).SendAsync("ReceiveMensagemFinalizacao", mensagemPersonalizada);

                // ✅ Salvar mensagem no banco
                if (client.ChamadoId.HasValue)
                {
                    await SalvarMensagemNoBanco("Técnico", "Tecnico", mensagemPersonalizada, client.ChamadoId.Value);
                }

                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Mensagem de finalização enviada para {client.UserName}.");
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveSystemMessage", "Cliente não encontrado ou já desconectado.");
            }
        }

        // ✅ MÉTODO PARA CARREGAR HISTÓRICO DO BANCO
        public async Task CarregarHistoricoDoBanco(string connectionId, int chamadoId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

            try
            {
                var mensagens = await context.ChatMessages
                    .Where(m => m.ChamadoId == chamadoId)
                    .OrderBy(m => m.DataEnvio)
                    .ToListAsync();

                var historicoDto = mensagens.Select(m => new ChatMessageDto
                {
                    UserName = m.TipoRemetente == "Cliente" ? "Você" : m.TipoRemetente,
                    Message = m.Conteudo,
                    Timestamp = m.DataEnvio,
                    IsFromClient = m.TipoRemetente == "Cliente"
                }).ToList();

                await Clients.Caller.SendAsync("ReceiveChatHistory", historicoDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao carregar histórico: {ex.Message}");
            }
        }

        // ✅ CLIENTE DESCONECTA
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            if (_connections.TryRemove(connectionId, out var user))
            {
                if (user.UserType == "Cliente")
                {
                    await Clients.Group("Tecnicos").SendAsync("ClientDisconnected", connectionId);

                    // ✅ Atualizar status do chamado quando cliente desconectar
                    if (user.ChamadoId.HasValue)
                    {
                        await AtualizarStatusChamado(user.ChamadoId.Value, "Finalizado");
                    }
                }

                await Groups.RemoveFromGroupAsync(connectionId,
                    user.UserType == "Cliente" ? "Clientes" : "Tecnicos");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ✅ MÉTODO DE TESTE
        public async Task TestarConexao(string clientConnectionId)
        {
            try
            {
                Console.WriteLine($"🔧 TestarConexao chamado para: {clientConnectionId}");

                if (_connections.TryGetValue(clientConnectionId, out var client))
                {
                    await Clients.Caller.SendAsync("ReceiveSystemMessage", $"✅ Cliente encontrado: {client.UserName}");
                    await Clients.Client(clientConnectionId).SendAsync("ReceiveTestMessage", "✅ Mensagem de teste recebida!");
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveSystemMessage", "❌ Cliente não encontrado");
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"💥 Erro no teste: {ex.Message}");
            }
        }

        // ✅ MÉTODO PARA OBTER ESTATÍSTICAS (OPCIONAL)
        public int GetCurrentConversationCount()
        {
            return _currentConversationId - 1;
        }

        // ✅ MÉTODO PARA REINICIAR CONTADOR (OPCIONAL)
        public void ResetConversationCounter()
        {
            lock (this)
            {
                _currentConversationId = 1;
            }
        }
    }
}



//using Microsoft.AspNetCore.SignalR;
//using Microsoft.EntityFrameworkCore;
//using Sistema_Suporte.Data;
//using Sistema_Suporte.Models;
//using System.Collections.Concurrent;

//namespace Sistema_Suporte
//{
//    public class ChatHub : Hub
//    {
//        private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
//        private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _chats = new();
//        private static int _currentConversationId = 1;
//        private readonly IServiceProvider _serviceProvider;

//        public ChatHub(IServiceProvider serviceProvider)
//        {
//            _serviceProvider = serviceProvider;
//        }

//        public class ChatMessageDto
//        {
//            public string UserName { get; set; } = string.Empty;
//            public string Message { get; set; } = string.Empty;
//            public DateTime Timestamp { get; set; }
//            public bool IsFromClient { get; set; }
//        }

//        public class UserConnection
//        {
//            public string UserId { get; set; } = string.Empty;
//            public string UserName { get; set; } = string.Empty;
//            public string UserType { get; set; } = string.Empty;
//            public DateTime ConnectionTime { get; set; }
//            public int? ChamadoId { get; set; }
//            public int ConversationId { get; set; }
//        }

//        // ✅ MÉTODO PARA GERAR ID DE CONVERSA
//        private int GerarConversationId()
//        {
//            lock (this)
//            {
//                if (_currentConversationId > 999)
//                {
//                    _currentConversationId = 1;
//                }
//                return _currentConversationId++;
//            }
//        }

//        // Cliente se conecta
//        public async Task JoinAsClient(string userName)
//        {
//            var connectionId = Context.ConnectionId;
//            var conversationId = GerarConversationId();
//            var chamadoId = await CriarChamado(userName, conversationId);

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = userName,
//                UserType = "Cliente",
//                ConnectionTime = DateTime.Now,
//                ChamadoId = chamadoId,
//                ConversationId = conversationId
//            };

//            if (!_chats.ContainsKey(connectionId))
//            {
//                _chats[connectionId] = new List<ChatMessageDto>
//                {
//                    new ChatMessageDto
//                    {
//                        UserName = "Sistema",
//                        Message = "Olá! Sou o assistente virtual. Como posso ajudar você hoje?",
//                        Timestamp = DateTime.Now,
//                        IsFromClient = false
//                    }
//                };
//            }

//            await Groups.AddToGroupAsync(connectionId, "Clientes");
//            await Clients.Caller.SendAsync("ReceiveChatHistory", _chats[connectionId]);
//            await Clients.Group("Tecnicos").SendAsync("NewClientConnected", userName, connectionId, chamadoId, conversationId);
//        }

//        // Técnico se conecta
//        public async Task JoinAsTechnician(string technicianName)
//        {
//            var connectionId = Context.ConnectionId;

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = technicianName,
//                UserType = "Tecnico",
//                ConnectionTime = DateTime.Now
//            };

//            await Groups.AddToGroupAsync(connectionId, "Tecnicos");

//            var activeClients = _connections.Values
//                .Where(c => c.UserType == "Cliente")
//                .Select(c => new {
//                    c.UserId,
//                    c.UserName,
//                    ConnectionTime = c.ConnectionTime,
//                    ChamadoId = c.ChamadoId,
//                    ConversationId = c.ConversationId
//                })
//                .ToList();

//            await Clients.Caller.SendAsync("ReceiveActiveClients", activeClients);
//        }

//        // Cliente envia mensagem para técnicos
//        public async Task SendMessageToTechnicians(string message)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(connectionId, out var user) && user.UserType == "Cliente")
//            {
//                var chatMessage = new ChatMessageDto
//                {
//                    UserName = user.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = true
//                };

//                // Salva no histórico em memória
//                if (_chats.ContainsKey(connectionId))
//                {
//                    _chats[connectionId].Add(chatMessage);
//                }

//                // ✅ Salvar mensagem no banco
//                if (user.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco(user.UserName, "Cliente", message, user.ChamadoId.Value);
//                }

//                // Envia para todos os técnicos
//                await Clients.Group("Tecnicos").SendAsync("ReceiveClientMessage",
//                    user.UserId, user.UserName, message, DateTime.Now, user.ChamadoId, user.ConversationId);

//                // Confirmação para o cliente
//                await Clients.Caller.SendAsync("ReceiveOwnMessage", message, DateTime.Now);
//            }
//        }

//        // Técnico envia mensagem para cliente específico
//        public async Task SendMessageToClient(string clientConnectionId, string message)
//        {
//            var technicianConnectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(technicianConnectionId, out var technician) &&
//                _connections.TryGetValue(clientConnectionId, out var client))
//            {
//                var chatMessage = new ChatMessageDto
//                {
//                    UserName = technician.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = false
//                };

//                // Salva no histórico em memória
//                if (_chats.ContainsKey(clientConnectionId))
//                {
//                    _chats[clientConnectionId].Add(chatMessage);
//                }

//                // ✅ Salvar mensagem no banco
//                if (client.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco(technician.UserName, "Tecnico", message, client.ChamadoId.Value);
//                }

//                // Envia para o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ReceiveTechnicianMessage",
//                    technician.UserName, message, DateTime.Now);

//                // Confirmação para o técnico
//                await Clients.Caller.SendAsync("ReceiveOwnMessageToClient",
//                    clientConnectionId, message, DateTime.Now);
//            }
//        }

//        // Técnico solicita histórico do cliente
//        public async Task RequestClientChatHistory(string clientConnectionId)
//        {
//            if (_chats.TryGetValue(clientConnectionId, out var chatHistory))
//            {
//                await Clients.Caller.SendAsync("ReceiveClientChatHistory", clientConnectionId, chatHistory);
//            }
//        }

//        // ✅ MÉTODO PARA SALVAR MENSAGEM NO BANCO
//        private async Task SalvarMensagemNoBanco(string remetente, string tipoRemetente, string mensagem, int chamadoId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var chatMessage = new ChatMessage
//                {
//                    Conteudo = mensagem,
//                    TipoRemetente = tipoRemetente,
//                    ChamadoId = chamadoId,
//                    DataEnvio = DateTime.Now
//                };

//                context.ChatMessages.Add(chatMessage);
//                await context.SaveChangesAsync();

//                Console.WriteLine($"✅ Mensagem salva no banco: {mensagem.Substring(0, Math.Min(mensagem.Length, 50))}...");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"❌ Erro ao salvar mensagem no banco: {ex.Message}");
//            }
//        }

//        // ✅ MÉTODO PARA CRIAR CHAMADO NO BANCO
//        private async Task<int> CriarChamado(string userName, int conversationId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                // Buscar usuário pelo nome
//                var usuario = await context.Usuarios
//                    .FirstOrDefaultAsync(u => u.Nome == userName);

//                if (usuario == null)
//                {
//                    // Se não encontrar, usar o primeiro usuário
//                    usuario = await context.Usuarios.FirstOrDefaultAsync();
//                    if (usuario == null)
//                    {
//                        // Criar usuário padrão se não existir
//                        usuario = new RegistroUsuarios
//                        {
//                            Nome = userName,
//                            Email = $"{userName.ToLower()}@temp.com",
//                            Senha = "temp",
//                            Tipo = "Usuario"
//                        };
//                        context.Usuarios.Add(usuario);
//                        await context.SaveChangesAsync();
//                    }
//                }

//                var chamado = new Chamado
//                {
//                    Titulo = $"Suporte #{conversationId:000}",
//                    Descricao = "Chamado aberto via chat online",
//                    UsuarioId = usuario.IdUsuario,
//                    Status = "Aberto",
//                    TipoChat = "Tecnico",
//                    DataAbertura = DateTime.Now
//                };

//                context.Chamados.Add(chamado);
//                await context.SaveChangesAsync();

//                Console.WriteLine($"✅ Chamado criado: #{conversationId:000} para {userName}");
//                return chamado.IdChamado;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"❌ Erro ao criar chamado: {ex.Message}");
//                return 0;
//            }
//        }

//        // ✅ MÉTODO PARA FINALIZAR ATENDIMENTO
//        public async Task FinalizarAtendimento(string clientConnectionId, string motivo = "Atendimento finalizado pelo técnico")
//        {
//            if (_connections.TryGetValue(clientConnectionId, out var client))
//            {
//                // ✅ Enviar mensagem de finalização para o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ReceiveAtendimentoFinalizado", motivo);

//                // ✅ Salvar mensagem de finalização no banco
//                if (client.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco("Sistema", "Tecnico", $"Atendimento finalizado: {motivo}", client.ChamadoId.Value);
//                }

//                // ✅ Atualizar status do chamado
//                if (client.ChamadoId.HasValue)
//                {
//                    await AtualizarStatusChamado(client.ChamadoId.Value, "Finalizado");
//                }

//                // ✅ Notificar técnicos que o cliente foi desconectado
//                await Clients.Group("Tecnicos").SendAsync("ClientDisconnected", clientConnectionId);

//                // ✅ Desconectar o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ForceDisconnect");

//                // ✅ Remover das conexões ativas
//                _connections.TryRemove(clientConnectionId, out _);
//                _chats.TryRemove(clientConnectionId, out _);

//                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Atendimento com {client.UserName} finalizado com sucesso.");
//            }
//            else
//            {
//                await Clients.Caller.SendAsync("ReceiveSystemMessage", "Cliente não encontrado ou já desconectado.");
//            }
//        }

//        // ✅ MÉTODO PARA MARCAR COMO RESOLVIDO
//        public async Task MarcarComoResolvido(string clientConnectionId)
//        {
//            if (_connections.TryGetValue(clientConnectionId, out var client))
//            {
//                var mensagemResolvido = "Seu problema foi marcado como RESOLVIDO! Obrigado por entrar em contato.";

//                // ✅ Enviar mensagem de resolução para o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ReceiveProblemaResolvido", mensagemResolvido);

//                // ✅ Salvar mensagem no banco
//                if (client.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco("Sistema", "Tecnico", mensagemResolvido, client.ChamadoId.Value);
//                }

//                // ✅ Atualizar status do chamado
//                if (client.ChamadoId.HasValue)
//                {
//                    await AtualizarStatusChamado(client.ChamadoId.Value, "Resolvido");
//                }

//                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Problema de {client.UserName} marcado como resolvido.");
//            }
//            else
//            {
//                await Clients.Caller.SendAsync("ReceiveSystemMessage", "Cliente não encontrado ou já desconectado.");
//            }
//        }

//        // ✅ MÉTODO PARA ENVIAR MENSAGEM DE FINALIZAÇÃO PERSONALIZADA
//        public async Task EnviarMensagemFinalizacao(string clientConnectionId, string mensagemPersonalizada)
//        {
//            if (_connections.TryGetValue(clientConnectionId, out var client))
//            {
//                // ✅ Enviar mensagem personalizada para o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ReceiveMensagemFinalizacao", mensagemPersonalizada);

//                // ✅ Salvar mensagem no banco
//                if (client.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco("Técnico", "Tecnico", mensagemPersonalizada, client.ChamadoId.Value);
//                }

//                await Clients.Caller.SendAsync("ReceiveSystemMessage", $"Mensagem de finalização enviada para {client.UserName}.");
//            }
//            else
//            {
//                await Clients.Caller.SendAsync("ReceiveSystemMessage", "Cliente não encontrado ou já desconectado.");
//            }
//        }

//        // ✅ MÉTODO PARA ATUALIZAR STATUS DO CHAMADO
//        private async Task AtualizarStatusChamado(int chamadoId, string status)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var chamado = await context.Chamados.FindAsync(chamadoId);
//                if (chamado != null)
//                {
//                    chamado.Status = status;
//                    chamado.DataAtualizacao = DateTime.Now;

//                    // ✅ Se for resolvido ou finalizado, atribuir ao técnico atual
//                    if (status == "Resolvido" || status == "Finalizado")
//                    {
//                        var technician = _connections.Values.FirstOrDefault(c => c.UserId == Context.ConnectionId);
//                        if (technician != null)
//                        {
//                            // Buscar técnico no banco pelo nome
//                            var tecnicoDb = await context.Tecnicos.FirstOrDefaultAsync(t => t.Nome == technician.UserName);
//                            if (tecnicoDb != null)
//                            {
//                                chamado.TecnicoId = tecnicoDb.IdTecnico;
//                            }
//                        }
//                    }

//                    await context.SaveChangesAsync();
//                    Console.WriteLine($"✅ Chamado #{chamadoId} atualizado para status: {status}");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"❌ Erro ao atualizar chamado: {ex.Message}");
//            }
//        }

//        // ✅ MÉTODO PARA CARREGAR HISTÓRICO DO BANCO
//        public async Task CarregarHistoricoDoBanco(string connectionId, int chamadoId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var mensagens = await context.ChatMessages
//                    .Where(m => m.ChamadoId == chamadoId)
//                    .OrderBy(m => m.DataEnvio)
//                    .ToListAsync();

//                var historicoDto = mensagens.Select(m => new ChatMessageDto
//                {
//                    UserName = m.TipoRemetente == "Cliente" ? "Você" : m.TipoRemetente,
//                    Message = m.Conteudo,
//                    Timestamp = m.DataEnvio,
//                    IsFromClient = m.TipoRemetente == "Cliente"
//                }).ToList();

//                await Clients.Caller.SendAsync("ReceiveChatHistory", historicoDto);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"❌ Erro ao carregar histórico: {ex.Message}");
//            }
//        }

//        // Cliente desconecta
//        public override async Task OnDisconnectedAsync(Exception? exception)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryRemove(connectionId, out var user))
//            {
//                if (user.UserType == "Cliente")
//                {
//                    await Clients.Group("Tecnicos").SendAsync("ClientDisconnected", connectionId);

//                    // ✅ Atualizar status do chamado quando cliente desconectar
//                    if (user.ChamadoId.HasValue)
//                    {
//                        await AtualizarStatusChamado(user.ChamadoId.Value, "Finalizado");
//                    }
//                }

//                await Groups.RemoveFromGroupAsync(connectionId,
//                    user.UserType == "Cliente" ? "Clientes" : "Tecnicos");
//            }

//            await base.OnDisconnectedAsync(exception);
//        }

//        // ✅ MÉTODO PARA OBTER ESTATÍSTICAS (OPCIONAL)
//        public int GetCurrentConversationCount()
//        {
//            return _currentConversationId - 1;
//        }

//        // ✅ MÉTODO PARA REINICIAR CONTADOR (OPCIONAL)
//        public void ResetConversationCounter()
//        {
//            lock (this)
//            {
//                _currentConversationId = 1;
//            }
//        }


//    }
//}




//using Microsoft.AspNetCore.SignalR;
//using Microsoft.EntityFrameworkCore;
//using Sistema_Suporte.Data;
//using Sistema_Suporte.Models;
//using System.Collections.Concurrent;

//namespace Sistema_Suporte
//{
//    public class ChatHub : Hub
//    {
//        private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
//        private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _chats = new();
//        private static int _currentConversationId = 1; // ✅ Contador para IDs de conversa
//        private readonly IServiceProvider _serviceProvider;

//        public ChatHub(IServiceProvider serviceProvider)
//        {
//            _serviceProvider = serviceProvider;
//        }

//        public class ChatMessageDto
//        {
//            public string UserName { get; set; } = string.Empty;
//            public string Message { get; set; } = string.Empty;
//            public DateTime Timestamp { get; set; }
//            public bool IsFromClient { get; set; }
//        }

//        public class UserConnection
//        {
//            public string UserId { get; set; } = string.Empty;
//            public string UserName { get; set; } = string.Empty;
//            public string UserType { get; set; } = string.Empty;
//            public DateTime ConnectionTime { get; set; }
//            public int? ChamadoId { get; set; }
//            public int ConversationId { get; set; } // ✅ ID numérico da conversa (1-999)
//        }

//        // ✅ MÉTODO PARA GERAR ID DE CONVERSA
//        private int GerarConversationId()
//        {
//            lock (this)
//            {
//                if (_currentConversationId > 999)
//                {
//                    _currentConversationId = 1; // Reinicia quando chegar a 999
//                }
//                return _currentConversationId++;
//            }
//        }

//        // Cliente se conecta
//        public async Task JoinAsClient(string userName)
//        {
//            var connectionId = Context.ConnectionId;

//            // ✅ Gerar ID numérico para a conversa
//            var conversationId = GerarConversationId();

//            // ✅ Criar chamado no banco para o cliente
//            var chamadoId = await CriarChamado(userName, conversationId);

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = userName,
//                UserType = "Cliente",
//                ConnectionTime = DateTime.Now,
//                ChamadoId = chamadoId,
//                ConversationId = conversationId // ✅ Atribuir ID numérico
//            };

//            // Cria conversa para o cliente
//            if (!_chats.ContainsKey(connectionId))
//            {
//                _chats[connectionId] = new List<ChatMessageDto>
//                {
//                    new ChatMessageDto
//                    {
//                        UserName = "Sistema",
//                        Message = "Olá! Sou o assistente virtual. Como posso ajudar você hoje?",
//                        Timestamp = DateTime.Now,
//                        IsFromClient = false
//                    }
//                };
//            }

//            await Groups.AddToGroupAsync(connectionId, "Clientes");
//            await Clients.Caller.SendAsync("ReceiveChatHistory", _chats[connectionId]);

//            // ✅ Enviar também o ID da conversa para os técnicos
//            await Clients.Group("Tecnicos").SendAsync("NewClientConnected",
//                userName, connectionId, chamadoId, conversationId);
//        }

//        // Técnico se conecta
//        public async Task JoinAsTechnician(string technicianName)
//        {
//            var connectionId = Context.ConnectionId;

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = technicianName,
//                UserType = "Tecnico",
//                ConnectionTime = DateTime.Now
//            };

//            await Groups.AddToGroupAsync(connectionId, "Tecnicos");

//            // Envia clientes ativos para o técnico com ID da conversa
//            var activeClients = _connections.Values
//                .Where(c => c.UserType == "Cliente")
//                .Select(c => new {
//                    c.UserId,
//                    c.UserName,
//                    ConnectionTime = c.ConnectionTime,
//                    ChamadoId = c.ChamadoId,
//                    ConversationId = c.ConversationId // ✅ Incluir ID da conversa
//                })
//                .ToList();

//            await Clients.Caller.SendAsync("ReceiveActiveClients", activeClients);
//        }

//        // Cliente envia mensagem para técnicos
//        public async Task SendMessageToTechnicians(string message)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(connectionId, out var user) && user.UserType == "Cliente")
//            {
//                var chatMessage = new ChatMessageDto
//                {
//                    UserName = user.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = true
//                };

//                // Salva no histórico em memória
//                if (_chats.ContainsKey(connectionId))
//                {
//                    _chats[connectionId].Add(chatMessage);
//                }

//                // ✅ Salvar mensagem no banco
//                if (user.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco(user.UserName, "Cliente", message, user.ChamadoId.Value);
//                }

//                // ✅ Enviar também o ID da conversa para os técnicos
//                await Clients.Group("Tecnicos").SendAsync("ReceiveClientMessage",
//                    user.UserId, user.UserName, message, DateTime.Now,
//                    user.ChamadoId, user.ConversationId);

//                // Confirmação para o cliente
//                await Clients.Caller.SendAsync("ReceiveOwnMessage", message, DateTime.Now);
//            }
//        }

//        // Técnico envia mensagem para cliente específico
//        public async Task SendMessageToClient(string clientConnectionId, string message)
//        {
//            var technicianConnectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(technicianConnectionId, out var technician) &&
//                _connections.TryGetValue(clientConnectionId, out var client))
//            {
//                var chatMessage = new ChatMessageDto
//                {
//                    UserName = technician.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = false
//                };

//                // Salva no histórico em memória
//                if (_chats.ContainsKey(clientConnectionId))
//                {
//                    _chats[clientConnectionId].Add(chatMessage);
//                }

//                // ✅ Salvar mensagem no banco
//                if (client.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco(technician.UserName, "Tecnico", message, client.ChamadoId.Value);
//                }

//                // Envia para o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ReceiveTechnicianMessage",
//                    technician.UserName, message, DateTime.Now);

//                // Confirmação para o técnico
//                await Clients.Caller.SendAsync("ReceiveOwnMessageToClient",
//                    clientConnectionId, message, DateTime.Now);
//            }
//        }

//        // Técnico solicita histórico do cliente
//        public async Task RequestClientChatHistory(string clientConnectionId)
//        {
//            if (_chats.TryGetValue(clientConnectionId, out var chatHistory))
//            {
//                await Clients.Caller.SendAsync("ReceiveClientChatHistory", clientConnectionId, chatHistory);
//            }
//        }

//        // ✅ MÉTODO PARA CRIAR CHAMADO NO BANCO (ATUALIZADO)
//        private async Task<int> CriarChamado(string userName, int conversationId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                // Buscar usuário pelo nome
//                var usuario = await context.Usuarios
//                    .FirstOrDefaultAsync(u => u.Nome == userName);

//                if (usuario == null)
//                {
//                    // Se não encontrar, usar o primeiro usuário
//                    usuario = await context.Usuarios.FirstOrDefaultAsync();
//                    if (usuario == null)
//                    {
//                        // Criar usuário padrão se não existir
//                        usuario = new RegistroUsuarios
//                        {
//                            Nome = userName,
//                            Email = $"{userName.ToLower()}@temp.com",
//                            Senha = "temp",
//                            Tipo = "Usuario"
//                        };
//                        context.Usuarios.Add(usuario);
//                        await context.SaveChangesAsync();
//                    }
//                }

//                var chamado = new Chamado
//                {
//                    Titulo = $"Suporte #{conversationId:000}", // ✅ Formatar ID com 3 dígitos
//                    Descricao = "Chamado aberto via chat online",
//                    UsuarioId = usuario.IdUsuario,
//                    Status = "Aberto",
//                    TipoChat = "Tecnico",
//                    DataAbertura = DateTime.Now
//                };

//                context.Chamados.Add(chamado);
//                await context.SaveChangesAsync();

//                return chamado.IdChamado;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao criar chamado: {ex.Message}");
//                return 0;
//            }
//        }

//        // ✅ MÉTODO PARA SALVAR MENSAGEM NO BANCO
//        private async Task SalvarMensagemNoBanco(string remetente, string tipoRemetente, string mensagem, int chamadoId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var chatMessage = new ChatMessage
//                {
//                    Conteudo = mensagem,
//                    TipoRemetente = tipoRemetente,
//                    ChamadoId = chamadoId,
//                    DataEnvio = DateTime.Now
//                };

//                context.ChatMessages.Add(chatMessage);
//                await context.SaveChangesAsync();

//                Console.WriteLine($"Mensagem salva no banco: {mensagem}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao salvar mensagem no banco: {ex.Message}");
//            }
//        }

//        // ✅ MÉTODO PARA CARREGAR HISTÓRICO DO BANCO
//        public async Task CarregarHistoricoDoBanco(string connectionId, int chamadoId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var mensagens = await context.ChatMessages
//                    .Where(m => m.ChamadoId == chamadoId)
//                    .OrderBy(m => m.DataEnvio)
//                    .ToListAsync();

//                var historicoDto = mensagens.Select(m => new ChatMessageDto
//                {
//                    UserName = m.TipoRemetente == "Cliente" ? "Você" : m.TipoRemetente,
//                    Message = m.Conteudo,
//                    Timestamp = m.DataEnvio,
//                    IsFromClient = m.TipoRemetente == "Cliente"
//                }).ToList();

//                await Clients.Caller.SendAsync("ReceiveChatHistory", historicoDto);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao carregar histórico: {ex.Message}");
//            }
//        }

//        // Cliente desconecta
//        public override async Task OnDisconnectedAsync(Exception? exception)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryRemove(connectionId, out var user))
//            {
//                if (user.UserType == "Cliente")
//                {
//                    await Clients.Group("Tecnicos").SendAsync("ClientDisconnected", connectionId);

//                    // ✅ Atualizar status do chamado quando cliente desconectar
//                    if (user.ChamadoId.HasValue)
//                    {
//                        await AtualizarStatusChamado(user.ChamadoId.Value, "Finalizado");
//                    }
//                }

//                await Groups.RemoveFromGroupAsync(connectionId,
//                    user.UserType == "Cliente" ? "Clientes" : "Tecnicos");
//            }

//            await base.OnDisconnectedAsync(exception);
//        }

//        // ✅ MÉTODO PARA ATUALIZAR STATUS DO CHAMADO
//        private async Task AtualizarStatusChamado(int chamadoId, string status)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var chamado = await context.Chamados.FindAsync(chamadoId);
//                if (chamado != null)
//                {
//                    chamado.Status = status;
//                    chamado.DataAtualizacao = DateTime.Now;
//                    await context.SaveChangesAsync();
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao atualizar chamado: {ex.Message}");
//            }
//        }

//        // ✅ MÉTODO PARA OBTER ESTATÍSTICAS (OPCIONAL)
//        public int GetCurrentConversationCount()
//        {
//            return _currentConversationId - 1;
//        }

//        // ✅ MÉTODO PARA REINICIAR CONTADOR (OPCIONAL)
//        public void ResetConversationCounter()
//        {
//            lock (this)
//            {
//                _currentConversationId = 1;
//            }
//        }
//    }
//}



//using Microsoft.AspNetCore.SignalR;
//using Microsoft.EntityFrameworkCore;
//using Sistema_Suporte.Data;
//using Sistema_Suporte.Models;
//using System.Collections.Concurrent;

//namespace Sistema_Suporte
//{
//    public class ChatHub : Hub
//    {
//        private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
//        private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _chats = new();
//        private readonly IServiceProvider _serviceProvider;

//        // ✅ Injetar ServiceProvider para acessar o DbContext
//        public ChatHub(IServiceProvider serviceProvider)
//        {
//            _serviceProvider = serviceProvider;
//        }

//        public class ChatMessageDto
//        {
//            public string UserName { get; set; } = string.Empty;
//            public string Message { get; set; } = string.Empty;
//            public DateTime Timestamp { get; set; }
//            public bool IsFromClient { get; set; }
//        }

//        public class UserConnection
//        {
//            public string UserId { get; set; } = string.Empty;
//            public string UserName { get; set; } = string.Empty;
//            public string UserType { get; set; } = string.Empty;
//            public DateTime ConnectionTime { get; set; }
//            public int? ChamadoId { get; set; } // ✅ Adicionar ChamadoId
//        }

//        // Cliente se conecta
//        public async Task JoinAsClient(string userName)
//        {
//            var connectionId = Context.ConnectionId;

//            // ✅ Criar chamado no banco para o cliente
//            var chamadoId = await CriarChamado(userName);

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = userName,
//                UserType = "Cliente",
//                ConnectionTime = DateTime.Now,
//                ChamadoId = chamadoId
//            };

//            // Cria conversa para o cliente
//            if (!_chats.ContainsKey(connectionId))
//            {
//                _chats[connectionId] = new List<ChatMessageDto>
//                {
//                    new ChatMessageDto
//                    {
//                        UserName = "Sistema",
//                        Message = "Olá! Sou o assistente virtual. Como posso ajudar você hoje?",
//                        Timestamp = DateTime.Now,
//                        IsFromClient = false
//                    }
//                };

//                // ✅ Salvar mensagem inicial no banco
//                //await SalvarMensagemNoBanco("Sistema", "Tecnico", "Olá! Sou o assistente virtual. Como posso ajudar você hoje?", chamadoId.Value);
//            }

//            await Groups.AddToGroupAsync(connectionId, "Clientes");
//            await Clients.Caller.SendAsync("ReceiveChatHistory", _chats[connectionId]);
//            await Clients.Group("Tecnicos").SendAsync("NewClientConnected", userName, connectionId, chamadoId);
//        }

//        // Técnico se conecta
//        public async Task JoinAsTechnician(string technicianName)
//        {
//            var connectionId = Context.ConnectionId;

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = technicianName,
//                UserType = "Tecnico",
//                ConnectionTime = DateTime.Now
//            };

//            await Groups.AddToGroupAsync(connectionId, "Tecnicos");

//            // Envia clientes ativos para o técnico
//            var activeClients = _connections.Values
//                .Where(c => c.UserType == "Cliente")
//                .Select(c => new { 
//                    c.UserId, 
//                    c.UserName, 
//                    ConnectionTime = c.ConnectionTime,
//                    ChamadoId = c.ChamadoId 
//                })
//                .ToList();

//            await Clients.Caller.SendAsync("ReceiveActiveClients", activeClients);
//        }

//        // Cliente envia mensagem para técnicos
//        public async Task SendMessageToTechnicians(string message)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(connectionId, out var user) && user.UserType == "Cliente")
//            {
//                var chatMessage = new ChatMessageDto
//                {
//                    UserName = user.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = true
//                };

//                // Salva no histórico em memória
//                if (_chats.ContainsKey(connectionId))
//                {
//                    _chats[connectionId].Add(chatMessage);
//                }

//                // ✅ Salvar mensagem no banco
//                if (user.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco(user.UserName, "Cliente", message, user.ChamadoId.Value);
//                }

//                // Envia para todos os técnicos
//                await Clients.Group("Tecnicos").SendAsync("ReceiveClientMessage",
//                    user.UserId, user.UserName, message, DateTime.Now, user.ChamadoId);

//                // Confirmação para o cliente
//                await Clients.Caller.SendAsync("ReceiveOwnMessage", message, DateTime.Now);
//            }
//        }

//        // Técnico envia mensagem para cliente específico
//        public async Task SendMessageToClient(string clientConnectionId, string message)
//        {
//            var technicianConnectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(technicianConnectionId, out var technician) &&
//                _connections.TryGetValue(clientConnectionId, out var client))
//            {
//                var chatMessage = new ChatMessageDto
//                {
//                    UserName = technician.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = false
//                };

//                // Salva no histórico em memória
//                if (_chats.ContainsKey(clientConnectionId))
//                {
//                    _chats[clientConnectionId].Add(chatMessage);
//                }

//                // ✅ Salvar mensagem no banco
//                if (client.ChamadoId.HasValue)
//                {
//                    await SalvarMensagemNoBanco(technician.UserName, "Tecnico", message, client.ChamadoId.Value);
//                }

//                // Envia para o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ReceiveTechnicianMessage",
//                    technician.UserName, message, DateTime.Now);

//                // Confirmação para o técnico
//                await Clients.Caller.SendAsync("ReceiveOwnMessageToClient",
//                    clientConnectionId, message, DateTime.Now);
//            }
//        }

//        // Técnico solicita histórico do cliente
//        public async Task RequestClientChatHistory(string clientConnectionId)
//        {
//            if (_chats.TryGetValue(clientConnectionId, out var chatHistory))
//            {
//                await Clients.Caller.SendAsync("ReceiveClientChatHistory", clientConnectionId, chatHistory);
//            }
//        }

//        // ✅ MÉTODO PARA CRIAR CHAMADO NO BANCO
//        private async Task<int> CriarChamado(string userName)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                // Buscar usuário pelo nome (ou criar lógica alternativa)
//                var usuario = await context.Usuarios
//                    .FirstOrDefaultAsync(u => u.Nome == userName);

//                if (usuario == null)
//                {
//                    // Se não encontrar, usar o primeiro usuário ou criar lógica alternativa
//                    usuario = await context.Usuarios.FirstOrDefaultAsync();
//                    if (usuario == null)
//                    {
//                        // Criar usuário padrão se não existir
//                        usuario = new RegistroUsuarios
//                        {
//                            Nome = userName,
//                            Email = $"{userName.ToLower()}@temp.com",
//                            Senha = "temp",
//                            Tipo = "Usuario"
//                        };
//                        context.Usuarios.Add(usuario);
//                        await context.SaveChangesAsync();
//                    }
//                }

//                var chamado = new Chamado
//                {
//                    Titulo = "Suporte via Chat",
//                    Descricao = "Chamado aberto via chat online",
//                    UsuarioId = usuario.IdUsuario,
//                    Status = "Aberto",
//                    TipoChat = "Tecnico",
//                    DataAbertura = DateTime.Now
//                };

//                context.Chamados.Add(chamado);
//                await context.SaveChangesAsync();

//                return chamado.IdChamado;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao criar chamado: {ex.Message}");
//                return 0; // Retorna 0 em caso de erro
//            }
//        }

//        // ✅ MÉTODO PARA SALVAR MENSAGEM NO BANCO
//        private async Task SalvarMensagemNoBanco(string remetente, string tipoRemetente, string mensagem, int chamadoId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var chatMessage = new ChatMessage
//                {
//                    Conteudo = mensagem,
//                    TipoRemetente = tipoRemetente,
//                    ChamadoId = chamadoId,
//                    DataEnvio = DateTime.Now
//                };

//                context.ChatMessages.Add(chatMessage);
//                await context.SaveChangesAsync();

//                Console.WriteLine($"Mensagem salva no banco: {mensagem}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao salvar mensagem no banco: {ex.Message}");
//            }
//        }

//        // ✅ MÉTODO PARA CARREGAR HISTÓRICO DO BANCO
//        public async Task CarregarHistoricoDoBanco(string connectionId, int chamadoId)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var mensagens = await context.ChatMessages
//                    .Where(m => m.ChamadoId == chamadoId)
//                    .OrderBy(m => m.DataEnvio)
//                    .ToListAsync();

//                var historicoDto = mensagens.Select(m => new ChatMessageDto
//                {
//                    UserName = m.TipoRemetente == "Cliente" ? "Você" : m.TipoRemetente,
//                    Message = m.Conteudo,
//                    Timestamp = m.DataEnvio,
//                    IsFromClient = m.TipoRemetente == "Cliente"
//                }).ToList();

//                await Clients.Caller.SendAsync("ReceiveChatHistory", historicoDto);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao carregar histórico: {ex.Message}");
//            }
//        }

//        // Cliente desconecta
//        public override async Task OnDisconnectedAsync(Exception? exception)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryRemove(connectionId, out var user))
//            {
//                if (user.UserType == "Cliente")
//                {
//                    await Clients.Group("Tecnicos").SendAsync("ClientDisconnected", connectionId);

//                    // ✅ Atualizar status do chamado quando cliente desconectar
//                    if (user.ChamadoId.HasValue)
//                    {
//                        await AtualizarStatusChamado(user.ChamadoId.Value, "Finalizado");
//                    }
//                }

//                await Groups.RemoveFromGroupAsync(connectionId,
//                    user.UserType == "Cliente" ? "Clientes" : "Tecnicos");
//            }

//            await base.OnDisconnectedAsync(exception);
//        }

//        // ✅ MÉTODO PARA ATUALIZAR STATUS DO CHAMADO
//        private async Task AtualizarStatusChamado(int chamadoId, string status)
//        {
//            using var scope = _serviceProvider.CreateScope();
//            var context = scope.ServiceProvider.GetRequiredService<BancoContext>();

//            try
//            {
//                var chamado = await context.Chamados.FindAsync(chamadoId);
//                if (chamado != null)
//                {
//                    chamado.Status = status;
//                    chamado.DataAtualizacao = DateTime.Now;
//                    await context.SaveChangesAsync();
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Erro ao atualizar chamado: {ex.Message}");
//            }
//        }
//    }
//}





//using Microsoft.AspNetCore.SignalR;
//using System.Collections.Concurrent;

//namespace Sistema_Suporte
//{
//    public class ChatHub : Hub
//    {
//        private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
//        private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _chats = new();

//        // ✅ MUDAR NOME para ChatMessageDto (Data Transfer Object)
//        public class ChatMessageDto
//        {
//            public string UserName { get; set; } = string.Empty;
//            public string Message { get; set; } = string.Empty;
//            public DateTime Timestamp { get; set; }
//            public bool IsFromClient { get; set; }
//        }

//        public class UserConnection
//        {
//            public string UserId { get; set; } = string.Empty;
//            public string UserName { get; set; } = string.Empty;
//            public string UserType { get; set; } = string.Empty;
//            public DateTime ConnectionTime { get; set; }
//        }

//        // Cliente se conecta
//        public async Task JoinAsClient(string userName)
//        {
//            var connectionId = Context.ConnectionId;

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = userName,
//                UserType = "Cliente",
//                ConnectionTime = DateTime.Now
//            };

//            // Cria conversa para o cliente
//            if (!_chats.ContainsKey(connectionId))
//            {
//                _chats[connectionId] = new List<ChatMessageDto> // ✅ Usar ChatMessageDto
//                {
//                    new ChatMessageDto // ✅ Usar ChatMessageDto
//                    {
//                        UserName = "Sistema",
//                        Message = "Olá! Sou o assistente virtual. Como posso ajudar você hoje?",
//                        Timestamp = DateTime.Now,
//                        IsFromClient = false
//                    }
//                };
//            }

//            await Groups.AddToGroupAsync(connectionId, "Clientes");
//            await Clients.Caller.SendAsync("ReceiveChatHistory", _chats[connectionId]);
//            await Clients.Group("Tecnicos").SendAsync("NewClientConnected", userName, connectionId);
//        }

//        // Técnico se conecta
//        public async Task JoinAsTechnician(string technicianName)
//        {
//            var connectionId = Context.ConnectionId;

//            _connections[connectionId] = new UserConnection
//            {
//                UserId = connectionId,
//                UserName = technicianName,
//                UserType = "Tecnico",
//                ConnectionTime = DateTime.Now
//            };

//            await Groups.AddToGroupAsync(connectionId, "Tecnicos");

//            // Envia clientes ativos para o técnico
//            var activeClients = _connections.Values
//                .Where(c => c.UserType == "Cliente")
//                .Select(c => new { c.UserId, c.UserName, ConnectionTime = c.ConnectionTime })
//                .ToList();

//            await Clients.Caller.SendAsync("ReceiveActiveClients", activeClients);
//        }

//        // Cliente envia mensagem para técnicos
//        public async Task SendMessageToTechnicians(string message)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(connectionId, out var user) && user.UserType == "Cliente")
//            {
//                var chatMessage = new ChatMessageDto // ✅ Usar ChatMessageDto
//                {
//                    UserName = user.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = true
//                };

//                // Salva no histórico
//                if (_chats.ContainsKey(connectionId))
//                {
//                    _chats[connectionId].Add(chatMessage);
//                }

//                // Envia para todos os técnicos
//                await Clients.Group("Tecnicos").SendAsync("ReceiveClientMessage",
//                    user.UserId, user.UserName, message, DateTime.Now);

//                // Confirmação para o cliente
//                await Clients.Caller.SendAsync("ReceiveOwnMessage", message, DateTime.Now);
//            }
//        }

//        // Técnico envia mensagem para cliente específico
//        public async Task SendMessageToClient(string clientConnectionId, string message)
//        {
//            var technicianConnectionId = Context.ConnectionId;

//            if (_connections.TryGetValue(technicianConnectionId, out var technician) &&
//                _connections.TryGetValue(clientConnectionId, out var client))
//            {
//                var chatMessage = new ChatMessageDto // ✅ Usar ChatMessageDto
//                {
//                    UserName = technician.UserName,
//                    Message = message,
//                    Timestamp = DateTime.Now,
//                    IsFromClient = false
//                };

//                // Salva no histórico
//                if (_chats.ContainsKey(clientConnectionId))
//                {
//                    _chats[clientConnectionId].Add(chatMessage);
//                }

//                // Envia para o cliente
//                await Clients.Client(clientConnectionId).SendAsync("ReceiveTechnicianMessage",
//                    technician.UserName, message, DateTime.Now);

//                // Confirmação para o técnico
//                await Clients.Caller.SendAsync("ReceiveOwnMessageToClient",
//                    clientConnectionId, message, DateTime.Now);
//            }
//        }

//        // Técnico solicita histórico do cliente
//        public async Task RequestClientChatHistory(string clientConnectionId)
//        {
//            if (_chats.TryGetValue(clientConnectionId, out var chatHistory))
//            {
//                await Clients.Caller.SendAsync("ReceiveClientChatHistory", clientConnectionId, chatHistory);
//            }
//        }

//        // Cliente desconecta
//        public override async Task OnDisconnectedAsync(Exception? exception)
//        {
//            var connectionId = Context.ConnectionId;

//            if (_connections.TryRemove(connectionId, out var user))
//            {
//                if (user.UserType == "Cliente")
//                {
//                    await Clients.Group("Tecnicos").SendAsync("ClientDisconnected", connectionId);
//                }

//                await Groups.RemoveFromGroupAsync(connectionId,
//                    user.UserType == "Cliente" ? "Clientes" : "Tecnicos");
//            }

//            await base.OnDisconnectedAsync(exception);
//        }
//    }
//}




//using Microsoft.AspNetCore.SignalR;
//using Sistema_Suporte.Data;
//using Sistema_Suporte.Models;
//using System.Threading.Tasks;

//namespace Sistema_Suporte
//{

//    public class ChatHub : Hub
//    {

//        public async Task SendMessage(string message)
//        {
//            await Clients.All.SendAsync("ReceiveMessage", message);
//        }
//    }
//}
