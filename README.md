# LiveStock Chat

Real-time chat application with stock quote integration. Built with .NET 8, SignalR, and RabbitMQ.

## Features

- User authentication (register/login)
- Real-time chat using SignalR
- Stock quotes via `/stock=CODE` command (e.g., `/stock=AAPL.US`)
- Message persistence (last 50 messages)
- Decoupled bot architecture using RabbitMQ

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  LiveStock.Web                   LiveStock.Bot                  │
│                                                                 │
│  Browser ─► SignalR Hub                                         │
│              │                                                  │
│              ├── /stock=XXX ──► [stock-commands] ──► Worker     │
│              │                                          │       │
│              │                                     stooq.com    │
│              │                                          │       │
│  StockResponseConsumer ◄────── [stock-responses] ◄──────┘       │
│              │                                                  │
│              └──► SignalR ──► All Browsers                      │
└─────────────────────────────────────────────────────────────────┘
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for RabbitMQ)

## Getting Started

### 1. Start RabbitMQ

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management
```

Management UI: http://localhost:15672 (guest/guest)

### 2. Initialize Database

```bash
cd src/LiveStock.Web
dotnet ef database update
```

### 3. Run the Applications

**Terminal 1 - Web Application:**
```bash
dotnet run --project src/LiveStock.Web
```

**Terminal 2 - Stock Bot:**
```bash
dotnet run --project src/LiveStock.Bot
```

### 4. Access the Application

Open https://localhost:5001 (or the URL shown in terminal)

1. Register a new account
2. Navigate to Chat
3. Send messages or use `/stock=AAPL.US` to get stock quotes

## Demo Accounts

| Email | Password |
|-------|----------|
| alice@livestock.app | LiveStock2025! |
| bob@livestock.app | LiveStock2025! |

Open two browser windows, login with different users, and start chatting.

## Running Tests

```bash
dotnet test
```

## Project Structure

```
LiveStock/
├── src/
│   ├── LiveStock.Web/          # ASP.NET Core web app
│   │   ├── Data/               # EF Core DbContext
│   │   ├── Hubs/               # SignalR ChatHub
│   │   ├── Models/             # Domain models
│   │   ├── Pages/              # Razor Pages
│   │   └── Services/           # RabbitMQ services
│   └── LiveStock.Bot/          # Worker service (stock bot)
└── tests/
    └── LiveStock.Tests/        # Unit tests
```

## Tech Stack

- ASP.NET Core 8 + Razor Pages
- ASP.NET Core Identity (authentication)
- SignalR (real-time communication)
- Entity Framework Core + SQLite
- RabbitMQ (message broker)
- xUnit + Moq (testing)

## API Reference

### SignalR Hub Methods

| Method | Description |
|--------|-------------|
| `SendMessage(message)` | Send a chat message or stock command |
| `GetRecentMessages()` | Get last 50 messages |

### SignalR Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `ReceiveMessage` | user, message, timestamp | New message received |

### Stock Command

```
/stock=CODE
```

Examples: `/stock=AAPL.US`, `/stock=MSFT.US`, `/stock=GOOGL.US`

The bot fetches data from [stooq.com](https://stooq.com) and returns the closing price.
