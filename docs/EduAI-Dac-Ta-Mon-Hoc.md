# ĐẶC TẢ MÔN HỌC / HỆ THỐNG EDUAI

**Môn:** PRN222 — Assignment 2  
**Tên hệ thống:** EduAI — Chatbot RAG & Quản lý tài liệu  
**Phiên bản tài liệu:** 1.0  
**Ngày cập nhật:** 19/06/2026  

---

## 1. Giới thiệu

EduAI là nền tảng web hỗ trợ học tập thông minh, cho phép giáo viên/quản trị viên tải tài liệu môn học lên hệ thống, tự động phân tích và lập chỉ mục (chunk + embedding), đồng thời cung cấp chatbot RAG (Retrieval-Augmented Generation) để sinh viên hỏi đáp dựa trên nội dung tài liệu đã được upload trong phạm vi từng môn.

Hệ thống được xây dựng theo mô hình **3 lớp (3-Layer Architecture)**, tuân thủ **Repository Pattern**, **Unit of Work** và **Dependency Injection**.

---

## 2. Mục tiêu

| STT | Mục tiêu | Mô tả |
|-----|----------|-------|
| 1 | Quản lý môn học | Tạo, sửa, ẩn/khôi phục môn; gán giáo viên phụ trách |
| 2 | Quản lý tài liệu | Upload, index, xem, tải xuống tài liệu theo chương |
| 3 | Chatbot RAG | Sinh viên hỏi đáp theo tài liệu môn học, có trích nguồn khi độ liên quan đủ cao |
| 4 | Phân quyền | Admin, Teacher, Student với quyền truy cập khác nhau |
| 5 | Theo dõi hệ thống | Audit log các thao tác quan trọng |
| 6 | Realtime | Cập nhật UI không cần reload (SignalR) cho môn học, thông báo, khóa tài khoản |

---

## 3. Công nghệ sử dụng

| Thành phần | Công nghệ |
|------------|-----------|
| Framework | ASP.NET Core 8 — Razor Pages |
| Cơ sở dữ liệu | SQL Server LocalDB |
| ORM | Entity Framework Core 8 |
| Xác thực | ASP.NET Core Identity (Cookie) |
| AI | Google Gemini API |
| Realtime | SignalR |
| Email | MailKit (SMTP Gmail) |
| Trích xuất văn bản | PdfPig, OpenXML (PDF, DOCX, PPTX) |

---

## 4. Kiến trúc hệ thống

### 4.1. Cấu trúc solution

```
Assigment2/
├── src/
│   ├── EduAI.Web/           → Presentation (Razor Pages, Hubs, Middleware)
│   ├── EduAI.BusinessLogic/ → Business Logic (Services, Helpers)
│   └── EduAI.Model/         → Data Access (Entities, Repositories, DbContext, Migrations)
└── docs/
    ├── EduAI-Architecture.drawio
    └── EduAI-Dac-Ta-Mon-Hoc.md
```

### 4.2. Luồng dữ liệu bắt buộc

```
Razor Page (PageModel)
    → Service (Business Logic)
        → Repository / UnitOfWork
            → AppDbContext
                → SQL Server
```

**Không được phép:** PageModel hoặc `.cshtml` gọi trực tiếp `DbContext` / `Repository`.

### 4.3. Các thành phần chính (Web layer)

| Thành phần | Mô tả |
|------------|-------|
| Razor Pages | Giao diện CRUD theo module |
| SignalR Hubs | `/hubs/user`, `/hubs/subjects`, `/hubs/notifications` |
| DocumentIndexingWorker | Background service index tài liệu (chunk + embedding) |
| InactiveUserMiddleware | Tự đăng xuất khi tài khoản bị khóa |
| DataSeeder | Seed role, tài khoản demo; hỗ trợ reset DB |

---

## 5. Vai trò người dùng

### 5.1. Admin (Quản trị viên)

- Quản lý tài khoản (tạo Teacher/Student, khóa/mở khóa)
- CRUD môn học (tạo, sửa, ẩn/khôi phục)
- Gán giáo viên cho môn (chỉ lần đầu — xem mục 7.2)
- Upload/quản lý tài liệu, chương, chunks
- Xem audit log, phiên chat (read-only)
- **Không** sửa thông tin cá nhân qua Profile (chỉ xem)

### 5.2. Teacher (Giáo viên)

- Xem các môn **được gán**
- Upload tài liệu cho môn được gán
- Quản lý chương, tài liệu thuộc môn mình
- Phải **xác thực email** trước khi đăng nhập
- Tự sửa Profile (họ tên, email, mật khẩu)

### 5.3. Student (Sinh viên)

