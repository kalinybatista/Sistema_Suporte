# 🎫 Sistema de Suporte Técnico

Sistema web desenvolvido para gerenciamento de chamados e solicitações de suporte, permitindo o controle eficiente de atendimentos, acompanhamento de solicitações e comunicação entre usuários e administradores.

---

## 📋 Sobre o Projeto

O Sistema de Suporte Técnico foi desenvolvido com o objetivo de centralizar o atendimento aos usuários, organizando chamados, acompanhando o andamento das solicitações e facilitando a gestão do suporte.

A aplicação oferece uma interface intuitiva para abertura de chamados, acompanhamento de status e gerenciamento administrativo.

---

## ✨ Funcionalidades

### 👤 Área do Usuário

* Cadastro e autenticação de usuários
* Abertura de chamados
* Consulta de chamados abertos
* Acompanhamento do status das solicitações
* Histórico de atendimentos
* Atualização de informações pessoais

### 👨‍💼 Área Administrativa

* Gerenciamento de usuários
* Gerenciamento de chamados
* Alteração de status dos chamados
* Resposta às solicitações
* Histórico completo de atendimentos
* Dashboard administrativo

### 📊 Controle de Chamados

* Chamados Abertos
* Em Atendimento
* Resolvidos
* Encerrados

---

## 🚀 Tecnologias Utilizadas

| Tecnologia         | Finalidade           |
| ------------------ | -------------------- |
| ASP.NET MVC        | Backend              |
| C#                 | Linguagem principal  |
| Entity Framework   | ORM                  |
| SQL Server / MySQL | Banco de Dados       |
| HTML5              | Estrutura            |
| CSS3               | Estilização          |
| Bootstrap          | Interface Responsiva |
| JavaScript         | Interatividade       |

---

## 🏗️ Arquitetura do Projeto

O sistema foi desenvolvido utilizando o padrão MVC (Model-View-Controller), promovendo organização, manutenção e escalabilidade.

```text
ProjetoSuporte/
│
├── Controllers/
├── Models/
├── Views/
├── Data/
├── Services/
├── Repository/
└── wwwroot/
```

---

## 🔒 Segurança

* Autenticação de usuários
* Controle de acesso por perfil
* Proteção contra CSRF
* Validação de formulários
* Criptografia de senhas

---

## 📱 Responsividade

O sistema foi desenvolvido para funcionar em diferentes dispositivos:

* 📱 Smartphones
* 📲 Tablets
* 💻 Notebooks
* 🖥️ Computadores Desktop

---

## ⚙️ Instalação

### Clone o repositório

```bash
git clone https://github.com/kalinybatista/sistema-suporte.git
```

### Acesse a pasta

```bash
cd sistema-suporte
```

### Configure a conexão com o banco

Edite o arquivo:

```text
appsettings.json
```

### Execute as migrations

```bash
dotnet ef database update
```

### Execute o projeto

```bash
dotnet run
```

---

## 📸 Capturas de Tela

Adicione imagens do sistema nesta seção.

```text
/docs/login.png
/docs/dashboard.png
/docs/chamados.png
/docs/atendimento.png
```

---

## 🎯 Objetivos do Projeto

* Organização de solicitações de suporte
* Controle de atendimentos
* Centralização da comunicação
* Melhor acompanhamento dos chamados
* Facilidade de gerenciamento administrativo

---

## 👨‍💻 Desenvolvedor

Desenvolvido por Kaliny.

Curso: Análise e Desenvolvimento de Sistemas

GitHub: https://github.com/kalinybatista

---
