# ĐẶC TẢ NGHIỆP VỤ — EDUAI

**Môn:** PRN222 — Assignment 2  
**Hệ thống:** EduAI — Chatbot RAG, quản lý tài liệu & gói cước AI  
**Phiên bản tài liệu:** 2.0  
**Ngày cập nhật:** 17/07/2026  

> Tài liệu này thay thế nội dung nghiệp vụ cũ trong `EduAI-Dac-Ta-Mon-Hoc.md` (v1.0 — 19/06/2026).  
> Phần **A** = đặc tả nghiệp vụ hiện tại. Phần **B** = kết quả quét logic / bất ổn cần xử lý.

---

# PHẦN A — ĐẶC TẢ NGHIỆP VỤ

## 1. Giới thiệu

EduAI là nền tảng web hỗ trợ học tập thông minh:

1. Giáo viên/Admin quản lý môn học và tải tài liệu.
2. Hệ thống tự index (chunk + embedding) để phục vụ RAG.
3. Sinh viên hỏi đáp theo đúng phạm vi môn học đã index.
4. Hỗ trợ nhiều model sinh câu trả lời (**Gemini**, **Ollama**).
5. Quản lý gói cước + hạn mức AI theo từng model.
6. Báo cáo: **Phân tích hệ thống** và **Benchmark AI**.

### 1.1. Kiến trúc

| Layer | Project | Trách nhiệm |
|-------|---------|-------------|
| Presentation | `EduAI.Web` | Razor Pages, SignalR Hubs, Middleware, Seed, Indexing Worker |
| Business | `EduAI.BusinessLogic` | Services, AI providers, helpers |
| Data | `EduAI.Model` | Entities, EF Core, Repository/UoW, Migrations, DTOs |

Luồng bắt buộc:

```
PageModel / Hub → Service → UnitOfWork / Repository → AppDbContext → SQL Server
```

### 1.2. Công nghệ

ASP.NET Core 8 (Razor Pages), EF Core 8, Identity (Cookie), SignalR, SQL Server, Gemini API, Ollama (local), VNPAY, MailKit, PdfPig/OpenXML.

---

## 2. Vai trò người dùng

| Vai trò | Quyền chính |
|---------|-------------|
| **Admin** | Quản lý user, môn học, cấu hình hệ thống, gói cước, audit, báo cáo đầy đủ (kể cả tài chính), xem phiên chat |
| **Teacher** | Quản lý chương/bài/tài liệu môn được gán; xem báo cáo phân tích theo môn mình; không mua gói |
| **Student** | Study tài liệu, chat RAG, chọn model AI, mua/nâng cấp gói cước, xem quota còn lại |

### 2.1. Quy tắc tài khoản

| Quy tắc | Chi tiết |
|---------|----------|
| Đăng ký tự phục vụ | Chỉ tạo **Student**; tự gán gói Free |
| Admin tạo user | Teacher/Student; mật khẩu tạm; `MustChangePassword = true` |
| Teacher | Phải xác thực email trước khi đăng nhập |
| Student | Không bắt buộc xác thực email (demo) |
| Khóa mềm | `IsActive = false`; middleware buộc logout |
| Đổi mật khẩu bắt buộc | Middleware chặn trang web đến khi đổi xong (trừ một số path exempt) |
| Admin Profile | Chỉ xem, không tự sửa qua Profile |

### 2.2. Tài khoản demo (seed)

| Email | Mật khẩu | Vai trò |
|-------|----------|---------|
| admin@gmail.com | 12345 | Admin |
| teacher@gmail.com | 12345 | Teacher |
| student@gmail.com | 12345 | Student |

---

## 3. Module môn học & tài liệu

### 3.1. Môn học (`/Subjects`)

| Quy tắc | Chi tiết |
|---------|----------|
| 1 môn — 1 GV hiện tại | `Subject.TeacherId` |
| Lịch sử gán | `SubjectAssignment` lưu quá khứ |
| Ẩn môn | `IsActive = false` → Student không thấy |
| Student thấy môn | Chỉ môn active và đã có tài liệu/index (tùy màn hình) |

### 3.2. Chương / Bài học / Tài liệu