- Xem môn học **đã có tài liệu**
- Tải/xem tài liệu (Study/Materials)
- Chat RAG theo môn (phiên chat gắn với một môn)
- Tự sửa Profile

---

## 6. Tài khoản demo (seed)

| Email | Mật khẩu | Vai trò |
|-------|----------|---------|
| admin@gmail.com | 12345 | Admin |
| teacher@gmail.com | 12345 | Teacher |
| student@gmail.com | 12345 | Student |

> Teacher seed có `EmailConfirmed = true` để đăng nhập ngay khi demo.

---

## 7. Quy tắc nghiệp vụ quan trọng

### 7.1. Môn học — Giáo viên

| Quy tắc | Chi tiết |
|---------|----------|
| 1 môn — 1 giáo viên | Mỗi môn chỉ có tối đa **một** `TeacherId` |
| 1 giáo viên — nhiều môn | Một giáo viên có thể phụ trách **nhiều** môn |
| Gán một lần (demo) | Sau khi đã gán giáo viên, **không đổi** và **không gỡ** (phiên bản demo) |
| Ẩn môn | Admin có thể deactivate môn; sinh viên không thấy môn đã ẩn |

### 7.2. Tài liệu

| Quy tắc | Chi tiết |
|---------|----------|
| Định dạng hỗ trợ | PDF, DOCX, PPTX, TXT |
| Giới hạn dung lượng | 50 MB / file (`MaxUploadBytes`) |
| 1 chương — 1 file | Mỗi chương chỉ được upload **một** tài liệu |
| Lưu trữ file | Thư mục `uploads/{subjectId}/{chapterId}/` |
| Index nền | Sau upload, hệ thống enqueue index → chunk + embedding bất đồng bộ |
| Trạng thái index | `Indexed` → hiển thị 「đã tải nội dung thành công」 |

### 7.3. Chat RAG

| Quy tắc | Chi tiết |
|---------|----------|
| Phạm vi môn | Chat **chỉ** retrieve chunk trong cùng `SubjectId` |
| Phiên chat | Mỗi phiên gắn 1 sinh viên + 1 môn |
| Trích nguồn | Chỉ hiện khi `relevance score ≥ 0.55` |
| Không có dữ liệu | Trả lời: không tìm thấy thông tin trong tài liệu môn |
| Ngôn ngữ | Gemini trả lời cùng ngôn ngữ với câu hỏi |

### 7.4. Tài khoản

| Quy tắc | Chi tiết |
|---------|----------|
| Khóa mềm | `IsActive = false`, không xóa cứng user |
| Khóa → logout ngay | Middleware + cập nhật SecurityStamp |
| Admin không khóa được | Tài khoản Admin được bảo vệ |
| Teacher mới | Gửi email xác thực qua SMTP |

---

## 8. Chức năng theo module

### 8.1. Quản lý người dùng (`/Users`)

- Danh sách, tạo, chi tiết, sửa (Admin)
- Khóa/mở khóa tài khoản
- Gửi lại email xác thực (Teacher)

### 8.2. Môn học (`/Subjects`)

- Index: danh sách môn (theo role), realtime SignalR
- Create/Edit/Details (Admin)
- Gán giáo viên tại Edit (chỉ khi chưa gán)
- Chi tiết: bảng tài liệu kiểu Google Drive, xem chunk accordion

### 8.3. Chương (`/Chapters`)

- CRUD chương trong môn (Admin/Teacher)

### 8.4. Tài liệu (`/Documents`)

- Upload, sửa metadata, xóa, chi tiết
- Progress index realtime trên trang Details
- Teacher chỉ thao tác môn được gán

### 8.5. Chunks (`/Chunks`) — Admin

- Xem/quản lý đoạn văn và embedding (debug/admin)

### 8.6. Chat (`/Chat`) — Student

- Tạo phiên chat theo môn
- Hỏi đáp RAG (form POST + reload)
- Enter gửi tin; hỗ trợ IME tiếng Việt; auto scroll xuống tin mới

### 8.7. Chat Sessions / Messages — Admin

- Xem phiên và tin nhắn (read-only)

### 8.8. Audit Logs (`/AuditLogs`)

- Ghi nhận: login, upload, AI request, CRUD user/subject...

### 8.9. Study (`/Study`)

- Sinh viên xem/tải tài liệu môn có materials

### 8.10. Profile (`/Account/Profile`)

- Teacher/Student: sửa thông tin, đổi mật khẩu

---

## 9. Luồng RAG (Retrieval-Augmented Generation)

### 9.1. Giai đoạn Indexing (chuẩn bị dữ liệu)

