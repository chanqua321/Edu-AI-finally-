using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Enums;
using EduAI.Model.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EduAI.Model;

namespace EduAI.Web.Data;

public static class DemoUploadChatBootstrap
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var docs = scope.ServiceProvider.GetRequiredService<IDocumentService>();
        var indexing = scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>();
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var teacher = await users.FindByEmailAsync("teacher@gmail.com");
        var student = await users.FindByEmailAsync("student@gmail.com");
        if (teacher == null || student == null)
        {
            Console.WriteLine("[demo-upload-chat] Không tìm thấy tài khoản GV / SV mẫu.");
            return;
        }

        // Đảm bảo SV demo có gói Enterprise để không bị giới hạn số câu hỏi khi seed dữ liệu
        var sub = await context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == student.Id);
        if (sub != null)
        {
            sub.PackageId = "Enterprise";
            sub.EndDate = DateTime.Now.AddDays(365);
            await context.SaveChangesAsync();
            Console.WriteLine("[demo-upload-chat] Đã nâng cấp tài khoản SV mẫu lên gói Enterprise.");
        }

        var demoDir = @"D:\Code\PRN222\Assigment2\Demo";
        if (!Directory.Exists(demoDir))
        {
            Console.WriteLine($"[demo-upload-chat] Thư mục Demo không tồn tại: {demoDir}");
            return;
        }

        var zipFiles = Directory.GetFiles(demoDir, "*.zip");
        if (zipFiles.Length == 0)
        {
            Console.WriteLine($"[demo-upload-chat] Không tìm thấy file zip nào trong: {demoDir}");
            return;
        }

        foreach (var zipPath in zipFiles)
        {
            var subjectName = Path.GetFileNameWithoutExtension(zipPath);
            Console.WriteLine($"\n[demo-upload-chat] Đang xử lý file zip môn học: {subjectName}");

            // 1. Ensure Subject exists
            var subject = await context.Subjects.FirstOrDefaultAsync(s => s.Name == subjectName);
            if (subject == null)
            {
                subject = new Subject
                {
                    Name = subjectName,
                    Description = $"Môn học demo {subjectName}",
                    TeacherId = teacher.Id,
                    IsActive = true
                };
                context.Subjects.Add(subject);
                await context.SaveChangesAsync();
                Console.WriteLine($"[demo-upload-chat] Đã tạo môn học mới: {subjectName} (ID: {subject.Id})");
            }

            // Ensure Teacher is assigned to the subject (if assignments are tracked)
            var hasAssignment = await context.SubjectAssignments.AnyAsync(a => a.SubjectId == subject.Id && a.TeacherId == teacher.Id);
            if (!hasAssignment)
            {
                context.SubjectAssignments.Add(new SubjectAssignment
                {
                    SubjectId = subject.Id,
                    TeacherId = teacher.Id
                });
                await context.SaveChangesAsync();
                Console.WriteLine($"[demo-upload-chat] Đã phân công giáo viên cho môn: {subjectName}");
            }

            // 2. Ensure Chapter exists
            var chapter = await context.Chapters.FirstOrDefaultAsync(c => c.SubjectId == subject.Id);
            if (chapter == null)
            {
                chapter = new Chapter
                {
                    SubjectId = subject.Id,
                    Name = "Chương 1: Tài liệu tổng hợp",
                    OrderNumber = 1
                };
                context.Chapters.Add(chapter);
                await context.SaveChangesAsync();
                Console.WriteLine($"[demo-upload-chat] Đã tạo chương mới cho: {subjectName}");
            }

            // 3. Ensure Lesson exists
            var lesson = await context.Lessons.FirstOrDefaultAsync(l => l.ChapterId == chapter.Id);
            if (lesson == null)
            {
                lesson = new Lesson
                {
                    ChapterId = chapter.Id,
                    Name = "Bài 1: Nội dung chi tiết",
                    OrderNumber = 1
                };
                context.Lessons.Add(lesson);
                await context.SaveChangesAsync();
                Console.WriteLine($"[demo-upload-chat] Đã tạo bài học mới cho: {subjectName}");
            }

            // 4. Extract Zip to temporary directory
            var tempDir = Path.Combine(Path.GetTempPath(), "EduAI_Demo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempDir);
                var extractedFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
                var uploadedCount = 0;

                foreach (var file in extractedFiles)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is not (".pdf" or ".docx" or ".pptx" or ".txt"))
                        continue;

                    var contentType = ext switch
                    {
                        ".pdf" => "application/pdf",
                        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                        _ => "text/plain"
                    };

                    Console.WriteLine($"[demo-upload-chat] Đang tải lên file: {Path.GetFileName(file)}");
                    
                    await using var stream = File.OpenRead(file);
                    var uploadResult = await docs.UploadAsync(new UploadDocumentDto
                    {
                        SubjectId = subject.Id,
                        ChapterId = chapter.Id,
                        LessonId = lesson.Id,
                        UploadedByUserId = teacher.Id,
                        UploaderRole = Roles.Teacher,
                        FileName = Path.GetFileName(file),
                        Category = DocumentCategory.Lecture,
                        FileStream = stream,
                        ContentType = contentType,
                        FileSizeBytes = stream.Length
                    }, null);

                    if (uploadResult.Success && uploadResult.DocumentId is int docId)
                    {
                        uploadedCount++;
                        Console.WriteLine($"[demo-upload-chat] Tải lên thành công ID: #{docId}. Đang tiến hành index...");
                        
                        // Sync index
                        await indexing.IndexAsync(docId, null);
                        Console.WriteLine($"[demo-upload-chat] Đã index thành công: #{docId}");
                    }
                    else
                    {
                        Console.WriteLine($"[demo-upload-chat] Thất bại tải lên {Path.GetFileName(file)}: {uploadResult.ErrorMessage}");
                    }
                }

                // 5. Run Demo chat query using Gemini RAG
                if (uploadedCount > 0)
                {
                    Console.WriteLine($"[demo-upload-chat] Tạo hội thoại hỏi đáp demo cho môn: {subjectName}");
                    var create = await chat.CreateSessionAsync(new CreateChatSessionDto
                    {
                        StudentId = student.Id,
                        SubjectId = subject.Id,
                        Title = $"Hỏi đáp demo - {subjectName}"
                    }, null);

                    if (create.Success && create.Session != null)
                    {
                        var questions = new[]
                        {
                            $"Tóm tắt tài liệu môn {subjectName}",
                            "Các nội dung chính trong tài liệu là gì?",
                            $"Giải thích các khái niệm quan trọng của môn {subjectName}"
                        };

                        foreach (var q in questions)
                        {
                            Console.WriteLine($"[demo-upload-chat] Gửi câu hỏi: '{q}'");
                            var ans = await chat.SendMessageAsync(new SendChatMessageDto
                            {
                                SessionId = create.Session.Id,
                                StudentId = student.Id,
                                SubjectId = subject.Id,
                                Question = q
                            }, null);

                            if (ans.Success)
                            {
                                Console.WriteLine($"[demo-upload-chat] Gemini trả lời thành công (Prompt tokens: {ans.PromptTokens})");
                            }
                            else
                            {
                                Console.WriteLine($"[demo-upload-chat] Lỗi hỏi đáp Gemini: {ans.ErrorMessage}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[demo-upload-chat] Lỗi khi xử lý file zip {zipPath}: {ex.Message}");
            }
            finally
            {
                // Cleanup temp files
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        Console.WriteLine("\n[demo-upload-chat] Đã tải lên và hỏi đáp xong dữ liệu Demo!");
    }
}