| Quy tắc | Chi tiết |
|---------|----------|
| Định dạng | PDF, DOCX, PPTX, TXT (theo SystemSettings) |
| Upload | Teacher (môn được gán); giới hạn dung lượng theo cấu hình |
| Index nền | Queue → Worker → extract → chunk → Gemini embedding |
| Trạng thái | Pending / Processing / Indexed / Failed |
| Chat sẵn sàng | Chỉ khi môn đã có chunk (đã index) |

### 3.3. Luồng indexing

```
Upload file
  → Lưu disk
  → DocumentIndexingQueue
  → DocumentIndexingWorker
      → Extract text
      → Chunk (DefaultChunkSize / DefaultChunkOverlap từ SystemSettings)
      → Embed bằng Gemini (embedding model từ AiRuntime / SystemSettings)
      → Lưu DocumentChunk + DocumentEmbedding
  → SignalR cập nhật tiến độ
```

---

## 4. Module Chat RAG (`/Chat`)

### 4.1. Quy tắc nghiệp vụ

| Quy tắc | Chi tiết |
|---------|----------|
| Phạm vi môn | Retrieval **chỉ** trong `SubjectId` của phiên |
| 1 phiên = 1 môn | Không hỏi chéo môn |
| Chọn model | Student chọn Gemini hoặc Ollama trên UI chat |
| Kiểm tra quota | Trước khi trả lời AI, kiểm tra quota theo model đã chọn |
| Retrieval | Ưu tiên semantic (cosine); lỗi/không có → keyword |
| Citation | Bật theo SystemSettings; chỉ hiện khi score ≥ 0.55 |
| Câu meta (xin chào…) | Trả lời giới thiệu ngắn, **không** gọi generation nặng / không ghi usage (xem phần B) |
| Realtime | SignalR Hub `/hubs/chat` |

### 4.2. Luồng hỏi đáp

```
Student gửi câu hỏi + ProviderId
  → ChatHub.SendQuestion
  → ChatService.SendMessageAsync
      → CheckProviderQuotaAsync(provider)
      → Nếu hết Gemini và còn Ollama → trả lỗi + gợi ý fallback (UI confirm)
      → Lưu tin user
      → Embed câu hỏi (luôn Gemini embedding)
      → Tìm chunk liên quan (TopK)
      → IAiGenerationProvider (Gemini / Ollama) sinh câu trả lời
      → Lưu tin assistant + AiUsageLog
  → UI cập nhật tin nhắn + quota còn lại
```

### 4.3. Multi-provider

| Provider | Vai trò | Quota |
|----------|---------|-------|
| Gemini 2.5 Flash | Generation cloud | Theo **tháng** (`MonthlyGeminiQuestions`) |
| Ollama (Local) | Generation local | Theo **ngày** (`DailyOllamaQuestions`), refill theo giờ reset |
| Gemini Embedding | Index + retrieval | Không tính vào quota câu hỏi user |

---

## 5. Module gói cước & thanh toán (`/Payment`)

### 5.1. Gói mặc định (seed)

| Gói | Giá | Gemini/tháng | Ollama/ngày | Ghi chú |
|-----|-----|--------------|-------------|---------|
| Free | 0 | 0 | 1 | Không hỗ trợ Gemini |
| Premium | 100.000 VND / 30 ngày | 40 | 5 | Gói phổ biến |
| Enterprise | 500.000 VND / 30 ngày | 150 | 20 | Quota lớn hơn |

Admin có thể chỉnh quota từng gói tại **Cấu hình hệ thống → Payment**.

### 5.2. Quy tắc mua / kích hoạt

| Quy tắc | Chi tiết |
|---------|----------|
| Thứ bậc | Free < Premium < Enterprise |
| Không hạ cấp | Đang Premium không kích hoạt lại Free |
| Gói hiện tại | Nút disabled “Gói Hiện Tại” |
| Free | Kích hoạt trực tiếp (không VNPAY) |
| Premium/Enterprise | Tạo transaction → redirect VNPAY → Return |
| Thanh toán thành công | Hiện **hóa đơn**; nút vào Chat / xem gói |
| Đổi gói cao hơn | Subscription cũ → Expired; tạo subscription mới |
| Gia hạn cùng gói | Cộng thêm `DurationDays` vào `EndDate` |
| Hiển thị quota | Packages + Profile + dropdown chat |