```
Upload file
  → Lưu disk (uploads/)
  → DocumentIndexingQueue
  → DocumentIndexingWorker (background)
      → Extract text (PDF/DOCX/PPTX)
      → Chunk (~800 ký tự, overlap 120)
      → Lưu DocumentChunk
      → Gemini embedContent (gemini-embedding-001)
      → Lưu DocumentEmbedding (vector JSON trong SQL)
  → SignalR: tiến độ index → UI Documents/Details
```

### 9.2. Giai đoạn Chat (hỏi đáp)

```
Sinh viên gửi câu hỏi
  → ChatService.SendMessageAsync
      → Lưu tin nhắn user
      → FindRelevantChunksAsync (Top 5)
          → Ưu tiên: semantic search (cosine similarity)
          → Fallback: keyword search
      → Ghép context từ chunk
      → Gemini generateContent (gemini-2.5-flash)
      → Lưu tin nhắn assistant (+ citations nếu đủ score)
  → Redirect → hiển thị lịch sử chat
```

### 9.3. Lưu trữ vector

| Thành phần | Chi tiết |
|------------|----------|
| Bảng | `DocumentEmbeddings` |
| Cột | `EmbeddingVector` — chuỗi JSON mảng `float[]` |
| Quan hệ | 1 chunk ↔ 1 embedding |
| Tìm kiếm | Cosine similarity trong memory (không dùng vector DB riêng) |

---

## 10. Mô hình dữ liệu chính

| Entity | Mô tả |
|--------|-------|
| ApplicationUser | Người dùng (Identity) |
| Subject | Môn học, `TeacherId` nullable |
| Chapter | Chương thuộc môn |
| Document | File tài liệu, `FilePath`, metadata |
| DocumentChunk | Đoạn văn bản đã tách |
| DocumentEmbedding | Vector embedding của chunk |
| ChatSession | Phiên chat (Student + Subject) |
| ChatMessage | Tin nhắn user/assistant, `Citations` |
| AuditLog | Nhật ký thao tác hệ thống |

**Quan hệ chính:**

- Subject 1—N Chapter 1—1 Document (mỗi chương tối đa 1 file)
- Document 1—N DocumentChunk 1—1 DocumentEmbedding
- Subject 1—N ChatSession 1—N ChatMessage

---

## 11. SignalR — Realtime

| Hub | Endpoint | Mục đích |
|-----|----------|----------|
| UserHub | `/hubs/user` | Khóa tài khoản → force logout |
| SubjectHub | `/hubs/subjects` | CRUD môn, gán GV, upload → cập nhật danh sách môn |
| NotificationHub | `/hubs/notifications` | Sự kiện entity khác (Documents, Chat, Audit...) |

---

## 12. Cấu hình hệ thống

File: `src/EduAI.Web/appsettings.json`

| Section | Mục đích |
|---------|----------|
| ConnectionStrings:DefaultConnection | SQL Server LocalDB |
| Database:ResetOnStartup | `true` → xóa DB + uploads + seed lại khi khởi động |
| AppSettings:UploadPath | Thư mục lưu file upload |
| AppSettings:MaxUploadBytes | Giới hạn dung lượng upload |
| Gemini | API key, model chat/embed, system prompt |
| EmailSettings | SMTP gửi mail xác thực Teacher |

---

## 13. Triển khai & vận hành

### 13.1. Yêu cầu môi trường

- .NET 8 SDK
- SQL Server LocalDB
- Visual Studio 2022 hoặc `dotnet CLI`
- API key Google Gemini (cấu hình trong appsettings)

### 13.2. Chạy ứng dụng

```powershell
cd src/EduAI.Web
dotnet run
```

URL mặc định: `https://localhost:7014`

### 13.3. Reset dữ liệu demo

1. Đặt `"Database": { "ResetOnStartup": true }` trong appsettings
2. Chạy app một lần
3. Đặt lại `false`

Hoặc thủ công: drop database `EduAI`, xóa thư mục `uploads/`, chạy lại app.

### 13.4. Migration

```powershell
dotnet ef database update --project src/EduAI.Model --startup-project src/EduAI.Web
```

---

## 14. Sơ đồ tham khảo

- **Kiến trúc tổng quan & luồng RAG:** mở file `docs/EduAI-Architecture.drawio` bằng [draw.io](https://app.diagrams.net) hoặc extension Draw.io trong VS Code.

---

## 15. Phạm vi ngoài demo (chưa triển khai)

- Đổi / gỡ giáo viên sau khi đã gán môn
- Vector database chuyên dụng (Pinecone, pgvector...)
- Chat streaming realtime (hiện dùng form POST + reload)
- Đa ngôn ngữ giao diện đầy đủ (UI chủ yếu tiếng Việt)

---

**Kết thúc tài liệu đặc tả EduAI**
