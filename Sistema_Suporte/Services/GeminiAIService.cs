using System.Text;
using System.Text.Json;

namespace Sistema_Suporte.Services
{
    public interface IGeminiAIService
    {
        Task<string> SendMessageAsync(string message, string conversationHistory = "");
    }

    public class GeminiAIService : IGeminiAIService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GeminiAIService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<string> SendMessageAsync(string userMessage, string conversationHistory)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return "Serviço de IA não configurado.";

            // 🔥 PROMPT ORGANIZADO PADRÃO (o único que será enviado)
            string systemRules = @"
Você é um assistente técnico que SEMPRE responde neste formato simples e organizado:

Título:
Resumo em 1 linha.

Tópicos:
- ponto 1
- ponto 2
- ponto 3

Passos:
1. passo 1
2. passo 2
3. passo 3

Regras:
- Não use markdown.
- Não use negrito.
- Não escreva parágrafos longos.
- Nunca envie blocos de texto colados.
- Sempre manter tudo limpo, direto e organizado.
- Responder apenas com texto simples.
";

            // 👇 Só isto vai para o modelo. Simples e direto.
            string mensagemFinal = @$"
{systemRules}

Histórico:
{conversationHistory}

Pergunta:
{userMessage}

Resposta:";

            return await SendMessageViaRestApi(mensagemFinal);
        }


        private async Task<string> SendMessageViaRestApi(string finalPrompt)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = finalPrompt }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var gemini = JsonSerializer.Deserialize<GeminiResponse>(responseText, options);

                var text = gemini?
                    .Candidates?.FirstOrDefault()?
                    .Content?.Parts?.FirstOrDefault()?
                    .Text;

                return string.IsNullOrWhiteSpace(text)
                    ? "Não consegui gerar uma resposta."
                    : text.Trim();
            }
            catch
            {
                return "Erro ao acessar a IA.";
            }
        }

        // MODELOS PARA DESERIALIZAÇÃO
        public class GeminiResponse
        {
            public Candidate[] Candidates { get; set; } = Array.Empty<Candidate>();
        }

        public class Candidate
        {
            public Content Content { get; set; } = new Content();
        }

        public class Content
        {
            public Part[] Parts { get; set; } = Array.Empty<Part>();
        }

        public class Part
        {
            public string Text { get; set; } = string.Empty;
        }
    }
}

