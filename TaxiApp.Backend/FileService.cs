using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaxiApp.Backend.Core.Interfaces;

namespace TaxiApp.Backend.Infrastructure
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment env;

        public FileService(IWebHostEnvironment env)
        {
            this.env = env;
        }
        public async Task<string?> SaveImageAsync(IFormFile img, string folderName)
        {
            if (img == null || img.Length == 0)
                return null;

            // إنشاء اسم فريد للصورة
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(img.FileName);

            // تحديد مسار المجلد الذي تريد حفظ الصور فيه
            // هنا نستخدم مجلد "Images" داخل المشروع
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), folderName);

            // إنشاء المجلد إذا لم يكن موجود
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // تحديد المسار الكامل للملف
            var filePath = Path.Combine(folderPath, fileName);

            // كتابة الملف على القرص بشكل async
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await img.CopyToAsync(stream);
            }

            // إرجاع اسم الملف فقط
            return fileName;
        }
    }
}
