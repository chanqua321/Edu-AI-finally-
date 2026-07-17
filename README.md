# EduAI (PRN222 — Assignment 2)

Nền tảng web hỗ trợ học tập thông minh: quản lý môn học/tài liệu theo chương, index tài liệu (chunk + embedding), chatbot RAG hỏi đáp theo nội dung tài liệu, gói cước AI và thanh toán VNPAY.

## Tính năng chính

| Module | Mô tả |
|--------|-------|
| **Môn học & tài liệu** | Quản lý môn, chương, bài học; upload PDF/DOCX/PPTX/TXT; index nền (extract → chunk → embedding) |
| **Chat RAG** | Hỏi đáp theo phạm vi môn học; retrieval semantic + keyword; citation; realtime qua SignalR |
| **Multi-provider AI** | Gemini (cloud) và Ollama (local); student chọn model trên UI chat |
| **Gói cước & thanh toán** | Free / Premium / Enterprise; quota Gemini theo tháng, Ollama theo ngày; VNPAY sandbox |
| **Cấu hình hệ thống** | Admin chỉnh chunk, TopK, upload, quota, gói cước, provider mặc định |
| **Báo cáo** | Phân tích hệ thống (KPI LMS) và Benchmark AI (so sánh Gemini vs Ollama) |
| **Audit & Realtime** | Audit log; SignalR hubs (User, Subject, Notification, Chat) |

## Tech stack

- **Backend**: ASP.NET Core 8 (Razor Pages), EF Core 8, Identity (Cookie), SignalR
- **Database**: SQL Server (LocalDB)
- **AI**: Gemini API (generation + embedding), Ollama (generation local)
- **Thanh toán**: VNPAY sandbox
- **Khác**: MailKit (SMTP), PdfPig, OpenXML

## Cấu trúc solution

```
src/
├── EduAI.Web/           # Razor Pages, Hubs, Middleware, Seed, Indexing Worker
├── EduAI.BusinessLogic/ # Services, AI providers, helpers
└── EduAI.Model/         # Entities, DbContext, Repository/UoW, Migrations, DTOs
```

Luồng bắt buộc:

```
PageModel / Hub → Service → UnitOfWork / Repository → AppDbContext → SQL Server
```

## Yêu cầu môi trường

- .NET SDK **8.x**
- SQL Server **LocalDB** (hoặc SQL Server khác nếu đổi connection string)
- (Tuỳ chọn) [Ollama](https://ollama.com/) chạy local nếu dùng provider Ollama
- (Tuỳ chọn) Visual Studio 2022 / JetBrains Rider

## Cấu hình

File mẫu: `src/EduAI.Web/appsettings.example.json` — copy thành `appsettings.json` hoặc dùng User Secrets.

### Database

- Connection string: `ConnectionStrings:DefaultConnection`
- Lần chạy đầu: app tự **migrate** và **seed** roles + tài khoản demo + gói cước

Reset dữ liệu demo (tuỳ chọn):

```json
"Database": { "ResetOnStartup": true }
```

Khi bật, app xoá DB, chạy lại migrations, xoá thư mục upload và seed lại. **Nên tắt lại sau khi reset.**

### Secrets (khuyến nghị)

Không commit password/API key vào git. Dùng User Secrets hoặc biến môi trường:

```powershell
cd src/EduAI.Web
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "<your-gemini-api-key>"
dotnet user-secrets set "EmailSettings:Password" "<your-smtp-app-password>"
dotnet user-secrets set "VNPay:vnp_TmnCode" "<your-vnpay-tmn-code>"
dotnet user-secrets set "VNPay:vnp_HashSecret" "<your-vnpay-hash-secret>"
```

### Các section cấu hình quan trọng

| Section | Mục đích |
|---------|----------|
| `Gemini` | API key, model chat/embedding |
| `AIProviders` | Ollama base URL, model |
| `AiRuntime` | Chunk mode, temperature, token limits, tỷ giá USD/VND |
| `VNPay` | Sandbox/production payment gateway |
| `EmailSettings` | SMTP (xác thực email Teacher) |
| `AppSettings` | Base URL, upload path, max upload bytes |

## Chạy ứng dụng

```powershell
cd src/EduAI.Web
dotnet restore
dotnet run
```

Mặc định: `https://localhost:7014`

Hoặc mở solution `EduAI.slnx` trong Visual Studio, set startup project `EduAI.Web`.

### Ollama (tuỳ chọn)

```powershell
ollama pull llama3.2
ollama serve
```

Cấu hình mặc định trỏ tới `http://localhost:11434`.

## Entity Framework

Migrations: `src/EduAI.Model/Migrations`

```powershell
# Update database
dotnet ef database update --project src/EduAI.Model --startup-project src/EduAI.Web

# Tạo migration mới
dotnet ef migrations add <MigrationName> --project src/EduAI.Model --startup-project src/EduAI.Web
```

## Tài khoản demo (seed)

| Email | Mật khẩu | Vai trò |
|-------|----------|---------|
| admin@gmail.com | 12345 | Admin |
| teacher@gmail.com | 12345 | Teacher |
| student@gmail.com | 12345 | Student |

## Gói cước mặc định

| Gói | Giá | Gemini/tháng | Ollama/ngày |
|-----|-----|--------------|-------------|
| Free | 0 | 0 | 1 |
| Premium | 100.000 VND / 30 ngày | 40 | 5 |
| Enterprise | 500.000 VND / 30 ngày | 150 | 20 |

Admin có thể chỉnh tại **Cấu hình hệ thống → Payment**.

## Vai trò & quyền truy cập

| Vai trò | Quyền chính |
|---------|-------------|
| **Admin** | User, môn học, cấu hình hệ thống, gói cước, audit, báo cáo đầy đủ |
| **Teacher** | Chương/bài/tài liệu môn được gán; báo cáo phân tích theo môn |
| **Student** | Study tài liệu, chat RAG, chọn model AI, mua/nâng cấp gói cước |

## Endpoint chính

| Path | Mô tả |
|------|-------|
| `/Account/*` | Đăng nhập, đăng ký, profile |
| `/Subjects`, `/Chapters`, `/Documents` | Quản lý nội dung học |
| `/Chat`, `/hubs/chat` | Chat RAG realtime |
| `/Payment/Packages`, `/Payment/Return` | Gói cước & hóa đơn |
| `/Settings/System` | Cấu hình Admin |
| `/Reports`, `/Reports/Benchmarks` | Báo cáo & benchmark AI |
| `/AuditLogs` | Nhật ký hệ thống |

## Tài liệu dự án

- [`docs/EduAI-Dac-Ta-Nghiep-Vu.md`](docs/EduAI-Dac-Ta-Nghiep-Vu.md) — Đặc tả nghiệp vụ v2.0 (đầy đủ)
- [`docs/EduAI-Dac-Ta-Mon-Hoc.md`](docs/EduAI-Dac-Ta-Mon-Hoc.md) — Đặc tả v1 (lịch sử)
- [`docs/EduAI-Architecture.drawio`](docs/EduAI-Architecture.drawio) — Sơ đồ kiến trúc (Draw.io)

## Kiểm thử nhanh (smoke)

1. Student Free: chỉ Ollama 1 lượt/ngày; Gemini disabled.
2. Teacher upload tài liệu → chờ index → Student chat đúng môn đó.
3. Mua Premium qua VNPAY sandbox → nhận hóa đơn → vào Chat.
4. Admin vào `/Settings/System` chỉnh quota gói → Student thấy cập nhật trên Packages/Profile/Chat.
5. `/Reports/Benchmarks`: so sánh latency/success rate Gemini vs Ollama.