### 5.3. Hóa đơn (Payment/Return)

Khi VNPAY trả về thành công, trang hóa đơn hiển thị tối thiểu:

- Trạng thái, mã đơn, mã VNPAY
- Gói, số tiền, ngân hàng (nếu có)
- Thời hạn subscription
- Nút **Vào Chat AI** / **Xem gói cước**

---

## 6. Module cấu hình hệ thống (`/Settings/System`) — Admin

| Nhóm | Nội dung chính |
|------|----------------|
| AI & RAG | Provider mặc định (UI), TopK, chunk size/overlap, citation |
| Upload | Max size, đuôi file |
| Chat & Quota | Timezone, giờ reset quota ngày, có tính request fail vào quota không |
| Payment | Sửa gói: giá, thời hạn, Gemini/tháng, Ollama/ngày, active |
| Benchmark & Logging | Bật/tắt log latency/token/cost (một phần đang chưa enforce — xem phần B) |
| General | Đơn giá token để ước tính chi phí |

---

## 7. Module báo cáo

### 7.1. Phân tích hệ thống (`/Reports`)

**Mục tiêu:** KPI nghiệp vụ LMS / vận hành.

- Users, courses, documents, tokens, AI cost (Admin), revenue (Admin)
- Xu hướng upload, token theo môn, chat activity, top documents/chunks
- **Không** so sánh Gemini vs Ollama

### 7.2. Benchmark AI (`/Reports/Benchmarks`) — Admin

**Mục tiêu:** So sánh provider.

- KPI: requests, success rate, latency (total / retrieval / generation)
- Bảng so sánh provider + model + tokens + cost
- Biểu đồ latency, success, usage, cost, tokens, trend 14 ngày
- Lịch sử request gần đây

---

## 8. Audit & Realtime

| Thành phần | Mô tả |
|------------|-------|
| AuditLog | Login, CRUD, upload, AI request, thanh toán… |
| UserHub | Force logout khi khóa |
| SubjectHub | Cập nhật danh sách môn |
| NotificationHub | Sự kiện entity / thông báo |
| ChatHub | Chat realtime student |

---

## 9. Mô hình dữ liệu chính (bổ sung so với v1)

| Entity | Mô tả |
|--------|-------|
| PaymentPackage | Gói + `MonthlyGeminiQuestions`, `DailyOllamaQuestions` |
| UserSubscription | Gói đang active / expired |
| PaymentTransaction | Giao dịch VNPAY / Free |
| SystemSettings | Cấu hình hệ thống tập trung |
| AiUsageLog | Log AI (provider, tokens, latency, cost, success) |
| SubjectAssignment | Lịch sử gán giáo viên |

---

# PHẦN B — QUÉT LOGIC: BẤT ỔN / RỦI RO

Kết quả rà soát toàn dự án (17/07/2026).  
**Cập nhật:** Các mục **#1–#14, #18–#19** đã được vá trong code cùng ngày. Mục #15–#17 giữ theo quyết định nghiệp vụ (meta không trừ quota; fallback confirm UI; VI/EN dọn dần).

## B.1. Cao (đã vá)

| # | Vấn đề | Cách đã sửa |
|---|--------|-------------|
| 1 | Xung đột policy `/Chunks` | Folder/page dùng `AdminOrTeacher` |
| 2 | MustChangePassword bypass Hub | `ChatHub` chặn khi `MustChangePassword` |
| 3 | VNPAY thiếu kiểm soát | Bỏ hardcode; Return verify amount+user; thêm `/Payment/Ipn` |
| 4 | Upload size 2 nguồn | Kestrel ceiling 200MB; Documents UI lấy `SystemSettings` |

## B.2. Trung bình (đã vá)

| # | Vấn đề | Cách đã sửa |
|---|--------|-------------|
| 5 | `MaxDailyQuestions` legacy | Ẩn khỏi modal; sync = Ollama/ngày |
| 6 | `GenerationProvider` | Default gợi ý trên Chat + khi không gửi ProviderId |
| 7 | Logging flags | Tôn trọng token/latency/cost; vẫn ghi log tối thiểu cho quota |
| 8 | ResolveQuotaPolicy hardcode | Tin DB khi package tồn tại |
| 9 | Hạ cấp gói | Chặn trong `CompleteTransactionAsync` |
| 10 | Expire subscription | Lazy trong `GetActiveSubscriptionAsync` |
| 11 | Warmup đốt quota | `UsageOperation = Warmup` loại khỏi quota/benchmark |
| 12 | Seed reset password | Không ghi đè password user đã tồn tại |
| 13 | Admin tạo Student thiếu Free | Tự tạo Free subscription |
| 14 | Cửa sổ quota lệch | Gemini tháng + Ollama ngày cùng timezone settings |

## B.3. Thấp / nợ kỹ thuật

| # | Vấn đề | Trạng thái |
|---|--------|------------|
| 15 | Trộn VI/EN trong message lỗi & audit | Còn lại — dọn dần |
| 16 | Meta không trừ quota | **Giữ cố ý** theo đặc tả |
| 17 | Fallback confirm client | **Giữ cố ý** |
| 18 | Teacher history list | Đã dùng `GetForTeacherWithHistoryAsync` |
| 19 | `appsettings.example.json` | Đã bỏ Indexing cũ, thêm VNPay mẫu |

---

## B.4. Ma trận quyết định nghiệp vụ đề xuất (chốt để sửa)

| Hạng mục | Đề xuất mặc định |
|----------|------------------|
| Meta/hello có trừ quota? | **Không** trừ (giữ UX thân thiện) — ghi rõ trong đặc tả |
| Hết Gemini còn Ollama | Hỏi user chuyển (giữ confirm), không auto |
| Hạ cấp gói | Cấm (UI + server CompleteTransaction) |
| Admin set quota = 0 | Tôn nghĩa “tắt provider đó”, bỏ hardcode fallback khi DB đã có cột |
| MaxDailyQuestions | Ẩn khỏi Admin UI hoặc đồng bộ = tổng Ollama (deprecate) |
| GenerationProvider SystemSettings | Đổi thành “Default provider gợi ý trên UI”, hoặc bỏ nếu student luôn tự chọn |
| VNPAY | Bắt buộc config; verify amount + user; thêm IPN khi lên production |

---

## B.5. Checklist kiểm thử chấp nhận (smoke)

1. Student Free: chỉ Ollama 1 lượt/ngày; Gemini disabled.
2. Mua Premium → hóa đơn → Chat; Free hiện “Đã có gói cao hơn”.
3. Chat chọn Gemini/Ollama; hết Gemini → confirm chuyển Ollama.
4. Đổi môn trên Create chat → banner “Môn: …” đổi theo select.
5. Admin sửa Gemini/Ollama quota gói → Student thấy đúng trên Packages/Profile/Chat.
6. Teacher upload → index → Student chat đúng môn đó, không lẫn môn khác.
7. Reports: Analytics không có so sánh provider; Benchmark có Gemini vs Ollama.
8. Teacher không vào Settings System / Benchmarks (nếu policy Admin).

---

# PHỤ LỤC

## A. Endpoint / Hub chính

| Path | Vai trò |
|------|---------|
| `/Account/*` | Auth, Profile |
| `/Subjects`, `/Chapters`, `/Documents` | Nội dung học |
| `/Chat`, `/hubs/chat` | Chat RAG |
| `/Payment/Packages`, `/Payment/Return` | Gói & hóa đơn |
| `/Settings/System` | Cấu hình Admin |
| `/Reports`, `/Reports/Benchmarks` | Báo cáo |
| `/AuditLogs` | Nhật ký |

## B. Tài liệu liên quan

- `docs/EduAI-Architecture.drawio` — sơ đồ kiến trúc  
- `docs/EduAI-Dac-Ta-Mon-Hoc.md` — bản đặc tả v1 (lịch sử)  
- `README.md` — hướng dẫn chạy dự án  

---

**Kết thúc đặc tả nghiệp vụ EduAI v2.0**
